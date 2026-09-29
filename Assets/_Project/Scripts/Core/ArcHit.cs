using System;

namespace HollowTide.Core
{
    public static class ArcHit
    {
        /// <summary>
        /// Um círculo inimigo (ex, ey, er) toca o setor de raio <paramref name="radius"/> e meio-ângulo
        /// <paramref name="halfAngleRad"/> centrado em (ox, oy) na direção (fx, fy)? A direção deve estar normalizada.
        /// O ângulo aceita uma folga asin(er/d) para o inimigo ter volume, não ser um ponto.
        /// </summary>
        public static bool InArc(float ox, float oy, float fx, float fy, float radius, float halfAngleRad,
                                 float ex, float ey, float er)
        {
            float dx = ex - ox, dy = ey - oy;
            float d2 = dx * dx + dy * dy;
            float reach = radius + er;
            if (d2 > reach * reach) return false;

            float d = MathF.Sqrt(d2);
            if (d <= er) return true; // a origem está dentro do inimigo

            float cos = (dx * fx + dy * fy) / d;
            cos = MathF.Max(-1f, MathF.Min(1f, cos));
            float angle = MathF.Acos(cos);
            float slack = MathF.Asin(MathF.Min(1f, er / d));
            return angle <= halfAngleRad + slack;
        }
    }
}
