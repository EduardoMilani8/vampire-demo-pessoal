using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HollowTide
{
    /// <summary>
    /// Ferramentas de desenvolvimento (IMGUI): FPS, contagens, atalhos de teste e um benchmark automático. F3 liga/desliga.
    ///
    /// F +100 inimigos | K +1000 | V invoca um elite (solta baú) | G sobe de nível | L sobe as armas | N nova arma | O novo passivo (prioriza evolução)
    /// C abre um baú (evolui) | T +5 min | Y pula para 29:50 (Ceifador em 10 s) | H invencível | R reinicia
    /// B benchmark: 20 s com 600 inimigos, 6 armas no nível máximo, invencível; mostra FPS médio, 1% pior e GCs.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        const float BenchSeconds = 20f;
        const float BenchWarmup = 3f;
        const int BenchEnemies = 600;
        const int MaxSamples = 20000;

        [SerializeField] bool visible = true;

        readonly StringBuilder _sb = new StringBuilder(384);
        string _text = "";
        float _nextRefresh;
        float _frameTimeAvg = 1f / 60f;
        GUIStyle _style, _bigStyle;

        // Benchmark
        bool _benchRunning;
        float _benchTime;
        readonly float[] _samples = new float[MaxSamples];
        int _sampleCount;
        int _gcStart;
        bool _gcMarked;
        string _benchResult = "";

        void Update()
        {
            _frameTimeAvg = Mathf.Lerp(_frameTimeAvg, Time.unscaledDeltaTime, 0.05f);

            var kb = Keyboard.current;
            if (kb == null) return;

            var s = GameSession.Instance;
            if (kb.f3Key.wasPressedThisFrame) visible = !visible;
            if (kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            if (s == null || s.IsGameOver) return;

            if (_benchRunning) TickBenchmark(s);
            if (s.IsLevelUpOpen || s.IsChestOpen || s.IsPaused) return;

            if (kb.fKey.wasPressedThisFrame) s.Spawner.SpawnBurst(100);
            if (kb.kKey.wasPressedThisFrame) s.Spawner.SpawnBurst(1000);
            if (kb.vKey.wasPressedThisFrame) s.Spawner.SpawnChestCarrier();
            if (kb.gKey.wasPressedThisFrame) s.DebugGrantLevel();
            if (kb.lKey.wasPressedThisFrame)
            {
                for (int i = 0; i < s.Weapons.Count; i++) s.Weapons.Weapons[i].TryLevelUp();
                s.Ui.SetLoadout(s.Weapons.Weapons, s.Passives.Items);
            }
            if (kb.nKey.wasPressedThisFrame) GiveNextWeapon(s);
            if (kb.oKey.wasPressedThisFrame) GiveNextPassive(s);
            if (kb.cKey.wasPressedThisFrame && s.TryEvolveAll() == 0) s.PostNotice("Nenhuma arma elegível para evoluir.");
            if (kb.tKey.wasPressedThisFrame) s.DebugSkipTime(300f);
            if (kb.yKey.wasPressedThisFrame) s.DebugSkipTime(Mathf.Max(0f, s.RunLengthSeconds - 10f - s.Elapsed));
            if (kb.hKey.wasPressedThisFrame) { s.GodMode = !s.GodMode; s.PostNotice(s.GodMode ? "Invencível: ligado" : "Invencível: desligado"); }
            if (kb.bKey.wasPressedThisFrame && !_benchRunning) StartBenchmark(s);
        }

        // ---- Atalhos -----------------------------------------------------------------------------

        static void GiveNextWeapon(GameSession s)
        {
            foreach (var w in s.Catalog.weapons)
                if (!w.evolutionOnly && s.Weapons.Find(w) == null && s.Weapons.Add(w) != null)
                {
                    s.Ui.SetLoadout(s.Weapons.Weapons, s.Passives.Items);
                    return;
                }
        }

        /// <summary>Dá primeiro o passivo que a evolução de alguma arma equipada exige; depois, qualquer um que falte.</summary>
        static void GiveNextPassive(GameSession s)
        {
            for (int i = 0; i < s.Weapons.Count; i++)
            {
                var need = s.Weapons.Weapons[i].Data.requiredPassive;
                if (need != null && s.Passives.Find(need) == null && s.Passives.Add(need) != null) { RefreshLoadout(s); return; }
            }

            foreach (var p in s.Catalog.passives)
                if (s.Passives.Find(p) == null && s.Passives.Add(p) != null) { RefreshLoadout(s); return; }
        }

        static void RefreshLoadout(GameSession s)
        {
            s.RecalculateStats();
            s.Ui.SetLoadout(s.Weapons.Weapons, s.Passives.Items);
        }

        // ---- Benchmark ---------------------------------------------------------------------------

        void StartBenchmark(GameSession s)
        {
            while (!s.Weapons.IsFull)
            {
                int before = s.Weapons.Count;
                GiveNextWeapon(s);
                if (s.Weapons.Count == before) break;
            }
            for (int level = 0; level < 8; level++)
                for (int i = 0; i < s.Weapons.Count; i++) s.Weapons.Weapons[i].TryLevelUp();
            s.Ui.SetLoadout(s.Weapons.Weapons, s.Passives.Items);

            s.GodMode = true;
            s.SuppressLevelUps = true;
            _benchRunning = true;
            _benchTime = 0f;
            _sampleCount = 0;
            _gcMarked = false;
            _benchResult = "";
            s.PostNotice("Benchmark de 20 s: ande com WASD para variar");
        }

        void TickBenchmark(GameSession s)
        {
            _benchTime += Time.unscaledDeltaTime;

            int missing = BenchEnemies - s.Enemies.Count;
            if (missing > 0) s.Spawner.SpawnBurst(Mathf.Min(missing, 60));

            if (_benchTime >= BenchWarmup)
            {
                if (!_gcMarked)
                {
                    _gcMarked = true;
                    _gcStart = GC.CollectionCount(0);
                }
                if (_sampleCount < MaxSamples) _samples[_sampleCount++] = Time.unscaledDeltaTime;
            }

            if (_benchTime >= BenchSeconds + BenchWarmup) FinishBenchmark(s);
        }

        void FinishBenchmark(GameSession s)
        {
            _benchRunning = false;
            s.GodMode = false;
            s.SuppressLevelUps = false;

            int n = _sampleCount;
            if (n < 10) { _benchResult = "Benchmark: poucas amostras."; return; }

            double sum = 0, worst = 0;
            for (int i = 0; i < n; i++) { sum += _samples[i]; if (_samples[i] > worst) worst = _samples[i]; }
            Array.Sort(_samples, 0, n);
            double p99 = _samples[Mathf.Min(n - 1, (int)(n * 0.99))];
            double avg = sum / n;
            int gcs = GC.CollectionCount(0) - _gcStart;

            _benchResult =
                $"BENCHMARK ({BenchEnemies} inimigos, {s.Weapons.Count} armas no nível máximo, {n} frames)\n" +
                $"FPS médio {1.0 / avg:0.0}  |  1% pior {1.0 / p99:0.0} FPS ({p99 * 1000.0:0.0} ms)  |  pior frame {worst * 1000.0:0.0} ms\n" +
                $"Coletas de GC (gen0) durante o teste: {gcs}";
            Debug.Log("[Benchmark] " + _benchResult.Replace('\n', ' '));
        }

        // ---- Desenho -----------------------------------------------------------------------------

        void OnGUI()
        {
            if (!visible) return;
            var s = GameSession.Instance;
            if (s == null || s.Enemies == null) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            _bigStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };

            // Durante o benchmark não remontamos o texto: as strings gerariam lixo e sujariam a medição de GC.
            if (!_benchRunning && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.25f;
                Rebuild(s);
            }

            GUI.Label(new Rect(12, 190, 1000, 120), _benchRunning ? "Benchmark em andamento..." : _text, _style);
            if (_benchResult.Length > 0)
                GUI.Label(new Rect(12, Screen.height - 90, 1400, 90), _benchResult, _style);
        }

        void Rebuild(GameSession s)
        {
            _sb.Clear();
            _sb.Append("FPS ").Append((1f / _frameTimeAvg).ToString("0")).Append("  (").Append((_frameTimeAvg * 1000f).ToString("0.0")).Append(" ms)");
            _sb.Append("   Inimigos ").Append(s.Enemies.Count).Append("   Projéteis ").Append(s.Projectiles.World.Count)
               .Append("   Gemas ").Append(s.Gems.Count).Append('\n');
            _sb.Append("Onda ").Append(s.Spawner.CurrentWaveIndex).Append("   Herói ").Append(s.Character.displayName)
               .Append("   Sorte ").Append(s.Stats.Luck.ToString("0.00")).Append(s.GodMode ? "   [INVENCÍVEL]" : "").Append('\n');
            _sb.Append("[F] +100  [K] +1000  [V] elite  [G] level  [L] armas  [N] arma  [O] passivo  [C] baú  [T] +5min  [Y] 29:50  [H] invencível  [B] benchmark  [R] reiniciar  [F3] ocultar");
            _text = _sb.ToString();
        }
    }
}
