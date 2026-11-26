using System;
using static System.Math;
using static PixelFx.PixelPainter;
using static PixelFx.FireDrawing;

namespace PixelFx
{
    // Patch PixelFX 5294518. Fire recipes adapt Sprite Fusion fire,
    // MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed partial class FireTornadoEffect
    {
        internal const int Width = 120, Height = 152, OriginX = 60, OriginY = 136;
        internal const int Duration = 2;
        private const double FunnelHeight = 80, Tau = 2 * PI;
        private readonly PixelPainter painter;
        private readonly bool full;
        private readonly int seed;
        private readonly uint[] heat;
        private readonly uint glow, smokeInk;

        private FireTornadoEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            full = timeline.Quality == PixelFxQuality.Full;
            seed = settings.seed;
            heat = Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            Array.Reverse(heat);
            glow = Mix(Mix(heat[3], heat[4], 0.5), White, 0.25);
            smokeInk = Mix(Hex(0x5b5566), heat[6], 0.25);
            if (full) InitializeParticles();
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var draw = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => draw(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new FireTornadoEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            double time = sample >= 0 && sample < Duration ? sample : Wrap(sample);
            double phase = time / Duration;
            painter.Clear();
            if (pass != PixelFxRenderPass.Front)
            {
                if (full) DrawSmoke(time);
                DrawFloorLight();
                if (full) DrawEmbers(time, false);
                DrawRing(time, phase, false);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                double flicker = 0.85 + 0.15 * NoiseAt(phase, 7, seed + 1);
                DrawFunnel(phase, flicker);
                if (full) DrawShedFlames(time);
                DrawRing(time, phase, true);
                if (full) DrawEmbers(time, true);
                painter.Glow(0, -FunnelHeight * 0.5, 80, glow, 0.28 * flicker);
                painter.Glow(0, -6, 18, heat[2], 0.3 * flicker);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawFloorLight()
        {
            for (int y = -8; y <= 8; y++)
            for (int x = -40; x < 40; x++)
            {
                double dx = (x + 0.5) / 40, dy = y / 8.0, distance = dx * dx + dy * dy;
                if (distance >= 1) continue;
                double k = 1 - Sqrt(distance);
                painter.Light(x, y, glow, k * k * 0.4);
            }
        }

        private void DrawRing(double time, double phase, bool front)
        {
            for (int i = 0; i < 12; i++)
            {
                double a = i / 12.0 * Tau + phase * Tau * 2, z = Sin(a);
                if ((z >= 0) != front) continue;
                int stream = unchecked((seed * 3 + i) * 19);
                double radius = 12 + 2 * NoiseAt(phase, 2, stream);
                double height = (5 + 5 * (0.5 + 0.5 * NoiseAt(phase, 5, stream + 1))) * (front ? 1 : 0.8);
                Tongue(painter, Cos(a) * radius, z * radius * 0.25, height, 1.6,
                    -Cos(a + PI / 2) * 3, time, stream, heat, front ? HotLayers : BackLayers, Duration);
            }
        }

        private void DrawFunnel(double phase, double flicker)
        {
            for (int y = 0; y >= -FunnelHeight * 1.2; y--)
            {
                double v = -y / FunnelHeight, vv = Min(1, v), center = Center(vv, phase);
                double radius = Radius(vv) * (1 + 0.08 * NoiseAt(phase, 3, seed + 2, y * 0.1));
                double spin = phase * Tau * 3 + v * 5.5;
                double cut = Max(0, Min(1, (v - 0.72) / 0.42));
                double lickL = 0.3 * Max(0, PeriodicNoise(-y * 0.22 + phase * 18, 18, seed + 3));
                double lickR = 0.3 * Max(0, PeriodicNoise(-y * 0.22 + phase * 18 + 9, 18, seed + 4));

                for (int x = (int)Floor(center - radius * 1.35); x <= Ceiling(center + radius * 1.35); x++)
                {
                    double dx = (x + 0.5 - center) / radius, abs = Abs(dx);
                    if (abs > 1 + (dx < 0 ? lickL : lickR)) continue;
                    double angle = Asin(Max(-1, Min(1, dx))), depth = Cos(angle);
                    double tear = 0.18 * PeriodicNoise(y * 0.45 + dx * 2 - phase * 30, 30, seed + 6);
                    double band = 0.5 + 0.5 * Sin((angle + spin + tear) * 3);
                    if (cut > 0 && band < 0.35 + cut * 0.6 + 0.1 * PeriodicNoise(x * 0.7 + phase * 20, 20, seed + 5)) continue;
                    double value = 0.22 + 0.36 * depth + 0.5 * (band * band * band) - 0.22 * vv + 0.1 * (1 - vv) - 0.25 * cut;
                    if (abs > 1) value = 0.3 - (abs - 1) * 1.5;
                    value *= flicker;
                    int color = value > 0.95 ? 0 : value > 0.82 ? 1 : value > 0.7 ? 2 : value > 0.58 ? 3
                        : value > 0.46 ? 4 : value > 0.34 ? 5 : value > 0.2 ? 6 : 7;
                    painter.Dot(x, y, heat[color]);
                }
                if (y % 10 == 0 && v <= 1) painter.Glow(center, y, radius * 2 + 8, glow, 0.22 * flicker);
            }
        }

        private static double Wrap(double time) => (time % Duration + Duration) % Duration;
        private static double Center(double v, double phase) =>
            3.2 * Sin(v * 4.2 - phase * Tau * 2) * v + 2 * Sin(phase * Tau) * v;
        private static double Radius(double v) => 3.5 + 15 * Pow(v, 1.35);
        private static double NoiseAt(double phase, int rate, int stream, double offset = 0) =>
            PeriodicNoise(phase * (Duration * rate) + offset, Duration * rate, stream);
    }
}
