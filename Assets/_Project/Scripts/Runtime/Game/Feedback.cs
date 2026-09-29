using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Feedback visual do combate: números de dano flutuantes e partículas, ambos em pools de tamanho fixo
    /// (reciclam o mais antigo quando cheios) e sem alocação por frame. Os números de 0..1999 usam strings pré-geradas.
    /// </summary>
    public sealed class Feedback : MonoBehaviour
    {
        const int MaxNumbers = 64;
        const int MaxParticles = 384;
        const int MaxNumbersPerFrame = 16;
        const int CachedStrings = 2000;

        public static Feedback Instance { get; private set; }

        [SerializeField] Sprite pixelSprite;

        static string[] _numberStrings;

        // Números de dano
        TextMesh[] _texts;
        Transform[] _textTransforms;
        float[] _numLife;
        Color[] _numColor;
        int _nextNumber;
        int _numbersThisFrame;

        // Partículas
        SpriteRenderer[] _particles;
        Transform[] _particleTransforms;
        float[] _px, _py, _pvx, _pvy, _plife, _pmax, _psize;
        Color[] _pcolor;
        int _nextParticle;

        void Awake()
        {
            Instance = this;
            BuildNumbers();
            BuildParticles();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static string NumberString(int n)
        {
            if (n < 0) n = 0;
            if (n >= CachedStrings) return n.ToString();
            _numberStrings ??= new string[CachedStrings];
            return _numberStrings[n] ??= n.ToString();
        }

        void BuildNumbers()
        {
            var font = UiFactory.DefaultFont;
            _texts = new TextMesh[MaxNumbers];
            _textTransforms = new Transform[MaxNumbers];
            _numLife = new float[MaxNumbers];
            _numColor = new Color[MaxNumbers];

            for (int i = 0; i < MaxNumbers; i++)
            {
                var go = new GameObject("DamageNumber");
                go.transform.SetParent(transform, false);
                var tm = go.AddComponent<TextMesh>();
                tm.font = font;
                tm.fontSize = 64;
                tm.fontStyle = FontStyle.Bold;
                tm.characterSize = 0.06f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.sortingOrder = 60;
                go.SetActive(false);
                _texts[i] = tm;
                _textTransforms[i] = go.transform;
            }
        }

        void BuildParticles()
        {
            _particles = new SpriteRenderer[MaxParticles];
            _particleTransforms = new Transform[MaxParticles];
            _px = new float[MaxParticles]; _py = new float[MaxParticles];
            _pvx = new float[MaxParticles]; _pvy = new float[MaxParticles];
            _plife = new float[MaxParticles]; _pmax = new float[MaxParticles]; _psize = new float[MaxParticles];
            _pcolor = new Color[MaxParticles];

            for (int i = 0; i < MaxParticles; i++)
            {
                var go = new GameObject("Particle");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = pixelSprite;
                sr.sortingOrder = 40;
                go.SetActive(false);
                _particles[i] = sr;
                _particleTransforms[i] = go.transform;
            }
        }

        public void DamageNumber(float x, float y, float amount, Color color)
        {
            if (_numbersThisFrame >= MaxNumbersPerFrame) return; // aura/explosões acertando 50 inimigos não viram um borrão
            _numbersThisFrame++;

            int i = _nextNumber;
            _nextNumber = (_nextNumber + 1) % MaxNumbers;

            _texts[i].text = NumberString(Mathf.Max(1, Mathf.RoundToInt(amount)));
            _numLife[i] = 0.7f;
            _numColor[i] = color;
            _texts[i].color = color;
            _textTransforms[i].position = new Vector3(x + Random.Range(-0.25f, 0.25f), y + 0.4f, 0f);
            _textTransforms[i].localScale = Vector3.one;
            _textTransforms[i].gameObject.SetActive(true);
        }

        public void Burst(float x, float y, int count, Color color, float speed, float life, float size)
        {
            for (int k = 0; k < count; k++)
            {
                int i = _nextParticle;
                _nextParticle = (_nextParticle + 1) % MaxParticles;

                float a = Random.value * Mathf.PI * 2f;
                float s = speed * (0.4f + 0.6f * Random.value);
                _px[i] = x; _py[i] = y;
                _pvx[i] = Mathf.Cos(a) * s; _pvy[i] = Mathf.Sin(a) * s;
                _plife[i] = _pmax[i] = life * (0.6f + 0.4f * Random.value);
                _psize[i] = size * (0.6f + 0.6f * Random.value);
                _pcolor[i] = color;
                _particles[i].gameObject.SetActive(true);
            }
        }

        void LateUpdate() => _numbersThisFrame = 0;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < MaxNumbers; i++)
            {
                if (_numLife[i] <= 0f) continue;
                _numLife[i] -= dt;
                if (_numLife[i] <= 0f) { _textTransforms[i].gameObject.SetActive(false); continue; }

                var p = _textTransforms[i].position;
                p.y += 1.6f * dt;
                _textTransforms[i].position = p;

                var c = _numColor[i];
                c.a = Mathf.Clamp01(_numLife[i] / 0.25f);
                _texts[i].color = c;
            }

            float drag = Mathf.Exp(-4f * dt);
            for (int i = 0; i < MaxParticles; i++)
            {
                if (_plife[i] <= 0f) continue;
                _plife[i] -= dt;
                if (_plife[i] <= 0f) { _particles[i].gameObject.SetActive(false); continue; }

                _pvx[i] *= drag; _pvy[i] *= drag;
                _px[i] += _pvx[i] * dt; _py[i] += _pvy[i] * dt;

                float k = _plife[i] / _pmax[i];
                var t = _particleTransforms[i];
                t.position = new Vector3(_px[i], _py[i], 0f);
                float s = _psize[i] * (0.4f + 0.6f * k);
                t.localScale = new Vector3(s, s, 1f);
                var c = _pcolor[i];
                c.a = k;
                _particles[i].color = c;
            }
        }
    }
}
