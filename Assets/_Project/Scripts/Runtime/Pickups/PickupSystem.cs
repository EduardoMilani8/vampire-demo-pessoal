using UnityEngine;
using UnityEngine.Pool;

namespace HollowTide
{
    public enum PickupKind : byte { Chest, Heal, Magnet, Bomb }

    /// <summary>
    /// Itens no chão coletados ao encostar: baús (de elites/chefes) e os drops raros (cura, ímã, bomba).
    /// São poucos, então ficam num array de structs pequeno; o efeito de cada um é aplicado pelo GameSession.
    /// </summary>
    public sealed class PickupSystem : MonoBehaviour
    {
        const int Capacity = 48;
        const float CollectRadius = 0.75f;

        struct Item
        {
            public PickupKind Kind;
            public float X, Y, Age;
            public SpriteRenderer Renderer;
        }

        [SerializeField] Sprite chestSprite;
        [SerializeField] Sprite healSprite;
        [SerializeField] Sprite magnetSprite;
        [SerializeField] Sprite bombSprite;
        [SerializeField] int sortingOrder = 6;

        readonly Item[] _items = new Item[Capacity];
        int _count;
        ObjectPool<SpriteRenderer> _pool;

        public int Count => _count;

        public void Init()
        {
            _pool = new ObjectPool<SpriteRenderer>(
                createFunc: () =>
                {
                    var go = new GameObject("Pickup");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = sortingOrder;
                    return sr;
                },
                actionOnGet: sr => sr.gameObject.SetActive(true),
                actionOnRelease: sr => sr.gameObject.SetActive(false),
                actionOnDestroy: sr => Destroy(sr.gameObject),
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: Capacity);
        }

        Sprite SpriteFor(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Chest: return chestSprite;
                case PickupKind.Heal: return healSprite;
                case PickupKind.Magnet: return magnetSprite;
                default: return bombSprite;
            }
        }

        public void Spawn(PickupKind kind, float x, float y)
        {
            if (_count == Capacity && !RemoveOldestNonChest()) return;

            var sr = _pool.Get();
            sr.sprite = SpriteFor(kind);
            sr.transform.position = new Vector3(x, y, 0f);
            sr.transform.localScale = Vector3.one;
            _items[_count++] = new Item { Kind = kind, X = x, Y = y, Age = 0f, Renderer = sr };
        }

        bool RemoveOldestNonChest()
        {
            int oldest = -1;
            for (int i = 0; i < _count; i++)
                if (_items[i].Kind != PickupKind.Chest && (oldest < 0 || _items[i].Age > _items[oldest].Age)) oldest = i;
            if (oldest < 0) return false;
            RemoveAt(oldest);
            return true;
        }

        void RemoveAt(int i)
        {
            _pool.Release(_items[i].Renderer);
            _items[i] = _items[--_count];
            _items[_count] = default;
        }

        /// <summary>Anima os itens e coleta os que o jogador tocou. Para de coletar assim que um baú abre (pausa o jogo).</summary>
        public void Tick(float dt, Vector2 player, GameSession session)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                ref Item it = ref _items[i];
                it.Age += dt;

                float bob = Mathf.Sin(it.Age * 4f) * 0.06f;
                float pulse = it.Kind == PickupKind.Chest ? 1f + 0.04f * Mathf.Sin(it.Age * 6f) : 1f;
                it.Renderer.transform.position = new Vector3(it.X, it.Y + bob, 0f);
                it.Renderer.transform.localScale = new Vector3(pulse, pulse, 1f);

                float dx = it.X - player.x, dy = it.Y - player.y;
                if (dx * dx + dy * dy > CollectRadius * CollectRadius) continue;

                PickupKind kind = it.Kind;
                float x = it.X, y = it.Y;
                RemoveAt(i);
                session.CollectPickup(kind, x, y);
                if (session.IsChestOpen) return;
            }
        }
    }
}
