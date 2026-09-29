using System;

namespace HollowTide.Core
{
    /// <summary>
    /// Estatísticas de uma arma num nível. Campos públicos + [Serializable] para o Inspector da Unity
    /// editar direto dentro do ScriptableObject, sem o Core depender do engine.
    /// Area, Duration e ProjectileSpeed são multiplicadores (1 = tamanho/velocidade base da arma).
    /// </summary>
    [Serializable]
    public struct WeaponLevelStats
    {
        public float Damage;
        public float Area;
        public float Cooldown;
        public float Duration;
        public float ProjectileSpeed;
        public float Knockback;
        public int Amount;
        public int Pierce;

        public static WeaponLevelStats Default => new WeaponLevelStats
        {
            Damage = 10f, Area = 1f, Cooldown = 1f, Duration = 1f,
            ProjectileSpeed = 1f, Knockback = 0f, Amount = 1, Pierce = 0
        };
    }

    /// <summary>
    /// Melhoria ganha ao subir para um nível. Somas planas para dano/quantidade/perfuração/knockback;
    /// percentuais aditivos (0.2 = +20%) sobre a base para os demais. CooldownPct negativo reduz o cooldown.
    /// </summary>
    [Serializable]
    public struct StatDelta
    {
        public float DamageAdd;
        public float AreaPct;
        public float CooldownPct;
        public float DurationPct;
        public float SpeedPct;
        public float KnockbackAdd;
        public int AmountAdd;
        public int PierceAdd;
    }

    /// <summary>Passivos e bônus de personagem consolidados. Os passivos do marco 4 escrevem aqui.</summary>
    public struct PlayerStats
    {
        public float Might;              // multiplicador de dano
        public float Armor;              // redução plana de dano recebido
        public float MaxHpMult;
        public float Recovery;           // vida por segundo
        public float Cooldown;           // multiplicador (menor = mais rápido)
        public float Area;
        public float ProjectileSpeed;
        public float Duration;
        public float MoveSpeed;
        public float Magnet;             // multiplicador do raio de coleta
        public float Growth;             // multiplicador de XP
        public float Luck;
        public int Amount;               // projéteis extras em todas as armas

        public static PlayerStats Default => new PlayerStats
        {
            Might = 1f, Armor = 0f, MaxHpMult = 1f, Recovery = 0f, Cooldown = 1f, Area = 1f,
            ProjectileSpeed = 1f, Duration = 1f, MoveSpeed = 1f, Magnet = 1f, Growth = 1f, Luck = 1f, Amount = 0
        };
    }

    public static class WeaponProgression
    {
        public const int MaxLevel = 8;
        public const float MinCooldown = 0.05f;

        /// <summary>
        /// Estatísticas no nível <paramref name="level"/> (1..MaxLevel). deltas[i] é a melhoria obtida
        /// ao chegar no nível i+2, então o nível 1 usa só a base e o nível 8 usa 7 deltas.
        /// </summary>
        public static WeaponLevelStats StatsAtLevel(in WeaponLevelStats baseStats, StatDelta[] deltas, int level)
        {
            level = Math.Max(1, Math.Min(MaxLevel, level));
            int apply = deltas == null ? 0 : Math.Min(level - 1, deltas.Length);

            float dmg = 0, area = 0, cd = 0, dur = 0, spd = 0, kb = 0;
            int amt = 0, prc = 0;
            for (int i = 0; i < apply; i++)
            {
                ref readonly StatDelta d = ref deltas[i];
                dmg += d.DamageAdd; area += d.AreaPct; cd += d.CooldownPct; dur += d.DurationPct;
                spd += d.SpeedPct; kb += d.KnockbackAdd; amt += d.AmountAdd; prc += d.PierceAdd;
            }

            WeaponLevelStats r = baseStats;
            r.Damage = baseStats.Damage + dmg;
            r.Area = baseStats.Area * (1f + area);
            r.Cooldown = Math.Max(MinCooldown, baseStats.Cooldown * (1f + cd));
            r.Duration = baseStats.Duration * (1f + dur);
            r.ProjectileSpeed = baseStats.ProjectileSpeed * (1f + spd);
            r.Knockback = baseStats.Knockback + kb;
            r.Amount = baseStats.Amount + amt;
            r.Pierce = baseStats.Pierce + prc;
            return r;
        }
    }

    public static class WeaponMath
    {
        /// <summary>Aplica os passivos do jogador às estatísticas do nível da arma.</summary>
        public static WeaponLevelStats Resolve(in WeaponLevelStats s, in PlayerStats p)
        {
            WeaponLevelStats r = s;
            r.Damage = s.Damage * p.Might;
            r.Area = s.Area * p.Area;
            r.Cooldown = Math.Max(WeaponProgression.MinCooldown, s.Cooldown * p.Cooldown);
            r.Duration = s.Duration * p.Duration;
            r.ProjectileSpeed = s.ProjectileSpeed * p.ProjectileSpeed;
            r.Amount = s.Amount + p.Amount;
            return r;
        }
    }

    /// <summary>
    /// Temporizador de cooldown. O cooldown é passado a cada Tick porque passivos podem mudá-lo no meio da run.
    /// O excesso de tempo do frame é preservado (disparo estável), mas nunca acumula backlog de disparos.
    /// </summary>
    public struct WeaponTimer
    {
        float _remaining;

        public float Remaining => _remaining;

        /// <summary>True se a arma deve disparar neste tick. Começa pronta (remaining = 0).</summary>
        public bool Tick(float dt, float cooldown)
        {
            _remaining -= dt;
            if (_remaining > 0f) return false;

            _remaining += cooldown;
            // Se o frame pulou um ciclo inteiro, descarta o excesso: reinicia o cooldown em vez de
            // disparar de novo no frame seguinte.
            if (_remaining <= 0f) _remaining = cooldown;
            return true;
        }

        public void Reset() => _remaining = 0f;
    }
}
