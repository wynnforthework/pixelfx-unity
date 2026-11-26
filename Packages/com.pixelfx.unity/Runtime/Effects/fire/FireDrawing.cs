using static System.Math;

namespace PixelFx
{
    // From Patch PixelFX fire/_lib.js, adapted from Sprite Fusion fire.
    // MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal static class FireDrawing
    {
        internal static readonly (double Width, double Height, int Heat)[] HotLayers =
            { (1, 1, 6), (0.76, 0.86, 4), (0.54, 0.68, 2), (0.3, 0.42, 0) };

        internal static void Tongue(PixelPainter painter, double x, double y, double height, double width,
            double sweep, double time, int seed, uint[] heat, (double Width, double Height, int Heat)[] layers)
        {
            if (height < 1) return;
            foreach (var layer in layers)
            {
                double extent = height * layer.Height;
                for (int r = 0; r <= extent; r++)
                {
                    double v = r / Max(1, extent);
                    double half = width * layer.Width * Pow(1 - v, 0.85) * Min(1, (r + 1.5) / 3.5)
                        + Noise(time * 9 + r * 0.45, seed) * 0.5;
                    if (half < 0.3) continue;
                    double center = x + sweep * v * v * layer.Height + Noise(time * 4 + r * 0.2, seed + 7) * v * 1.1;
                    int row = Round(y - r), right = Round(center + half);
                    for (int px = Round(center - half); px <= right; px++) painter.Dot(px, row, heat[layer.Heat]);
                }
            }
        }

        internal static void Puff(PixelPainter painter, double x, double y, double k, double size, double seed, uint[] heat)
        {
            double age = 1 - k;
            int index = Min(7, (int)Floor(Min(0.999, Pow(age, 0.62) + seed % 1 * 0.1) * 8));
            double r = size * (0.55 + 0.45 * Min(1, age * 7)) * (1 - age * 0.55);
            uint color = heat[index];
            if (index == 7)
            {
                painter.SoftDisc(x, y, r, color, Min(1, k * 3) * 0.85, 0);
                return;
            }
            painter.Circle(x, y, r, heat[index + 2]);
            painter.Circle(x - r * 0.16, y - r * 0.22, r * 0.78, color);
            if (r > 1.6) painter.Circle(x, y - r * 0.9, r * 0.42, color);
            if (index <= 3 && r > 1.8)
                painter.Circle(x - r * 0.28, y - r * 0.34, r * 0.36, heat[Max(0, index - 1)]);
            if (index < 6) painter.Glow(x, y, r * 2.4 + 2, color, 0.18 * k);
        }

        internal static double Noise(double x, int seed)
        {
            int cell = (int)Floor(x);
            double f = x - cell, u = f * f * (3 - 2 * f);
            double a = PixelFxRandom.At(cell, seed), b = PixelFxRandom.At(cell + 1, seed);
            return (a + (b - a) * u) * 2 - 1;
        }

        private static int Round(double x) => (int)Floor(x + 0.5);
    }
}
