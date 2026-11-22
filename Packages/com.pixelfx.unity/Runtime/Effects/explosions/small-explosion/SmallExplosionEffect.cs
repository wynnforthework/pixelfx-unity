using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Patch PixelFX 5294518 and Sprite Fusion explosion, MIT License.
    // Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed class SmallExplosionEffect
    {
        internal const int Width = 300, Height = 240, OriginX = 150, OriginY = 196;
        internal const float Duration = 5.4f;
        private const double BaseRadius = 18;
        private static readonly uint Soot = Hex(0x0d0a12), SootEdge = Hex(0x17121d), Warm = Rgb(255, 176, 96);

        internal readonly struct Stone
        {
            internal readonly uint Body, Light, Shade, Dust;

            internal Stone(uint body)
            {
                Body = body;
                Light = Mix(Scale(body, 1.35), White, 0.08);
                Shade = Scale(body, 0.64);
                Dust = Mix(Scale(body, 1.2), Rgb(150, 140, 150), 0.3);
            }
        }

        private readonly PixelPainter painter;
        private readonly uint[] colors, shockColors;
        private readonly SmallExplosionParticles particles;
        private readonly double radius, centerY, size, power, flashStrength;

        internal SmallExplosionEffect(PixelFxSettings settings, bool full, PixelPainter painter, double size = 1, double strength = 1)
        {
            this.painter = painter;
            this.size = size;
            radius = BaseRadius * size;
            centerY = -radius * 0.3;
            power = 0.9 * Pow(size, 0.8);
            flashStrength = Sqrt(strength);
            colors = Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            var stone = new Stone(Hex(0x4a3f69));
            shockColors = new[] { stone.Body, stone.Light, colors[3], colors[6] };
            if (full)
                particles = new SmallExplosionParticles(settings.seed, colors, stone, radius, size, power);
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = timeline.Settings.glow };
            var effect = new SmallExplosionEffect(timeline.Settings, timeline.Quality == PixelFxQuality.Full, painter);
            return (pixels, _, time, _, pass) =>
            {
                painter.Clear();
                effect.Draw(time, pass);
                painter.FadeLightEdges();
                painter.Composite(pixels, true, false);
            };
        }

        internal void Draw(double time, PixelFxRenderPass pass)
        {
            particles?.Advance(time);
            if (pass != PixelFxRenderPass.Front)
            {
                Scorch(time);
                particles?.DrawBack(painter);
                ShockFront(time, back: true);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                ShockFront(time, back: false);
                particles?.DrawFront(painter);
                OpeningFlash(time);
            }
        }

        private void Scorch(double time)
        {
            double fadeTime = Clamp(time - 4.2);
            double fade = 1 - fadeTime * fadeTime * (3 - 2 * fadeTime);
            if (time <= 0 || fade <= 0) return;
            double rx = 22 * size * Min(1, time / 0.08);
            double cool = Pow(Clamp(1 - time / (1.2 + radius / 14)), 1.4);
            for (int y = 0; y <= 2; y++)
            for (int x = (int)Floor(-rx); x <= (int)Ceiling(rx); x++)
            {
                double dx = (x + 0.5) / rx, dy = (y + 0.5) / 4;
                double distance = Sqrt(dx * dx + dy * dy);
                if (distance > 1) continue;
                double noise = PixelFxRandom.At(x * 31 + y, 3);
                double heat = cool * (1.25 - distance) - noise * 0.35;
                if (heat > 0.06)
                {
                    uint ink = colors[7 - (int)Floor((1 - Min(1, heat)) * 7)];
                    painter.Dot(x, y, ink, fade);
                    if ((x & 3) == 0) painter.Glow(x + 0.5, y, 6, ink, 0.18 * heat * fade);
                }
                else
                    painter.Dot(x, y, distance < 0.5 ? Soot : SootEdge,
                        (distance < 0.95 - noise * 0.3 ? 1 : 1 - distance) * fade);
            }
        }

        private void ShockFront(double time, bool back)
        {
            double k = time / 0.5;
            if (k >= 1) return;
            double spread = 80 * size * (1 - Pow(1 - k, 3));
            double thickness = Max(1, (2 + radius / 9) * (1 - k)), alpha = 1.15 - k;
            int samples = (int)Ceiling(spread * (2 * PI) * 1.2);
            for (int i = 0; i < samples; i++)
            {
                double angle = i / (double)samples * (2 * PI), y = Sin(angle), x = Cos(angle);
                if ((y < 0) != back) continue;
                for (double offset = thickness; offset >= 0; offset -= 0.5)
                {
                    double u = offset / thickness, r = spread - offset;
                    painter.Dot(x * r, 0.5 + y * r * 0.16, shockColors[u < 0.15 ? 3 : u < 0.4 ? 2 : u < 0.7 ? 1 : 0],
                        alpha * (u < 0.4 ? 1.2 : 1.15 - u));
                }
            }
        }

        private void OpeningFlash(double time)
        {
            if (time < 0.06)
            {
                double life = 1 - time / 0.06, flashRadius = radius * 1.05 * (0.8 + 0.2 * life);
                painter.Circle(0, centerY, flashRadius + 1.2, colors[2]);
                painter.Circle(0, centerY, flashRadius, colors[life > 0.45 ? 6 : 5]);
                painter.Circle(0, centerY, flashRadius * 0.6, life > 0.45 ? White : colors[6]);
            }
            Ring(time, 0.34, 0.45, 2.7, 3, colors[5]);
            Ring(time, 0.22, 0.3, 1.6, 1.8, colors[3]);
            Light(time, 0.7, radius * 3.4, Warm, 0.6 * power * flashStrength);
            Light(time, 0.08, radius * 1.6, White, 0.6 * flashStrength);
        }

        private void Ring(double time, double duration, double from, double to, double thickness, uint ink)
        {
            if (time >= duration) return;
            double k = 1 - time / duration;
            double outer = radius * from + (radius * to - radius * from) * (1 - Pow(k, 3));
            double inner = Max(0, outer - Max(1, thickness * k + 0.4));
            painter.Shade(-outer, centerY - outer, outer, centerY + outer,
                (Inner: inner * inner, Outer: outer * outer, Y: centerY, Ink: ink, Alpha: Min(1, k * 1.6)),
                static (x, y, s) =>
                {
                    double dy = y - s.Y, d = x * x + dy * dy;
                    return d >= s.Inner && d <= s.Outer ? (s.Ink, s.Alpha) : default;
                });
        }

        private void Light(double time, double duration, double radius, uint ink, double intensity)
        {
            if (time >= duration) return;
            double k = 1 - time / duration;
            painter.Glow(0, centerY, radius * (0.55 + 0.45 * k), ink, intensity * k * k);
        }

        private static double Clamp(double value) => Max(0, Min(1, value));
    }
}
