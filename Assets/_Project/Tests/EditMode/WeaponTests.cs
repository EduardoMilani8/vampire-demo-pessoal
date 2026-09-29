using HollowTide.Core;
using NUnit.Framework;

namespace HollowTide.Tests
{
    public class WeaponProgressionTests
    {
        static WeaponLevelStats Base => new WeaponLevelStats
        {
            Damage = 10f, Area = 1f, Cooldown = 1.4f, Duration = 0.2f, ProjectileSpeed = 1f, Knockback = 6f, Amount = 1, Pierce = 0
        };

        static StatDelta[] Deltas => new[]
        {
            new StatDelta { DamageAdd = 5f },                      // nível 2
            new StatDelta { AmountAdd = 1 },                       // 3
            new StatDelta { AreaPct = 0.2f },                      // 4
            new StatDelta { DamageAdd = 5f },                      // 5
            new StatDelta { CooldownPct = -0.1f },                 // 6
            new StatDelta { AreaPct = 0.2f },                      // 7
            new StatDelta { AmountAdd = 1, DamageAdd = 5f },       // 8
        };

        [Test]
        public void Level1_IsTheBase()
        {
            var s = WeaponProgression.StatsAtLevel(Base, Deltas, 1);

            Assert.AreEqual(10f, s.Damage);
            Assert.AreEqual(1, s.Amount);
            Assert.AreEqual(1.4f, s.Cooldown, 1e-5f);
        }

        [Test]
        public void Level2_AppliesOnlyFirstDelta()
        {
            var s = WeaponProgression.StatsAtLevel(Base, Deltas, 2);

            Assert.AreEqual(15f, s.Damage);
            Assert.AreEqual(1, s.Amount);
        }

        [Test]
        public void Level8_AccumulatesAllDeltas()
        {
            var s = WeaponProgression.StatsAtLevel(Base, Deltas, 8);

            Assert.AreEqual(25f, s.Damage);
            Assert.AreEqual(3, s.Amount);
            Assert.AreEqual(1.4f, s.Area, 1e-5f);                                 // 1 * (1 + 0.2 + 0.2)
            Assert.AreEqual(1.26f, s.Cooldown, 1e-5f);                            // 1.4 * 0.9
        }

        [Test]
        public void Level_IsClampedToValidRange()
        {
            var lo = WeaponProgression.StatsAtLevel(Base, Deltas, -3);
            var hi = WeaponProgression.StatsAtLevel(Base, Deltas, 99);

            Assert.AreEqual(10f, lo.Damage);
            Assert.AreEqual(25f, hi.Damage);
        }

        [Test]
        public void MissingDeltas_ActAsNoUpgrade()
        {
            var s = WeaponProgression.StatsAtLevel(Base, null, 8);

            Assert.AreEqual(10f, s.Damage);
        }

        [Test]
        public void Cooldown_NeverDropsBelowFloor()
        {
            var deltas = new[] { new StatDelta { CooldownPct = -5f } };

            var s = WeaponProgression.StatsAtLevel(Base, deltas, 2);

            Assert.AreEqual(WeaponProgression.MinCooldown, s.Cooldown);
        }
    }

    public class WeaponMathTests
    {
        static WeaponLevelStats Level => new WeaponLevelStats
        {
            Damage = 10f, Area = 1f, Cooldown = 2f, Duration = 1f, ProjectileSpeed = 1f, Knockback = 6f, Amount = 1, Pierce = 1
        };

        [Test]
        public void DefaultPlayerStats_LeaveWeaponUnchanged()
        {
            var r = WeaponMath.Resolve(Level, PlayerStats.Default);

            Assert.AreEqual(10f, r.Damage);
            Assert.AreEqual(2f, r.Cooldown);
            Assert.AreEqual(1, r.Amount);
        }

