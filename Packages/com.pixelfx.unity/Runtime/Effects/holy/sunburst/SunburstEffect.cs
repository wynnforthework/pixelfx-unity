using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed class SunburstEffect
    {
        internal const int Width = 128, Height = 128, OriginX = 64, OriginY = 64;
        internal const float Duration = 4;
        private const int RayCount = 16;
        private const double Tau = 2 * PI, CoreRadius = 4.5, CoronaRadius = 7.5, RayStart = 9;

        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly (double Birth, double Life, double X, double Y, double Size)[] twinkles;

        private SunburstEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            if (timeline.Quality != PixelFxQuality.Full) return;

            twinkles = new (double, double, double, double, double)[10];
            for (int i = 0; i < twinkles.Length; i++)
            {
                var timing = new PixelFxRandom(settings.seed, i, 911);
                var random = new PixelFxRandom(settings.seed, i);
                double angle = random.Range(0, Tau), radius = random.Range(16, 44);
                twinkles[i] = (timing.Range(0, 1) * Duration, timing.Range(0.35, 0.7),
                    Cos(angle) * radius, Sin(angle) * radius, random.Range(0.5, 1));
            }
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new SunburstEffect(timeline);
            return (pixels, _, time, _) => effect.Render(pixels, time);
        }

        private void Render(byte[] pixels, double time)
        {
            time %= Duration;
            double phase = time / Duration, rotation = phase * (2 * Tau / RayCount);
            painter.Clear();
            painter.Glow(0, 0, 62, colors[4], 0.3 + 0.06 * Sin(phase * Tau * 2));
            DrawThreads(phase, rotation);
            DrawRays(phase, rotation);
            DrawCorona(phase);
            if (twinkles != null) DrawTwinkles(time);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawThreads(double phase, double rotation)
        {
            for (int i = 0; i < RayCount; i++)
            {
                double angle = rotation + (i + 0.5) * Tau / RayCount;
                double length = 14 + 6 * Sin(angle * 3 + phase * Tau);
                double ca = Cos(angle), sa = Sin(angle);
                painter.Line(ca * (RayStart + 1), sa * (RayStart + 1), ca * (RayStart + length),
                    sa * (RayStart + length), colors, static (u, c) => (c[u < 0.3 ? 6 : 5], 0.7 - u * 0.8));
            }
        }

        private void DrawRays(double phase, double rotation)
        {
            for (int i = 0; i < RayCount; i++)
            {
                double angle = rotation + i * Tau / RayCount;
                bool longRay = i % 2 == 0;
                double length = (longRay ? 36 : 21) * (0.86 + 0.14 * Sin(angle * 3 - phase * Tau * 2));
                double ca = Cos(angle), sa = Sin(angle);
                DrawRay(ca, sa, length, longRay ? 4 : 2.6);
                if (!longRay) continue;

                double glint = phase * 2 + angle * 3 / Tau;
                glint -= Floor(glint);
                double radius = RayStart + glint * length;
                if (glint < 0.9)
                {
                    painter.Dot(ca * radius, sa * radius, White);
                    painter.Glow(ca * radius, sa * radius, 5, colors[6], 0.45 * (1 - glint));
                }
                else
                    DrawSparkle(ca * (RayStart + length), sa * (RayStart + length),
                        1 + 3 * Sin((glint - 0.9) / 0.1 * PI), colors[6], 0.5);
            }
        }

        private void DrawRay(double ca, double sa, double length, double rootWidth)
        {
            double endX = ca * (RayStart + length), endY = sa * (RayStart + length), pad = rootWidth + 1;
            painter.Shade(Min(0, endX) - pad, Min(0, endY) - pad, Max(0, endX) + pad, Max(0, endY) + pad,
                (Cos: ca, Sin: sa, Length: length, Width: rootWidth, Colors: colors), static (x, y, s) =>
                {
                    double u = (x * s.Cos + y * s.Sin - RayStart) / s.Length;
                    if (u < 0 || u > 1) return default;
                    double across = Abs(-x * s.Sin + y * s.Cos), halfWidth = s.Width / 2 * (1 - u * 0.85) + 0.25;
                    if (across > halfWidth) return default;

                    uint ink;
                    if (across < 0.5) ink = u < 0.4 ? White : s.Colors[u < 0.75 ? 6 : 5];
                    else if (across > halfWidth - 0.6 && halfWidth > 0.9 && u > 0.15) ink = s.Colors[3];
                    else ink = s.Colors[u < 0.3 ? 6 : u < 0.62 ? 5 : 4];
                    return (ink, u < 0.72 ? 1 : (1 - (u - 0.72) / 0.28) * 1.2);
                });
            double glowEnd = RayStart + length * 0.8;
            painter.GlowLine(ca * RayStart, sa * RayStart, ca * glowEnd, sa * glowEnd, 5, colors[5], 0.16);
        }

        private void DrawCorona(double phase)
        {
            const double bound = CoronaRadius + 2;
            painter.Shade(-bound, -bound, bound, bound, (Phase: phase * Tau * 3, Colors: colors), static (x, y, s) =>
            {
                double distance = Sqrt(x * x + y * y);
                if (distance > CoronaRadius) return default;
                double flicker = 0.5 + 0.5 * Sin(Atan2(y, x) * 5 + s.Phase);
                uint ink = distance < CoreRadius ? White : distance < CoreRadius + 1.5
                    ? (flicker > 0.6 ? White : s.Colors[6]) : s.Colors[flicker > 0.5 ? 5 : 4];
                return (ink, 1);
            });
            painter.Glow(0, 0, 16, White, 0.28);
        }

        private void DrawTwinkles(double time)
        {
            foreach (var (birth, life, x, y, size) in twinkles)
            {
                double age = ((time - birth) % Duration + Duration) % Duration;
                if (age >= life) continue;
                double pulse = Sin(age / life * PI);
                DrawSparkle(x, y, 1 + 2.4 * pulse * size, colors[5], 0.4 * pulse);
            }
        }

        private void DrawSparkle(double x, double y, double arm, uint tip, double glow)
        {
            int length = (int)Floor(arm + 0.5);
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= length; d++)
            {
                uint ink = d > length * 0.6 ? tip : White;
                painter.Dot(px + d, py, ink);
                painter.Dot(px - d, py, ink);
                painter.Dot(px, py + d, ink);
                painter.Dot(px, py - d, ink);
            }
            if (length >= 3)
            {
                painter.Dot(px + 1, py + 1, White);
                painter.Dot(px + 1, py - 1, White);
                painter.Dot(px - 1, py + 1, White);
                painter.Dot(px - 1, py - 1, White);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + length * 2, White, glow);
        }
    }
}
