using System;
using System.Diagnostics;
using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    /// <summary>
    /// Garante que a simulação (sem a parte visual) não gera lixo no estado estável e cabe no orçamento de frame
    /// com carga de estresse: 1000 inimigos, 300 projéteis, 400 gemas. É o requisito de 60 FPS / sem GC no Update
    /// medido na parte que dá para medir fora do editor.
    /// </summary>
    public class SimulationPerformanceTests
    {
        const int Enemies = 1000, Projectiles = 300, Gems = 400;

        sealed class Rig
        {
            public readonly EnemyWorld Enemy = new EnemyWorld(2048);
            public readonly GemWorld Gem = new GemWorld(1024);
            public readonly ProjectileWorld Proj = new ProjectileWorld(768);
            public readonly SpatialHashGrid Grid = new SpatialHashGrid(1.5f, 4096, 2048);
            public readonly int[] Buffer = new int[1024];
            public readonly SimRandom Rng = new SimRandom(77);
            public float PlayerX, PlayerY;

            public Rig()
            {
                var stats = new EnemyStats { MaxHp = 1e9f, Speed = 1.5f, Radius = 0.4f, ContactDamage = 1f };
                for (int i = 0; i < Enemies; i++)
                    Enemy.Spawn(stats, Rng.Range(-14f, 14f), Rng.Range(-9f, 9f));
                for (int i = 0; i < Gems; i++) Gem.Spawn(Rng.Range(-14f, 14f), Rng.Range(-9f, 9f), 1f);
            }

            public void Frame(float dt)
            {
                // Mantém a carga de projéteis constante: repõe os que expiraram.
                while (Proj.Count < Projectiles)
                {
                    float a = Rng.Range(0f, 6.2831f);
                    Proj.Spawn(new ProjectileSpawn
                    {
                        X = PlayerX, Y = PlayerY, Vx = (float)Math.Cos(a) * 7f, Vy = (float)Math.Sin(a) * 7f,
                        Radius = 0.25f, Damage = 1f, Life = 2f, Pierce = 1, TurnRate = 5f,
                    });
                }

                PlayerX += 3f * dt;
                Enemy.Step(dt, PlayerX, PlayerY, 40f, 14f);
                Enemy.RebuildGrid(Grid);
                Enemy.Separate(Grid, dt, Buffer);
                Proj.Step(dt, Enemy, Grid, Buffer);
                Gem.Step(dt, PlayerX, PlayerY, 1.6f, 0.4f);
                while (Gem.Count < Gems) Gem.Spawn(PlayerX + Rng.Range(-14f, 14f), PlayerY + Rng.Range(-9f, 9f), 1f);
            }
        }

        [Test]
        public void SteadyState_AllocatesNothing()
        {
            var rig = new Rig();
            for (int i = 0; i < 120; i++) rig.Frame(1f / 60f);   // aquecimento (JIT, buffers)

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) rig.Frame(1f / 60f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, $"a simulação alocou {allocated} bytes em 600 frames");
        }

        [Test]
        public void FrameCost_FitsTheBudgetUnderStress()
        {
            var rig = new Rig();
            for (int i = 0; i < 120; i++) rig.Frame(1f / 60f);

            var sw = Stopwatch.StartNew();
            const int frames = 600;
            for (int i = 0; i < frames; i++) rig.Frame(1f / 60f);
            sw.Stop();
            double msPerFrame = sw.Elapsed.TotalMilliseconds / frames;

            TestContext.WriteLine($"Simulação com {Enemies} inimigos, {Projectiles} projéteis, {Gems} gemas: {msPerFrame:0.000} ms/frame");
            // 60 FPS = 16,6 ms por frame no total. A simulação sozinha, mesmo em build Debug, deve usar uma fração disso.
            Assert.Less(msPerFrame, 6.0, $"{msPerFrame:0.00} ms/frame está caro demais para o orçamento de 16,6 ms");
        }
    }
}
