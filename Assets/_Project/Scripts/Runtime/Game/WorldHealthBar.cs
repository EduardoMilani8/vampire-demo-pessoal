using UnityEngine;

namespace HollowTide
{
    /// <summary>Barra de vida no mundo, embaixo do jogador (o HUD completo chega no marco 6).</summary>
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] Transform fill;
        [SerializeField] float width = 1.2f;
        [SerializeField] float height = 0.12f;

        void Awake() => SetRatio(1f);

        public void SetRatio(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            fill.localScale = new Vector3(width * ratio, height, 1f);
            fill.localPosition = new Vector3(-width * 0.5f + width * ratio * 0.5f, 0f, 0f);
        }
    }
}
