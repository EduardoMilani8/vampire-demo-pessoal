using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Movimento top-down sem física: o transform é movido direto.
    /// Colisão com inimigos é resolvida pela simulação (spatial hash), não por Collider2D.
    /// </summary>
    [DefaultExecutionOrder(-100)] // move o jogador antes do GameSession simular o frame
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 3.4f;
        [SerializeField] Transform visual;
        [SerializeField] SpriteRenderer spriteRenderer;

        [Header("Feel")]
        [SerializeField] float bobSpeed = 14f;
        [SerializeField] float bobAmount = 0.06f;

        GameInput _input;
        float _bobTime;
        Color _flashColor;
        float _flashTime;
        float _flashDuration = 0.15f;

        /// <summary>Multiplicador vindo dos passivos/personagem (1 = base).</summary>
        public float MoveSpeedMultiplier { get; set; } = 1f;

        /// <summary>Última direção não nula de movimento; armas em arco usam isto.</summary>
        public Vector2 Facing { get; private set; } = Vector2.right;

        /// <summary>Velocidade instantânea desejada (unidades/s), útil para armas e câmera.</summary>
        public Vector2 Velocity { get; private set; }

        public Vector2 Position => transform.position;

        public bool PausePressedThisFrame => _input != null && _input.PausePressedThisFrame;

        /// <summary>Troca o sprite do personagem escolhido no menu.</summary>
        public void SetSprite(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null) spriteRenderer.sprite = sprite;
        }

        void OnEnable()
        {
            _input ??= new GameInput();
            _input.Enable();
        }

        void OnDisable() => _input?.Disable();

        void OnDestroy() => _input?.Dispose();

        /// <summary>Pisca o sprite numa cor (ex.: vermelho ao levar dano) e volta ao normal.</summary>
        public void Flash(Color color, float duration = 0.15f)
        {
            _flashColor = color;
            _flashDuration = duration;
            _flashTime = duration;
        }

        void Update()
        {
            Vector2 move = _input.Move;
            float dt = Time.deltaTime;

            if (_flashTime > 0f && spriteRenderer != null)
            {
                _flashTime -= dt;
                spriteRenderer.color = Color.Lerp(Color.white, _flashColor, Mathf.Clamp01(_flashTime / _flashDuration));
            }

            Velocity = move * (moveSpeed * MoveSpeedMultiplier);
            transform.position += (Vector3)(Velocity * dt);

            bool moving = move.sqrMagnitude > 0.01f;
            if (moving)
            {
                Facing = move.normalized;
                if (spriteRenderer != null && Mathf.Abs(move.x) > 0.05f)
                    spriteRenderer.flipX = move.x < 0f;
            }

            AnimateVisual(moving, dt);
        }

        void AnimateVisual(bool moving, float dt)
        {
            if (visual == null) return;

            if (moving) _bobTime += dt * bobSpeed;
            else _bobTime = Mathf.MoveTowards(_bobTime, Mathf.Round(_bobTime / Mathf.PI) * Mathf.PI, dt * bobSpeed);

            float s = Mathf.Abs(Mathf.Sin(_bobTime)) * bobAmount;
            visual.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f);
        }
    }
}
