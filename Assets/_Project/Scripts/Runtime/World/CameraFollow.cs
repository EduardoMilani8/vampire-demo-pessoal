using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Câmera ortográfica que segue o alvo. O mapa é infinito, então não há limites.
    /// Suavização exponencial (independente de framerate) bem curta: o jogo original
    /// tem câmera praticamente rígida, e suavizar demais atrapalha a leitura dos inimigos.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float followSharpness = 18f;
        [SerializeField] float orthographicSize = 6f;

        Camera _cam;
        Vector2 _pos;
        float _trauma;

        [SerializeField] float maxShakeOffset = 0.32f;
        [SerializeField] float traumaDecay = 2.2f;

        public Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());

        /// <summary>Meia altura/largura visível em unidades de mundo (usado pelo spawn em anel, marco 2).</summary>
        public Vector2 HalfExtents => new Vector2(Cam.orthographicSize * Cam.aspect, Cam.orthographicSize);

        void Awake()
        {
            Cam.orthographic = true;
            Cam.orthographicSize = orthographicSize;
        }

        void Start()
        {
            if (target != null) Snap();
        }

        public void SetTarget(Transform t)
        {
            target = t;
            Snap();
        }

        /// <summary>Screenshake: acumula "trauma" (0..1); o deslocamento cresce com o quadrado dele e decai sozinho.</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);

        void Snap()
        {
            Vector3 p = target.position;
            _pos = new Vector2(p.x, p.y);
            transform.position = new Vector3(_pos.x, _pos.y, transform.position.z);
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 goal = target.position;
            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            _pos.x = Mathf.Lerp(_pos.x, goal.x, k);
            _pos.y = Mathf.Lerp(_pos.y, goal.y, k);

            Vector2 offset = Vector2.zero;
            if (_trauma > 0f)
            {
                float shake = _trauma * _trauma * maxShakeOffset;
                float t = Time.unscaledTime * 28f;
                offset.x = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f * shake;
                offset.y = (Mathf.PerlinNoise(0.91f, t) - 0.5f) * 2f * shake;
                _trauma = Mathf.Max(0f, _trauma - traumaDecay * Time.unscaledDeltaTime);
            }

            transform.position = new Vector3(_pos.x + offset.x, _pos.y + offset.y, transform.position.z);
        }
    }
}
