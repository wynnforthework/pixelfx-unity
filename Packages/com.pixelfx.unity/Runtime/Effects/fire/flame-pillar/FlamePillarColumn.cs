using System;
using static System.Math;
using static PixelFx.FireDrawing;

namespace PixelFx
{
    internal sealed partial class FlamePillarEffect
    {
        private double ColumnHeight()
        {
            if (time < Erupt) return 0;
            if (time < Erupt + Rise) return MaxHeight * OutCubic((time - Erupt) / Rise);
            if (time < Fall) return MaxHeight * (1 + 0.05 * Noise(time * 3, noiseSeed));
            double u = Clamp((time - Fall) / (Gone - Fall));
            return MaxHeight * (1 - u * u);
        }

        private double ColumnWidth()
        {
            if (time < Erupt) return 0;
            if (time < Fall)
                return 12 * (1 + 0.45 * Exp(-(time - Erupt) * 7)) * (0.94 + 0.06 * Noise(time * 9, noiseSeed + 1));
            return 12 * (1 - Smooth(Fall, Gone, time)) + 1.5 * (1 - Smooth(Gone - 0.1, Gone, time));
        }

        private void DrawColumn()
        {
            double height = ColumnHeight(), width = ColumnWidth();
            if (height < 1 || width < 0.3) return;
            double strength = Clamp(width / 12);
            if (time < Fall + 0.25)
                for (int i = 0; i < 5; i++)
                {
                    int seed = unchecked((noiseSeed + i) * 17);
                    double x = (i - 2) * width * 0.4;
                    double length = (12 + 12 * (0.5 + 0.5 * Noise(time * 3.3 + i, seed))) * strength
                        * (i == 2 ? 1.2 : i % 4 == 0 ? 0.6 : 0.9);
                    Tongue(painter, x + Noise(time * 2, seed + 2) * 1.5, -height + 12, length,
                        3.4 * strength * (i % 4 == 0 ? 0.7 : 1), -x * 0.3 + Noise(time * 2.2, seed + 1) * 3,
                        time, seed, heat, HotLayers);
                }

            Span<double> strandCenters = stackalloc double[strands.Length];
            for (int i = 0; i < strandCenters.Length; i++)
            {
                var s = strands[i];
                strandCenters[i] = s.Offset + 0.06 * Sin(time * s.Frequency * 3 + s.Offset * 9);
            }
            int flickerFrame = (int)Floor(time * 20);
            for (int y = 0; y >= -height; y--)
            {
                double v = -y / height;
                double center = Noise(-y * 0.04 + time * 1.6, noiseSeed + 2) * 1.6 * v;
                double foot = 1 + 0.7 * Pow(Max(0, 1 - -y / 12.0), 2);
                double top = 1 - Smooth(0.82, 1, v) * 0.7;
                double wobble = y * 0.2 + time * 8;
                double left = width * foot * top * (1 + 0.65 * Pow(Max(0, Noise(wobble, noiseSeed + 3)), 1.5));
                double right = width * foot * top * (1 + 0.65 * Pow(Max(0, Noise(wobble, noiseSeed + 4)), 1.5));
                int pulse = (-y * 0.032 + time * 3.6) % 1 < 0.09 ? 1 : 0;
                for (int x = (int)Floor(center - left * 1.15); x <= Ceiling(center + right * 1.15); x++)
                {
                    double dx = x + 0.5 - center;
                    double q = Abs(dx) / (dx < 0 ? left : right);
                    if (q > 1.15) continue;
                    int level = q < 0.12 ? 0 : q < 0.26 ? 1 : q < 0.42 ? 2 : q < 0.56 ? 3
                        : q < 0.7 ? 4 : q < 0.84 ? 5 : q < 0.96 ? 6 : 7;
                    for (int i = 0; i < strands.Length && level > 0; i++)
                        if (Abs(dx / width - strandCenters[i]) < strands[i].Width) level--;
                    level = Max(0, level - pulse);
                    if (level != 7 || PixelFxRandom.At(x, y, flickerFrame) >= 0.5)
                        painter.Dot(x, y, heat[level]);
                }
                if (y % 12 == 0) painter.Glow(center, y, width * 2.4 + 8, glow, 0.28 * strength);
            }
            painter.Glow(0, -height * 0.45, 92, glow, 0.3 * strength);
        }
    }
}
