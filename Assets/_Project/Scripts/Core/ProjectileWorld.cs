using System;

namespace HollowTide.Core
{
    public struct ProjectileSpawn
    {
        public float X, Y, Vx, Vy;
        public float Radius, Damage, Knockback;
        /// <summary>Segundos de vida. Para projéteis em arco é o tempo de voo até cair.</summary>
        public float Life;
        /// <summary>Inimigos atravessados além do primeiro (0 = some no primeiro acerto).</summary>
        public int Pierce;
        /// <summary>rad/s; > 0 faz o projétil perseguir o inimigo mais próximo.</summary>
        public float TurnRate;
        /// <summary>> 0 = projétil em arco: não colide no voo e explode nesse raio ao cair.</summary>
        public float ExplodeRadius;
        /// <summary>Altura máxima do arco (só visual).</summary>
        public float Arc;
    }

    /// <summary>
    /// Projéteis em struct-of-arrays (retos, teleguiados e em arco). O Step não conhece dano nem morte de inimigos:
    /// ele escreve eventos em buffers (<see cref="HitProjectile"/>/<see cref="HitEnemy"/>, <see cref="ExplodedIds"/>,
    /// <see cref="ExpiredIds"/>) que o chamador aplica logo em seguida, antes de qualquer novo Spawn (os dados de
    /// projéteis expirados permanecem nos arrays até lá).
    /// </summary>
    public sealed class ProjectileWorld
    {
        /// <summary>Quantos acertos recentes cada projétil lembra, para não acertar o mesmo inimigo em frames seguidos.</summary>
        public const int HitMemory = 4;

        public readonly int Capacity;
        public readonly float[] X, Y, Vx, Vy, Radius, Damage, Knockback, Life, Duration, TurnRate, ExplodeRadius, Arc;
        public readonly int[] Pierce, Target;
        public readonly bool[] Alive;
        public readonly int[] Active;

        public readonly int[] HitProjectile;
        public readonly int[] HitEnemy;
        public int HitCount { get; private set; }

        public readonly int[] ExplodedIds;
        public int ExplodedCount { get; private set; }

        /// <summary>Todos os projéteis removidos no último Step (inclui os que explodiram), para liberar as views.</summary>
        public readonly int[] ExpiredIds;
        public int ExpiredCount { get; private set; }

        public int Count { get; private set; }

        /// <summary>Distância máxima em que um projétil teleguiado procura alvo.</summary>
        public float HomingSearchRadius = 14f;

        readonly int[] _hitIds;
        readonly int[] _hitCounter;
        readonly int[] _slot;
        readonly int[] _free;
        int _freeCount;

        public ProjectileWorld(int capacity, int maxHitEventsPerStep = 4096)
        {
            Capacity = capacity;
            X = new float[capacity]; Y = new float[capacity];
            Vx = new float[capacity]; Vy = new float[capacity];
            Radius = new float[capacity]; Damage = new float[capacity]; Knockback = new float[capacity];
            Life = new float[capacity]; Duration = new float[capacity]; TurnRate = new float[capacity];
            ExplodeRadius = new float[capacity]; Arc = new float[capacity];
            Pierce = new int[capacity]; Target = new int[capacity];
            Alive = new bool[capacity];
            Active = new int[capacity];
            HitProjectile = new int[maxHitEventsPerStep];
            HitEnemy = new int[maxHitEventsPerStep];
            ExplodedIds = new int[capacity];
            ExpiredIds = new int[capacity];
            _hitIds = new int[capacity * HitMemory];
            _hitCounter = new int[capacity];
            _slot = new int[capacity];
            _free = new int[capacity];
            for (int i = 0; i < capacity; i++) _free[i] = capacity - 1 - i;
            _freeCount = capacity;
        }

        /// <summary>Retorna o id, ou -1 se cheio.</summary>
        public int Spawn(in ProjectileSpawn s)
        {
            if (_freeCount == 0) return -1;
            int id = _free[--_freeCount];

            X[id] = s.X; Y[id] = s.Y; Vx[id] = s.Vx; Vy[id] = s.Vy;
            Radius[id] = s.Radius; Damage[id] = s.Damage; Knockback[id] = s.Knockback;
            Life[id] = s.Life; Duration[id] = s.Life;
            Pierce[id] = s.Pierce; TurnRate[id] = s.TurnRate;
            ExplodeRadius[id] = s.ExplodeRadius; Arc[id] = s.Arc;
            Target[id] = -1;
            _hitCounter[id] = 0;
            Alive[id] = true;

            _slot[id] = Count;
            Active[Count++] = id;
            return id;
        }

