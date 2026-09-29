using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Fundo "infinito": uma grade fixa de (2*radius+1)^2 SpriteRenderers que é reposicionada
    /// quando a câmera muda de célula. A variante de cada tile é função só das coordenadas
    /// da célula no mundo, então o mesmo lugar sempre mostra o mesmo tile (sem "piscar")
    /// e a repetição fica menos óbvia que com uma textura única.
    /// </summary>
    public sealed class InfiniteBackground : MonoBehaviour
    {
        [SerializeField] Sprite[] tileVariants;
        [SerializeField] Transform followTarget;
        [SerializeField] float tileSize = 4f;
        [SerializeField] int radius = 4;
        [SerializeField] int sortingOrder = -100;

        SpriteRenderer[] _tiles;
        Vector2Int _lastCell = new Vector2Int(int.MinValue, int.MinValue);

        void Awake()
        {
            int side = radius * 2 + 1;
            _tiles = new SpriteRenderer[side * side];
            for (int i = 0; i < _tiles.Length; i++)
            {
                var go = new GameObject("Tile");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = sortingOrder;
                _tiles[i] = sr;
            }
        }

        void LateUpdate()
        {
            if (followTarget == null || tileVariants == null || tileVariants.Length == 0) return;

            Vector3 p = followTarget.position;
            var cell = new Vector2Int(Mathf.FloorToInt(p.x / tileSize), Mathf.FloorToInt(p.y / tileSize));
            if (cell == _lastCell) return;
            _lastCell = cell;

            int i = 0;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int cx = cell.x + dx, cy = cell.y + dy;
                    var sr = _tiles[i++];
                    sr.transform.position = new Vector3((cx + 0.5f) * tileSize, (cy + 0.5f) * tileSize, 0f);
                    int variant = Variant(cx, cy, tileVariants.Length);
                    sr.sprite = tileVariants[variant];
                    // Tiles não se sobrepõem, então a ordem não muda o visual; uma ordem por variante agrupa os desenhos
                    // por textura (4 batches em vez de dezenas com as texturas alternando).
                    sr.sortingOrder = sortingOrder + variant;
                }
            }
        }

        /// <summary>Hash inteiro barato e determinístico; nunca retorna negativo.</summary>
        static int Variant(int x, int y, int count)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (int)(h % (uint)count);
            }
        }
    }
}
