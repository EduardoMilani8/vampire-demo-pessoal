using HollowTide.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace HollowTide
{
    /// <summary>
    /// Espelho visual do GemWorld (mesmo padrão do EnemyViews). Gemas paradas não precisam de sync por frame,
    /// então só as magnetizadas (em movimento) têm o transform atualizado.
    /// </summary>
    public sealed class GemViews : MonoBehaviour
    {
        [Tooltip("Sprites por faixa de valor: 0 = pequena, 1 = média, 2 = grande.")]
        [SerializeField] Sprite[] tierSprites;
        [SerializeField] int sortingOrder = 5;

        ObjectPool<SpriteRenderer> _pool;
        SpriteRenderer[] _renderers;
        Transform[] _transforms;

        public void Init(int capacity)
        {
            _renderers = new SpriteRenderer[capacity];
            _transforms = new Transform[capacity];
            _pool = new ObjectPool<SpriteRenderer>(
                createFunc: () =>
                {
                    var go = new GameObject("Gem");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = sortingOrder;
                    return sr;
                },
                actionOnGet: sr => sr.gameObject.SetActive(true),
                actionOnRelease: sr => sr.gameObject.SetActive(false),
                actionOnDestroy: sr => Destroy(sr.gameObject),
                collectionCheck: false,
                defaultCapacity: 256,
                maxSize: capacity);
        }

        public static int TierOf(float value) => value < 3f ? 0 : value < 10f ? 1 : 2;

        public void Attach(int id, float value, float x, float y)
        {
            var sr = _pool.Get();
            _renderers[id] = sr;
            _transforms[id] = sr.transform;
            sr.sprite = tierSprites[Mathf.Min(TierOf(value), tierSprites.Length - 1)];
            _transforms[id].position = new Vector3(x, y, 0f);
        }

        public void Release(int id)
        {
            var sr = _renderers[id];
            if (sr == null) return;
            _pool.Release(sr);
            _renderers[id] = null;
            _transforms[id] = null;
        }

        void LateUpdate()
        {
            var session = GameSession.Instance;
            if (session == null || _renderers == null) return;

            GemWorld g = session.Gems;
            for (int i = 0; i < g.Count; i++)
            {
                int id = g.Active[i];
                if (g.Magnetized[id]) _transforms[id].position = new Vector3(g.X[id], g.Y[id], 0f);
            }
        }
    }
}
