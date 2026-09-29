using System;

namespace HollowTide.Core
{
    /// <summary>Os 12 atributos que os passivos podem alterar.</summary>
    public enum PassiveStat
    {
        Might,            // dano
        Armor,            // armadura
        MaxHp,            // vida máxima
        Recovery,         // regeneração
        Cooldown,         // recarga
        Area,             // área
        ProjectileSpeed,  // velocidade de projétil
        Duration,         // duração
        MoveSpeed,        // velocidade de movimento
        Magnet,           // raio de coleta
        Growth,           // XP extra
        Luck,             // sorte
    }

    public static class PassiveMath
    {
        /// <summary>Recarga nunca cai abaixo de 30% da base, por mais passivos que existam.</summary>
        public const float MinCooldownMultiplier = 0.3f;

        /// <summary>Soma o efeito de um passivo (<paramref name="amount"/> = valor por nível x nível) às estatísticas.</summary>
        public static void Apply(ref PlayerStats s, PassiveStat stat, float amount)
        {
            switch (stat)
            {
                case PassiveStat.Might: s.Might += amount; break;
                case PassiveStat.Armor: s.Armor += amount; break;
                case PassiveStat.MaxHp: s.MaxHpMult += amount; break;
                case PassiveStat.Recovery: s.Recovery += amount; break;
                case PassiveStat.Cooldown: s.Cooldown = Math.Max(MinCooldownMultiplier, s.Cooldown - amount); break;
                case PassiveStat.Area: s.Area += amount; break;
                case PassiveStat.ProjectileSpeed: s.ProjectileSpeed += amount; break;
                case PassiveStat.Duration: s.Duration += amount; break;
                case PassiveStat.MoveSpeed: s.MoveSpeed += amount; break;
                case PassiveStat.Magnet: s.Magnet += amount; break;
                case PassiveStat.Growth: s.Growth += amount; break;
                case PassiveStat.Luck: s.Luck += amount; break;
                default: throw new ArgumentOutOfRangeException(nameof(stat));
            }
        }
    }
}
