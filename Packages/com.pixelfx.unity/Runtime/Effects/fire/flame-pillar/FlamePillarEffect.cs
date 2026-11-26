using System;
using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Patch PixelFX 5294518. Adapts Sprite Fusion's explosion, holy light and fire,
    // MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed partial class FlamePillarEffect
    {
        internal const int Width = 184, Height = 256, OriginX = 92, OriginY = 232;
        internal const float Duration = 5;
        private const double Erupt = 0.55, Rise = 0.12, Fall = 1.95, Gone = 2.45;
        private const double MaxHeight = 140, Tau = 2 * PI, Step = ImpactParticles.Step;
        private static readonly uint Soot = Hex(0x0d0a12);
        private static readonly uint Warm = Rgb(255, 176, 96);
        private static readonly SmallExplosionEffect.Stone Stone = new SmallExplosionEffect.Stone(Hex(0x4a3f69));
        private readonly PixelPainter painter;
        private readonly uint[] heat;
        private readonly uint glow;
        private readonly int noiseSeed;
        private readonly bool full;
        private readonly Crack[] cracks;
        private readonly (double Offset, double Width, double Frequency)[] strands =
            new (double, double, double)[7];
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private double time;

        private sealed class Crack
        {
            internal (double X, double Y)[] Points;
            internal double Start, Growth;
            internal bool Wide;

            internal int VisibleCount(double at) => (int)Floor(Points.Length * Clamp((at - Start) / Growth));
        }

        private FlamePillarEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            heat = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            Array.Reverse(heat);
            glow = Mix(Mix(heat[3], heat[4], 0.5), White, 0.25);
            noiseSeed = unchecked(settings.seed * 13);
            full = timeline.Quality == PixelFxQuality.Full;
            random = new PixelFxRandom(settings.seed);
            cracks = CreateCracks();
            initialRandom = random;
            for (int i = 0; i < strands.Length; i++)
                strands[i] = ((PixelFxRandom.At(i, noiseSeed, 1) - 0.5) * 1.4,
                    0.05 + PixelFxRandom.At(i, noiseSeed, 2) * 0.06, 1 + PixelFxRandom.At(i, noiseSeed, 3) * 2);
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var draw = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => draw(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new FlamePillarEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            Advance(sample);
            painter.Clear();
            if (sample >= Duration)
            {
                Array.Clear(pixels, 0, pixels.Length);
                return;
            }
            if (pass != PixelFxRenderPass.Front)
            {
                DrawCracks();
                if (full) DrawParticles(true);
                DrawRing(true);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                DrawColumn();
                if (full)
                {
                    DrawRocks();
                    DrawParticles(false);
                }
                DrawRing(false);
                DrawFlash();
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private Crack[] CreateCracks()
        {
            var result = new List<Crack>(18);
            for (int i = 0; i < 9; i++)
            {
                double angle = i / 9.0 * Tau + random.Range(-0.25, 0.25);
                var main = CreateCrack(random.Range(-1, 1), random.Range(-0.3, 0.3),
                    angle, random.Range(20, 40), random.Range(0, 0.16), true);
                result.Add(main);
                int branch = 3 + RangeInt(0, main.Points.Length - 6);
                if (main.Points.Length <= 8) continue;
                var (x, y) = main.Points[branch];
                result.Add(CreateCrack(x, y, angle + Side() * random.Range(0.5, 1),
                    random.Range(6, 14), 0.18 + random.Range(0, 0.15), false));
            }
            return result.ToArray();
        }

        private Crack CreateCrack(double x, double y, double angle, double length, double start, bool wide)
        {
            var points = new (double, double)[1 + (int)Ceiling(length / 2)];
            points[0] = (x, y);
            for (int i = 1; i < points.Length; i++)
            {
                angle += random.Range(-0.55, 0.55);
                x += Cos(angle) * 2;
                y += Sin(angle) * 0.76;
                points[i] = (x, y);
            }
            return new Crack { Points = points, Start = start, Growth = random.Range(0.22, 0.36), Wide = wide };
        }

        private void DrawCracks()
        {
            double k = Smooth(0, Erupt, time) * 0.75 + 0.25 * Smooth(Erupt - 0.05, Erupt + 0.1, time)
                - Smooth(Gone - 0.3, Duration - 0.6, time) * 1.05;
            if (k <= -0.05) return;
            foreach (var crack in cracks)
            {
                int count = crack.VisibleCount(time);
                for (int i = 1; i < count; i++)
                {
                    double hot = k * (1 - 0.45 * (i / (double)crack.Points.Length));
                    var (x0, y0) = crack.Points[i - 1];
                    var (x1, y1) = crack.Points[i];
                    if (hot < 0)
                    {
                        painter.Line(x0, y0, x1, y1, Soot, 1 + hot * 3);
                        continue;
                    }
                    uint ink = heat[hot > 0.85 ? 1 : hot > 0.65 ? 2 : hot > 0.45 ? 4 : hot > 0.25 ? 5 : hot > 0.1 ? 6 : 7];
                    if (crack.Wide && i < crack.Points.Length * 0.5 && hot > 0.2)
                    {
                        painter.Line(x0, y0 + 1, x1, y1 + 1, heat[hot > 0.6 ? 5 : 6]);
                        painter.Line(x0, y0 - 1, x1, y1 - 1, heat[7]);
                    }
                    painter.Line(x0, y0, x1, y1, ink);
                    if (i % 2 == 0) painter.Glow(x1, y1, 5 + 7 * hot, glow, 0.3 * hot);
                }
            }
            double strength = 0.35 * k;
            if (strength <= 0.004) return;
            for (int y = -8; y <= 8; y++)
            for (int x = -44; x <= 44; x++)
            {
                double dx = (x + 0.5) / 44, dy = y / 8.0;
                double distance = dx * dx + dy * dy;
                if (distance >= 1) continue;
                double f = 1 - Sqrt(distance);
                painter.Light(x, y, glow, f * f * strength);
            }
        }

        private void DrawRing(bool back)
        {
            double k = (time - Erupt) / 0.45;
            if (k <= 0 || k >= 1) return;
            double radius = 84 * OutCubic(k), width = Max(1, 4 * (1 - k));
            int count = (int)Ceiling(radius * Tau * 1.2);
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau, sin = Sin(angle);
                if ((sin < 0) != back) continue;
                double cos = Cos(angle);
                for (double d = width; d >= 0; d -= 0.5)
                {
                    double u = d / width;
                    uint ink = u < 0.15 ? (!back && k < 0.15 ? White : heat[1])
                        : u < 0.4 ? heat[4] : u < 0.7 ? Stone.Light : Stone.Body;
                    painter.Dot(cos * (radius - d), 0.5 + sin * (radius - d) * 0.18, ink,
                        (1.15 - k) * (u < 0.4 ? 1.2 : 1.15 - u));
                }
            }
        }

        private void DrawFlash()
        {
            double f = (time - Erupt) / 0.08;
            if (f > 0 && f < 1)
            {
                DrawEllipse(-2, 16 * (1 - f * 0.3), 6, heat[2]);
                DrawEllipse(-3, 11 * (1 - f * 0.3), 4, f < 0.5 ? White : heat[1]);
            }
            DrawBurstLight(-40, 90, Warm, warmLife, 0.6);
            DrawBurstLight(-8, 44, White, whiteLife, 0.1);
        }

        private void DrawEllipse(double centerY, double rx, double ry, uint ink)
        {
            painter.Shade(-rx, centerY - ry, rx, centerY + ry, (centerY, rx, ry, ink), static (x, y, s) =>
            {
                double dx = x / s.rx, dy = (y - s.centerY) / s.ry;
                return dx * dx + dy * dy <= 1 ? (s.ink, 1.0) : default;
            });
        }

        private void DrawBurstLight(double y, double radius, uint ink, double life, double duration)
        {
            if (life <= 0) return;
            double k = life / duration;
            painter.Glow(0, y, radius * (0.55 + 0.45 * k), ink, 0.7 * k * k);
        }

        private static double Clamp(double k) => Max(0, Min(1, k));
        private static double OutCubic(double k) => 1 - Pow(1 - k, 3);
        private static double Smooth(double a, double b, double value)
        {
            double k = Clamp((value - a) / (b - a));
            return k * k * (3 - 2 * k);
        }
    }
}
