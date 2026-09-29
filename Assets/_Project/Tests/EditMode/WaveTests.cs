using System.Linq;
using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class WaveSchedulerTests
    {
        static WaveDefinition Wave(float start, float rate, int max = 1000, float hp = 1f, params WeightedEnemy[] enemies)
            => new WaveDefinition
            {
                StartSeconds = start, SpawnsPerSecond = rate, MaxAlive = max, HpMultiplier = hp,
                Enemies = enemies.Length > 0 ? enemies : new[] { new WeightedEnemy { EnemyIndex = 0, Weight = 1f } },
            };

        static WeightedEnemy W(int index, float weight) => new WeightedEnemy { EnemyIndex = index, Weight = weight };

        /// <summary>Roda o agendador por <paramref name="seconds"/> a 60 Hz e devolve todos os pedidos.</summary>
        static System.Collections.Generic.List<SpawnRequest> Run(WaveScheduler s, float seconds, int alive = 0)
        {
            var all = new System.Collections.Generic.List<SpawnRequest>();
            var buf = new SpawnRequest[64];
            for (float t = 0f; t < seconds - 1e-4f; t += 1f / 60f)
            {
                int n = s.Update(1f / 60f, alive, buf);
                for (int i = 0; i < n; i++) all.Add(buf[i]);
            }
            return all;
        }

        [Test]
        public void NothingSpawnsBeforeTheFirstWaveStarts()
        {
            var s = new WaveScheduler(new[] { Wave(10f, 5f) }, new SimRandom(1));

            Assert.AreEqual(0, Run(s, 9.5f).Count);
        }

        [Test]
        public void SpawnRate_MatchesSpawnsPerSecond()
        {
            var s = new WaveScheduler(new[] { Wave(0f, 2f) }, new SimRandom(1));

            int count = Run(s, 60f).Count;

            Assert.That(count, Is.InRange(118, 121));
        }

        [Test]
        public void MaxAlive_StopsContinuousSpawning()
        {
            var s = new WaveScheduler(new[] { Wave(0f, 20f, max: 50) }, new SimRandom(1));

            Assert.AreEqual(0, Run(s, 5f, alive: 50).Count);
            Assert.AreEqual(0, Run(s, 5f, alive: 80).Count);
        }

        [Test]
        public void SpawningResumesWhenEnemiesDie_WithoutABacklogFlood()
        {
            var s = new WaveScheduler(new[] { Wave(0f, 10f, max: 100) }, new SimRandom(1));
            Run(s, 30f, alive: 100);                     // 30s bloqueado pelo teto

            var buf = new SpawnRequest[64];
            int n = s.Update(1f / 60f, 0, buf);          // teto liberado

            Assert.LessOrEqual(n, 3, "acumulou spawns durante o bloqueio e despejou tudo de uma vez");
        }

        [Test]
        public void LaterWaves_SpawnFaster()
        {
            var s = new WaveScheduler(new[] { Wave(0f, 1f), Wave(60f, 3f), Wave(120f, 6f) }, new SimRandom(1));

            var all = Run(s, 180f);
            int minute1 = all.Count(r => true);            // total; contagem por minuto abaixo
            var perMinute = new int[3];
            var s2 = new WaveScheduler(new[] { Wave(0f, 1f), Wave(60f, 3f), Wave(120f, 6f) }, new SimRandom(1));
            var buf = new SpawnRequest[64];
            for (int f = 0; f < 180 * 60; f++)
                perMinute[f / 3600] += s2.Update(1f / 60f, 0, buf);

            Assert.Greater(perMinute[1], perMinute[0] * 2);
            Assert.Greater(perMinute[2], perMinute[1] * 1.5f);
            Assert.AreEqual(all.Count, minute1);
        }

        [Test]
        public void WaveHpMultiplier_IsCarriedIntoRequests()
        {
            var s = new WaveScheduler(new[] { Wave(0f, 5f, hp: 1f), Wave(30f, 5f, hp: 2.5f) }, new SimRandom(1));

            var early = Run(s, 20f);
            var late = Run(s, 30f);

            Assert.IsTrue(early.All(r => r.HpMultiplier == 1f));
            Assert.IsTrue(late.Skip(late.Count / 2).All(r => r.HpMultiplier == 2.5f));
        }

        [Test]
        public void EnemyChoice_FollowsWeights()
        {
            var s = new WaveScheduler(new[] { Wave(0f, 30f, 100000, 1f, W(0, 3f), W(1, 1f), W(2, 0f)) }, new SimRandom(5));

            var all = Run(s, 60f);
            int zero = all.Count(r => r.EnemyIndex == 0), one = all.Count(r => r.EnemyIndex == 1), two = all.Count(r => r.EnemyIndex == 2);

            Assert.AreEqual(0, two);
            Assert.That((double)zero / (zero + one), Is.InRange(0.72, 0.78));
        }

        [Test]
        public void SameSeed_ProducesTheSameSequence()
        {
            var w = new[] { Wave(0f, 12f, 1000, 1f, W(0, 1f), W(1, 1f), W(2, 1f)) };

            var a = Run(new WaveScheduler(w, new SimRandom(9)), 20f).Select(r => r.EnemyIndex).ToArray();
            var b = Run(new WaveScheduler(w, new SimRandom(9)), 20f).Select(r => r.EnemyIndex).ToArray();

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Burst_FiresOnceAtItsTime()
        {
            var wave = Wave(0f, 0f);
            wave.Bursts = new[] { new WaveBurst { EnemyIndex = 3, Count = 10, AtSecond = 5f } };
            var s = new WaveScheduler(new[] { wave }, new SimRandom(1));

            Assert.AreEqual(0, Run(s, 4.9f).Count);
            var fired = Run(s, 10f);

            Assert.AreEqual(10, fired.Count);
            Assert.IsTrue(fired.All(r => r.EnemyIndex == 3));
        }

        [Test]
        public void SurroundBurst_SpreadsEvenlyAroundTheRing()
        {
            var wave = Wave(0f, 0f);
            wave.Bursts = new[] { new WaveBurst { EnemyIndex = 0, Count = 8, AtSecond = 0f, Surround = true } };
            var s = new WaveScheduler(new[] { wave }, new SimRandom(1));

            var fired = Run(s, 1f);

            Assert.AreEqual(8, fired.Count);
            for (int i = 0; i < 8; i++) Assert.AreEqual(i / 8f, fired[i].RingPosition, 1e-5f);
        }

        [Test]
        public void RegularSpawns_HaveRandomRingPosition()
        {
            var s = new WaveScheduler(new[] { Wave(0f, 10f) }, new SimRandom(1));

            Assert.IsTrue(Run(s, 3f).All(r => r.RingPosition < 0f));
        }

        [Test]
        public void BigBurst_IsDeliveredAcrossSeveralFramesWithoutLoss()
        {
            var wave = Wave(0f, 0f);
            wave.Bursts = new[] { new WaveBurst { EnemyIndex = 0, Count = 200, AtSecond = 0f } };
            var s = new WaveScheduler(new[] { wave }, new SimRandom(1));

            var fired = Run(s, 1f);                        // buffer de 64 por frame

            Assert.AreEqual(200, fired.Count);
        }

        [Test]
        public void Burst_IgnoresMaxAlive()
        {
            var wave = Wave(0f, 0f, max: 10);
            wave.Bursts = new[] { new WaveBurst { EnemyIndex = 0, Count = 5, AtSecond = 0f } };
            var s = new WaveScheduler(new[] { wave }, new SimRandom(1));

            Assert.AreEqual(5, Run(s, 1f, alive: 500).Count);
        }

        [Test]
        public void BigTimeSkip_LandsOnTheLatestWave()
        {
            var s = new WaveScheduler(new[] { Wave(0f, 1f), Wave(60f, 2f), Wave(120f, 3f) }, new SimRandom(1));

            s.Update(500f, 0, new SpawnRequest[64]);

            Assert.AreEqual(2, s.CurrentIndex);
        }

        [Test]
        public void UnsortedInput_IsSorted()
        {
            var s = new WaveScheduler(new[] { Wave(120f, 3f), Wave(0f, 1f), Wave(60f, 2f) }, new SimRandom(1));

            s.Update(70f, 0, new SpawnRequest[64]);

            Assert.AreEqual(1, s.CurrentIndex);
            Assert.AreEqual(2f, s.Current.SpawnsPerSecond);
        }

        [Test]
        public void EmptyTable_NeverSpawns()
        {
            var s = new WaveScheduler(new WaveDefinition[0], new SimRandom(1));

            Assert.AreEqual(0, Run(s, 10f).Count);
        }
    }

    public class WaveTableValidatorTests
    {
        static WaveDefinition W(float start, float rate, int max) => new WaveDefinition { StartSeconds = start, SpawnsPerSecond = rate, MaxAlive = max };

        [Test]
        public void GrowingTable_IsValid()
        {
            Assert.IsTrue(WaveTableValidator.IsGrowing(new[] { W(0, 1, 100), W(60, 2, 150), W(120, 2, 200) }, out var problem));
            Assert.IsNull(problem);
        }

        [Test]
        public void FallingRate_IsRejected()
        {
            Assert.IsFalse(WaveTableValidator.IsGrowing(new[] { W(0, 3, 100), W(60, 2, 150) }, out var problem));
            StringAssert.Contains("ritmo", problem);
        }

        [Test]
        public void FallingCap_IsRejected()
        {
            Assert.IsFalse(WaveTableValidator.IsGrowing(new[] { W(0, 1, 300), W(60, 2, 150) }, out var problem));
            StringAssert.Contains("teto", problem);
        }

        [Test]
        public void ValidationIgnoresInputOrder()
        {
            Assert.IsTrue(WaveTableValidator.IsGrowing(new[] { W(120, 3, 300), W(0, 1, 100), W(60, 2, 200) }, out _));
        }
    }
}
