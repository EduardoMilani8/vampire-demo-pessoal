using System.Collections.Generic;
using HollowTide.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace HollowTide
{
    /// <summary>
    /// Golpe em arco na direção do movimento. Com Amount > 1 os golpes seguintes alternam de lado
    /// (frente, trás, frente...) com um pequeno intervalo. O acerto é um setor circular (ArcHit) contra o
    /// spatial hash; o sprite do efeito é só o crescente da borda, então a hitbox é um pouco mais generosa que o visual.
    /// </summary>
    public sealed class ArcSlashBehavior : WeaponBehavior
    {
        const float SwingInterval = 0.15f;
        const float FxBaseDuration = 0.2f;
        const float Deg2Rad = 0.0174532925f;

        struct Fx
        {
            public SpriteRenderer Renderer;
            public float Time;
            public float Duration;
        }

        ObjectPool<SpriteRenderer> _pool;
        readonly List<Fx> _fx = new List<Fx>(16);
        public override void OnRemoved()
        {
            for (int i = 0; i < _fx.Count; i++) _pool.Release(_fx[i].Renderer);
            _fx.Clear();
        }

        int _pending;
        int _swingIndex;
        float _swingDelay;

        protected override void OnInit()
        {
            _pool = new ObjectPool<SpriteRenderer>(
                createFunc: CreateFx,
                actionOnGet: sr => sr.gameObject.SetActive(true),
                actionOnRelease: sr => sr.gameObject.SetActive(false),
                actionOnDestroy: sr => Object.Destroy(sr.gameObject),
                collectionCheck: false,
                defaultCapacity: 8,
                maxSize: 64);
        }

        SpriteRenderer CreateFx()
        {
            var go = new GameObject("SlashFx");
            go.transform.SetParent(FxRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 30;
            sr.sprite = Weapon.Data.effectSprite;
            return sr;
        }

        public override void Tick(float dt)
        {
            WeaponLevelStats stats = Weapon.Resolved(Session.Stats);

            // O cooldown só corre quando não há golpes pendentes na sequência.
            if (_pending == 0 && Weapon.Timer.Tick(dt, stats.Cooldown))
            {
                _pending = Mathf.Max(1, stats.Amount);
                _swingIndex = 0;
                _swingDelay = 0f;
            }

            if (_pending > 0)
            {
                _swingDelay -= dt;
                if (_swingDelay <= 0f)
                {
                    Swing(stats, _swingIndex++);
                    _pending--;
                    _swingDelay = SwingInterval;
                }
            }

            UpdateFx(dt);
        }

        void Swing(in WeaponLevelStats stats, int index)
        {
            Vector2 facing = Session.Player.Facing;
            Vector2 dir = (index & 1) == 0 ? facing : -facing;
            Vector2 origin = Session.Player.Position + dir * 0.2f;

            float radius = Weapon.Data.baseSize * stats.Area;
            float halfAngle = Weapon.Data.arcAngleDegrees * 0.5f * Deg2Rad;

            EnemyWorld enemies = Session.Enemies;
            int[] buffer = Session.QueryBuffer;
            int n = Session.Grid.Query(origin.x, origin.y, radius + enemies.MaxRadius, buffer);

            for (int i = 0; i < n; i++)
            {
                int id = buffer[i];
                if (!enemies.Alive[id]) continue;

                float ex = enemies.X[id], ey = enemies.Y[id];
                if (!ArcHit.InArc(origin.x, origin.y, dir.x, dir.y, radius, halfAngle, ex, ey, enemies.Radius[id]))
                    continue;

                // Knockback radial a partir do golpe; se estiver exatamente na origem, usa a direção do golpe.
                float kx = ex - origin.x, ky = ey - origin.y;
                float len = Mathf.Sqrt(kx * kx + ky * ky);
                if (len > 1e-4f) { kx /= len; ky /= len; } else { kx = dir.x; ky = dir.y; }

                Session.HitEnemy(id, stats.Damage, kx, ky, stats.Knockback);
            }

            SpawnFx(origin, dir, radius, FxBaseDuration * stats.Duration);
        }

        void SpawnFx(Vector2 origin, Vector2 dir, float radius, float duration)
        {
            var sr = _pool.Get();
            float spriteRadius = sr.sprite.bounds.size.x; // o pivô fica no centro do arco; o sprite se estende 1 raio
            float scale = radius / spriteRadius;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            var t = sr.transform;
            t.position = new Vector3(origin.x, origin.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = new Vector3(scale, scale, 1f);
            sr.color = Weapon.Data.effectTint;

            _fx.Add(new Fx { Renderer = sr, Time = 0f, Duration = Mathf.Max(0.05f, duration) });
        }

        void UpdateFx(float dt)
        {
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                Fx f = _fx[i];
                f.Time += dt;

                if (f.Time >= f.Duration)
                {
                    _pool.Release(f.Renderer);
                    int last = _fx.Count - 1;
                    _fx[i] = _fx[last];
                    _fx.RemoveAt(last);
                    continue;
                }

                float k = f.Time / f.Duration;
                var tint = Weapon.Data.effectTint;
                f.Renderer.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - k * k));
                _fx[i] = f;
            }
        }
    }
}
