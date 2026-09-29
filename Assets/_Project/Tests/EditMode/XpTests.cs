using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class XpCurveTests
    {
        [Test]
        public void FirstLevelNeedsFiveXp()
        {
            Assert.AreEqual(5, XpCurve.RequiredForNext(1));
        }

        [Test]
        public void Requirement_IsStrictlyIncreasing()
        {
            for (int l = 1; l < 120; l++)
                Assert.Greater(XpCurve.RequiredForNext(l + 1), XpCurve.RequiredForNext(l), $"nível {l}");
        }

        [Test]
        public void Requirement_JumpsAtLevels20And40()
        {
            int before20 = XpCurve.RequiredForNext(19), at20 = XpCurve.RequiredForNext(20);
            int before40 = XpCurve.RequiredForNext(39), at40 = XpCurve.RequiredForNext(40);

            Assert.Greater(at20 - before20, 200);
            Assert.Greater(at40 - before40, 800);
        }

        [Test]
        public void InvalidLevelIsTreatedAsLevelOne()
        {
            Assert.AreEqual(XpCurve.RequiredForNext(1), XpCurve.RequiredForNext(0));
            Assert.AreEqual(XpCurve.RequiredForNext(1), XpCurve.RequiredForNext(-5));
        }

        [Test]
        public void Cumulative_MatchesSumOfSteps()
        {
            Assert.AreEqual(0, XpCurve.CumulativeToReach(1));
            Assert.AreEqual(5, XpCurve.CumulativeToReach(2));
            Assert.AreEqual(5 + 14 + 23, XpCurve.CumulativeToReach(4));
        }
    }

    public class XpTrackerTests
    {
        [Test]
        public void StartsAtLevelOneWithNothing()
        {
            var x = new XpTracker();

            Assert.AreEqual(1, x.Level);
            Assert.AreEqual(0f, x.Current);
            Assert.AreEqual(0, x.PendingLevelUps);
        }

        [Test]
        public void BelowThreshold_DoesNotLevelUp()
        {
            var x = new XpTracker();

            Assert.AreEqual(0, x.AddXp(4f));
            Assert.AreEqual(1, x.Level);
            Assert.AreEqual(0.8f, x.Ratio, 1e-5f);
        }

        [Test]
        public void ReachingThreshold_LevelsUpAndCarriesRemainder()
        {
            var x = new XpTracker();

            int gained = x.AddXp(7f);

            Assert.AreEqual(1, gained);
            Assert.AreEqual(2, x.Level);
            Assert.AreEqual(2f, x.Current, 1e-5f);
            Assert.AreEqual(1, x.PendingLevelUps);
        }

        [Test]
        public void HugeGain_ProducesSeveralLevelUpsAtOnce()
        {
            var x = new XpTracker();

            int gained = x.AddXp(XpCurve.CumulativeToReach(6) + 1f);

            Assert.AreEqual(5, gained);
            Assert.AreEqual(6, x.Level);
            Assert.AreEqual(5, x.PendingLevelUps);
            Assert.AreEqual(1f, x.Current, 1e-3f);
        }

        [Test]
        public void ConsumePending_DrainsOneAtATime()
        {
            var x = new XpTracker();
            x.AddXp(XpCurve.CumulativeToReach(4));

            Assert.IsTrue(x.ConsumePending());
            Assert.IsTrue(x.ConsumePending());
            Assert.IsTrue(x.ConsumePending());
            Assert.IsFalse(x.ConsumePending());
            Assert.AreEqual(4, x.Level); // consumir a escolha não altera o nível
        }

        [Test]
        public void NonPositiveXp_IsIgnored()
        {
            var x = new XpTracker();

            Assert.AreEqual(0, x.AddXp(0f));
            Assert.AreEqual(0, x.AddXp(-10f));
            Assert.AreEqual(0f, x.Current);
        }
    }

    public class GemWorldTests
    {
        static void Run(GemWorld g, float seconds, float px, float py, float pickup, float collect, out float xp)
        {
            xp = 0f;
            for (float t = 0f; t < seconds; t += 0.016f) xp += g.Step(0.016f, px, py, pickup, collect);
        }

        [Test]
        public void GemOutsidePickupRadius_StaysPut()
        {
            var g = new GemWorld(8);
            int id = g.Spawn(10f, 0f, 3f);

            Run(g, 2f, 0f, 0f, 2f, 0.3f, out float xp);

            Assert.AreEqual(0f, xp);
            Assert.AreEqual(10f, g.X[id]);
            Assert.IsFalse(g.Magnetized[id]);
        }

        [Test]
        public void GemInsidePickupRadius_FliesToPlayerAndIsCollected()
        {
            var g = new GemWorld(8);
            g.Spawn(1.5f, 0f, 3f);

            Run(g, 2f, 0f, 0f, 2f, 0.3f, out float xp);

            Assert.AreEqual(3f, xp, 1e-5f);
            Assert.AreEqual(0, g.Count);
        }

        [Test]
        public void MagnetizedGem_KeepsChasingEvenAfterPlayerMovesAway()
        {
            var g = new GemWorld(8);
            g.Spawn(1f, 0f, 1f);
            g.Step(0.016f, 0f, 0f, 2f, 0.3f);     // entra no raio e vira magnetizada
            float xp = 0f;
            for (int i = 0; i < 200; i++) xp += g.Step(0.016f, 5f, 0f, 2f, 0.3f); // jogador fora do raio

            Assert.AreEqual(1f, xp, 1e-5f);
        }

        [Test]
        public void MagnetizeAll_PullsGemsFromAnywhere()
        {
            var g = new GemWorld(8);
            g.Spawn(40f, 0f, 1f);
            g.Spawn(-30f, 20f, 2f);
            g.MagnetizeAll();

            Run(g, 4f, 0f, 0f, 0.1f, 0.3f, out float xp);

            Assert.AreEqual(3f, xp, 1e-5f);
        }

        [Test]
        public void Collection_ReportsIdsForViewRelease()
        {
            var g = new GemWorld(8);
            int a = g.Spawn(0.1f, 0f, 1f);
            int b = g.Spawn(0.2f, 0f, 1f);
            g.Spawn(50f, 0f, 1f);

            g.Step(0.016f, 0f, 0f, 1f, 0.3f);

            Assert.AreEqual(2, g.CollectedCount);
            var ids = new System.Collections.Generic.HashSet<int> { g.CollectedIds[0], g.CollectedIds[1] };
            Assert.IsTrue(ids.Contains(a) && ids.Contains(b));
            Assert.AreEqual(1, g.Count);
        }

        [Test]
        public void ManyGemsCollectedInOneStep_AllAccountedFor()
        {
            var g = new GemWorld(64);
            for (int i = 0; i < 50; i++) g.Spawn(0.1f, 0.01f * i, 1f);

            float xp = g.Step(0.016f, 0f, 0f, 1f, 0.6f);

            Assert.AreEqual(50f, xp, 1e-4f);
            Assert.AreEqual(0, g.Count);
        }

        [Test]
        public void FullWorld_MergesValueIntoNewestGemInsteadOfLosingXp()
        {
            var g = new GemWorld(2);
            g.Spawn(10f, 0f, 1f);
            int newest = g.Spawn(11f, 0f, 1f);

            int result = g.Spawn(12f, 0f, 5f);

            Assert.AreEqual(-1, result);
            Assert.AreEqual(2, g.Count);
            Assert.AreEqual(6f, g.Value[newest], 1e-5f);
        }

        [Test]
        public void Despawn_ReusesIdsAndIsIdempotent()
        {
            var g = new GemWorld(4);
            int a = g.Spawn(0f, 0f, 1f);
            g.Despawn(a);
            g.Despawn(a);

            int x = g.Spawn(0f, 0f, 1f);
            int y = g.Spawn(0f, 0f, 1f);
            Assert.AreNotEqual(x, y);
            Assert.AreEqual(2, g.Count);
        }
    }

    public class PassiveMathTests
    {
        [Test]
        public void EachStat_AffectsOnlyItsOwnField()
        {
            var d = PlayerStats.Default;
            var s = PlayerStats.Default;
            PassiveMath.Apply(ref s, PassiveStat.Might, 0.1f);
            Assert.AreEqual(d.Might + 0.1f, s.Might, 1e-5f);
            Assert.AreEqual(d.Area, s.Area);

            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.Armor, 2f);
            Assert.AreEqual(2f, s.Armor);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.MaxHp, 0.2f);
            Assert.AreEqual(1.2f, s.MaxHpMult, 1e-5f);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.Recovery, 0.4f);
            Assert.AreEqual(0.4f, s.Recovery, 1e-5f);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.Area, 0.1f);
            Assert.AreEqual(1.1f, s.Area, 1e-5f);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.ProjectileSpeed, 0.1f);
            Assert.AreEqual(1.1f, s.ProjectileSpeed, 1e-5f);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.Duration, 0.15f);
            Assert.AreEqual(1.15f, s.Duration, 1e-5f);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.MoveSpeed, 0.1f);
            Assert.AreEqual(1.1f, s.MoveSpeed, 1e-5f);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.Magnet, 0.25f);
            Assert.AreEqual(1.25f, s.Magnet, 1e-5f);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.Growth, 0.08f);
            Assert.AreEqual(1.08f, s.Growth, 1e-5f);
            s = PlayerStats.Default; PassiveMath.Apply(ref s, PassiveStat.Luck, 0.1f);
            Assert.AreEqual(1.1f, s.Luck, 1e-5f);
        }

        [Test]
        public void Cooldown_ReducesButNeverBelowFloor()
        {
            var s = PlayerStats.Default;
            PassiveMath.Apply(ref s, PassiveStat.Cooldown, 0.08f * 5);
            Assert.AreEqual(0.6f, s.Cooldown, 1e-5f);

            PassiveMath.Apply(ref s, PassiveStat.Cooldown, 10f);
            Assert.AreEqual(PassiveMath.MinCooldownMultiplier, s.Cooldown);
        }

        [Test]
        public void EveryStatEnumValueIsHandled()
        {
            foreach (PassiveStat stat in System.Enum.GetValues(typeof(PassiveStat)))
            {
                var s = PlayerStats.Default;
                Assert.DoesNotThrow(() => PassiveMath.Apply(ref s, stat, 0.1f), stat.ToString());
            }
        }
    }
}
