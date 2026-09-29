using System;
using UnityEngine;

namespace HollowTide
{
    public enum Sfx
    {
        Hit, Kill, Gem, Coin, LevelUp, Hurt, Explosion, Chest, Evolve, Pickup, Click, Bomb, GameOver,
    }

    /// <summary>
    /// Sons sintetizados proceduralmente (nenhum arquivo de áudio). Os clipes são gerados uma vez no início a partir de
    /// fórmulas (senoides, ruído, envelopes). Efeitos frequentes têm um intervalo mínimo para não virar ruído contínuo.
    /// Volume e liga/desliga vêm do save.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        const int SampleRate = 44100;
        const int Voices = 12;

        static AudioManager _instance;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("AudioManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AudioManager>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        AudioClip[] _clips;
        float[] _minInterval;
        float[] _lastPlayed;
        AudioSource[] _voices;
        int _nextVoice;

        /// <summary>Toca o efeito (respeita som ligado/desligado, volume e o intervalo mínimo do efeito).</summary>
        public static void Play(Sfx sfx, float volumeScale = 1f)
        {
            var a = Instance;
            var save = SaveSystem.Current;
            if (!save.SoundOn) return;

            int i = (int)sfx;
            float now = Time.unscaledTime;
            if (now - a._lastPlayed[i] < a._minInterval[i]) return;
            a._lastPlayed[i] = now;

            var src = a._voices[a._nextVoice];
            a._nextVoice = (a._nextVoice + 1) % Voices;
            src.pitch = 1f + UnityEngine.Random.Range(-0.06f, 0.06f);
            src.PlayOneShot(a._clips[i], save.Volume * volumeScale);
        }

        void Build()
        {
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _voices[i] = src;
            }

            int count = Enum.GetValues(typeof(Sfx)).Length;
            _clips = new AudioClip[count];
            _minInterval = new float[count];
            _lastPlayed = new float[count];
            for (int i = 0; i < count; i++) _lastPlayed[i] = -10f;

            Register(Sfx.Hit, 0.07f, 0.045f, Hit);
            Register(Sfx.Kill, 0.14f, 0.05f, Kill);
            Register(Sfx.Gem, 0.08f, 0.04f, Gem);
            Register(Sfx.Coin, 0.16f, 0.05f, Coin);
            Register(Sfx.LevelUp, 0.6f, 0f, LevelUp);
            Register(Sfx.Hurt, 0.22f, 0.1f, Hurt);
            Register(Sfx.Explosion, 0.4f, 0.06f, Explosion);
            Register(Sfx.Chest, 0.8f, 0f, Chest);
            Register(Sfx.Evolve, 1.0f, 0f, Evolve);
            Register(Sfx.Pickup, 0.18f, 0.05f, Pickup);
            Register(Sfx.Click, 0.04f, 0.02f, Click);
            Register(Sfx.Bomb, 0.7f, 0f, Bomb);
            Register(Sfx.GameOver, 0.9f, 0f, GameOver);
        }

        void Register(Sfx sfx, float seconds, float minInterval, Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)SampleRate), -1f, 1f);

            var clip = AudioClip.Create(sfx.ToString(), n, 1, SampleRate, false);
            clip.SetData(data, 0);
            _clips[(int)sfx] = clip;
            _minInterval[(int)sfx] = minInterval;
        }

        // ---- Fórmulas dos sons -------------------------------------------------------------------

        static readonly System.Random Noise = new System.Random(12345);
        static float N() => (float)(Noise.NextDouble() * 2.0 - 1.0);
        static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        static float Env(float t, float decay) => Mathf.Exp(-t * decay);
        static float Note(float hz, float t, float start, float decay)
            => t < start ? 0f : Sin(hz, t - start) * Env(t - start, decay);

        static float Hit(float t) => (N() * 0.5f + Sin(210f - 500f * t, t) * 0.5f) * Env(t, 55f) * 0.5f;
        static float Kill(float t) => Sin(320f - 1400f * t, t) * Env(t, 22f) * 0.5f + N() * Env(t, 70f) * 0.15f;
        static float Gem(float t) => Sin(880f + 5000f * t, t) * Env(t, 30f) * 0.35f;
        static float Coin(float t) => (Note(1240f, t, 0f, 26f) + Note(1660f, t, 0.055f, 22f)) * 0.3f;
        static float Hurt(float t) => (Sin(150f - 200f * t, t) + 0.5f * Sin(75f, t)) * Env(t, 12f) * 0.6f;
        static float Pickup(float t) => Sin(660f + 1800f * t, t) * Env(t, 12f) * 0.4f;
        static float Click(float t) => Sin(1400f, t) * Env(t, 160f) * 0.3f;

        static float _lp;
        static float Explosion(float t)
        {
            _lp += (N() - _lp) * 0.18f;                          // ruído passa-baixa = estrondo
            return (_lp * 1.6f + Sin(70f - 60f * t, t) * 0.5f) * Env(t, 7f) * 0.7f;
        }

        static float Bomb(float t)
        {
            _lp += (N() - _lp) * 0.10f;
            return (_lp * 1.8f + Sin(55f - 30f * t, t) * 0.7f) * Env(t, 3.5f) * 0.85f;
        }

        static float LevelUp(float t)
            => (Note(523f, t, 0f, 9f) + Note(659f, t, 0.09f, 9f) + Note(784f, t, 0.18f, 9f) + Note(1046f, t, 0.27f, 7f)) * 0.28f;

        static float Chest(float t)
            => (Note(392f, t, 0f, 6f) + Note(494f, t, 0.12f, 6f) + Note(587f, t, 0.24f, 6f) + Note(784f, t, 0.36f, 5f)
                + N() * Env(t, 30f) * 0.1f) * 0.3f;

        static float Evolve(float t)
            => (Note(392f, t, 0f, 4f) + Note(523f, t, 0.1f, 4f) + Note(659f, t, 0.2f, 4f) + Note(784f, t, 0.3f, 4f)
                + Note(1046f, t, 0.4f, 3.5f) + Note(1318f, t, 0.5f, 3f)) * 0.22f;

        static float GameOver(float t)
            => (Note(392f, t, 0f, 5f) + Note(330f, t, 0.2f, 5f) + Note(262f, t, 0.4f, 4f) + Note(196f, t, 0.6f, 3f)) * 0.3f;
    }
}
