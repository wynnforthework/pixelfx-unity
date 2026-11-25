using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed partial class SmiteEffect
    {
        internal const int Width = 224, Height = 176, OriginX = 112, OriginY = 148;
        internal const float Duration = 2.8f;
        private const double Tau = PI * 2, Top = -148, Squash = 0.38, Mark = 0.42, Hit = 0.54;
        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly Crack[] cracks = new Crack[7];
        private readonly bool full;
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private double time, hitAge = double.PositiveInfinity;
        private int tick;

        private struct Crack
        {
            internal (double X, double Y)[] Main, Fork;
            internal double Length, Cooling;
        }

        private SmiteEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            full = timeline.Quality == PixelFxQuality.Full;
            random = new PixelFxRandom(settings.seed);
            for (int i = 0; i < cracks.Length; i++)
            {
                double angle = 0.12 + i / 6.0 * (PI - 0.24) + random.Range(-0.12, 0.12);
                double length = random.Range(30, 54) * (Abs(Sin(angle)) > 0.7 ? 0.6 : 1);
                var main = MakePath(0, 0, Cos(angle) * length, Sin(angle) * length, 0.28, 16);
                for (int j = 0; j < main.Length; j++) main[j].Y *= Squash;
                var (x, y) = main[main.Length / 2];
                double endX = x + Cos(angle + Sign() * 0.7) * length * 0.35;
                double endY = y + Sin(angle + Sign() * 0.7) * length * 0.35 * Squash;
                var fork = MakePath(x, y, endX, endY, 0.3, 8);
                cracks[i] = new Crack { Main = main, Fork = fork, Length = length, Cooling = random.Range(1.6, 2.1) };
            }
            initialRandom = random;
        }

        private (double X, double Y)[] MakePath(double x, double y, double endX, double endY, double jag, int segments)
        {
            var points = new (double X, double Y)[segments + 1];
            points[0] = (x, y);
            points[segments] = (endX, endY);
            for (int stride = segments; stride > 1; stride /= 2)
            for (int i = 0; i < segments; i += stride)
            {
                var a = points[i];
                var b = points[i + stride];
                double dx = b.X - a.X, dy = b.Y - a.Y, length = Sqrt(dx * dx + dy * dy);
                double offset = random.Range(-1, 1) * jag * length;
                double divisor = length == 0 ? 1 : length;
                points[i + stride / 2] = ((a.X + b.X) / 2 - dy / divisor * offset, (a.Y + b.Y) / 2 + dx / divisor * offset);
            }
            return points;
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, t, strength) => render(pixels, stage, t, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new SmiteEffect(timeline);
            return (pixels, _, t, _, pass) => effect.Render(pixels, t, pass);
        }

        private static double Clamp(double t) => Max(0, Min(1, t));
        private static double Progress(double a, double b, double t) => Clamp((t - a) / (b - a));
        private static double OutCubic(double u) => 1 - Pow(1 - u, 3);
        private static double TipY(double t)
        {
            double u = Progress(Mark, Hit, t);
            return Top - 6 + (6 - Top) * u * u * u;
        }

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            Advance(sample);
            painter.Clear();
            if (pass != PixelFxRenderPass.Front)
            {
                DrawCracks();
                if (full) DrawParticles(true);
                DrawWave(true);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                if (time < Hit) DrawMark();
                DrawHammer();
                DrawAfterimage();
                DrawWave(false);
                DrawAirShock();
                if (time >= Hit && time < Hit + 0.07)
                {
                    painter.Circle(0, -6, 16, White);
                    painter.Circle(0, -6, 19, colors[6], 0.6);
                }
                if (full)
                {
                    DrawChips();
                    DrawParticles(false);
                }
                if (time >= Hit && time < Hit + 0.25)
                    Sparkle(0, -8, 16 * (1 - (time - Hit) / 0.25), White, colors[5], 0.7);
                DrawFlash(-24, 92, colors[5], 1, 0.6);
                DrawFlash(-8, 40, White, 0.9, 0.12);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawMark()
        {
            double u = Progress(0, Mark, time), k = Progress(0, 0.08, time);
            k = k * k * (3 - 2 * k);
            double spin = time * 3, eased = u < 0.5 ? 4 * u * u * u : 1 - Pow(-2 * u + 2, 3) / 2;
            double radius = 26 - 18 * eased;
            DrawOval(0, radius, radius * Squash, colors[5], k * (0.65 + 0.35 * Sin(time * 40)), spin);
            DrawOval(1, radius, radius * Squash, colors[3], k * 0.6, nearOnly: true);
            double opening = Clamp(time / 0.2) - 1;
            double length = (3 + 8 * (1 + 3 * Pow(opening, 3) + 2 * Pow(opening, 2)))
                * (1 + 0.6 * Progress(Mark, Hit, time));
            for (int i = 0; i < 4; i++)
            {
                double angle = spin * 0.3 + i * PI / 2, len = length * (i % 2 == 0 ? 1 : 0.6);
                painter.Line(0, -2, Cos(angle) * len, -2 + Sin(angle) * len, colors[5],
                    static (v, ink) => (v < 0.4 ? White : ink, 1.2 - v));
            }
            painter.Dot(0, -2, White);
            painter.Glow(0, -2, 14 + 10 * u, colors[5], 0.5 * k);
            painter.GlowLine(0, Top + 6, 0, -2, 5, colors[4], 0.12 * u);
        }

        private void DrawOval(double y, double rx, double ry, uint ink, double alpha, double spin = double.NaN, bool nearOnly = false)
        {
            int count = (int)Ceiling(rx * Tau * 1.5) + 6;
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau, sine = Sin(angle);
                if (nearOnly && sine <= 0) continue;
                painter.Dot(Cos(angle) * rx, y + sine * ry, Sin(angle * 4 + spin) > 0 ? White : ink, alpha);
            }
        }

        private static readonly (int X, double Strength)[] SpeedLines = { (-5, 0.6), (-2, 0.9), (2, 0.8), (5, 0.5) };

        private void DrawHammer()
        {
            if (time < Mark || time >= Hit + 0.06) return;
            bool white = time >= Hit;
            double end = white ? 0 : TipY(time);
            if (end < Top + 12) return;
            double start = Max(Top + 5, end - 44 * (white ? 1 - (time - Hit) / 0.06 : 1));
            foreach (var (x, power) in SpeedLines)
                painter.Line(x, Max(Top, start - 70), x, start, (power, colors),
                    static (u, s) => (s.colors[u > 0.7 ? 6 : 4], u * s.power));
            if (end - 3 > start) DrawBeam(start, end - 3, 4.5, white, false);
            DrawBeam(end - 6, end - 1, 6.5, white, true);
            painter.GlowLine(0, start + 10, 0, end, 16, colors[5], 0.6);
            painter.Glow(0, end - 4, Min(22, end - Top - 6), White, 0.5);
        }

        private void DrawAfterimage()
        {
            double u = Progress(Hit + 0.04, Hit + 0.55, time);
            if (time <= Hit + 0.04 || u >= 1) return;
            DrawBeam(Top + 4, -4, (1 + 4 * Pow(1 - u, 2)) / 2, false, false, 1 - u * 0.5);
            painter.GlowLine(0, Top + 16, 0, -6, 10, colors[5], 0.4 * (1 - u));
        }

        private void DrawBeam(double start, double end, double radius, bool white, bool head, double afterimage = 0)
        {
            for (int y = (int)Floor(start - radius); y <= Ceiling(end + radius); y++)
            for (int x = (int)Floor(-radius); x <= Ceiling(radius); x++)
            {
                double u = Clamp((y + 0.5 - start) / (end - start));
                double dx = x + 0.5, dy = start + (end - start) * u - (y + 0.5);
                double v = Sqrt(dx * dx + dy * dy) / radius;
                if (v > 1) continue;
                uint ink = white || v < (head ? 0.6 : afterimage > 0 ? 0.5 : 0.35)
                    ? White : colors[head || (afterimage == 0 && v < 0.7) ? 6 : 5];
                painter.Dot(x, y, ink, afterimage > 0 ? (0.3 + 0.7 * u) * afterimage : 1);
            }
        }

        private void DrawWave(bool back)
        {
            double k = Progress(Hit, Hit + 0.42, time);
            if (time < Hit || k >= 1) return;
            double radius = 6 + 84 * OutCubic(k), thickness = Max(1, 5 * (1 - k));
            int count = (int)Ceiling(radius * Tau * 1.2);
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau, sy = Sin(angle);
                if ((sy < 0) != back) continue;
                double sx = Cos(angle);
                for (double offset = thickness; offset >= 0; offset -= 0.5)
                {
                    double u = offset / thickness;
                    uint ink = u < 0.15 && k < 0.12 ? White : colors[u < 0.15 ? 7 : u < 0.4 ? 6 : u < 0.7 ? 5 : 4];
                    painter.Dot(sx * (radius - offset), sy * (radius - offset) * (Squash * 0.8),
                        ink, (1.15 - k) * (u < 0.4 ? 1.2 : 1.15 - u));
                }
            }
        }

        private void DrawAirShock()
        {
            double d = Progress(Hit, Hit + 0.28, time);
            if (time < Hit || d >= 1) return;
            double radius = 8 + 40 * OutCubic(d), thickness = Max(1, 3 * (1 - d));
            painter.Shade(-radius, -radius, radius, 0,
                (Radius: radius, Thickness: thickness, Progress: d, Ink: colors[5]), static (x, y, s) =>
                {
                    double distance = Sqrt(x * x + y * y);
                    if (distance > s.Radius || distance < s.Radius - s.Thickness) return default;
                    double v = (distance - (s.Radius - s.Thickness)) / s.Thickness;
                    return (s.Progress < 0.2 || v > 0.6 ? White : s.Ink, 1.1 - s.Progress);
                });
            for (double angle = PI * 1.1; angle < Tau - 0.2; angle += 0.5)
                painter.Glow(Cos(angle) * radius, Sin(angle) * radius, 8, colors[5], 0.3 * (1 - d));
        }

        private void DrawCracks()
        {
            if (time < Hit) return;
            double grow = OutCubic(Progress(Hit, Hit + 0.1, time));
            foreach (var crack in cracks)
            {
                DrawCrackPath(crack, crack.Main, grow, true);
                DrawCrackPath(crack, crack.Fork, grow, false);
            }
            double heat = Clamp(1 - (time - Hit) / 1.6);
            if (heat <= 0) return;
            DrawOval(0, 5, 1.6, heat > 0.6 ? White : colors[heat > 0.3 ? 5 : 3], Min(1, heat * 3));
            painter.Glow(0, 0, 18, colors[4], 0.45 * heat);
        }

        private void DrawCrackPath(Crack crack, (double X, double Y)[] points, double grow, bool main)
        {
            int count = Max(1, (int)Floor((points.Length - 1) * grow + 0.5));
            for (int i = 0; i < count; i++)
            {
                var (x, y) = points[i];
                var end = points[i + 1];
                double depth = y / Squash, distance = Sqrt(x * x + depth * depth) / crack.Length;
                double heat = Clamp(1 - (time - Hit) / crack.Cooling - distance * 0.5);
                if (heat <= 0) continue;
                uint ink = heat > 0.85 ? White : colors[heat > 0.65 ? 6 : heat > 0.45 ? 5 : heat > 0.3 ? 4 : heat > 0.18 ? 3 : 2];
                double alpha = Min(1, heat / 0.12);
                painter.Line(x, y, end.X, end.Y, ink, alpha);
                if (main && distance < 0.45)
                    painter.Line(x, y + 1, end.X, end.Y + 1, colors[heat > 0.5 ? 4 : 2], alpha);
                if (i % 2 == 0) painter.Glow(x, y, 10, colors[4], 0.45 * heat);
            }
        }

        private void DrawFlash(double y, double radius, uint ink, double strength, double life)
        {
            double k = 1 - hitAge / life;
            if (k > 0) painter.Glow(0, y, radius * (0.55 + 0.45 * k), ink, strength * k * k);
        }
    }
}
