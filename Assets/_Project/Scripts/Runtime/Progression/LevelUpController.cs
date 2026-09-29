using System;
using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>Como uma opção aparece na UI de level up.</summary>
    public struct OptionView
    {
        public string Title;
        public string Tag;
        public string Description;
        public Sprite Icon;
        public OptionKind Kind;
    }

    /// <summary>
    /// Cola entre o Core (sorteio puro por índices) e o conteúdo real: monta os níveis atuais a partir
    /// das armas/passivos equipados, sorteia, descreve as opções para a UI e aplica a escolhida.
    /// </summary>
    public sealed class LevelUpController
    {
        const float FallbackHealFraction = 0.3f;

        readonly ContentCatalog _catalog;
        readonly PlayerWeapons _weapons;
        readonly PlayerPassives _passives;
        readonly LevelUpRoller _roller;

        readonly Candidate[] _pool = new Candidate[64];
        // 6 posições: o level up usa até 4, o baú até 5 melhorias.
        readonly LevelUpOption[] _options = new LevelUpOption[6];
        readonly LevelUpOption[] _previous = new LevelUpOption[LevelUpRoller.MaxOptions];
        readonly int[] _weaponLevels;
        readonly int[] _passiveLevels;
        readonly int[] _passiveMax;
        int _previousCount;

        public int OptionCount { get; private set; }

        public LevelUpController(ContentCatalog catalog, PlayerWeapons weapons, PlayerPassives passives, uint seed)
        {
            _catalog = catalog;
            _weapons = weapons;
            _passives = passives;
            _roller = new LevelUpRoller(new SimRandom(seed));
            _weaponLevels = new int[catalog.weapons.Length];
            _passiveLevels = new int[catalog.passives.Length];
            _passiveMax = new int[catalog.passives.Length];
        }

        /// <param name="avoidCurrent">Reroll: evita repetir as opções que estavam na tela.</param>
        public void Roll(float luck, bool avoidCurrent)
        {
            if (avoidCurrent)
            {
                Array.Copy(_options, _previous, OptionCount);
                _previousCount = OptionCount;
            }
            else
            {
                _previousCount = 0;
            }

            int poolCount = BuildPool();
            OptionCount = _roller.Roll(_pool, poolCount, _roller.OptionCount(luck), _options, _previous, _previousCount);
        }

        int BuildPool()
        {
            for (int i = 0; i < _weaponLevels.Length; i++)
            {
                var data = _catalog.weapons[i];
                var owned = _weapons.Find(data);
                // Fora do pool: armas só obtidas por evolução e armas cuja evolução já foi conquistada.
                bool evolvedAway = data.evolvesInto != null && _weapons.Find(data.evolvesInto) != null;
                _weaponLevels[i] = owned != null ? owned.Level : (data.evolutionOnly || evolvedAway ? -1 : 0);
            }

            for (int i = 0; i < _passiveLevels.Length; i++)
            {
                var data = _catalog.passives[i];
                var owned = _passives.Find(data);
                _passiveLevels[i] = owned != null ? owned.Level : 0;
                _passiveMax[i] = data.maxLevel;
            }

            return LevelUpPool.Build(_pool, _weaponLevels, WeaponProgression.MaxLevel, LevelUpPool.MaxWeapons,
                                     _passiveLevels, _passiveMax, LevelUpPool.MaxPassives);
        }

        /// <summary>
        /// Recompensa de baú: sorteia <paramref name="count"/> melhorias entre o que o jogador já possui (sem itens novos)
        /// e as APLICA. Devolve quantas foram aplicadas; use GetView(i) para descrevê-las. Sem nada para melhorar,
        /// devolve 1 opção de Suprimentos (cura).
        /// </summary>
        public int RollAndApplyUpgrades(GameSession session, int count)
        {
            int n = BuildPool();
            int m = 0;
            for (int i = 0; i < n; i++)
                if (_pool[i].Kind == OptionKind.UpgradeWeapon || _pool[i].Kind == OptionKind.UpgradePassive) _pool[m++] = _pool[i];

            _previousCount = 0;
            OptionCount = _roller.Roll(_pool, m, count, _options, null, 0);
            for (int i = 0; i < OptionCount; i++) Apply(session, i);
            return OptionCount;
        }

        public OptionView GetView(int i)
        {
            LevelUpOption o = _options[i];
            switch (o.Kind)
            {
                case OptionKind.NewWeapon:
                {
                    var w = _catalog.weapons[o.Index];
                    return new OptionView { Kind = o.Kind, Title = w.displayName, Tag = "NOVA ARMA", Description = w.description, Icon = w.icon };
                }
                case OptionKind.UpgradeWeapon:
                {
                    var w = _catalog.weapons[o.Index];
                    int deltaIndex = o.NewLevel - 2;
                    string desc = deltaIndex >= 0 && deltaIndex < w.levelUps.Length ? StatText.DescribeDelta(w.levelUps[deltaIndex]) : "";
                    return new OptionView { Kind = o.Kind, Title = w.displayName, Tag = LevelTag(o.NewLevel, WeaponProgression.MaxLevel), Description = desc, Icon = w.icon };
                }
                case OptionKind.NewPassive:
                {
                    var p = _catalog.passives[o.Index];
                    return new OptionView { Kind = o.Kind, Title = p.displayName, Tag = "NOVO PASSIVO", Description = p.description + "\n" + p.DescribeLevel(), Icon = p.icon };
                }
                case OptionKind.UpgradePassive:
                {
                    var p = _catalog.passives[o.Index];
                    return new OptionView { Kind = o.Kind, Title = p.displayName, Tag = LevelTag(o.NewLevel, p.maxLevel), Description = p.DescribeLevel(), Icon = p.icon };
                }
                default:
                    return new OptionView { Kind = o.Kind, Title = "Suprimentos", Tag = "", Description = "Recupera 30% da vida.", Icon = null };
            }
        }

        static string LevelTag(int newLevel, int max) => newLevel >= max ? $"NÍVEL {newLevel} (MÁX)" : $"NÍVEL {newLevel}";

        public void Apply(GameSession session, int i)
        {
            LevelUpOption o = _options[i];
            switch (o.Kind)
            {
                case OptionKind.NewWeapon: _weapons.Add(_catalog.weapons[o.Index]); break;
                case OptionKind.UpgradeWeapon: _weapons.Find(_catalog.weapons[o.Index])?.TryLevelUp(); break;
                case OptionKind.NewPassive: _passives.Add(_catalog.passives[o.Index]); break;
                case OptionKind.UpgradePassive:
                    var p = _passives.Find(_catalog.passives[o.Index]);
                    if (p != null) _passives.TryLevelUp(p);
                    break;
                case OptionKind.Fallback: session.Health.Heal(session.Health.Max * FallbackHealFraction); break;
            }
        }
    }
}
