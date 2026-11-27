using System;
using static System.Math;
using static PixelFx.PixelPainter;
using static PixelFx.FireDrawing;

namespace PixelFx
{
    // Patch PixelFX 5294518. Adapts Sprite Fusion explosion,
    // MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed partial class FireBreathEffect
    {
        internal const int Width = 160, Height = 116, OriginX = 12, OriginY = 72;
        internal const float Duration = 1.5f;
        private const double Spread = 0.24, Speed = 320;
        private static readonly (double Width, double Height, int Heat)[] CoolLayers =
            { (1, 1, 7), (0.72, 0.82, 6), (0.44, 0.58, 5) };
        private static readonly (double Length, double Width, double Angle)[] Jet =
            { (42, 4.2, 0), (32, 3.8, -0.08), (30, 3.8, 0.08), (20, 4.6, 0) };
        private readonly PixelPainter painter;
        private readonly uint[] heat;
        private readonly uint glow, smokeInk;
        private readonly int seed;
        private readonly bool full;

        private FireBreathEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            heat = Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            Array.Reverse(heat);
            glow = Mix(Mix(heat[3], heat[4], 0.5), White, 0.25);
            smokeInk = Mix(Hex(0x5b5566), heat[6], 0.3);
            seed = settings.seed;
            full = timeline.Quality == PixelFxQuality.Full;
            InitializeParticles();
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new FireBreathEffect(timeline);
            return (pixels, _, time, _) => effect.Render(pixels, time);
        }

        private void Render(byte[] pixels, double sample)
        {
            double time = sample >= 0 && sample < Duration ? sample : Wrap(sample);
            painter.Clear();
            DrawCone(time);
            DrawEdgeFlames(time);
            if (full) DrawStreaks(time);
            DrawJet(time);
            if (full)
            {
                DrawSparks(time);
                DrawEmbers(time);
            }
            double k = Roar(time);
            painter.Glow(3, 0, 34 * k, glow, 0.75 * k);
            painter.Glow(3, 0, 10, heat[0], 0.55);
            for (int d = 10; d < 126; d += 8)
                painter.Glow(d, -d * 0.1, 10 + d * 0.2, glow, 0.32 * k * (1 - d / 150.0));
            painter.Glow(64, -12, 96, glow, 0.16 * k);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawEdgeFlames(double time)
        {
            for (int i = 0; i < 6; i++)
            {
                int stream = unchecked((seed * 3 + i) * 19), side = i % 2 == 0 ? -1 : 1;
                double d = 18 + (i >> 1) * 16 + NoiseAt(time, 2, stream) * 4;
                double length = (9 + 5 * (0.5 + 0.5 * NoiseAt(time, 5, stream + 1))) * (1 - d / 120);
                double y = side * d * Spread * 0.85 - d * 0.06;
                Blade(painter, d, y, 0.75, side * 0.45 - 0.48, length, 1.6 + length * 0.08,
                    -side * 3, time, stream, heat, Duration);
            }
        }

        private void DrawJet(double time)
        {
            for (int i = 0; i < Jet.Length; i++)
            {
                var j = Jet[i];
                int stream = unchecked((seed * 5 + i) * 17);
                double length = j.Length * Roar(time) * (0.85 + 0.2 * NoiseAt(time, 5, stream));
                double angle = j.Angle + Sway(time);
                Blade(painter, 0, 0, Cos(angle), Sin(angle), length, j.Width, NoiseAt(time, 3, stream + 3) * 3,
                    time, stream, heat, Duration, wobble: 1.5);
            }
        }

        private static double Wrap(double time) => (time % Duration + Duration) % Duration;
        private static double NoiseAt(double time, double rate, int stream) => LoopNoise(time, rate, stream, Duration);
        private double Roar(double time) => 0.85 + 0.15 * NoiseAt(time, 3, seed + 1);
        private double Sway(double time) => NoiseAt(time, 1.3, seed + 2) * 0.05;
    }
}
