using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class ProjectileWorldTests
    {
        sealed class Rig
        {
            public readonly EnemyWorld Enemies = new EnemyWorld(64);
            public readonly SpatialHashGrid Grid = new SpatialHashGrid(1.5f, 128, 64);
            public readonly ProjectileWorld Projectiles = new ProjectileWorld(32);
            public readonly int[] Buffer = new int[256];

            public int AddEnemy(float x, float y, float speed = 0f, float radius = 0.4f)
                => Enemies.Spawn(new EnemyStats { MaxHp = 10f, Speed = speed, Radius = radius, ContactDamage = 1f }, x, y);

            public void Step(float dt)
            {
                Enemies.RebuildGrid(Grid);
                Projectiles.Step(dt, Enemies, Grid, Buffer);
            }
        }

        static ProjectileSpawn Bolt(float x, float y, float vx, float vy) => new ProjectileSpawn
        {
            X = x, Y = y, Vx = vx, Vy = vy, Radius = 0.2f, Damage = 5f, Knockback = 1f, Life = 3f
        };

        [Test]
        public void Bolt_HitsEnemyAndDisappears()
        {
            var r = new Rig();
            r.AddEnemy(2f, 0f);
            r.Projectiles.Spawn(Bolt(0f, 0f, 10f, 0f));

            int hits = 0;
            for (int i = 0; i < 40; i++) { r.Step(0.016f); hits += r.Projectiles.HitCount; }

            Assert.AreEqual(1, hits);
            Assert.AreEqual(0, r.Projectiles.Count);
        }

        [Test]
        public void Hit_IsReportedWithProjectileAndEnemyIds()
        {
            var r = new Rig();
            int e = r.AddEnemy(1f, 0f);
            int p = r.Projectiles.Spawn(Bolt(0f, 0f, 10f, 0f));

            int hits = 0;
            for (int i = 0; i < 30; i++)
            {
                r.Step(0.016f);
                for (int k = 0; k < r.Projectiles.HitCount; k++)
                {
                    Assert.AreEqual(p, r.Projectiles.HitProjectile[k]);
                    Assert.AreEqual(e, r.Projectiles.HitEnemy[k]);
                    hits++;
                }
            }
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void Bolt_MissesEnemyOffItsPath()
        {
            var r = new Rig();
            r.AddEnemy(2f, 3f);
            r.Projectiles.Spawn(Bolt(0f, 0f, 10f, 0f));

            int hits = 0;
            for (int i = 0; i < 40; i++) { r.Step(0.016f); hits += r.Projectiles.HitCount; }

            Assert.AreEqual(0, hits);
        }

        [Test]
        public void Pierce_LetsBoltHitSeveralEnemiesThenDie()
        {
            var r = new Rig();
            r.AddEnemy(1f, 0f); r.AddEnemy(2f, 0f); r.AddEnemy(3f, 0f);
            var s = Bolt(0f, 0f, 10f, 0f);
            s.Pierce = 1; // atravessa 1 e morre no 2º
            r.Projectiles.Spawn(s);

            int hits = 0;
            for (int i = 0; i < 60; i++) { r.Step(0.016f); hits += r.Projectiles.HitCount; }

            Assert.AreEqual(2, hits);
            Assert.AreEqual(0, r.Projectiles.Count);
        }

        [Test]
        public void PiercingBolt_DoesNotHitTheSameEnemyEveryFrame()
        {
            var r = new Rig();
            r.AddEnemy(1f, 0f, radius: 1.0f);            // inimigo grande: o projétil fica dentro por vários frames
            var s = Bolt(0f, 0f, 2f, 0f);
            s.Pierce = 5;
            r.Projectiles.Spawn(s);

            int hits = 0;
            for (int i = 0; i < 120; i++) { r.Step(0.016f); hits += r.Projectiles.HitCount; }

            Assert.AreEqual(1, hits);
        }

        [Test]
        public void Bolt_ExpiresWhenLifeRunsOut()
        {
            var r = new Rig();
            var s = Bolt(0f, 0f, 1f, 0f);
            s.Life = 0.1f;
            r.Projectiles.Spawn(s);

            int expiredTotal = 0;
            for (int i = 0; i < 20; i++) { r.Step(0.016f); expiredTotal += r.Projectiles.ExpiredCount; }

            Assert.AreEqual(1, expiredTotal);
            Assert.AreEqual(0, r.Projectiles.Count);
        }

        [Test]
        public void Homing_TurnsTowardTargetAndHitsIt()
        {
            var r = new Rig();
            int e = r.AddEnemy(0f, 5f);                   // atrás/ao lado do disparo inicial
            var s = Bolt(0f, 0f, 6f, 0f);                // sai para +X, alvo está em +Y
            s.TurnRate = 6f;
            s.Life = 4f;
            r.Projectiles.Spawn(s);

            int hits = 0;
            for (int i = 0; i < 400; i++) { r.Step(0.016f); hits += r.Projectiles.HitCount; }

            Assert.AreEqual(1, hits);
        }

        [Test]
        public void Homing_WithoutTargetsFliesStraight()
        {
            var r = new Rig();
            var s = Bolt(0f, 0f, 4f, 0f);
            s.TurnRate = 6f;
            int p = r.Projectiles.Spawn(s);

            for (int i = 0; i < 25; i++) r.Step(0.016f);

            Assert.AreEqual(0f, r.Projectiles.Y[p], 1e-4f);
            Assert.Greater(r.Projectiles.X[p], 1f);
        }

        [Test]
        public void Homing_ReacquiresAfterTargetDies()
        {
            var r = new Rig();
            int first = r.AddEnemy(0f, 4f);
            int second = r.AddEnemy(8f, 0f);
            var s = Bolt(0f, 0f, 0f, 6f);
            s.TurnRate = 8f; s.Life = 6f;
            r.Projectiles.Spawn(s);

            r.Step(0.016f);                               // trava no mais próximo
            r.Enemies.Despawn(first);                     // alvo some
            int hits = 0;
            int lastEnemy = -1;
            for (int i = 0; i < 600; i++)
            {
                r.Step(0.016f);
                for (int k = 0; k < r.Projectiles.HitCount; k++) { hits++; lastEnemy = r.Projectiles.HitEnemy[k]; }
            }

            Assert.AreEqual(1, hits);
            Assert.AreEqual(second, lastEnemy);
        }

        [Test]
        public void Lob_DoesNotCollideInFlightAndExplodesOnLanding()
        {
            var r = new Rig();
            r.AddEnemy(2f, 0f);                           // no meio do caminho: um arco passa por cima
            var s = Bolt(0f, 0f, 4f, 0f);                 // pousa em x = 4 após 1s
            s.Life = 1f; s.ExplodeRadius = 1.5f; s.Arc = 1.5f;
            int p = r.Projectiles.Spawn(s);

            int hits = 0, exploded = 0;
            float landX = 0f;
            for (int i = 0; i < 80; i++)
            {
                r.Step(0.016f);
                hits += r.Projectiles.HitCount;
                for (int k = 0; k < r.Projectiles.ExplodedCount; k++)
                {
                    exploded++;
                    landX = r.Projectiles.X[r.Projectiles.ExplodedIds[k]];
                }
            }

            Assert.AreEqual(0, hits);
            Assert.AreEqual(1, exploded);
            Assert.AreEqual(4f, landX, 0.1f);
            Assert.AreEqual(0, r.Projectiles.Count);
        }

        [Test]
        public void FullWorld_RejectsNewProjectiles()
        {
            var w = new ProjectileWorld(2);
            w.Spawn(Bolt(0, 0, 1, 0));
            w.Spawn(Bolt(0, 0, 1, 0));

            Assert.AreEqual(-1, w.Spawn(Bolt(0, 0, 1, 0)));
        }

        [Test]
        public void IdsAreReusedAfterExpiry()
        {
            var r = new Rig();
            var s = Bolt(0f, 0f, 1f, 0f);
            s.Life = 0.01f;
            for (int i = 0; i < 32; i++) r.Projectiles.Spawn(s);
            r.Step(0.05f);

            Assert.AreEqual(0, r.Projectiles.Count);
            Assert.GreaterOrEqual(r.Projectiles.Spawn(Bolt(0, 0, 1, 0)), 0);
        }

        [Test]
        public void ManyProjectilesExpiringTogether_AreAllReported()
        {
            var r = new Rig();
            var s = Bolt(0f, 0f, 1f, 0f);
            s.Life = 0.01f;
            for (int i = 0; i < 20; i++) r.Projectiles.Spawn(s);

            r.Step(0.05f);

            Assert.AreEqual(20, r.Projectiles.ExpiredCount);
            Assert.AreEqual(20, new System.Collections.Generic.HashSet<int>(
                System.Linq.Enumerable.Take(r.Projectiles.ExpiredIds, 20)).Count);
        }
    }

    public class HitCooldownsTests
    {
        [Test]
        public void FirstHitAllowed_ThenBlockedUntilIntervalPasses()
        {
            var h = new HitCooldowns(8);

            Assert.IsTrue(h.TryHit(3, 10f, 0.5f));
            Assert.IsFalse(h.TryHit(3, 10.3f, 0.5f));
            Assert.IsTrue(h.TryHit(3, 10.5f, 0.5f));
        }

        [Test]
        public void EnemiesAreTrackedIndependently()
        {
            var h = new HitCooldowns(8);

            Assert.IsTrue(h.TryHit(1, 0f, 1f));
            Assert.IsTrue(h.TryHit(2, 0f, 1f));
            Assert.IsFalse(h.TryHit(1, 0.5f, 1f));
        }

        [Test]
        public void Clear_ResetsAllCooldowns()
        {
            var h = new HitCooldowns(4);
            h.TryHit(0, 5f, 100f);

            h.Clear();

            Assert.IsTrue(h.TryHit(0, 5f, 1f));
        }
    }

    public class EvolutionRulesTests
    {
        [Test]
        public void Evolves_OnlyAtMaxLevelWithRequiredPassive()
        {
            Assert.IsTrue(EvolutionRules.IsEligible(8, 8, true));
            Assert.IsFalse(EvolutionRules.IsEligible(8, 8, false));
            Assert.IsFalse(EvolutionRules.IsEligible(7, 8, true));
            Assert.IsFalse(EvolutionRules.IsEligible(1, 8, false));
        }
    }
}
