using System;

namespace HollowTide.Core
{
    public enum OptionKind : byte
    {
        NewWeapon,
        UpgradeWeapon,
        NewPassive,
        UpgradePassive,
        Fallback,        // nada disponível: a UI oferece um prêmio de consolação (cura/ouro)
    }

    /// <summary>Uma escolha possível. Index aponta para o catálogo do runtime; Level é o nível atual (0 = ainda não possui).</summary>
    public struct Candidate
    {
        public OptionKind Kind;
        public int Index;
        public int Level;
        public float Weight;
    }

    /// <summary>Uma opção sorteada. NewLevel é o nível resultante ao escolhê-la (1 para itens novos).</summary>
    public struct LevelUpOption
    {
        public OptionKind Kind;
        public int Index;
        public int NewLevel;
    }

    /// <summary>Monta o pool de candidatos respeitando limites de espaço e nível máximo.</summary>
    public static class LevelUpPool
    {
        public const int MaxWeapons = 6;
        public const int MaxPassives = 6;

        /// <summary>
        /// Níveis por item: 0 = não possui, > 0 = nível atual, &lt; 0 = fora do pool
        /// (ex.: arma que só existe como evolução). Retorna quantos candidatos foram escritos em <paramref name="buffer"/>.
        /// </summary>
        public static int Build(Candidate[] buffer,
                                int[] weaponLevels, int weaponMaxLevel, int weaponSlots,
                                int[] passiveLevels, int[] passiveMaxLevels, int passiveSlots)
        {
            int n = 0;
            n = AddGroup(buffer, n, weaponLevels, null, weaponMaxLevel, weaponSlots, OptionKind.NewWeapon, OptionKind.UpgradeWeapon);
            n = AddGroup(buffer, n, passiveLevels, passiveMaxLevels, int.MaxValue, passiveSlots, OptionKind.NewPassive, OptionKind.UpgradePassive);
            return n;
        }

        static int AddGroup(Candidate[] buffer, int n, int[] levels, int[] perItemMax, int defaultMax, int slots,
                            OptionKind newKind, OptionKind upgradeKind)
        {
            int owned = 0;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] > 0) owned++;

            for (int i = 0; i < levels.Length; i++)
            {
                int lvl = levels[i];
                if (lvl < 0) continue;

                if (lvl == 0)
                {
                    if (owned >= slots) continue;
                    buffer[n++] = new Candidate { Kind = newKind, Index = i, Level = 0, Weight = 1f };
                }
                else
                {
                    int max = perItemMax != null ? perItemMax[i] : defaultMax;
                    if (lvl >= max) continue;
                    buffer[n++] = new Candidate { Kind = upgradeKind, Index = i, Level = lvl, Weight = 1f };
                }
            }
            return n;
        }
    }

    /// <summary>Sorteio das opções mostradas ao subir de nível.</summary>
    public sealed class LevelUpRoller
    {
        public const int BaseOptions = 3;
        public const int MaxOptions = 4;

        readonly SimRandom _rng;
        readonly Candidate[] _primary;
        readonly Candidate[] _secondary;

        public LevelUpRoller(SimRandom rng, int maxPoolSize = 64)
        {
            _rng = rng;
            _primary = new Candidate[maxPoolSize];
            _secondary = new Candidate[maxPoolSize];
        }

        /// <summary>3 opções, com chance de uma 4ª que cresce com a sorte (sorte 1 = nunca, 1.5 = 25%, 2+ = 50%).</summary>
        public int OptionCount(float luck)
        {
            float chance = Math.Max(0f, Math.Min(0.5f, (luck - 1f) * 0.5f));
            return _rng.NextFloat() < chance ? MaxOptions : BaseOptions;
        }

        /// <summary>
        /// Sorteia até <paramref name="wanted"/> opções distintas, ponderadas por peso, em <paramref name="results"/>.
        /// Candidatos presentes em <paramref name="avoid"/> (opções recém-mostradas, no reroll) só entram se faltar alternativa.
        /// Com pool vazio devolve uma única opção Fallback. Retorna a quantidade escrita.
        /// </summary>
        public int Roll(Candidate[] pool, int poolCount, int wanted, LevelUpOption[] results,
                        LevelUpOption[] avoid = null, int avoidCount = 0)
        {
            if (poolCount == 0)
            {
                results[0] = new LevelUpOption { Kind = OptionKind.Fallback, Index = 0, NewLevel = 0 };
                return 1;
            }

            int a = 0, b = 0;
            for (int i = 0; i < poolCount; i++)
            {
                if (IsAvoided(pool[i], avoid, avoidCount)) _secondary[b++] = pool[i];
                else _primary[a++] = pool[i];
            }

            int n = 0;
            wanted = Math.Min(wanted, results.Length);
            n = Draw(_primary, ref a, wanted, results, n);
            if (n < wanted) n = Draw(_secondary, ref b, wanted, results, n);
            return n;
        }

        int Draw(Candidate[] set, ref int count, int wanted, LevelUpOption[] results, int n)
        {
            while (n < wanted && count > 0)
            {
                float total = 0f;
                for (int i = 0; i < count; i++) total += set[i].Weight;

                int pick = count - 1;
                if (total > 0f)
                {
                    float r = _rng.NextFloat() * total;
                    for (int i = 0; i < count; i++)
                    {
                        r -= set[i].Weight;
                        if (r < 0f) { pick = i; break; }
                    }
                }

                Candidate c = set[pick];
                set[pick] = set[--count];
                results[n++] = new LevelUpOption { Kind = c.Kind, Index = c.Index, NewLevel = c.Level + 1 };
            }
            return n;
        }

        static bool IsAvoided(in Candidate c, LevelUpOption[] avoid, int avoidCount)
        {
            for (int i = 0; i < avoidCount; i++)
                if (avoid[i].Kind == c.Kind && avoid[i].Index == c.Index) return true;
            return false;
        }
    }

    /// <summary>Rerolls e skips restantes na partida (valores iniciais vêm da meta-progressão no marco 6).</summary>
    public sealed class LevelUpAllowance
    {
        public int Rerolls { get; private set; }
        public int Skips { get; private set; }

        public LevelUpAllowance(int rerolls, int skips)
        {
            Rerolls = rerolls;
            Skips = skips;
        }

        public bool TryReroll()
        {
            if (Rerolls <= 0) return false;
            Rerolls--;
            return true;
        }

        public bool TrySkip()
        {
            if (Skips <= 0) return false;
            Skips--;
            return true;
        }
    }
}
