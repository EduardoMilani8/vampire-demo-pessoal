using System;

namespace HollowTide.Core
{
    /// <summary>Posições de spawn num anel logo fora da tela.</summary>
    public static class SpawnRing
    {
        /// <summary>Raio do círculo que circunscreve a tela (meia-diagonal) mais a margem.</summary>
        public static float RingRadius(float halfWidth, float halfHeight, float margin)
            => MathF.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight) + margin;

        /// <summary>
        /// Sorteia um ponto no anel entre RingRadius e RingRadius + jitter ao redor de (cx, cy).
        /// O círculo circunscreve o retângulo visível, então o ponto nunca aparece dentro da tela.
        /// </summary>
        public static void Sample(SimRandom rng, float cx, float cy, float halfWidth, float halfHeight,
                                  float margin, float jitter, out float x, out float y)
        {
            float angle = rng.NextFloat() * (MathF.PI * 2f);
            float r = RingRadius(halfWidth, halfHeight, margin) + rng.NextFloat() * jitter;
            x = cx + MathF.Cos(angle) * r;
            y = cy + MathF.Sin(angle) * r;
        }
    }
}
