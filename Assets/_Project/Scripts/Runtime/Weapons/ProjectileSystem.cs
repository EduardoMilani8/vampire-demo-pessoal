using System.Collections.Generic;
using HollowTide.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace HollowTide
{
    /// <summary>
    /// Ponte entre o ProjectileWorld (simulação) e a cena: cria as views, aplica os eventos de acerto/explosão
    /// no GameSession e sincroniza a posição visual. Projéteis em arco desenham uma altura parabólica
    /// (só visual) e giram enquanto voam.
    /// </summary>
    public sealed class ProjectileSystem : MonoBehaviour
    {
        struct ExplosionFx
        {
            public SpriteRenderer Renderer;
            public float Time;
            public float Radius;
        }

        [SerializeField] int capacity = 768;
        [SerializeField] Sprite explosionSprite;
        [SerializeField] int sortingOrder = 30;
        [SerializeField] float explosionDuration = 0.28f;

        public ProjectileWorld World { get; private set; }

        ObjectPool<SpriteRenderer> _pool;
        ObjectPool<SpriteRenderer> _explosionPool;
        SpriteRenderer[] _renderers;
        Transform[] _transforms;
        readonly List<ExplosionFx> _explosions = new List<ExplosionFx>(16);

        public void Init()
        {
            World = new ProjectileWorld(capacity);
            _renderers = new SpriteRenderer[capacity];
            _transforms = new Transform[capacity];

            _pool = MakePool("Projectile", sortingOrder);
            _explosionPool = MakePool("Explosion", sortingOrder + 1);
        }

        ObjectPool<SpriteRenderer> MakePool(string objectName, int order)
        {
            return new ObjectPool<SpriteRenderer>(
                createFunc: () =>
                {
                    var go = new GameObject(objectName);
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = order;
                    return sr;
                },
                actionOnGet: sr => sr.gameObject.SetActive(true),
                actionOnRelease: sr => sr.gameObject.SetActive(false),
                actionOnDestroy: sr => Destroy(sr.gameObject),
                collectionCheck: false,
                defaultCapacity: 32,
                maxSize: capacity);
        }

        /// <summary>Cria o projétil e sua view. <paramref name="scale"/> é a escala uniforme do sprite. Retorna -1 se cheio.</summary>
        public int Spawn(in ProjectileSpawn spawn, Sprite sprite, float scale, Color tint)
        {
            int id = World.Spawn(spawn);
            if (id < 0) return -1;

            var sr = _pool.Get();
            sr.sprite = sprite;
            sr.color = tint;
            var t = sr.transform;
            t.localScale = new Vector3(scale, scale, 1f);
            t.position = new Vector3(spawn.X, spawn.Y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(spawn.Vy, spawn.Vx) * Mathf.Rad2Deg);

            _renderers[id] = sr;
            _transforms[id] = t;
            return id;
        }

        public void Tick(float dt)
        {
            var s = GameSession.Instance;
            World.Step(dt, s.Enemies, s.Grid, s.QueryBuffer);

            for (int k = 0; k < World.HitCount; k++)
            {
                int proj = World.HitProjectile[k];
                float vx = World.Vx[proj], vy = World.Vy[proj];
                float len = Mathf.Sqrt(vx * vx + vy * vy);
                if (len > 1e-4f) { vx /= len; vy /= len; } else { vx = 1f; vy = 0f; }
                s.HitEnemy(World.HitEnemy[k], World.Damage[proj], vx, vy, World.Knockback[proj]);
            }

            for (int k = 0; k < World.ExplodedCount; k++)
            {
                int id = World.ExplodedIds[k];
                float x = World.X[id], y = World.Y[id], r = World.ExplodeRadius[id];
                s.DamageArea(x, y, r, World.Damage[id], World.Knockback[id]);
                SpawnExplosionFx(x, y, r);
            }

            for (int k = 0; k < World.ExpiredCount; k++)
            {
                int id = World.ExpiredIds[k];
                if (_renderers[id] == null) continue;
                _pool.Release(_renderers[id]);
                _renderers[id] = null;
                _transforms[id] = null;
            }

            UpdateExplosions(dt);
        }

        void SpawnExplosionFx(float x, float y, float radius)
        {
            if (explosionSprite == null) return;
            var sr = _explosionPool.Get();
            sr.sprite = explosionSprite;
            sr.transform.position = new Vector3(x, y, 0f);
            _explosions.Add(new ExplosionFx { Renderer = sr, Time = 0f, Radius = radius });
        }

        void UpdateExplosions(float dt)
        {
            for (int i = _explosions.Count - 1; i >= 0; i--)
            {
                ExplosionFx fx = _explosions[i];
                fx.Time += dt;

                if (fx.Time >= explosionDuration)
                {
                    _explosionPool.Release(fx.Renderer);
                    int last = _explosions.Count - 1;
                    _explosions[i] = _explosions[last];
                    _explosions.RemoveAt(last);
                    continue;
                }

                float k = fx.Time / explosionDuration;
                float scale = fx.Radius * (0.55f + 0.45f * Mathf.Sqrt(k)); // o sprite tem diâmetro 2 = raio 1 em escala 1
                fx.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
                fx.Renderer.color = new Color(1f, 0.75f, 0.35f, 1f - k * k);
                _explosions[i] = fx;
            }
        }

        void LateUpdate()
        {
            if (World == null) return;

            for (int i = 0; i < World.Count; i++)
            {
                int id = World.Active[i];
                Transform t = _transforms[id];
                float x = World.X[id], y = World.Y[id];

                if (World.Arc[id] > 0f)
                {
                    float progress = 1f - World.Life[id] / World.Duration[id];
                    y += 4f * World.Arc[id] * progress * (1f - progress);
                    t.rotation = Quaternion.Euler(0f, 0f, progress * 720f);
                }
                else
                {
                    t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(World.Vy[id], World.Vx[id]) * Mathf.Rad2Deg);
                }

                t.position = new Vector3(x, y, 0f);
            }
        }
    }
}
