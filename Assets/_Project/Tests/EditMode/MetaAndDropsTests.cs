using System.Linq;
using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class RareDropTests
    {
        static int[] Count(float luck, int rolls, uint seed = 1)
        {
            var rng = new SimRandom(seed);
            var counts = new int[4];
            for (int i = 0; i < rolls; i++) counts[(int)RareDropRoller.Roll(rng, luck)]++;
            return counts;
        }

        [Test]
        public void MostKillsDropNothing()
        {
            var c = Count(1f, 100000);

            Assert.Greater(c[(int)RareDrop.None], 98000);
        }

        [Test]
        public void DropRates_MatchTheConstants()
        {
            var c = Count(1f, 400000);

            Assert.That(c[(int)RareDrop.Heal] / 400000f, Is.EqualTo(RareDropRoller.HealChance).Within(0.0012f));
            Assert.That(c[(int)RareDrop.Magnet] / 400000f, Is.EqualTo(RareDropRoller.MagnetChance).Within(0.0009f));
            Assert.That(c[(int)RareDrop.Bomb] / 400000f, Is.EqualTo(RareDropRoller.BombChance).Within(0.0009f));
        }

        [Test]
        public void Luck_IncreasesDrops()
        {
            int low = 100000 - Count(1f, 100000)[0], high = 100000 - Count(2f, 100000)[0];

            Assert.Greater(high, low * 1.6f);
        }

        [Test]
        public void ZeroOrNegativeLuck_StillWorks()
        {
            Assert.DoesNotThrow(() => Count(0f, 1000));
            Assert.DoesNotThrow(() => Count(-3f, 1000));
        }
    }

    public class ChestRewardsTests
    {
        static int[] Histogram(float luck, int n)
        {
            var rng = new SimRandom(3);
            var h = new int[6];
            for (int i = 0; i < n; i++) h[ChestRewards.ItemCount(rng, luck)]++;
            return h;
        }

        [Test]
        public void OnlyOneThreeOrFiveItems()
        {
            var h = Histogram(1.5f, 5000);

            Assert.AreEqual(0, h[0] + h[2] + h[4]);
            Assert.AreEqual(5000, h[1] + h[3] + h[5]);
        }

        [Test]
        public void WithoutLuck_NeverFiveAndSometimesThree()
        {
            var h = Histogram(1f, 20000);

            Assert.AreEqual(0, h[5]);
            Assert.That(h[3] / 20000f, Is.InRange(0.18f, 0.22f));
        }

        [Test]
        public void Luck_ImprovesTheOdds()
        {
            var lo = Histogram(1f, 20000);
            var hi = Histogram(2f, 20000);

            Assert.Greater(hi[5], 0);
            Assert.Greater(hi[3] + hi[5], lo[3] + lo[5]);
        }

        [Test]
        public void Gold_IsWithinBoundsAndGrowsWithTime()
        {
            var rng = new SimRandom(4);
            for (int i = 0; i < 500; i++)
            {
                int early = ChestRewards.GoldAmount(rng, 1f, 0f);
                Assert.That(early, Is.InRange(15, 40));
            }

            var a = new SimRandom(8);
            var b = new SimRandom(8);
            Assert.Greater(ChestRewards.GoldAmount(a, 1f, 20f), ChestRewards.GoldAmount(b, 1f, 0f));
        }
    }

    public class MetaMathTests
    {
        [Test]
        public void Cost_GrowsGeometrically()
        {
            Assert.AreEqual(100, MetaMath.Cost(100, 1.5f, 0));
            Assert.AreEqual(150, MetaMath.Cost(100, 1.5f, 1));
            Assert.AreEqual(225, MetaMath.Cost(100, 1.5f, 2));
        }

        [Test]
        public void Apply_RoutesStatsAndRunResources()
        {
            var s = PlayerStats.Default;
            int rerolls = 2, skips = 2;

            MetaMath.Apply(ref s, ref rerolls, ref skips, MetaStat.Might, 0.1f);
            MetaMath.Apply(ref s, ref rerolls, ref skips, MetaStat.Rerolls, 1f);
            MetaMath.Apply(ref s, ref rerolls, ref skips, MetaStat.Skips, 2f);

            Assert.AreEqual(1.1f, s.Might, 1e-5f);
            Assert.AreEqual(3, rerolls);
            Assert.AreEqual(4, skips);
        }

        [Test]
        public void EveryMetaStat_IsHandled()
        {
            foreach (MetaStat stat in System.Enum.GetValues(typeof(MetaStat)))
            {
                var s = PlayerStats.Default;
                int r = 0, k = 0;
                Assert.DoesNotThrow(() => MetaMath.Apply(ref s, ref r, ref k, stat, 1f), stat.ToString());
            }
        }
    }

    public class SaveDataTests
    {
        [Test]
        public void UpgradeLevels_AreTrackedByIdAndDefaultToZero()
        {
            var save = new SaveData();

            Assert.AreEqual(0, save.GetUpgradeLevel("might"));
            save.SetUpgradeLevel("might", 2);
            save.SetUpgradeLevel("armor", 1);
            save.SetUpgradeLevel("might", 3);

            Assert.AreEqual(3, save.GetUpgradeLevel("might"));
            Assert.AreEqual(1, save.GetUpgradeLevel("armor"));
            Assert.AreEqual(2, save.UpgradeIds.Count);
        }

        [Test]
        public void Unlock_IsIdempotent()
        {
            var save = new SaveData();
            save.Unlock("warden");
            save.Unlock("warden");

            Assert.IsTrue(save.IsUnlocked("warden"));
            Assert.AreEqual(1, save.UnlockedCharacters.Count);
            Assert.IsFalse(save.IsUnlocked("adept"));
        }

        [Test]
        public void Sanitize_RepairsCorruptedData()
        {
            var save = new SaveData
            {
                Gold = -50, TotalKills = -1, Volume = 7f, BestTimeSeconds = float.NaN,
                UpgradeIds = new System.Collections.Generic.List<string> { "a", "b", "c" },
                UpgradeLevels = new System.Collections.Generic.List<int> { 1, -4 },
            };

            save.Sanitize();

            Assert.AreEqual(0, save.Gold);
            Assert.AreEqual(0, save.TotalKills);
            Assert.AreEqual(1f, save.Volume);
            Assert.AreEqual(0f, save.BestTimeSeconds);
            Assert.AreEqual(2, save.UpgradeIds.Count);
            Assert.AreEqual(2, save.UpgradeLevels.Count);
            Assert.AreEqual(0, save.UpgradeLevels[1]);
        }

        [Test]
        public void Sanitize_ReplacesNullLists()
        {
            var save = new SaveData { UpgradeIds = null, UpgradeLevels = null, UnlockedCharacters = null };

            save.Sanitize();

            Assert.IsNotNull(save.UpgradeIds);
            Assert.IsNotNull(save.UnlockedCharacters);
            Assert.DoesNotThrow(() => save.GetUpgradeLevel("x"));
        }
    }

    public class MetaShopTests
    {
        [Test]
        public void Buying_DebitsGoldAndRaisesTheLevel()
        {
            var save = new SaveData { Gold = 500 };

            Assert.IsTrue(MetaShop.TryBuyUpgrade(save, "might", 100, 1.5f, 5));
            Assert.AreEqual(400, save.Gold);
            Assert.AreEqual(1, save.GetUpgradeLevel("might"));

            Assert.IsTrue(MetaShop.TryBuyUpgrade(save, "might", 100, 1.5f, 5));
            Assert.AreEqual(250, save.Gold);          // segundo nível custa 150
            Assert.AreEqual(2, save.GetUpgradeLevel("might"));
        }

        [Test]
        public void NotEnoughGold_ChangesNothing()
        {
            var save = new SaveData { Gold = 99 };

            Assert.IsFalse(MetaShop.TryBuyUpgrade(save, "might", 100, 1.5f, 5));
            Assert.AreEqual(99, save.Gold);
            Assert.AreEqual(0, save.GetUpgradeLevel("might"));
        }

        [Test]
        public void MaxLevel_CannotBeExceeded()
        {
            var save = new SaveData { Gold = 100000 };
            for (int i = 0; i < 3; i++) Assert.IsTrue(MetaShop.TryBuyUpgrade(save, "armor", 10, 1.2f, 3));
            int gold = save.Gold;

            Assert.IsFalse(MetaShop.TryBuyUpgrade(save, "armor", 10, 1.2f, 3));
            Assert.AreEqual(gold, save.Gold);
            Assert.AreEqual(3, save.GetUpgradeLevel("armor"));
        }

        [Test]
        public void Character_UnlocksOnceAndCostsGold()
        {
            var save = new SaveData { Gold = 300 };

            Assert.IsFalse(MetaShop.TryUnlockCharacter(save, "adept", 400));
            Assert.IsTrue(MetaShop.TryUnlockCharacter(save, "adept", 250));
            Assert.AreEqual(50, save.Gold);
            Assert.IsFalse(MetaShop.TryUnlockCharacter(save, "adept", 250));   // já desbloqueado: não cobra de novo
            Assert.AreEqual(50, save.Gold);
        }
    }
}
