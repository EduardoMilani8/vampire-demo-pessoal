using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class PlayerHealthTests
    {
        [Test]
        public void Hit_ReducesHealthAndGrantsInvulnerability()
        {
            var h = new PlayerHealth(100f);

            Assert.IsTrue(h.TryHit(10f));
            Assert.AreEqual(90f, h.Current);
            Assert.IsTrue(h.IsInvulnerable);
            Assert.IsFalse(h.TryHit(10f));
            Assert.AreEqual(90f, h.Current);
        }

        [Test]
        public void Invulnerability_EndsAfterDuration()
        {
            var h = new PlayerHealth(100f) { InvulnDuration = 0.5f };
            h.TryHit(10f);

            h.Tick(0.6f);

            Assert.IsFalse(h.IsInvulnerable);
            Assert.IsTrue(h.TryHit(10f));
        }

        [Test]
        public void Armor_ReducesDamageButNeverBelowOne()
        {
            var h = new PlayerHealth(100f) { Armor = 3f };
            h.TryHit(10f);
            Assert.AreEqual(93f, h.Current);

            h.Tick(1f);
            h.TryHit(2f);
            Assert.AreEqual(92f, h.Current);
        }

        [Test]
        public void Health_ClampsAtZeroAndReportsDeath()
        {
            var h = new PlayerHealth(10f);

            h.TryHit(999f);

            Assert.AreEqual(0f, h.Current);
            Assert.IsTrue(h.IsDead);
            Assert.IsFalse(h.TryHit(1f));
        }

        [Test]
        public void Regen_HealsOverTimeUpToMax()
        {
            var h = new PlayerHealth(100f) { RegenPerSecond = 2f };
            h.TryHit(20f);

            h.Tick(5f);
            Assert.AreEqual(90f, h.Current, 1e-4f);

            h.Tick(100f);
            Assert.AreEqual(100f, h.Current);
        }

        [Test]
        public void Dead_DoesNotRegenOrHeal()
        {
            var h = new PlayerHealth(10f) { RegenPerSecond = 5f };
            h.TryHit(50f);

            h.Tick(10f);
            h.Heal(5f);

            Assert.IsTrue(h.IsDead);
        }

        [Test]
        public void SetMax_PreservesRatio()
        {
            var h = new PlayerHealth(100f);
            h.TryHit(50f);

            h.SetMax(200f);

            Assert.AreEqual(200f, h.Max);
            Assert.AreEqual(100f, h.Current, 1e-4f);
        }
    }

    public class SpawnRingTests
    {
        [Test]
        public void Samples_AreAlwaysOutsideTheVisibleRectangle()
        {
            var rng = new SimRandom(7);
            const float hw = 10.7f, hh = 6f, margin = 1.5f;

            for (int i = 0; i < 5000; i++)
            {
                SpawnRing.Sample(rng, 20f, -8f, hw, hh, margin, 2f, out float x, out float y);

                bool inside = System.Math.Abs(x - 20f) <= hw && System.Math.Abs(y + 8f) <= hh;
                Assert.IsFalse(inside, $"sample {i} ({x},{y}) apareceu dentro da tela");
            }
        }

        [Test]
        public void Samples_StayWithinRingBand()
        {
            var rng = new SimRandom(11);
            float min = SpawnRing.RingRadius(10f, 6f, 1f);

            for (int i = 0; i < 1000; i++)
            {
                SpawnRing.Sample(rng, 0f, 0f, 10f, 6f, 1f, 3f, out float x, out float y);
                float d = (float)System.Math.Sqrt(x * x + y * y);
                Assert.GreaterOrEqual(d, min - 1e-3f);
                Assert.LessOrEqual(d, min + 3f + 1e-3f);
            }
        }

        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new SimRandom(5);
            var b = new SimRandom(5);

            SpawnRing.Sample(a, 0, 0, 10, 6, 1, 2, out float ax, out float ay);
            SpawnRing.Sample(b, 0, 0, 10, 6, 1, 2, out float bx, out float by);

            Assert.AreEqual(ax, bx);
            Assert.AreEqual(ay, by);
        }

        [Test]
        public void Samples_CoverAllDirections()
        {
            var rng = new SimRandom(3);
            var quadrants = new bool[4];

            for (int i = 0; i < 200; i++)
            {
                SpawnRing.Sample(rng, 0, 0, 10, 6, 1, 0, out float x, out float y);
                quadrants[(x >= 0 ? 1 : 0) + (y >= 0 ? 2 : 0)] = true;
            }

            CollectionAssert.AreEqual(new[] { true, true, true, true }, quadrants);
        }
    }
}
