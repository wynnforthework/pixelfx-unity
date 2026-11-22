using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class MagicCircleCastEffect
    {
        private static readonly int[] Dither = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        private void DrawColumn()
        {
            if (time < Flash - 0.22 || time > Break + 0.6) return;
            if (time < Flash)
            {
                double alpha = Smooth(Flash - 0.22, Flash - 0.04, time), tip = -(Top - 10) * OutCubic(alpha);
                for (int y = -1; y >= tip; y--)
                    painter.Dot(0, y, y > tip + 6 ? colors[6] : White, alpha * (0.35 + 0.65 * (1 + (double)y / Top)));
                painter.Glow(0, tip, 9, colors[6], 0.5 * alpha);
                painter.Glow(0, -6, 24, colors[5], 0.4 * alpha);
                return;
            }

            double age = time - Flash, breakup = Clamp((time - Break) / 0.45);
            double rise = Clamp(age / 0.09);
            int top = (int)Ceiling(-Top * (rise >= 1 ? 1 : 1 - Pow(2, -10 * rise)));
            double width = time < Break ? BeamWidth * (1 + 0.38 * Exp(-age * 9)) : BeamWidth * (1 - OutCubic(breakup) * 0.85);
            double fade = 1 - Smooth(Break + 0.25, Break + 0.55, time);
            if (fade <= 0) return;
            bool white = age < 0.067;
            for (int y = 3; y >= top; y--)
            {
                int height = -y;
                double spread = width * (1 + 0.5 * Exp(-Max(0, height) / 6.0));
                double pulsePhase = height * 0.022 - age * 2.6;
                int pulse = pulsePhase - Floor(pulsePhase) < 0.07 ? 1 : 0;
                for (int x = (int)Floor(-spread * 1.3); x <= Ceiling(spread * 1.3); x++)
                {
                    if (breakup > 0 && Noise(x * 0.32, height * 0.11 - (time - Break) * 6) < breakup * 1.25 - 0.1) continue;
                    double ratio = (x + 0.5) / spread, q = Abs(ratio);
                    int level = q < 0.2 ? 0 : q < 0.42 ? 1 : q < 0.66 ? 2 : q < 0.88 ? 3 : 4;
                    foreach (var strand in strands)
                        if (Abs(ratio - strand.Offset - 0.05 * Sin(time * strand.Frequency + strand.Offset * 9)) < strand.Width)
                            level = Max(0, level - 1);
                    double alpha = (q < 0.88 ? 1 : (1.3 - q) / 0.42) * fade;
                    double threshold = (Dither[((y & 3) << 2) | (x & 3)] + 0.5) / 16;
                    if (alpha > threshold * (q < 0.66 ? 0.2 : 1))
                        painter.Dot(x, y, columnColors[white ? 0 : Max(0, level - pulse)]);
                }
                if ((height & 7) == 0 && height < Top - spread * 2.4 - 8)
                    painter.Glow(0, y, spread * 2.4 + 6, colors[5], 0.3 * fade);
            }
            for (int i = 0; i < 6; i++)
            {
                double k = (time - (Flash + 0.08 + i * 0.16)) / 0.8;
                if (k < 0 || k > 1) continue;
                double radius = width * 1.4 * (1 + k * 0.35), y = -3 - (1 - (1 - k) * (1 - k)) * 120;
                int count = (int)Ceiling(radius * 7);
                for (int j = 0; j < count; j++)
                {
                    double angle = j / (double)count * Tau, depth = Sin(angle);
                    painter.Dot(Cos(angle) * radius, y + depth * (radius * Squash), k < 0.3 ? White : colors[6],
                        (1 - k) * fade * (depth < 0 ? 0.6 : 1));
                }
            }
            painter.Glow(0, -4, 34, colors[6], 0.45 * fade);
            painter.Glow(0, -60, 70, colors[4], 0.25 * fade);
        }

        private double Noise(double x, double y)
        {
            int ix = (int)Floor(x), iy = (int)Floor(y);
            double fx = x - ix, fy = y - iy;
            double ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            return Lerp(Lerp(PixelFxRandom.At(ix, iy, seed), PixelFxRandom.At(ix + 1, iy, seed), ux),
                Lerp(PixelFxRandom.At(ix, iy + 1, seed), PixelFxRandom.At(ix + 1, iy + 1, seed), ux), uy);
        }

        private void Shock(double delay, double start, double end, double width, double duration, bool far)
        {
            double k = (time - Flash - delay) / duration;
            if (k < 0 || k >= 1) return;
            double radius = Lerp(start, end, OutCubic(k)), thickness = Max(1, width * (1 - k));
            int count = (int)Ceiling(radius * Tau * 1.2);
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau, x = Cos(angle), depth = Sin(angle);
                if ((depth < 0) != far) continue;
                for (double offset = thickness; offset >= 0; offset -= 0.5)
                {
                    double u = offset / thickness, r = radius - offset;
                    uint ink = u < 0.15 ? White : u < 0.4 ? colors[6] : u < 0.7 ? colors[5] : colors[3];
                    painter.Dot(x * r, depth * r * Squash, ink, (1.15 - k) * (u < 0.4 ? 1.2 : 1.15 - u));
                }
            }
        }

        private void DrawFlash()
        {
            double age = (time - Flash) / 0.24;
            if (age < 0 || age >= 1) return;
            double length = 34 * Pow(1 - age, 1.5);
            if (length >= 1)
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * PI / 4, x = Cos(angle), y = Sin(angle);
                    double ray = i % 4 == 0 ? length : i % 2 == 0 ? length * 0.6 : length * 0.3;
                    for (double d = 0; d <= ray; d += 0.5)
                    {
                        double u = d / ray;
                        painter.Dot(x * d, -8 + y * d, u < 0.35 ? White : u < 0.7 ? colors[6] : colors[5],
                            u < 0.6 ? 1 : (1 - age) * 1.5 * (1 - u) + 0.2);
                    }
                }
            painter.Glow(0, -26, 56, White, 0.5 * (1 - age));
        }

        private void DrawFlashLights()
        {
            FlashLight(-56, 104, colors[5], 0.9, 0.6);
            FlashLight(-5, 36, White, 0.8, 0.14);
        }

        private void FlashLight(double y, double radius, uint ink, double strength, double duration)
        {
            if (flashStep == 0) return;
            double life = duration;
            for (int i = flashStep; i <= steps && life > 0; i++) life -= Step;
            if (life <= 0) return;
            double k = life / duration;
            painter.Glow(0, y, radius * (0.55 + 0.45 * k), ink, strength * k * k);
        }

        private void Sparkle(double x, double y, double arm, uint ink, uint tip, double light)
        {
            int n = (int)Floor(arm + 0.5);
            if (n < 1) return;
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= n; d++)
            {
                uint color = d > n * 0.6 ? tip : ink;
                painter.Dot(px + d, py, color);
                painter.Dot(px - d, py, color);
                painter.Dot(px, py + d, color);
                painter.Dot(px, py - d, color);
            }
            for (int d = 1; d <= n / 3 && d <= 2; d++)
            {
                uint color = d == 1 ? ink : tip;
                painter.Dot(px + d, py + d, color);
                painter.Dot(px + d, py - d, color);
                painter.Dot(px - d, py + d, color);
                painter.Dot(px - d, py - d, color);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + n * 2, ink, light);
        }
    }
}
