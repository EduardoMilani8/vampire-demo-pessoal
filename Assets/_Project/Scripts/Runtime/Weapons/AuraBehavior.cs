using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Aura de dano contínuo em volta do jogador. O Cooldown da arma é o intervalo entre os pulsos de dano;
    /// cada pulso acerta todos os inimigos dentro do raio (baseSize * Area).
    /// </summary>
    public sealed class AuraBehavior : WeaponBehavior
    {
        const float BaseAlpha = 0.22f;
        const float PulseDecay = 3f;

        SpriteRenderer _visual;
        float _pulse;

        protected override void OnInit()
        {
            var go = new GameObject("AuraFx");
            go.transform.SetParent(FxRoot, false);
            _visual = go.AddComponent<SpriteRenderer>();
            _visual.sprite = Weapon.Data.effectSprite;
            _visual.sortingOrder = 8; // acima do chão e das gemas, abaixo dos inimigos
        }

        public override void OnRemoved()
        {
            if (_visual != null) Object.Destroy(_visual.gameObject);
        }

        public override void Tick(float dt)
        {
            WeaponLevelStats stats = Weapon.Resolved(Session.Stats);
            Vector2 p = Session.Player.Position;
            float radius = Weapon.Data.baseSize * stats.Area;

            if (Weapon.Timer.Tick(dt, stats.Cooldown))
            {
                Session.DamageArea(p.x, p.y, radius, stats.Damage, stats.Knockback);
                _pulse = 1f;
            }

            _pulse = Mathf.Max(0f, _pulse - dt * PulseDecay);

            if (_visual != null && _visual.sprite != null)
            {
                float spriteRadius = _visual.sprite.bounds.size.x * 0.5f;
                float scale = radius / spriteRadius * (1f + 0.04f * _pulse);
                var t = _visual.transform;
                t.position = new Vector3(p.x, p.y, 0f);
                t.localScale = new Vector3(scale, scale, 1f);

                var tint = Weapon.Data.effectTint;
                _visual.color = new Color(tint.r, tint.g, tint.b, tint.a * (BaseAlpha + 0.28f * _pulse));
            }
        }
    }
}
