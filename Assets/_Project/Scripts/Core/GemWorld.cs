using System;

namespace HollowTide.Core
{
    /// <summary>
    /// Gemas de XP em struct-of-arrays (mesmo padrão do EnemyWorld). Uma gema fica parada até o jogador
    /// entrar no raio de coleta; a partir daí ela é "magnetizada", acelera na direção do jogador e é coletada
    /// ao encostar. <see cref="MagnetizeAll"/> puxa todas (item ímã do marco 5).
    /// </summary>
    public sealed class GemWorld
    {
        public readonly int Capacity;
        public readonly float[] X, Y, Value, Speed;
        public readonly bool[] Alive, Magnetized;
        public readonly int[] Active;

        /// <summary>Ids coletados no último <see cref="Step"/> (para liberar as views); válido até o próximo Step.</summary>
        public readonly int[] CollectedIds;
        public int CollectedCount { get; private set; }

        readonly int[] _slot;
        readonly int[] _free;
        int _freeCount;

        public int Count { get; private set; }

        public float StartSpeed = 5f;
        public float Acceleration = 45f;
        public float MaxSpeed = 30f;

        public GemWorld(int capacity)
        {
            Capacity = capacity;
            X = new float[capacity]; Y = new float[capacity];
            Value = new float[capacity]; Speed = new float[capacity];
            Alive = new bool[capacity]; Magnetized = new bool[capacity];
            Active = new int[capacity];
            CollectedIds = new int[capacity];
            _slot = new int[capacity];
            _free = new int[capacity];
            for (int i = 0; i < capacity; i++) _free[i] = capacity - 1 - i;
            _freeCount = capacity;
        }

        /// <summary>
        /// Cria uma gema e retorna o id. Se o mundo estiver cheio, o valor é somado à gema mais recente
        /// (nenhum XP se perde) e retorna -1, sem criar uma nova view.
        /// </summary>
        public int Spawn(float x, float y, float value)
        {
            if (_freeCount == 0)
            {
                if (Count > 0) Value[Active[Count - 1]] += value;
                return -1;
            }

            int id = _free[--_freeCount];
            X[id] = x; Y[id] = y; Value[id] = value; Speed[id] = 0f;
            Alive[id] = true; Magnetized[id] = false;
            _slot[id] = Count;
            Active[Count++] = id;
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

        public void MagnetizeAll()
        {
            for (int i = 0; i < Count; i++)
            {
                int id = Active[i];
                if (!Magnetized[id]) { Magnetized[id] = true; Speed[id] = StartSpeed; }
            }
        }

        /// <summary>
        /// Avança as gemas. Retorna o XP total coletado neste passo; os ids coletados ficam em
        /// <see cref="CollectedIds"/>[0..<see cref="CollectedCount"/>).
        /// </summary>
        public float Step(float dt, float px, float py, float pickupRadius, float collectRadius)
        {
            CollectedCount = 0;
            float total = 0f;
            float pickupSq = pickupRadius * pickupRadius;
            float collectSq = collectRadius * collectRadius;

            // De trás para frente: Despawn faz swap-remove e não pode pular ninguém.
            for (int i = Count - 1; i >= 0; i--)
            {
                int id = Active[i];
                float dx = px - X[id], dy = py - Y[id];
                float d2 = dx * dx + dy * dy;

                if (!Magnetized[id])
                {
                    if (d2 > pickupSq) continue;
                    Magnetized[id] = true;
                    Speed[id] = StartSpeed;
                }

                Speed[id] = MathF.Min(MaxSpeed, Speed[id] + Acceleration * dt);
                float step = Speed[id] * dt;
                float d = MathF.Sqrt(d2);

                if (d <= collectRadius || d <= step)
                {
                    total += Value[id];
                    CollectedIds[CollectedCount++] = id;
                    Despawn(id);
                    continue;
                }

                float k = step / d;
                X[id] += dx * k;
                Y[id] += dy * k;
            }
            return total;
        }
    }
}