        [Test]
        public void PassivesScaleDamageAreaCooldownAndAmount()
        {
            var p = PlayerStats.Default;
            p.Might = 1.5f; p.Area = 1.25f; p.Cooldown = 0.8f; p.Amount = 1; p.Duration = 2f; p.ProjectileSpeed = 1.1f;

            var r = WeaponMath.Resolve(Level, p);

            Assert.AreEqual(15f, r.Damage, 1e-5f);
            Assert.AreEqual(1.25f, r.Area, 1e-5f);
            Assert.AreEqual(1.6f, r.Cooldown, 1e-5f);
            Assert.AreEqual(2, r.Amount);
            Assert.AreEqual(2f, r.Duration, 1e-5f);
            Assert.AreEqual(1.1f, r.ProjectileSpeed, 1e-5f);
            Assert.AreEqual(6f, r.Knockback);
            Assert.AreEqual(1, r.Pierce);
        }

        [Test]
        public void Resolve_KeepsCooldownAboveFloor()
        {
            var p = PlayerStats.Default;
            p.Cooldown = 0.0001f;

            Assert.AreEqual(WeaponProgression.MinCooldown, WeaponMath.Resolve(Level, p).Cooldown);
        }
    }

    public class WeaponTimerTests
    {
        [Test]
        public void StartsReadyAndFiresImmediately()
        {
            var t = new WeaponTimer();

            Assert.IsTrue(t.Tick(0.016f, 1f));
        }

        [Test]
        public void FiresOncePerCooldown()
        {
            var t = new WeaponTimer();
            int fires = 0;
            for (int i = 0; i < 600; i++) if (t.Tick(0.01f, 1f)) fires++; // 6s a 100 Hz, cooldown 1s

            Assert.AreEqual(6, fires);
        }

        [Test]
        public void HugeFrameFiresOnlyOnce_NoBacklog()
        {
            var t = new WeaponTimer();
            t.Tick(0.01f, 1f);

            Assert.IsTrue(t.Tick(10f, 1f));
            Assert.IsFalse(t.Tick(0.5f, 1f));
        }

        [Test]
        public void CooldownChangeAppliesFromNextCycle()
        {
            var t = new WeaponTimer();
            t.Tick(0.01f, 2f);          // dispara, próximo em ~2s

            Assert.IsFalse(t.Tick(1f, 0.5f));
            Assert.IsTrue(t.Tick(1.1f, 0.5f));
        }
    }

    public class ArcHitTests
    {
        const float Half = 50f * 0.0174532925f; // 50°

        [Test]
        public void EnemyInFront_IsHit()
        {
            Assert.IsTrue(ArcHit.InArc(0, 0, 1, 0, 2f, Half, 1.5f, 0f, 0.3f));
        }

        [Test]
        public void EnemyBehind_IsMissed()
        {
            Assert.IsFalse(ArcHit.InArc(0, 0, 1, 0, 2f, Half, -1.5f, 0f, 0.3f));
        }

        [Test]
        public void EnemyBeyondRadius_IsMissed()
        {
            Assert.IsFalse(ArcHit.InArc(0, 0, 1, 0, 2f, Half, 3f, 0f, 0.3f));
        }

        [Test]
        public void EnemyRadiusExtendsReach()
        {
            Assert.IsTrue(ArcHit.InArc(0, 0, 1, 0, 2f, Half, 2.2f, 0f, 0.3f));
        }

        [Test]
        public void AngleEdgeAcceptsBodyOverlapButNotCenterOutside()
        {
            // Centro a 60° (fora dos 50°), mas o corpo largo ainda encosta na borda do setor.
            float a = 60f * 0.0174532925f;
            float ex = 1.5f * (float)System.Math.Cos(a), ey = 1.5f * (float)System.Math.Sin(a);

            Assert.IsFalse(ArcHit.InArc(0, 0, 1, 0, 2f, Half, ex, ey, 0.05f));
            Assert.IsTrue(ArcHit.InArc(0, 0, 1, 0, 2f, Half, ex, ey, 0.6f));
        }

        [Test]
        public void EnemyOnTopOfOrigin_IsAlwaysHit()
        {
            Assert.IsTrue(ArcHit.InArc(0, 0, 0, 1, 2f, Half, 0.05f, 0f, 0.4f));
        }

        [Test]
        public void WorksForArbitraryFacing()
        {
            Assert.IsTrue(ArcHit.InArc(0, 0, 0, 1, 2f, Half, 0f, 1.5f, 0.3f));
            Assert.IsFalse(ArcHit.InArc(0, 0, 0, 1, 2f, Half, 1.5f, 0f, 0.3f));
        }
    }
}
