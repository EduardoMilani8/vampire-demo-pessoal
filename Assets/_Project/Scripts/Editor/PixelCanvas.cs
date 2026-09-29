using UnityEngine;

namespace HollowTide.EditorTools
{
    /// <summary>Mini-canvas em pixels para desenhar os sprites placeholder por código (sem assets externos).</summary>
    sealed class PixelCanvas
    {
        public readonly int Width, Height;
        readonly Color32[] _px;

        public PixelCanvas(int w, int h, Color32? fill = null)
        {
            Width = w; Height = h;
            _px = new Color32[w * h];
            if (fill.HasValue)
                for (int i = 0; i < _px.Length; i++) _px[i] = fill.Value;
        }

        public void Set(int x, int y, Color32 c)
        {
            if ((uint)x < (uint)Width && (uint)y < (uint)Height) _px[y * Width + x] = c;
        }

        public Color32 Get(int x, int y) => _px[y * Width + x];

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++) Set(i, j, c);
        }

        public void Circle(float cx, float cy, float r, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r);
            int y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r2) Set(x, y, c);
                }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx);
            int y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        /// <summary>Cópia em que todo pixel opaco vira branco (usada no flash de dano); semitransparentes, como sombras, ficam.</summary>
        public PixelCanvas Silhouette()
        {
            var c = new PixelCanvas(Width, Height);
            for (int i = 0; i < _px.Length; i++)
                c._px[i] = _px[i].a == 255 ? new Color32(255, 255, 255, 255) : _px[i];
            return c;
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(_px);
            tex.Apply();
            return tex;
        }
    }
}
