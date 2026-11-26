using System;
using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;
using static PixelFx.FireDrawing;

namespace PixelFx
{
    // Patch PixelFX 5294518. Fire and explosion recipes adapt Sprite Fusion VFX,
    // MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed partial class FireRingEffect
    {
        internal const int Width = 224, Height = 128, OriginX = 112, OriginY = 92;
        internal const float Duration = 3.9f;
        private const double End = 3.9, Tau = 2 * PI, Squash = 0.2, Charge = 0.32, Spread = 0.75, Reach = 92;
        private static readonly uint Soot = Hex(0x0d0a12), SootEdge = Hex(0x17121d), Warm = Rgb(255, 176, 96);
        private static readonly (double Width, double Height, int Heat)[] BackLayers =
            { (1, 1, 7), (0.76, 0.86, 5), (0.54, 0.68, 3), (0.3, 0.42, 2) };
        private static readonly (double Width, double Height, int Heat)[] EmberLayers =
            { (1, 1, 7), (0.7, 0.8, 6), (0.4, 0.55, 5) };

        private struct Site
        {
            internal double X, Y, Start, Life, Height, Width;
            internal int Seed;
            internal bool Back;
        }

        private readonly PixelPainter painter;
        private readonly Site[] sites;
        private readonly uint[] heat;
        private readonly uint glow;
        private readonly int scorchSeed;
        private readonly bool full;
        private double time;

        private FireRingEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            full = timeline.Quality == PixelFxQuality.Full;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            heat = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            Array.Reverse(heat);
            glow = Mix(Mix(heat[3], heat[4], 0.5), White, 0.25);
            scorchSeed = unchecked(settings.seed * 7);
            random = new PixelFxRandom(settings.seed);
            sites = MakeSites();
            initialRandom = random;
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new FireRingEffect(timeline);
            return (pixels, _, t, _, pass) => effect.Render(pixels, t, pass);
        }

