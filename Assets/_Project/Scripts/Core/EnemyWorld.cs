using System;

namespace HollowTide.Core
{
    [Serializable]
    public struct EnemyStats
    {
        public float MaxHp;
        public float Speed;
        public float ContactDamage;
        public float Radius;
        /// <summary>0 = leva todo o knockback, 1 = imune.</summary>
        public float KnockbackResistance;
    }

    /// <summary>
    /// Simulação dos inimigos em struct-of-arrays. Nenhum GameObject aqui: a camada visual só espelha o estado.
    /// Ids são índices estáveis em [0, Capacity) reaproveitados por uma free-list; <see cref="Active"/> é uma lista
    /// densa dos ids vivos (remoção por swap), para iterar sem varrer slots vazios.
    /// </summary>
    public sealed class EnemyWorld
    {
        public readonly int Capacity;

        public readonly float[] X, Y;          // posição
        public readonly float[] Kx, Ky;        // velocidade de knockback (decai sozinha)
        public readonly float[] Hp, Speed, Radius, ContactDamage, KbResist;
        public readonly float[] FlashTimer;    // > 0 enquanto pisca ao levar dano
        public readonly bool[] Alive;
        public readonly int[] Active;

        readonly int[] _slot;
        readonly int[] _free;
        int _freeCount;

        public int Count { get; private set; }

        /// <summary>Maior raio já spawnado; define a margem de busca das consultas no grid.</summary>
        public float MaxRadius { get; private set; }

        public float KnockbackDecay = 8f;      // 1/s
        public float FlashDuration = 0.08f;

        public EnemyWorld(int capacity)
        {
            Capacity = capacity;
            X = new float[capacity]; Y = new float[capacity];
            Kx = new float[capacity]; Ky = new float[capacity];
            Hp = new float[capacity]; Speed = new float[capacity]; Radius = new float[capacity];
            ContactDamage = new float[capacity]; KbResist = new float[capacity];
            FlashTimer = new float[capacity];
            Alive = new bool[capacity];
            Active = new int[capacity];
            _slot = new int[capacity];
            _free = new int[capacity];
            for (int i = 0; i < capacity; i++) _free[i] = capacity - 1 - i; // id 0 sai primeiro
            _freeCount = capacity;
        }

        /// <summary>Retorna o id, ou -1 se o mundo está cheio.</summary>
        public int Spawn(in EnemyStats s, float x, float y)
        {
            if (_freeCount == 0) return -1;
            int id = _free[--_freeCount];

            X[id] = x; Y[id] = y; Kx[id] = 0f; Ky[id] = 0f;
            Hp[id] = s.MaxHp; Speed[id] = s.Speed; Radius[id] = s.Radius;
            ContactDamage[id] = s.ContactDamage; KbResist[id] = s.KnockbackResistance;
            FlashTimer[id] = 0f;
            Alive[id] = true;

            _slot[id] = Count;
            Active[Count++] = id;
            if (s.Radius > MaxRadius) MaxRadius = s.Radius;
            return id;
        }

        public void Despawn(int id)
        {
            if (!Alive[id]) return;
            Alive[id] = false;

            int slot = _slot[id];
            int last = Active[--Count];
            Active[slot] = last;
            _slot[last] = slot;

            _free[_freeCount++] = id;
        }

        /// <summary>Aplica dano; retorna true se o inimigo morreu (o chamador decide despawn e drops).</summary>
        public bool Damage(int id, float amount)
        {
            Hp[id] -= amount;
            FlashTimer[id] = FlashDuration;
            return Hp[id] <= 0f;
        }

        /// <summary>Empurra na direção (dx, dy), que deve estar normalizada.</summary>
        public void Knockback(int id, float dx, float dy, float force)
        {
            float scale = MathF.Max(0f, 1f - KbResist[id]);
            Kx[id] += dx * force * scale;
            Ky[id] += dy * force * scale;
        }

        /// <summary>
        /// Persegue o jogador, integra o knockback e realoca inimigos que ficaram longe demais
        /// para o lado oposto do jogador, na distância do anel de spawn (vira reforço em vez de sumir).
        /// </summary>
        public void Step(float dt, float px, float py, float relocateDistance, float ringDistance)
        {
            float decay = MathF.Exp(-KnockbackDecay * dt);
            float kbStep = (1f - decay) / KnockbackDecay;
            float relocateSq = relocateDistance * relocateDistance;

            for (int i = 0; i < Count; i++)
            {
                int id = Active[i];
                float dx = px - X[id], dy = py - Y[id];
                float d2 = dx * dx + dy * dy;

                if (d2 > relocateSq)
                {
                    float s = ringDistance / MathF.Sqrt(d2);
                    X[id] = px + dx * s;
                    Y[id] = py + dy * s;
                    Kx[id] = 0f; Ky[id] = 0f;
                    continue;
                }

                float mx = 0f, my = 0f;
                if (d2 > 1e-6f)
                {
                    float k = Speed[id] / MathF.Sqrt(d2);
                    mx = dx * k; my = dy * k;
                }

                // Knockback: integral exata de v0·e^(-λt) no intervalo, então a distância total
                // percorrida (v0/λ) não depende do framerate.
                X[id] += mx * dt + Kx[id] * kbStep;
                Y[id] += my * dt + Ky[id] * kbStep;
                Kx[id] *= decay;
                Ky[id] *= decay;
                if (FlashTimer[id] > 0f) FlashTimer[id] -= dt;
            }
        }

        public void RebuildGrid(SpatialHashGrid grid)
        {
            grid.Clear();
            for (int i = 0; i < Count; i++)
            {
                int id = Active[i];
                grid.Insert(id, X[id], Y[id]);
            }
        }

        /// <summary>
        /// Separação suave: inimigos sobrepostos se empurram, evitando que virem uma pilha num ponto só.
        /// Usa o grid já reconstruído neste frame (posições levemente defasadas não importam para isso).
        /// </summary>
        public void Separate(SpatialHashGrid grid, float dt, int[] buffer, float strength = 6f)
        {
            float k = MathF.Min(1f, strength * dt) * 0.5f;

            for (int i = 0; i < Count; i++)
            {
                int a = Active[i];
                float ax = X[a], ay = Y[a], ar = Radius[a];
                int n = grid.Query(ax, ay, ar + MaxRadius, buffer);

                float pushX = 0f, pushY = 0f;
                for (int j = 0; j < n; j++)
                {
                    int b = buffer[j];
                    if (b == a) continue;

                    float dx = ax - X[b], dy = ay - Y[b];
                    float minD = ar + Radius[b];
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= minD * minD) continue;

                    if (d2 < 1e-8f)
                    {
                        // Exatamente no mesmo ponto: desempata pelo id para não travar juntos.
                        pushX += a < b ? 1f : -1f;
                        continue;
                    }
                    float d = MathF.Sqrt(d2);
                    float overlap = minD - d;
                    pushX += dx / d * overlap;
                    pushY += dy / d * overlap;
                }

                X[a] += pushX * k;
                Y[a] += pushY * k;
            }
        }
    }
}