        void Despawn(int id)
        {
            if (!Alive[id]) return;
            Alive[id] = false;

            int slot = _slot[id];
            int last = Active[--Count];
            Active[slot] = last;
            _slot[last] = slot;
            _free[_freeCount++] = id;
        }

        public void Step(float dt, EnemyWorld enemies, SpatialHashGrid grid, int[] buffer)
        {
            HitCount = 0;
            ExplodedCount = 0;
            ExpiredCount = 0;

            // De trás para frente: Despawn faz swap-remove e não pode pular ninguém.
            for (int i = Count - 1; i >= 0; i--)
            {
                int id = Active[i];
                if (TurnRate[id] > 0f) Steer(id, dt, enemies, grid);

                X[id] += Vx[id] * dt;
                Y[id] += Vy[id] * dt;
                Life[id] -= dt;

                bool dead = false;
                if (ExplodeRadius[id] <= 0f) dead = CollideWithEnemies(id, enemies, grid, buffer);

                if (Life[id] <= 0f)
                {
                    dead = true;
                    if (ExplodeRadius[id] > 0f) ExplodedIds[ExplodedCount++] = id;
                }

                if (dead)
                {
                    ExpiredIds[ExpiredCount++] = id;
                    Despawn(id);
                }
            }
        }

        bool CollideWithEnemies(int id, EnemyWorld enemies, SpatialHashGrid grid, int[] buffer)
        {
            float px = X[id], py = Y[id], r = Radius[id];
            int n = grid.Query(px, py, r + enemies.MaxRadius, buffer);

            for (int k = 0; k < n; k++)
            {
                int e = buffer[k];
                if (!enemies.Alive[e]) continue;

                float dx = enemies.X[e] - px, dy = enemies.Y[e] - py;
                float reach = r + enemies.Radius[e];
                if (dx * dx + dy * dy > reach * reach) continue;
                if (AlreadyHit(id, e)) continue;

                Remember(id, e);
                if (HitCount < HitProjectile.Length)
                {
                    HitProjectile[HitCount] = id;
                    HitEnemy[HitCount++] = e;
                }

                if (Pierce[id]-- <= 0) return true;
            }
            return false;
        }

        void Steer(int id, float dt, EnemyWorld enemies, SpatialHashGrid grid)
        {
            int t = Target[id];
            if (t < 0 || !enemies.Alive[t])
            {
                if (!grid.FindNearest(X[id], Y[id], HomingSearchRadius, out t, out _))
                {
                    Target[id] = -1;
                    return;
                }
                Target[id] = t;
            }

            float desired = MathF.Atan2(enemies.Y[t] - Y[id], enemies.X[t] - X[id]);
            float current = MathF.Atan2(Vy[id], Vx[id]);
            float diff = desired - current;
            while (diff > MathF.PI) diff -= MathF.PI * 2f;
            while (diff < -MathF.PI) diff += MathF.PI * 2f;

            float maxTurn = TurnRate[id] * dt;
            float turn = MathF.Max(-maxTurn, MathF.Min(maxTurn, diff));
            float speed = MathF.Sqrt(Vx[id] * Vx[id] + Vy[id] * Vy[id]);
            float angle = current + turn;
            Vx[id] = MathF.Cos(angle) * speed;
            Vy[id] = MathF.Sin(angle) * speed;
        }

        bool AlreadyHit(int id, int enemy)
        {
            int count = Math.Min(_hitCounter[id], HitMemory);
            int baseIndex = id * HitMemory;
            for (int i = 0; i < count; i++)
                if (_hitIds[baseIndex + i] == enemy) return true;
            return false;
        }

        void Remember(int id, int enemy)
        {
            _hitIds[id * HitMemory + _hitCounter[id] % HitMemory] = enemy;
            _hitCounter[id]++;
        }
    }
}
