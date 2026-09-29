using System;

namespace HollowTide.Core
{
    /// <summary>Vida do jogador: armadura plana (mínimo 1 de dano), invulnerabilidade curta pós-dano e regeneração.</summary>
    public sealed class PlayerHealth
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public float Armor { get; set; }
        public float RegenPerSecond { get; set; }
        public float InvulnDuration { get; set; } = 0.5f;

        float _invuln;

        public PlayerHealth(float max)
        {
            Max = max;
            Current = max;
        }

        public bool IsDead => Current <= 0f;
        public bool IsInvulnerable => _invuln > 0f;
        public float Ratio => Max <= 0f ? 0f : Current / Max;

        /// <summary>Retorna true se o golpe causou dano (falso se morto ou invulnerável).</summary>
        public bool TryHit(float rawDamage)
        {
            if (IsDead || IsInvulnerable) return false;
            float dmg = MathF.Max(1f, rawDamage - Armor);
            Current = MathF.Max(0f, Current - dmg);
            _invuln = InvulnDuration;
            return true;
        }

        public void Tick(float dt)
        {
            if (_invuln > 0f) _invuln -= dt;
            if (!IsDead && RegenPerSecond > 0f && Current < Max)
                Current = MathF.Min(Max, Current + RegenPerSecond * dt);
        }

        public void Heal(float amount)
        {
            if (!IsDead) Current = MathF.Min(Max, Current + amount);
        }

        /// <summary>Muda a vida máxima preservando a proporção atual (passivo de vida máxima).</summary>
        public void SetMax(float newMax)
        {
            float ratio = Ratio;
            Max = newMax;
            Current = MathF.Max(IsDead ? 0f : 1f, newMax * ratio);
        }
    }
}
