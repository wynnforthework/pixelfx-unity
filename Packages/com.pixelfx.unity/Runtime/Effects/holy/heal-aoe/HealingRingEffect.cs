using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed partial class HealingRingEffect
    {
        internal const int Width = 256, Height = 136, OriginX = 128, OriginY = 104;
        internal const float Duration = 2.9f;
        private const double Tau = 2 * PI, Squash = 0.2, Charge = 0.3, Reach = 104;
        private const double Step = 1.0 / 120;
        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly Site[] sites = new Site[26];
        private readonly (double Angle, double Speed)[] riders;
        private readonly bool full;
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private double time, releaseAge = double.PositiveInfinity;
        private int step;

        private struct Site
        {
            internal double X, Z, Start, Rise, Life, Sway;
            internal int Arm;
            internal bool Thick;
        }

        private HealingRingEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            full = timeline.Quality == PixelFxQuality.Full;
            random = new PixelFxRandom(settings.seed);
            for (int i = 0; i < sites.Length; i++)
            {
                double angle = random.Range(0, Tau), distance = Reach * Sqrt(random.Range(0.04, 0.92));
                int kind = i % 5;
                sites[i] = new Site
                {
                    X = Cos(angle) * distance, Z = Sin(angle) * distance,
                    Start = Passed(distance) + random.Range(0, 0.06), Rise = random.Range(18, 34),
                    Life = random.Range(0.9, 1.3), Sway = random.Range(0, Tau),
                    Arm = kind == 0 ? 4 : kind == 1 ? 3 : kind == 2 ? 1 : 2, Thick = kind < 2,
                };
            }
            if (full)
            {
                riders = new (double, double)[14];
                for (int i = 0; i < riders.Length; i++)
                    riders[i] = (i / 14.0 * Tau + random.Range(-0.2, 0.2), random.Range(0.6, 1.1) * (i % 2 == 0 ? -1 : 1));
            }
            initialRandom = random;
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new HealingRingEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            Advance(sample);
            painter.Clear();
            double radius = Front(time);
            if (pass != PixelFxRenderPass.Front)
            {
                DrawWash(radius);
                DrawRing(radius, true);
                DrawSprouts(true);
                if (full) DrawParticles(true);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                DrawRing(radius, false);
                DrawRelease();
                DrawSprouts(false);
                if (full) DrawParticles(false);
                DrawReleaseGlow(0, -16, 76, colors[5], 0.85, 0.55);
                DrawReleaseGlow(0, -6, 28, White, 0.8, 0.12);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private static double Clamp(double t) => Max(0, Min(1, t));
        private static double Progress(double a, double b, double t) => Clamp((t - a) / (b - a));
        private static double Smooth(double a, double b, double t)
        {
            double u = Progress(a, b, t);
            return u * u * (3 - 2 * u);
        }

        private static double OutCubic(double u) => 1 - Pow(1 - u, 3);
        private static double Front(double t) => Reach * OutCubic(Progress(Charge, Charge + 1, t));
        private static double Passed(double distance) => Charge + (1 - Pow(1 - Min(0.999, distance / Reach), 1.0 / 3));

        private void DrawWash(double radius)
        {
            double fade = 1 - Smooth(1.4, 1.9, time);
            if (radius < 2 || fade <= 0) return;
            for (int y = (int)Floor(-radius * Squash); y < Ceiling(radius * Squash); y++)
            for (int x = (int)Floor(-radius); x < Ceiling(radius); x++)
            {
                double dx = x + 0.5, dy = (y + 0.5) / Squash;
                double distance = Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;
                double k = Exp(-(time - Passed(distance)) / 0.35) * Smooth(radius, radius - 6, distance) * fade;
                if (k >= 0.04) painter.Light(x, y, colors[4], 0.32 * k);
            }
        }

        private void DrawRing(double radius, bool back)
        {
            if (time < Charge) return;
            double u = Progress(Charge, Charge + 1 + 0.25, time), k = u < 0.7 ? 1 : (1 - u) / 0.3;
            if (k <= 0) return;
            double thickness = Max(2, 7 * (1 - u * 0.7));
            bool hot = time < Charge + 0.08;
            int count = (int)Ceiling(radius * Tau * 1.2);
            for (int j = 0; j < count; j++)
            {
                double angle = j / (double)count * Tau, sine = Sin(angle);
                if ((sine < 0) != back) continue;
                double cosine = Cos(angle);
                for (double offset = thickness; offset >= 0; offset -= 0.5)
                {
                    double depth = offset / thickness;
                    uint ink = depth < 0.15 && hot ? White : colors[depth < 0.15 ? 7 : depth < 0.4 ? 6 : depth < 0.7 ? 5 : 4];
                    painter.Dot(cosine * (radius - offset), sine * (radius - offset) * Squash,
                        ink, k * (depth < 0.4 ? 1.2 : 1.15 - depth));
                }
            }
            count = (int)Ceiling((radius + 1) * Tau * 1.5) + 6;
            for (int j = 0; j < count; j++)
            {
                double angle = j / (double)count * Tau, sine = Sin(angle);
                if ((sine < 0) != back) continue;
                painter.Dot(Cos(angle) * (radius + 1), sine * ((radius + 1) * Squash),
                    sine > 0.3 ? White : colors[6], k * (back ? 0.7 : 1));
            }

            if (full) DrawCrest(radius, k, back);
            if (!back)
                for (int i = 0; i < 8; i++)
                {
                    double angle = i / 8.0 * Tau;
                    painter.Glow(Cos(angle) * radius, Sin(angle) * radius * Squash - 2, 16, colors[4], 0.22 * k);
                }
        }

        private void DrawCrest(double radius, double k, bool back)
        {
            int count = (int)Ceiling(radius * 0.9), frame = (int)Floor(time * 20);
            for (int j = 0; j < count; j++)
            {
                double angle = j / (double)count * Tau, sine = Sin(angle);
                if ((sine < 0) != back || PixelFxRandom.At(j, frame) > 0.45) continue;
                double x = Cos(angle) * radius, y = sine * radius * Squash;
                double h = (1 + PixelFxRandom.At(j, 7) * 4) * k * (back ? 0.6 : 1);
                for (int i = 1; i <= h; i++)
                    painter.Dot(x, y - i, i > h - 1 ? White : colors[6], k * (1.2 - i / (h + 1)));
            }
            foreach (var (start, speed) in riders)
            {
                double angle = start + speed * (time - Charge) * 2.2, sine = Sin(angle);
                if ((sine < 0) != back) continue;
                double x = Cos(angle) * radius, y = sine * radius * Squash;
                if (back) painter.Dot(x, y, colors[6], k);
                else Sparkle(x, y - 1, Sin(time * 9 + start * 5) > 0.3 ? 3 : 2, White, colors[6], 0.35 * k);
            }
        }

        private void DrawRelease()
        {
            if (time < Charge)
            {
                double k = time / Charge;
                Sparkle(0, -3, 2 + 4 * k * k, White, colors[6], 0.3 + 0.4 * k);
                painter.Glow(0, -3, 10 + 16 * k, colors[4], 0.5 * k);
            }
            else if (time < Charge + 0.2)
            {
                double k = 1 - (time - Charge) / 0.2;
                double length = 22 * Pow(k, 1.5);
                if (length >= 1)
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * PI / 4, len = length * (i % 4 == 0 ? 1 : i % 2 == 0 ? 0.6 : 0.3);
                        for (double d = 0; d <= len; d += 0.5)
                        {
                            double u = d / len;
                            painter.Dot(Cos(angle) * d, -3 + Sin(angle) * d, u < 0.35 ? White : colors[u < 0.7 ? 6 : 5],
                                u < 0.6 ? 1 : k * 1.5 * (1 - u) + 0.2);
                        }
                    }
            }

            double g = Progress(Charge, Charge + 0.5, time);
            if (g <= 0 || g >= 1) return;
            double top = -4 - 46 * OutCubic(Min(1, g * 2.2)) - 10 * g;
            double bottom = -2 - 50 * Pow(Progress(0.35, 1, g), 2), width = 1 + 2.4 * (1 - g);
            if (bottom - top <= 2) return;
            painter.Shade(-width, top - width, width, bottom + width,
                (Top: top, Bottom: bottom, Width: width, Colors: colors), static (x, y, s) =>
                {
                    double u = Clamp((y - s.Bottom) / (s.Top - s.Bottom));
                    double dy = s.Bottom + (s.Top - s.Bottom) * u - y;
                    double v = Sqrt(x * x + dy * dy) / s.Width;
                    return v > 1 ? default : (v < 0.45 ? White : s.Colors[u > 0.8 ? 5 : 6], 1);
                });
            painter.GlowLine(0, bottom, 0, top, 12, colors[5], 0.55 * (1 - g));
            Sparkle(0, top, 4 * (1 - g) + 1, White, colors[6], 0.5 * (1 - g));
        }

        private void DrawReleaseGlow(double x, double y, double radius, uint ink, double strength, double life)
        {
            double k = 1 - releaseAge / life;
            if (k > 0) painter.Glow(x, y, radius * (0.55 + 0.45 * k), ink, strength * k * k);
        }

        private void DrawSprouts(bool back)
        {
            foreach (var site in sites)
            {
                double age = time - site.Start;
                if (age < 0 || age > site.Life || (site.Z < 0) != back) continue;
                double u = age / site.Life, rise = site.Rise * OutCubic(u);
                double x = site.X + Sin(age * 4 + site.Sway) * 1.5 * u, y = site.Z * Squash - 2 - rise;
                double opening = Clamp(age / 0.12) - 1, pop = 1 + 3.6 * Pow(opening, 3) + 2.6 * Pow(opening, 2);
                double fadeStart = site.Life - 0.15;
                int arm = Max(1, (int)Floor(site.Arm * pop - (back ? 1 : 0) + 0.5));
                if (age < 0.04) Sparkle(x, y, 3, White, colors[6], 0.5);
                else if (age < fadeStart)
                    DrawPlus(x, y, arm, site.Thick && (!back || arm >= 3), back, !back && Sin(age * 12 + site.Sway * 3) > 0.8);
                else if (full)
                {
                    double v = (age - fadeStart) / 0.15, d = 2 + v * 5;
                    DrawDiagonals(x, y - v * 2, d, v < 0.4 ? White : colors[6], 1 - v);
                }

                if (full && u < 0.5)
                    for (double j = 1; j < rise * (1 - u * 2); j += 1.5)
                        painter.Dot(site.X, site.Z * Squash - 2 - rise + j + 3, colors[5],
                            (1 - j / rise) * (1 - u * 2) * (back ? 0.5 : 0.8));
                painter.Glow(x, y, 7 + arm * 2, colors[5],
                    (back ? 0.2 : 0.34) * (age < fadeStart ? 1 : 1 - (age - fadeStart) / 0.15));
            }
        }

        private static bool InPlus(int x, int y, int arm, int width) =>
            (Abs(x) <= width && Abs(y) <= arm) || (Abs(y) <= width && Abs(x) <= arm);

        private void DrawPlus(double x, double y, int arm, bool thick, bool back, bool glint)
        {
            int px = (int)Floor(x + 0.5), py = (int)Floor(y + 0.5), width = thick ? 1 : 0, n = back ? 7 : 8;
            double alpha = back ? 0.8 : 1;
            for (int j = -arm - 1; j <= arm + 1; j++)
            for (int i = -arm - 1; i <= arm + 1; i++)
            {
                if (!InPlus(i, j, arm, width))
                {
                    if (thick && (InPlus(i - 1, j, arm, width) || InPlus(i + 1, j, arm, width)
                        || InPlus(i, j - 1, arm, width) || InPlus(i, j + 1, arm, width)))
                        painter.Dot(px + i, py + j, colors[n - 6], alpha * 0.9);
                    continue;
                }

                int distance = Abs(i) + Abs(j);
                uint ink;
                if (distance == 0 || (glint && ((i == 0 && j == -arm) || (i == -arm && j == 0)))) ink = White;
                else if (!thick || i == 0 || j == 0) ink = colors[n - (distance < arm ? 2 : 3)];
                else ink = colors[n - (i < 0 || j < 0 ? 3 : 5)];
                painter.Dot(px + i, py + j, ink, alpha);
            }
        }

        private void DrawDiagonals(double x, double y, double d, uint ink, double alpha)
        {
            painter.Dot(x + d, y + d, ink, alpha);
            painter.Dot(x + d, y - d, ink, alpha);
            painter.Dot(x - d, y + d, ink, alpha);
            painter.Dot(x - d, y - d, ink, alpha);
        }

        private void Sparkle(double x, double y, double arm, uint core, uint tip, double light)
        {
            int length = (int)Floor(arm + 0.5);
            if (length < 1) return;
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= length; d++)
            {
                uint ink = d > length * 0.6 ? tip : core;
                painter.Dot(px + d, py, ink);
                painter.Dot(px - d, py, ink);
                painter.Dot(px, py + d, ink);
                painter.Dot(px, py - d, ink);
            }
            if (length >= 3) DrawDiagonals(px, py, 1, core, 1);
            if (length >= 6) DrawDiagonals(px, py, 2, tip, 1);
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + length * 2, core, light);
        }
    }
}
