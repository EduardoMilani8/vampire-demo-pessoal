using System;

namespace HollowTide.Core
{
    [Serializable]
    public struct WeightedEnemy
    {
        /// <summary>Índice do tipo de inimigo na lista do spawner.</summary>
        public int EnemyIndex;
        public float Weight;
    }

    [Serializable]
    public struct WaveBurst
    {
        public int EnemyIndex;
        public int Count;
        /// <summary>Segundos depois do início da onda em que a rajada dispara (uma única vez).</summary>
        public float AtSecond;
        /// <summary>true = distribui os inimigos igualmente ao redor do jogador (cerco); false = posições aleatórias no anel.</summary>
        public bool Surround;
    }

    /// <summary>Uma faixa da tabela de ondas: vale do StartSeconds até o início da próxima onda.</summary>
    public sealed class WaveDefinition
    {
        public float StartSeconds;
        public float SpawnsPerSecond;
        /// <summary>Teto de inimigos vivos para o spawn contínuo (rajadas ignoram o teto).</summary>
        public int MaxAlive = 1000;
        /// <summary>Multiplicador de vida dos inimigos que nascem nesta onda.</summary>
        public float HpMultiplier = 1f;
        public WeightedEnemy[] Enemies = new WeightedEnemy[0];
        public WaveBurst[] Bursts = new WaveBurst[0];
    }

    public struct SpawnRequest
    {
        public int EnemyIndex;
        public float HpMultiplier;
        /// <summary>Posição no anel em [0,1) para rajadas de cerco; -1 = sortear.</summary>
        public float RingPosition;
    }

    /// <summary>
    /// Decide quais inimigos nascem e quando. Puro e determinístico (mesma semente = mesma sequência).
    /// Não sabe onde spawnar nem o que é um inimigo: devolve <see cref="SpawnRequest"/>s para o chamador executar.
    /// </summary>
    public sealed class WaveScheduler
    {
        const int MaxPendingBursts = 64;

        readonly WaveDefinition[] _waves;
        readonly SimRandom _rng;
        readonly bool[][] _burstFired;
        float _elapsed;
        float _accumulator;
        int _current = -1;

        // Rajadas disparadas que ainda não couberam no buffer de saída.
        readonly int[] _pendEnemy = new int[MaxPendingBursts];
        readonly int[] _pendTotal = new int[MaxPendingBursts];
        readonly int[] _pendIssued = new int[MaxPendingBursts];
        readonly bool[] _pendSurround = new bool[MaxPendingBursts];
        readonly float[] _pendHp = new float[MaxPendingBursts];
        int _pendCount;

        public float Elapsed => _elapsed;
        public int CurrentIndex => _current;
        public WaveDefinition Current => _current >= 0 ? _waves[_current] : null;

        public WaveScheduler(WaveDefinition[] waves, SimRandom rng)
        {
            _waves = (WaveDefinition[])waves.Clone();
            Array.Sort(_waves, (a, b) => a.StartSeconds.CompareTo(b.StartSeconds));
            _rng = rng;
            _burstFired = new bool[_waves.Length][];
            for (int i = 0; i < _waves.Length; i++) _burstFired[i] = new bool[_waves[i].Bursts.Length];
        }

