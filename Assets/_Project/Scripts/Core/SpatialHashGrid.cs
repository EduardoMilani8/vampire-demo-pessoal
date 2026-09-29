using System;

namespace HollowTide.Core
{
    /// <summary>
    /// Spatial hash para mapa infinito. Reconstruído todo frame (Clear + Insert), sem alocação.
    /// Itens são identificados por um id inteiro em [0, itemCapacity).
    /// Cada bucket é uma lista encadeada em arrays (head/next). Como células distintas podem cair
    /// no mesmo bucket, cada item guarda sua célula e a consulta só aceita itens da célula visitada:
    /// nunca há resultado duplicado ou fora da célula.
    /// </summary>
    public sealed class SpatialHashGrid
    {
        readonly float _cell;
        readonly float _inv;
        readonly int _mask;
        readonly int[] _head;
        readonly int[] _next;
        readonly int[] _cx;
        readonly int[] _cy;
        readonly float[] _x;
        readonly float[] _y;

        public float CellSize => _cell;

        /// <param name="bucketCount">Deve ser potência de 2.</param>
        public SpatialHashGrid(float cellSize, int bucketCount, int itemCapacity)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (bucketCount <= 0 || (bucketCount & (bucketCount - 1)) != 0)
                throw new ArgumentException("bucketCount deve ser potência de 2", nameof(bucketCount));

            _cell = cellSize;
            _inv = 1f / cellSize;
            _mask = bucketCount - 1;
            _head = new int[bucketCount];
            _next = new int[itemCapacity];
            _cx = new int[itemCapacity];
            _cy = new int[itemCapacity];
            _x = new float[itemCapacity];
            _y = new float[itemCapacity];
            Clear();
        }

        public void Clear() => Array.Fill(_head, -1);

        public void Insert(int id, float x, float y)
        {
            int cx = Cell(x), cy = Cell(y);
            int b = Bucket(cx, cy);
            _cx[id] = cx;
            _cy[id] = cy;
            _x[id] = x;
            _y[id] = y;
            _next[id] = _head[b];
            _head[b] = id;
        }

        /// <summary>
        /// Escreve em <paramref name="results"/> os ids a até <paramref name="radius"/> do ponto (centro a centro).
        /// Retorna a quantidade escrita; se o buffer encher, para (truncamento silencioso).
        /// </summary>
        public int Query(float x, float y, float radius, int[] results)
        {
            int n = 0;
            float r2 = radius * radius;
            int minX = Cell(x - radius), maxX = Cell(x + radius);
            int minY = Cell(y - radius), maxY = Cell(y + radius);

            for (int cy = minY; cy <= maxY; cy++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    for (int id = _head[Bucket(cx, cy)]; id >= 0; id = _next[id])
                    {
                        if (_cx[id] != cx || _cy[id] != cy) continue;
                        float dx = _x[id] - x, dy = _y[id] - y;
                        if (dx * dx + dy * dy > r2) continue;
                        if (n == results.Length) return n;
                        results[n++] = id;
                    }
                }
            }
            return n;
        }

        /// <summary>
        /// Item mais próximo dentro de <paramref name="maxRadius"/>. Varre quadrados de células
        /// com meia-largura k = 1, 2, 4...; um candidato a distância &lt;= k*cell é garantidamente o
        /// mais próximo, porque o ponto está dentro da célula central e o quadrado se estende
        /// pelo menos k*cell em todas as direções.
        /// </summary>
        public bool FindNearest(float x, float y, float maxRadius, out int nearestId, out float nearestDistSq)
        {
            nearestId = -1;
            nearestDistSq = maxRadius * maxRadius;
            int pcx = Cell(x), pcy = Cell(y);

            for (int k = 1; ; k *= 2)
            {
                for (int cy = pcy - k; cy <= pcy + k; cy++)
                {
                    for (int cx = pcx - k; cx <= pcx + k; cx++)
                    {
                        for (int id = _head[Bucket(cx, cy)]; id >= 0; id = _next[id])
                        {
                            if (_cx[id] != cx || _cy[id] != cy) continue;
                            float dx = _x[id] - x, dy = _y[id] - y;
                            float d2 = dx * dx + dy * dy;
                            if (d2 <= nearestDistSq && (nearestId < 0 || d2 < nearestDistSq))
                            {
                                nearestDistSq = d2;
                                nearestId = id;
                            }
                        }
                    }
                }

                float guaranteed = k * _cell;
                if (nearestId >= 0 && nearestDistSq <= guaranteed * guaranteed) return true;
                if (guaranteed >= maxRadius) return nearestId >= 0;
            }
        }

        int Cell(float v) => (int)MathF.Floor(v * _inv);

        int Bucket(int cx, int cy)
        {
            unchecked
            {
                uint h = (uint)(cx * 73856093) ^ (uint)(cy * 19349663);
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                return (int)(h & (uint)_mask);
            }
        }
    }
}
