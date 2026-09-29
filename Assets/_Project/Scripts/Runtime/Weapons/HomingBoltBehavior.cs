using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Projéteis teleguiados: saem na direção do inimigo mais próximo (achado no spatial hash) e continuam
    /// curvando em direção a um alvo enquanto vivem. Vários disparos saem em sequência, levemente abertos em leque.
    /// </summary>
    public sealed class HomingBoltBehavior : WeaponBehavior
    {
        const float ShotInterval = 0.12f;
        const float SearchRadius = 16f;
        const float TurnRate = 6f;
        const float SpreadDegrees = 9f;

        int _pending;
        int _shotIndex;
        float _shotDelay;

        public override void Tick(float dt)
        {
            WeaponLevelStats stats = Weapon.Resolved(Session.Stats);

            if (_pending == 0 && Weapon.Timer.Tick(dt, stats.Cooldown))
            {
                _pending = Mathf.Max(1, stats.Amount);
                _shotIndex = 0;
                _shotDelay = 0f;
            }

            if (_pending > 0)
            {
                _shotDelay -= dt;
                if (_shotDelay <= 0f)
                {
                    Fire(stats, _shotIndex++);
                    _pending--;
                    _shotDelay = ShotInterval;
                }
            }
        }

        void Fire(in WeaponLevelStats stats, int index)
        {
            Vector2 origin = Session.Player.Position;
            Vector2 dir = Session.Player.Facing;
            if (Session.Grid.FindNearest(origin.x, origin.y, SearchRadius, out int target, out _))
            {
                dir = new Vector2(Session.Enemies.X[target] - origin.x, Session.Enemies.Y[target] - origin.y).normalized;
            }

            // Leque: 0, +1, -1, +2, -2 ... passos de SpreadDegrees.
            int step = (index + 1) / 2 * ((index & 1) == 1 ? 1 : -1);
            float angle = Mathf.Atan2(dir.y, dir.x) + step * SpreadDegrees * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var data = Weapon.Data;
            float speed = data.baseSpeed * stats.ProjectileSpeed;
            float radius = data.baseSize * stats.Area;

            var spawn = new ProjectileSpawn
            {
                X = origin.x, Y = origin.y, Vx = d.x * speed, Vy = d.y * speed,
                Radius = radius, Damage = stats.Damage, Knockback = stats.Knockback,
                Life = data.baseDuration * stats.Duration,
                Pierce = stats.Pierce, TurnRate = TurnRate,
            };

            float spriteWidth = data.effectSprite != null ? data.effectSprite.bounds.size.x : 1f;
            Session.Projectiles.Spawn(spawn, data.effectSprite, radius * 2f / spriteWidth, data.effectTint);
        }
    }
}
