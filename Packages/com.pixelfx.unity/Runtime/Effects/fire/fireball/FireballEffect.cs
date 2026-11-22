using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Patch PixelFX 5294518. Fire and explosion recipes derive from Sprite Fusion
    // VFX, MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed class FireballEffect
    {
        internal const int Width = 136, Height = 56, OriginX = 112, OriginY = 28;
        internal const float Duration = 1.2f;
        internal const double Period = 1.2;
        private const double Radius = 5, Speed = 120;
        private static readonly (double Length, double Width, double Y)[] Body =
            { (30, 4.2, 0), (23, 4.4, -1.6), (22, 4.4, 1.6), (15, 4.6, 0) };
        private static readonly (double Angle, double Length, double Width)[] Corona =
            { (-1.3, 6, 1.1), (-1.8, 8, 1.2), (-2.3, 7, 1.1), (1.3, 6, 1.1), (1.8, 8, 1.2), (2.3, 7, 1.1) };
        private static readonly (double Width, double Length, int Heat)[] Layers =
            { (1, 1, 6), (0.76, 0.86, 4), (0.54, 0.68, 2), (0.3, 0.42, 0) };

        private readonly PixelPainter painter;
        private readonly uint[] heat;
        private readonly uint glow;
        private readonly int seed;
        private readonly FireballTrail trail;

        internal FireballEffect(PixelFxSettings settings, bool full, PixelPainter painter)
        {
            this.painter = painter;
            seed = settings.seed;
            heat = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            Array.Reverse(heat);
            glow = Mix(Mix(heat[3], heat[4], 0.5), White, 0.25);
            if (full)
                trail = new FireballTrail(seed, heat, Speed, Radius);
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = timeline.Settings.glow };
            var effect = new FireballEffect(timeline.Settings, timeline.Quality == PixelFxQuality.Full, painter);
            return (pixels, _, time, _) =>
            {
                painter.Clear();
                effect.Draw(time);
                painter.FadeLightEdges();
                painter.Composite(pixels, true, false);
            };
        }

        internal void Draw(double time)
        {
            double phase = time % Period / Period;
            double x = Noise(phase, 3, seed + 50) * 0.5, y = Noise(phase, 2.5, seed + 51) * 0.7;
            trail?.DrawTail(painter, phase * Period);
            for (int i = 0; i < Body.Length; i++)
            {
                var (length, width, offset) = Body[i];
                int stream = unchecked((seed * 5 + i) * 17);
                double l = length * (0.82 + 0.25 * Noise(phase, 4, stream)) *
                    (1 + 0.3 * Max(0, Noise(phase, 2.4, stream + 3)));
                Blade(phase, stream, x + 1, y + offset * 0.5, -1, offset * 0.02,
                    l, width, Noise(phase, 3, stream + 5) * 3 + offset, 1.1 + l * 0.05);
            }
            for (int i = 0; i < Corona.Length; i++)
            {
                var (angle, length, width) = Corona[i];
                int stream = unchecked((seed * 5 + i + 20) * 17);
                double l = length * (0.7 + 0.4 * (0.5 + 0.5 * Noise(phase, 6, stream)));
                double c = Cos(angle), s = Sin(angle);
                double dx = c * 0.55 - 1, dy = s * 0.55, norm = Sqrt(dx * dx + dy * dy);
                Blade(phase, stream, x + c * Radius * 0.85, y + s * Radius * 0.85,
                    dx / norm, dy / norm, l, width, -Sign(angle) * 1.5, 1.1);
            }
            painter.Shade(x - Radius * 2.4, y - Radius * 1.7, x + Radius * 1.5, y + Radius * 1.7,
                (X: x, Y: y, Phase: phase, Effect: this), static (px, py, state) =>
                    state.Effect.Head(px - state.X, py - state.Y, state.Phase));
            trail?.DrawSparks(painter, phase * Period, x, y);
            double flicker = 0.85 + 0.15 * Noise(phase, 9, seed + 7);
            painter.GlowLine(x - 6, y, -50, -2, 12, glow, 0.2 * flicker);
            painter.Glow(x - 2, y, 42, glow, 0.42 * flicker);
            painter.Glow(x, y, 13, heat[1], 0.3 * flicker);
        }

        private void Blade(double phase, int seed, double x, double y, double ux, double uy,
            double length, double width, double bend, double wobble)
        {
            double nx = -uy, ny = ux;
            foreach (var layer in Layers)
            {
                double span = length * layer.Length;
                for (double r = 0; r <= span; r += 0.5)
                {
                    double u = r / Max(1, span);
                    double half = width * layer.Width * Pow(1 - u, 0.85) * Min(1, (r + 1.5) / 3.5)
                        + Noise(phase, 9, seed, r * 0.45) * 0.5;
                    if (half < 0.3) continue;
                    double offset = bend * u * u * layer.Length + Noise(phase, 4, seed + 7, r * 0.2) * u * wobble;
                    double cx = x + ux * r + nx * offset, cy = y + uy * r + ny * offset;
                    for (double q = -half; q <= half; q += 0.5)
                        painter.Dot(cx + nx * q, cy + ny * q, heat[layer.Heat]);
                }
            }
        }

        private (uint Ink, double Alpha) Head(double x, double y, double phase)
        {
            double distance = Sqrt(x * x + y * y), angle = Atan2(y, x);
            double back = Max(0, -Cos(angle)), a = angle / (2 * PI);
            double bump = 0.55 * PeriodicNoise(a * 9 + phase * 9, 9, seed)
                + 0.35 * PeriodicNoise(a * 5 - phase * 10, 5, seed + 1);
            double rim = Radius * (1 + 0.55 * back * back) + bump;
            if (distance > rim) return default;
            double swirl = PeriodicNoise(a * 5 - phase * 10 + distance * 0.45, 5, seed + 2);
            double h = 1 - distance / rim + 0.26 * swirl + 0.1 * (1 - back);
            int index = rim - distance < 1 && back < 0.3 ? (h > 0.3 ? 3 : 5)
                : h > 0.74 ? 0 : h > 0.58 ? 1 : h > 0.42 ? 2 : h > 0.27 ? 4 : h > 0.13 ? 5 : 6;
            return (heat[index], 1);
        }

        private static double Noise(double phase, double rate, int seed, double offset = 0)
        {
            int period = Max(1, (int)Floor(rate * Period + 0.5));
            return PeriodicNoise(phase * period + offset, period, seed);
        }

        private static double PeriodicNoise(double x, int period, int seed)
        {
            int cell = (int)Floor(x);
            double f = x - cell, u = f * f * (3 - 2 * f);
            double a = PixelFxRandom.At((cell % period + period) % period, seed);
            double b = PixelFxRandom.At(((cell + 1) % period + period) % period, seed);
            return (a + (b - a) * u) * 2 - 1;
        }
    }
}