        private Site[] MakeSites()
        {
            var result = new List<Site>();
            for (int r = 10; r <= Reach; r += 9)
            {
                double ry = r * Squash;
                int count = Max(5, Round(Tau * Sqrt((r * r + ry * ry) / 2) / 8.5));
                for (int i = 0; i < count; i++)
                {
                    double angle = (i + random.Range(0.15, 0.85)) / count * Tau;
                    double radius = r + random.Range(-3, 3), q = Min(1, radius / Reach), depth = Sin(angle);
                    result.Add(new Site
                    {
                        X = Cos(angle) * radius, Y = depth * radius * Squash, Back = depth < 0,
                        Start = ReachedAt(radius) + random.Range(0, 0.04), Life = (0.3 + q * q) * random.Range(0.85, 1.2),
                        Height = random.Range(9, 15) * (0.6 + 0.55 * q), Width = random.Range(1.8, 2.7) * (0.75 + 0.3 * q),
                        Seed = (int)Floor(random.Range(0, 100001)),
                    });
                }
            }
            result.Sort((a, b) => a.Y.CompareTo(b.Y));
            return result.ToArray();
        }

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            Advance(sample);
            painter.Clear();
            double radius = Reach * OutCubic(Clamp((time - Charge) / Spread));
            if (pass != PixelFxRenderPass.Front)
            {
                DrawScorch(radius);
                DrawRing(radius, true);
                DrawFlames(true);
                if (full) DrawParticles(true);
                double k = time < Charge ? 0 : 1 - Smooth(Charge + Spread, Charge + Spread + 1.4, time);
                if (k > 0)
                    for (int i = 0; i < 16; i++)
                    {
                        double a = i / 16.0 * Tau;
                        painter.Glow(Cos(a) * radius, Sin(a) * radius * Squash - 3, 16, glow, 0.2 * k);
                    }
            }
            if (pass != PixelFxRenderPass.Back)
            {
                DrawRing(radius, false);
                DrawFlames(false);
                if (full) DrawParticles(false);
                DrawFlash();
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawScorch(double radius)
        {
            double fade = 1 - Smooth(End - 1.2, End - 0.15, time);
            if (radius < 1 || fade <= 0) return;
            for (int y = (int)Floor(-radius * Squash); y <= Ceiling(radius * Squash); y++)
            for (int x = (int)Floor(-radius); x <= Ceiling(radius); x++)
            {
                double depth = (y + 0.5) / Squash;
                double distance = Sqrt((x + 0.5) * (x + 0.5) + depth * depth);
                if (distance > radius) continue;
                double age = time - ReachedAt(distance), variation = PixelFxRandom.At(x, y, scorchSeed);
                double hot = 1 - age / (1.2 + variation * 0.9);
                if (hot > 0 && variation < 0.03 + 0.06 * hot)
                {
                    painter.Dot(x, y, heat[hot > 0.7 ? 3 : hot > 0.45 ? 4 : hot > 0.2 ? 5 : 6]);
                    painter.Light(x, y, heat[4], 0.3 * hot);
                }
                else
                    painter.BlendPixel(x, y, variation < 0.5 ? Soot : SootEdge, Min(1, age * 3) * (0.55 - distance / Reach * 0.25) * fade);
            }
        }

        private void DrawRing(double radius, bool back)
        {
            double k = (time - Charge) / (Spread + 0.1);
            if (k < 0 || k >= 1) return;
            radius += 1.5;
            double thickness = Max(1.5, 6 * (1 - k)), alpha = k < 0.75 ? 1 : (1 - k) / 0.25;
            int count = (int)Ceiling(radius * Tau * 1.2);
            for (int i = 0; i < count; i++)
            {
                double a = i / (double)count * Tau, x = Cos(a), depth = Sin(a);
                if ((depth < 0) != back) continue;
                for (double offset = thickness; offset >= 0; offset -= 0.5)
                {
                    double u = offset / thickness, r = radius - offset;
                    uint ink = u < 0.15 ? (k < 0.12 ? White : heat[1]) : heat[u < 0.4 ? 2 : u < 0.7 ? 4 : 6];
                    painter.Dot(x * r, depth * r * Squash, ink, alpha * (u < 0.4 ? 1.2 : 1.15 - u));
                }
            }
        }

        private void DrawFlames(bool back)
        {
            foreach (var site in sites)
            {
                double age = time - site.Start;
                if (site.Back != back || age < 0 || age > site.Life) continue;
                double u = age / site.Life, entry = Clamp(age / 0.08);
                double rise = entry <= 0 ? 0 : entry >= 1 ? 1 : 1 + 3 * Pow(entry - 1, 3) + 2 * Pow(entry - 1, 2);
                double fade = 1 - Pow(Max(0, (u - 0.1) / 0.9), 1.3);
                double height = site.Height * rise * fade * (0.85 + 0.15 * Noise(time * 3.3, site.Seed))
                    * (1 + 0.5 * Max(0, Noise(time * 2.6, site.Seed + 70) - 0.55));
                if (height < 1) continue;
                var layers = u < 0.3 && !back ? HotLayers : u < 0.65 ? BackLayers : EmberLayers;
                double width = site.Width * (0.6 + 0.4 * fade), sweep = Noise(time * 2.2, site.Seed + 40) * 1.5 - site.X * 0.02;
                Tongue(painter, site.X, site.Y, height, width, sweep, time, site.Seed, heat, layers);
            }
        }

        private void DrawFlash()
        {
            if (time < Charge) painter.Glow(0, -3, 8 + 22 * (time / Charge), glow, 0.6 * (time / Charge));
            if (time >= Charge && time < Charge + 0.1)
            {
                double k = 1 - (time - Charge) / 0.1, radius = 0.6 + 0.4 * k;
                FlashEllipse(14 * radius, 4, heat[2]);
                FlashEllipse(9 * radius, 2.6, k > 0.5 ? White : heat[1]);
            }
            double fade = Smooth(Charge - 0.05, Charge, time) * (1 - Smooth(Charge + Spread * 0.5, Charge + Spread + 1.2, time));
            if (fade > 0) painter.Glow(0, -6, 92, glow, 0.3 * fade);
            BurstLight(-18, 64, Warm, 0.85, warmLife, 0.5);
            BurstLight(-6, 30, White, 0.8, whiteLife, 0.08);
        }

        private void FlashEllipse(double rx, double ry, uint ink)
        {
            painter.Shade(-rx, -1 - ry, rx, -1 + ry, (rx, ry, ink), static (x, y, s) =>
            {
                double dx = x / s.rx, dy = (y + 1) / s.ry;
                return dx * dx + dy * dy <= 1 ? (s.ink, 1.0) : default;
            });
        }

        private void BurstLight(double y, double radius, uint ink, double strength, double life, double duration)
        {
            if (life <= 0) return;
            double k = life / duration;
            painter.Glow(0, y, radius * (0.55 + 0.45 * k), ink, strength * k * k);
        }

        private static double ReachedAt(double distance) => Charge + Spread * (1 - Pow(1 - Min(1, distance / Reach), 1.0 / 3));
        private static double OutCubic(double k) => 1 - Pow(1 - k, 3);
        private static double Clamp(double k) => Max(0, Min(1, k));
        private static int Round(double x) => (int)Floor(x + 0.5);
        private static double Smooth(double a, double b, double x)
        {
            double k = Clamp((x - a) / (b - a));
            return k * k * (3 - 2 * k);
        }

    }
}