        /// <summary>
        /// Avança o relógio e escreve os spawns deste frame em <paramref name="output"/>. <paramref name="alive"/> é a
        /// quantidade de inimigos vivos (para o teto). Se pularmos várias ondas de uma vez, valem só as ondas atuais.
        /// </summary>
        public int Update(float dt, int alive, SpawnRequest[] output)
        {
            _elapsed += dt;
            while (_current + 1 < _waves.Length && _waves[_current + 1].StartSeconds <= _elapsed) _current++;
            if (_current < 0) return 0;

            WaveDefinition wave = _waves[_current];
            int n = 0;

            for (int b = 0; b < wave.Bursts.Length; b++)
            {
                if (_burstFired[_current][b] || _elapsed < wave.StartSeconds + wave.Bursts[b].AtSecond) continue;
                _burstFired[_current][b] = true;
                EnqueueBurst(wave.Bursts[b], wave.HpMultiplier);
            }
            n = DrainBursts(output, n);

            _accumulator += wave.SpawnsPerSecond * dt;
            while (_accumulator >= 1f && n < output.Length)
            {
                if (alive + n >= wave.MaxAlive) break;
                _accumulator -= 1f;
                output[n++] = new SpawnRequest
                {
                    EnemyIndex = PickEnemy(wave),
                    HpMultiplier = wave.HpMultiplier,
                    RingPosition = -1f,
                };
            }
            if (_accumulator > 2f) _accumulator = 2f; // nada de backlog após hitch/pausa/teto

            return n;
        }

        void EnqueueBurst(in WaveBurst burst, float hp)
        {
            if (_pendCount >= MaxPendingBursts || burst.Count <= 0) return;
            _pendEnemy[_pendCount] = burst.EnemyIndex;
            _pendTotal[_pendCount] = burst.Count;
            _pendIssued[_pendCount] = 0;
            _pendSurround[_pendCount] = burst.Surround;
            _pendHp[_pendCount] = hp;
            _pendCount++;
        }

        int DrainBursts(SpawnRequest[] output, int n)
        {
            int p = 0;
            while (p < _pendCount && n < output.Length)
            {
                int total = _pendTotal[p];
                while (_pendIssued[p] < total && n < output.Length)
                {
                    int i = _pendIssued[p]++;
                    output[n++] = new SpawnRequest
                    {
                        EnemyIndex = _pendEnemy[p],
                        HpMultiplier = _pendHp[p],
                        RingPosition = _pendSurround[p] ? (float)i / total : -1f,
                    };
                }
                if (_pendIssued[p] >= total) RemovePending(p); else p++;
            }
            return n;
        }

        void RemovePending(int p)
        {
            int last = --_pendCount;
            if (p == last) return;
            _pendEnemy[p] = _pendEnemy[last]; _pendTotal[p] = _pendTotal[last]; _pendIssued[p] = _pendIssued[last];
            _pendSurround[p] = _pendSurround[last]; _pendHp[p] = _pendHp[last];
        }

        int PickEnemy(WaveDefinition wave)
        {
            var list = wave.Enemies;
            if (list.Length == 0) return 0;

            float total = 0f;
            for (int i = 0; i < list.Length; i++) total += list[i].Weight;
            if (total <= 0f) return list[0].EnemyIndex;

            float r = _rng.NextFloat() * total;
            for (int i = 0; i < list.Length; i++)
            {
                r -= list[i].Weight;
                if (r < 0f) return list[i].EnemyIndex;
            }
            return list[list.Length - 1].EnemyIndex;
        }
    }

    public static class WaveTableValidator
    {
        /// <summary>A tabela "sempre cresce": ritmo e teto de inimigos nunca diminuem de uma onda para a seguinte.</summary>
        public static bool IsGrowing(WaveDefinition[] waves, out string problem)
        {
            var sorted = (WaveDefinition[])waves.Clone();
            Array.Sort(sorted, (a, b) => a.StartSeconds.CompareTo(b.StartSeconds));

            for (int i = 1; i < sorted.Length; i++)
            {
                if (sorted[i].SpawnsPerSecond < sorted[i - 1].SpawnsPerSecond)
                {
                    problem = $"O ritmo cai na onda que começa em {sorted[i].StartSeconds}s ({sorted[i - 1].SpawnsPerSecond} → {sorted[i].SpawnsPerSecond}).";
                    return false;
                }
                if (sorted[i].MaxAlive < sorted[i - 1].MaxAlive)
                {
                    problem = $"O teto de inimigos cai na onda que começa em {sorted[i].StartSeconds}s.";
                    return false;
                }
            }
            problem = null;
            return true;
        }
    }
}
