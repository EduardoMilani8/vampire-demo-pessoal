using HollowTide.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace HollowTide
{
    /// <summary>
    /// Espelho visual do EnemyWorld: um SpriteRenderer pooled por inimigo vivo, indexado pelo id da simulação.
    /// Nenhuma lógica de jogo aqui, só apresentação. Sem GetComponent, dicionários ou alocação por frame.
    /// </summary>
    public sealed class EnemyViews : MonoBehaviour
    {
        [SerializeField] int sortingOrder = 10;

        ObjectPool<SpriteRenderer> _pool;
        SpriteRenderer[] _renderers;
        Transform[] _transforms;
        EnemyData[] _data;
        bool[] _flashing;

        public void Init(int capacity)
        {
            _renderers = new SpriteRenderer[capacity];
            _transforms = new Transform[capacity];
            _data = new EnemyData[capacity];
            _flashing = new bool[capacity];

            _pool = new ObjectPool<SpriteRenderer>(
                createFunc: CreateView,
                actionOnGet: sr => sr.gameObject.SetActive(true),
                actionOnRelease: sr => sr.gameObject.SetActive(false),
                actionOnDestroy: sr => Destroy(sr.gameObject),
                collectionCheck: false,
                defaultCapacity: 256,
                maxSize: capacity);
        }

        SpriteRenderer CreateView()
        {
            var go = new GameObject("Enemy");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        public void Attach(int id, EnemyData data, float x, float y)
        {
            var sr = _pool.Get();
            _renderers[id] = sr;
            _transforms[id] = sr.transform;
            _data[id] = data;
            _flashing[id] = false;

            sr.sprite = data.sprite;
            sr.sortingOrder = data.sortingOrder;
            sr.flipX = false;
            _transforms[id].localScale = new Vector3(data.visualScale, data.visualScale, 1f);
            _transforms[id].position = new Vector3(x, y, 0f);
        }

        public void Release(int id)
        {
            var sr = _renderers[id];
            if (sr == null) return;
            _pool.Release(sr);
            _renderers[id] = null;
            _transforms[id] = null;
            _data[id] = null;
        }

        public EnemyData DataOf(int id) => _data[id];

        void LateUpdate()
        {
            var session = GameSession.Instance;
            if (session == null || _renderers == null) return;

            EnemyWorld w = session.Enemies;
            float playerX = session.Player.Position.x;

            for (int i = 0; i < w.Count; i++)
            {
                int id = w.Active[i];
                float x = w.X[id];
                _transforms[id].position = new Vector3(x, w.Y[id], 0f);

                var sr = _renderers[id];
                sr.flipX = x > playerX; // sprites são desenhados olhando para a direita; anda para o jogador

                bool flash = w.FlashTimer[id] > 0f;
                if (flash != _flashing[id])
                {
                    _flashing[id] = flash;
                    var d = _data[id];
                    sr.sprite = flash && d.flashSprite != null ? d.flashSprite : d.sprite;
                }
            }
        }
    }
}
