using System.Collections.Generic;
using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Orbitais em volta do jogador. Ficam ativos por baseDuration * Duration segundos e somem por Cooldown
    /// segundos antes de voltar. Cada orbital só acerta o mesmo inimigo a cada HitInterval (HitCooldowns).
    /// </summary>
    public sealed class OrbitBehavior : WeaponBehavior
    {
        const float HitInterval = 0.55f;
        const float BaseHitRadius = 0.32f;

        readonly List<SpriteRenderer> _orbs = new List<SpriteRenderer>(12);
        HitCooldowns _hits;
        bool _active;
        float _activeLeft;
        float _angle;

        protected override void OnInit() => _hits = new HitCooldowns(Session.Enemies.Capacity);

        public override void OnRemoved() => DestroyOrbs();

        public override void Tick(float dt)
        {
            WeaponLevelStats stats = Weapon.Resolved(Session.Stats);

            if (!_active)
            {
                if (Weapon.Timer.Tick(dt, stats.Cooldown))
                {
                    _active = true;
                    _activeLeft = Weapon.Data.baseDuration * stats.Duration;
                }
                else
                {
                    return;
                }
            }

            _activeLeft -= dt;
            if (_activeLeft <= 0f)
            {
                _active = false;
                DestroyOrbs();
                return;
            }

            int count = Mathf.Max(1, stats.Amount);
            EnsureOrbs(count);

            _angle += Weapon.Data.baseSpeed * stats.ProjectileSpeed * Mathf.Deg2Rad * dt;
            Vector2 center = Session.Player.Position;
            float orbitRadius = Weapon.Data.baseSize;
            float hitRadius = BaseHitRadius * stats.Area;

            for (int k = 0; k < count; k++)
            {
                float a = _angle + k * (Mathf.PI * 2f / count);
                var pos = new Vector2(center.x + Mathf.Cos(a) * orbitRadius, center.y + Mathf.Sin(a) * orbitRadius);

                var orb = _orbs[k].transform;
                orb.position = new Vector3(pos.x, pos.y, 0f);
                orb.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                float s = stats.Area;
                orb.localScale = new Vector3(s, s, 1f);

                HitAround(pos, hitRadius, stats);
            }
        }

        void HitAround(Vector2 pos, float radius, in WeaponLevelStats stats)
        {
            EnemyWorld enemies = Session.Enemies;
            int[] buffer = Session.QueryBuffer;
            int n = Session.Grid.Query(pos.x, pos.y, radius + enemies.MaxRadius, buffer);

            for (int i = 0; i < n; i++)
            {
                int id = buffer[i];
                if (!enemies.Alive[id]) continue;

                float dx = enemies.X[id] - pos.x, dy = enemies.Y[id] - pos.y;
                float reach = radius + enemies.Radius[id];
                if (dx * dx + dy * dy > reach * reach) continue;
                if (!_hits.TryHit(id, Session.Elapsed, HitInterval)) continue;

                float len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len > 1e-4f) { dx /= len; dy /= len; } else { dx = 1f; dy = 0f; }
                Session.HitEnemy(id, stats.Damage, dx, dy, stats.Knockback);
            }
        }

        void EnsureOrbs(int count)
        {
            while (_orbs.Count < count)
            {
                var go = new GameObject("Orbital");
                go.transform.SetParent(FxRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Weapon.Data.effectSprite;
                sr.color = Weapon.Data.effectTint;
                sr.sortingOrder = 25;
                _orbs.Add(sr);
            }

            for (int i = 0; i < _orbs.Count; i++) _orbs[i].gameObject.SetActive(i < count);
        }

        void DestroyOrbs()
        {
            for (int i = 0; i < _orbs.Count; i++)
                if (_orbs[i] != null) Object.Destroy(_orbs[i].gameObject);
            _orbs.Clear();
        }
    }
}
