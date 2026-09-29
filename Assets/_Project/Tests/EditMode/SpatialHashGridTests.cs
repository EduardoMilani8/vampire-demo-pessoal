using System.Collections.Generic;
using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class SpatialHashGridTests
    {
        static SpatialHashGrid NewGrid(int capacity = 1024) => new SpatialHashGrid(1.5f, 256, capacity);

        [Test]
        public void Query_ReturnsOnlyItemsWithinRadius()
        {
            var g = NewGrid();
            g.Insert(0, 0f, 0f);
            g.Insert(1, 1f, 0f);
            g.Insert(2, 5f, 5f);
            var buf = new int[16];

            int n = g.Query(0f, 0f, 1.2f, buf);

            var found = new HashSet<int>(System.Linq.Enumerable.Take(buf, n));
            Assert.AreEqual(2, n);
            Assert.IsTrue(found.Contains(0) && found.Contains(1));
        }

        [Test]
        public void Query_WorksAcrossNegativeCoordinates()
        {
            var g = NewGrid();
            g.Insert(0, -0.1f, -0.1f);
            g.Insert(1, 0.1f, 0.1f);
            g.Insert(2, -100.5f, -200.5f);
            var buf = new int[16];

            Assert.AreEqual(2, g.Query(0f, 0f, 0.5f, buf));
            Assert.AreEqual(1, g.Query(-100f, -200f, 1f, buf));
        }

        [Test]
        public void Query_NeverReturnsDuplicatesEvenWithTinyBucketTable()
        {
            var g = new SpatialHashGrid(1f, 2, 256); // só 2 buckets: colisão garantida
            for (int i = 0; i < 100; i++) g.Insert(i, i * 0.3f, (i % 7) * 0.4f);
            var buf = new int[256];

            int n = g.Query(15f, 1f, 30f, buf);

            Assert.AreEqual(100, n);
            Assert.AreEqual(100, new HashSet<int>(System.Linq.Enumerable.Take(buf, n)).Count);
        }

        [Test]
        public void Query_TruncatesWhenBufferIsFull()
        {
            var g = NewGrid();
            for (int i = 0; i < 10; i++) g.Insert(i, 0f, 0f);

            Assert.AreEqual(4, g.Query(0f, 0f, 1f, new int[4]));
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            var g = NewGrid();
            g.Insert(0, 0f, 0f);
            g.Clear();

            Assert.AreEqual(0, g.Query(0f, 0f, 10f, new int[8]));
        }

        [Test]
        public void FindNearest_MatchesBruteForce()
        {
            var rng = new SimRandom(42);
            var g = NewGrid(600);
            var xs = new float[600];
            var ys = new float[600];
            for (int i = 0; i < 600; i++)
            {
                xs[i] = rng.Range(-60f, 60f);
                ys[i] = rng.Range(-60f, 60f);
                g.Insert(i, xs[i], ys[i]);
            }

            for (int q = 0; q < 200; q++)
            {
                float qx = rng.Range(-70f, 70f), qy = rng.Range(-70f, 70f);
                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < 600; i++)
                {
                    float d = (xs[i] - qx) * (xs[i] - qx) + (ys[i] - qy) * (ys[i] - qy);
                    if (d < bestD) { bestD = d; best = i; }
                }

                bool found = g.FindNearest(qx, qy, 500f, out int id, out float d2);

                Assert.IsTrue(found);
                Assert.AreEqual(best, id, $"query {q}");
                Assert.AreEqual(bestD, d2, 1e-3f);
            }
        }

        [Test]
        public void FindNearest_RespectsMaxRadius()
        {
            var g = NewGrid();
            g.Insert(0, 10f, 0f);

            Assert.IsFalse(g.FindNearest(0f, 0f, 5f, out int id, out _));
            Assert.AreEqual(-1, id);
            Assert.IsTrue(g.FindNearest(0f, 0f, 10.5f, out id, out _));
            Assert.AreEqual(0, id);
        }

        [Test]
        public void FindNearest_EmptyGridReturnsFalse()
        {
            Assert.IsFalse(NewGrid().FindNearest(0f, 0f, 50f, out _, out _));
        }

        [Test]
        public void Constructor_RejectsNonPowerOfTwoBuckets()
        {
            Assert.Throws<System.ArgumentException>(() => new SpatialHashGrid(1f, 100, 10));
        }
    }
}
