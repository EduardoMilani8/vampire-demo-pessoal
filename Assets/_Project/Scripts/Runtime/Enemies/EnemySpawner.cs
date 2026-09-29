using System.Collections.Generic;
using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Executa a tabela de ondas: converte os WaveData (ScriptableObjects) em WaveDefinitions do Core, pede ao
    /// WaveScheduler o que nascer a cada frame e posiciona os inimigos no anel logo fora da tela.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] float ringMargin = 1.5f;
        [SerializeField] float ringJitter = 2f;
        [SerializeField] uint seed = 12345;

        readonly SpawnRequest[] _requests = new SpawnRequest[128];
        readonly List<EnemyData> _types = new List<EnemyData>();
        SimRandom _rng;
        WaveScheduler _scheduler;

        public float RingMargin => ringMargin;
        public int TypeCount => _types.Count;
        public WaveDefinition CurrentWave => _scheduler?.Current;
        public int CurrentWaveIndex => _scheduler?.CurrentIndex ?? -1;

        public void Init(ContentCatalog catalog)
        {
            _rng = new SimRandom(seed);
            _types.Clear();

            var waves = catalog.waves;
            WaveDefinition[] defs;
            if (waves == null || waves.Length == 0)
            {
                Debug.LogWarning("[Spawner] O catálogo não tem ondas (rode Tools > Build Demo). Usando uma onda de emergência.");
                defs = new[]
                {
                    new WaveDefinition
                    {
                        StartSeconds = 0f, SpawnsPerSecond = 1.5f, MaxAlive = 150,
                        Enemies = new[] { new WeightedEnemy { EnemyIndex = IndexOf(catalog.enemies[0]), Weight = 1f } },
                    },
                };
            }
            else
            {
                defs = new WaveDefinition[waves.Length];
                for (int i = 0; i < waves.Length; i++) defs[i] = Convert(waves[i]);
            }

            if (!WaveTableValidator.IsGrowing(defs, out string problem))
                Debug.LogWarning("[Spawner] A tabela de ondas não cresce: " + problem);

            _scheduler = new WaveScheduler(defs, _rng);
        }

        WaveDefinition Convert(WaveData w)
        {
            var enemies = new List<WeightedEnemy>();
            foreach (var e in w.enemies)
                if (e.enemy != null) enemies.Add(new WeightedEnemy { EnemyIndex = IndexOf(e.enemy), Weight = e.weight });

            var bursts = new List<WaveBurst>();
            foreach (var b in w.bursts)
                if (b.enemy != null && b.count > 0)
                    bursts.Add(new WaveBurst { EnemyIndex = IndexOf(b.enemy), Count = b.count, AtSecond = b.atSecond, Surround = b.surround });

            return new WaveDefinition
            {
                StartSeconds = w.startMinute * 60f,
                SpawnsPerSecond = w.spawnsPerSecond,
                MaxAlive = w.maxAlive,
                HpMultiplier = Mathf.Max(0.1f, w.hpMultiplier),
                Enemies = enemies.ToArray(),
                Bursts = bursts.ToArray(),
            };
        }

        int IndexOf(EnemyData data)
        {
            int i = _types.IndexOf(data);
            if (i >= 0) return i;
            _types.Add(data);
            return _types.Count - 1;
        }

        public void Tick(float dt)
        {
            var session = GameSession.Instance;
            int n = _scheduler.Update(dt, session.Enemies.Count, _requests);
            for (int i = 0; i < n; i++)
            {
                SpawnRequest r = _requests[i];
                Spawn(_types[r.EnemyIndex], r.HpMultiplier, r.RingPosition);
            }
        }

        /// <summary>Avança o relógio das ondas sem gerar inimigos (atalho de teste para pular o tempo).</summary>
        public void SkipClock(float seconds) => _scheduler.Update(seconds, int.MaxValue, _requests);

        /// <summary>Spawna um inimigo no anel. ringPosition em [0,1) fixa o ângulo (cerco); negativo sorteia.</summary>
        public bool Spawn(EnemyData data, float hpMultiplier = 1f, float ringPosition = -1f)
        {
            var s = GameSession.Instance;
            Vector2 half = s.CameraFollow.HalfExtents;
            Vector2 p = s.Player.Position;

            float x, y;
            if (ringPosition >= 0f)
            {
                float radius = SpawnRing.RingRadius(half.x, half.y, ringMargin);
                float angle = ringPosition * Mathf.PI * 2f;
                x = p.x + Mathf.Cos(angle) * radius;
                y = p.y + Mathf.Sin(angle) * radius;
            }
            else
            {
                SpawnRing.Sample(_rng, p.x, p.y, half.x, half.y, ringMargin, ringJitter, out x, out y);
            }
            return s.SpawnEnemy(data, x, y, hpMultiplier);
        }

        /// <summary>Atalho de teste: spawna o primeiro tipo que derruba baú (o elite), para testar o baú sem esperar as ondas.</summary>
        public bool SpawnChestCarrier()
        {
            for (int i = 0; i < _types.Count; i++)
                if (_types[i].dropsChest) return Spawn(_types[i]);
            return false;
        }

        /// <summary>Teste de estresse: spawna <paramref name="count"/> inimigos aleatórios das ondas de uma vez.</summary>
        public void SpawnBurst(int count)
        {
            if (_types.Count == 0) return;
            for (int i = 0; i < count; i++) Spawn(_types[_rng.NextInt(_types.Count)]);
        }
    }
}
