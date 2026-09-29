using System.Collections.Generic;
using System.Linq;
using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class LevelUpPoolTests
    {
        static Candidate[] Buf() => new Candidate[64];

        static int Build(Candidate[] buf, int[] w, int[] p, int[] pMax = null)
            => LevelUpPool.Build(buf, w, 8, LevelUpPool.MaxWeapons, p, pMax ?? Enumerable.Repeat(5, p.Length).ToArray(), LevelUpPool.MaxPassives);

        [Test]
        public void EmptyInventory_OffersEverythingAsNew()
        {
            var buf = Buf();

            int n = Build(buf, new[] { 0, 0, 0 }, new[] { 0, 0 });

            Assert.AreEqual(5, n);
            Assert.IsTrue(buf.Take(n).All(c => c.Kind == OptionKind.NewWeapon || c.Kind == OptionKind.NewPassive));
        }

        [Test]
        public void OwnedItems_AreOfferedAsUpgradesWithCurrentLevel()
        {
            var buf = Buf();

            int n = Build(buf, new[] { 3, 0 }, new[] { 2 });

            var up = buf.Take(n).Single(c => c.Kind == OptionKind.UpgradeWeapon);
            Assert.AreEqual(0, up.Index);
            Assert.AreEqual(3, up.Level);
            Assert.AreEqual(1, buf.Take(n).Count(c => c.Kind == OptionKind.UpgradePassive));
        }

        [Test]
        public void MaxedWeapon_IsNotOffered()
        {
            var buf = Buf();

            int n = Build(buf, new[] { 8 }, new int[0]);

            Assert.AreEqual(0, n);
        }

        [Test]
        public void MaxedPassive_UsesItsOwnMaxLevel()
        {
            var buf = Buf();

            int n = Build(buf, new int[0], new[] { 5, 2 }, new[] { 5, 3 });

            Assert.AreEqual(1, n);
            Assert.AreEqual(1, buf[0].Index);
        }

        [Test]
        public void FullWeaponSlots_OnlyAllowUpgradesToOwnedWeapons()
        {
            var buf = Buf();
            var weapons = new[] { 1, 1, 1, 1, 1, 1, 0, 0 }; // 6 equipadas, 2 disponíveis

            int n = Build(buf, weapons, new int[0]);

            Assert.AreEqual(6, n);
            Assert.IsTrue(buf.Take(n).All(c => c.Kind == OptionKind.UpgradeWeapon));
        }

        [Test]
        public void FullPassiveSlots_OnlyAllowUpgradesToOwnedPassives()
        {
            var buf = Buf();
            var passives = new[] { 1, 1, 1, 1, 1, 1, 0, 0, 0 };

            int n = Build(buf, new int[0], passives);

            Assert.AreEqual(6, n);
            Assert.IsTrue(buf.Take(n).All(c => c.Kind == OptionKind.UpgradePassive));
        }

        [Test]
        public void NegativeLevel_ExcludesItemFromPool()
        {
            var buf = Buf();

            int n = Build(buf, new[] { -1, 0 }, new int[0]);

            Assert.AreEqual(1, n);
            Assert.AreEqual(1, buf[0].Index);
        }

        [Test]
        public void ExcludedItems_DoNotCountAsOwned()
        {
            var buf = Buf();
            var weapons = new[] { 1, 1, 1, 1, 1, -1, 0 }; // 5 equipadas + 1 excluída: ainda cabe uma nova

            int n = Build(buf, weapons, new int[0]);

            Assert.IsTrue(buf.Take(n).Any(c => c.Kind == OptionKind.NewWeapon && c.Index == 6));
        }
    }

    public class LevelUpRollerTests
    {
        static Candidate[] Pool(int count)
        {
            var p = new Candidate[count];
            for (int i = 0; i < count; i++) p[i] = new Candidate { Kind = OptionKind.NewPassive, Index = i, Level = 0, Weight = 1f };
            return p;
        }

        [Test]
        public void ReturnsRequestedNumberOfDistinctOptions()
        {
            var roller = new LevelUpRoller(new SimRandom(1));
            var res = new LevelUpOption[4];

            for (int trial = 0; trial < 200; trial++)
            {
                int n = roller.Roll(Pool(10), 10, 3, res);

                Assert.AreEqual(3, n);
                Assert.AreEqual(3, res.Take(n).Select(o => o.Index).Distinct().Count());
            }
        }

        [Test]
        public void SmallPool_ReturnsOnlyWhatExists()
        {
            var roller = new LevelUpRoller(new SimRandom(2));
            var res = new LevelUpOption[4];

            Assert.AreEqual(2, roller.Roll(Pool(2), 2, 3, res));
            Assert.AreEqual(1, roller.Roll(Pool(1), 1, 4, res));
        }

        [Test]
        public void EmptyPool_ReturnsSingleFallback()
        {
            var roller = new LevelUpRoller(new SimRandom(3));
            var res = new LevelUpOption[4];

            int n = roller.Roll(Pool(0), 0, 3, res);

            Assert.AreEqual(1, n);
            Assert.AreEqual(OptionKind.Fallback, res[0].Kind);
        }

        [Test]
        public void NewLevel_IsCurrentPlusOne()
        {
            var roller = new LevelUpRoller(new SimRandom(4));
            var pool = new[]
            {
                new Candidate { Kind = OptionKind.UpgradeWeapon, Index = 0, Level = 4, Weight = 1f },
                new Candidate { Kind = OptionKind.NewWeapon, Index = 1, Level = 0, Weight = 1f },
            };
            var res = new LevelUpOption[4];

            int n = roller.Roll(pool, 2, 3, res);

            Assert.AreEqual(2, n);
            Assert.AreEqual(5, res.Take(n).Single(o => o.Kind == OptionKind.UpgradeWeapon).NewLevel);
            Assert.AreEqual(1, res.Take(n).Single(o => o.Kind == OptionKind.NewWeapon).NewLevel);
        }

        [Test]
        public void Roll_DoesNotMutateTheCallersPool()
        {
            var roller = new LevelUpRoller(new SimRandom(5));
            var pool = Pool(6);
            var before = pool.Select(c => c.Index).ToArray();

            roller.Roll(pool, 6, 3, new LevelUpOption[4]);

            CollectionAssert.AreEqual(before, pool.Select(c => c.Index).ToArray());
        }

        [Test]
        public void Weights_BiasTheDraw()
        {
            var roller = new LevelUpRoller(new SimRandom(6));
            var pool = new[]
            {
                new Candidate { Kind = OptionKind.NewPassive, Index = 0, Weight = 9f },
                new Candidate { Kind = OptionKind.NewPassive, Index = 1, Weight = 1f },
            };
            var res = new LevelUpOption[4];
            int heavyFirst = 0;

            for (int i = 0; i < 2000; i++)
            {
                roller.Roll(pool, 2, 1, res);
                if (res[0].Index == 0) heavyFirst++;
            }

            Assert.That(heavyFirst, Is.InRange(1700, 1900)); // ~90%
        }

        [Test]
        public void ZeroWeightCandidates_StillFillTheSlots()
        {
            var roller = new LevelUpRoller(new SimRandom(7));
            var pool = new[]
            {
                new Candidate { Kind = OptionKind.NewPassive, Index = 0, Weight = 0f },
                new Candidate { Kind = OptionKind.NewPassive, Index = 1, Weight = 0f },
            };

            Assert.AreEqual(2, roller.Roll(pool, 2, 3, new LevelUpOption[4]));
        }

        [Test]
        public void EveryCandidateEventuallyAppears()
        {
            var roller = new LevelUpRoller(new SimRandom(8));
            var seen = new HashSet<int>();
            var res = new LevelUpOption[4];

            for (int i = 0; i < 300; i++)
            {
                int n = roller.Roll(Pool(9), 9, 3, res);
                for (int k = 0; k < n; k++) seen.Add(res[k].Index);
            }

            Assert.AreEqual(9, seen.Count);
        }

        [Test]
        public void Avoid_ExcludesRecentlyShownOptionsWhenAlternativesExist()
        {
            var roller = new LevelUpRoller(new SimRandom(9));
            var res = new LevelUpOption[4];
            var avoid = new[]
            {
                new LevelUpOption { Kind = OptionKind.NewPassive, Index = 0 },
                new LevelUpOption { Kind = OptionKind.NewPassive, Index = 1 },
                new LevelUpOption { Kind = OptionKind.NewPassive, Index = 2 },
            };

            for (int trial = 0; trial < 100; trial++)
            {
                int n = roller.Roll(Pool(8), 8, 3, res, avoid, avoid.Length);

                Assert.AreEqual(3, n);
                Assert.IsTrue(res.Take(n).All(o => o.Index >= 3), "reroll repetiu uma opção recém-mostrada");
            }
        }

        [Test]
        public void Avoid_IsRelaxedWhenPoolIsTooSmall()
        {
            var roller = new LevelUpRoller(new SimRandom(10));
            var res = new LevelUpOption[4];
            var avoid = new[]
            {
                new LevelUpOption { Kind = OptionKind.NewPassive, Index = 0 },
                new LevelUpOption { Kind = OptionKind.NewPassive, Index = 1 },
                new LevelUpOption { Kind = OptionKind.NewPassive, Index = 2 },
            };

            int n = roller.Roll(Pool(4), 4, 3, res, avoid, avoid.Length);

            Assert.AreEqual(3, n);
            Assert.AreEqual(3, res.Take(n).Select(o => o.Index).Distinct().Count());
            Assert.IsTrue(res.Take(n).Any(o => o.Index == 3)); // a única não evitada sempre entra
        }

        [Test]
        public void Avoid_MatchesKindAndIndexNotJustIndex()
        {
            var roller = new LevelUpRoller(new SimRandom(11));
            var pool = new[]
            {
                new Candidate { Kind = OptionKind.NewWeapon, Index = 0, Weight = 1f },
                new Candidate { Kind = OptionKind.NewPassive, Index = 0, Weight = 1f },
            };
            var avoid = new[] { new LevelUpOption { Kind = OptionKind.NewWeapon, Index = 0 } };
            var res = new LevelUpOption[4];

            roller.Roll(pool, 2, 1, res, avoid, 1);

            Assert.AreEqual(OptionKind.NewPassive, res[0].Kind);
        }

        [Test]
        public void OptionCount_IsThreeWithoutLuck()
        {
            var roller = new LevelUpRoller(new SimRandom(12));

            for (int i = 0; i < 500; i++) Assert.AreEqual(3, roller.OptionCount(1f));
        }

        [Test]
        public void OptionCount_FourthOptionChanceGrowsWithLuck()
        {
            var roller = new LevelUpRoller(new SimRandom(13));
            int four = 0;

            for (int i = 0; i < 4000; i++) if (roller.OptionCount(1.5f) == 4) four++;

            Assert.That(four, Is.InRange(850, 1150)); // ~25%
            for (int i = 0; i < 4000; i++) Assert.LessOrEqual(roller.OptionCount(99f), 4);
        }
    }

    public class LevelUpAllowanceTests
    {
        [Test]
        public void Reroll_ConsumesUntilEmpty()
        {
            var a = new LevelUpAllowance(2, 0);

            Assert.IsTrue(a.TryReroll());
            Assert.IsTrue(a.TryReroll());
            Assert.IsFalse(a.TryReroll());
            Assert.AreEqual(0, a.Rerolls);
        }

        [Test]
        public void Skip_ConsumesUntilEmptyAndIsIndependentOfReroll()
        {
            var a = new LevelUpAllowance(1, 1);

            Assert.IsTrue(a.TrySkip());
            Assert.IsFalse(a.TrySkip());
            Assert.AreEqual(1, a.Rerolls);
        }
    }
}
