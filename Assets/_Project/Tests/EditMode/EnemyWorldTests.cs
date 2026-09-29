using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class EnemyWorldTests
    {
        static EnemyStats Basic => new EnemyStats { MaxHp = 20f, Speed = 2f, ContactDamage = 5f, Radius = 0.4f, KnockbackResistance = 0f };

        [Test]
        public void Spawn_TracksCountAndActiveList()
        {
            var w = new EnemyWorld(8);
            int a = w.Spawn(Basic, 1f, 1f);
            int b = w.Spawn(Basic, 2f, 2f);

            Assert.AreEqual(2, w.Count);
            Assert.AreNotEqual(a, b);
            Assert.IsTrue(w.Alive[a] && w.Alive[b]);
        }

        [Test]
        public void Spawn_ReturnsMinusOneWhenFull()
        {
            var w = new EnemyWorld(2);
            w.Spawn(Basic, 0f, 0f);
            w.Spawn(Basic, 0f, 0f);

            Assert.AreEqual(-1, w.Spawn(Basic, 0f, 0f));
        }

        [Test]
        public void Despawn_KeepsActiveListDenseAndReusesIds()
        {
            var w = new EnemyWorld(4);
            int a = w.Spawn(Basic, 0f, 0f);
            int b = w.Spawn(Basic, 0f, 0f);
            int c = w.Spawn(Basic, 0f, 0f);

            w.Despawn(a);

            Assert.AreEqual(2, w.Count);
            Assert.IsFalse(w.Alive[a]);
            var live = new System.Collections.Generic.HashSet<int> { w.Active[0], w.Active[1] };
            Assert.IsTrue(live.Contains(b) && live.Contains(c));
            Assert.AreEqual(a, w.Spawn(Basic, 0f, 0f)); // id reaproveitado
        }

        [Test]
        public void Despawn_IsIdempotent()
        {
            var w = new EnemyWorld(4);
            int a = w.Spawn(Basic, 0f, 0f);
            w.Despawn(a);
            w.Despawn(a);

            Assert.AreEqual(0, w.Count);
            // Um double-free não pode duplicar o id na free-list: dois spawns devem dar ids distintos.
            int x = w.Spawn(Basic, 0f, 0f);
            int y = w.Spawn(Basic, 0f, 0f);
            Assert.AreNotEqual(x, y);
        }

        [Test]
        public void Step_MovesTowardPlayerAtSpeed()
        {
            var w = new EnemyWorld(2);
            int e = w.Spawn(Basic, 10f, 0f);

            w.Step(0.5f, 0f, 0f, 1000f, 20f);

            Assert.AreEqual(9f, w.X[e], 1e-4f); // speed 2 * 0.5s
            Assert.AreEqual(0f, w.Y[e], 1e-4f);
        }

        [Test]
        public void Knockback_PushesAwayThenDecays()
        {
            var w = new EnemyWorld(2);
            var s = Basic; s.Speed = 0f;
            int e = w.Spawn(s, 5f, 0f);

            w.Knockback(e, 1f, 0f, 6f);
            w.Step(0.1f, 0f, 0f, 1000f, 20f);
            float afterFirst = w.X[e];
            for (int i = 0; i < 100; i++) w.Step(0.1f, 0f, 0f, 1000f, 20f);

            Assert.Greater(afterFirst, 5f);
            Assert.Greater(w.X[e], afterFirst);          // continuou deslizando
            Assert.Less(w.X[e], 5f + 6f / w.KnockbackDecay + 0.1f); // distância total limitada: v0/decay
            Assert.AreEqual(0f, w.Kx[e], 1e-3f);         // knockback zerou
        }

        [Test]
        public void Knockback_TotalDistanceIsIndependentOfFramerate()
        {
            float Slide(float dt)
            {
                var w = new EnemyWorld(1);
                var s = Basic; s.Speed = 0f;
                int e = w.Spawn(s, 0f, 0f);
                w.Knockback(e, 1f, 0f, 6f);
                for (float t = 0f; t < 3f; t += dt) w.Step(dt, 100f, 0f, 1000f, 20f);
                return w.X[e];
            }

            float expected = 6f / new EnemyWorld(1).KnockbackDecay; // v0 / λ
            Assert.AreEqual(expected, Slide(1f / 144f), 0.02f);
            Assert.AreEqual(expected, Slide(1f / 60f), 0.02f);
            Assert.AreEqual(expected, Slide(1f / 20f), 0.02f);
        }

        [Test]
        public void Knockback_IsReducedByResistance()
        {
            var w = new EnemyWorld(4);
            var light = Basic; light.Speed = 0f;
            var heavy = light; heavy.KnockbackResistance = 0.75f;
            int l = w.Spawn(light, 0f, 0f);
            int h = w.Spawn(heavy, 0f, 0f);

            w.Knockback(l, 1f, 0f, 8f);
            w.Knockback(h, 1f, 0f, 8f);

            Assert.AreEqual(8f, w.Kx[l], 1e-4f);
            Assert.AreEqual(2f, w.Kx[h], 1e-4f);
        }

        [Test]
        public void Damage_ReportsKillOnlyWhenHpDepleted()
        {
            var w = new EnemyWorld(2);
            int e = w.Spawn(Basic, 0f, 0f);

            Assert.IsFalse(w.Damage(e, 19f));
            Assert.Greater(w.FlashTimer[e], 0f);
            Assert.IsTrue(w.Damage(e, 1f));
        }

        [Test]
        public void Step_RelocatesFarEnemyToOppositeSideAtRingDistance()
        {
            var w = new EnemyWorld(2);
            int e = w.Spawn(Basic, 100f, 0f);

            w.Step(0.016f, 0f, 0f, 50f, 20f);

            Assert.AreEqual(-20f, w.X[e], 1e-3f);
            Assert.AreEqual(0f, w.Y[e], 1e-3f);
        }

        [Test]
        public void Separate_PushesOverlappingEnemiesApart()
        {
            var w = new EnemyWorld(4);
            var s = Basic; s.Speed = 0f;
            int a = w.Spawn(s, 0f, 0f);
            int b = w.Spawn(s, 0.2f, 0f);
            var grid = new SpatialHashGrid(1.5f, 64, 4);
            var buf = new int[16];
            float before = w.X[b] - w.X[a];

            for (int i = 0; i < 30; i++)
            {
                w.RebuildGrid(grid);
                w.Separate(grid, 0.016f, buf);
            }

            Assert.Greater(w.X[b] - w.X[a], before);
            Assert.AreEqual(0f, w.Y[a], 1e-4f);
        }

        [Test]
        public void Separate_SeparatesEnemiesSpawnedOnExactlySamePoint()
        {
            var w = new EnemyWorld(4);
            var s = Basic; s.Speed = 0f;
            int a = w.Spawn(s, 3f, 3f);
            int b = w.Spawn(s, 3f, 3f);
            var grid = new SpatialHashGrid(1.5f, 64, 4);
            var buf = new int[16];

            for (int i = 0; i < 10; i++)
            {
                w.RebuildGrid(grid);
                w.Separate(grid, 0.016f, buf);
            }

            Assert.AreNotEqual(w.X[a], w.X[b]);
        }
    }
}
