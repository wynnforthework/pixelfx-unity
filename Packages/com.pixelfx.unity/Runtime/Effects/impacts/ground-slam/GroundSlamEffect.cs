using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX impacts/ground-slam and its Sprite Fusion-derived recipes.
    internal sealed class GroundSlamEffect
    {
        internal const int Width = 288, Height = 208, OriginX = 144, OriginY = 164;
        internal const float Duration = 3.2f;
        internal const double Wave = 0.6;
        private readonly PixelPainter painter = new PixelPainter(Width, Height, OriginX, OriginY);
        private readonly uint[] colors;
        private readonly GroundSlamCracks cracks;
        private readonly GroundSlamDebris debris;
        private readonly (double Angle, double Length, double Width)[] rays;
        private readonly bool full;
        private readonly Stone stone = new Stone(Hex(0x4a3f69));

        internal readonly struct Stone
        {
            internal readonly uint Light, Body, Shade, Dust, Lip;
            internal Stone(uint color)
            {
                Body = color;
                Light = Mix(Scale(color, 1.35), White, 0.08);
                Shade = Scale(color, 0.64);
                Dust = Mix(Scale(color, 1.2), Rgb(150, 140, 150), 0.3);
                Lip = Mix(Scale(color, 1.55), White, 0.12);
            }
        }

        private GroundSlamEffect(PixelFxSettings settings, bool full)
        {
            this.full = full;
            painter.LightStrength = settings.glow;
            colors = Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            var rng = new PixelFxRandom(settings.seed);
            cracks = new GroundSlamCracks(ref rng, colors, stone.Light);
            rays = new (double, double, double)[]
            {
                (-0.07, rng.Range(34, 40), 1.9), (PI + 0.07, rng.Range(34, 40), 1.9),
                (-0.42 + rng.Range(-0.08, 0.08), rng.Range(15, 20), 1.5),
                (-PI + 0.42 + rng.Range(-0.08, 0.08), rng.Range(15, 20), 1.5),
                (-PI / 2 + rng.Range(-0.15, 0.15), rng.Range(18, 24), 1.7),
                (-1.05 + rng.Range(-0.1, 0.1), rng.Range(10, 14), 1.2),
                (-PI + 1.05 + rng.Range(-0.1, 0.1), rng.Range(10, 14), 1.2)
            };
            if (!full) return;
            cracks.SeedEmbers(ref rng);
            debris = new GroundSlamDebris(rng, stone, colors);
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var renderer = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => renderer(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new GroundSlamEffect(timeline.Settings, timeline.Quality == PixelFxQuality.Full);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Render(byte[] pixels, double time, PixelFxRenderPass pass)
        {
            debris?.Advance(time);
            // The upstream simulation uses its accumulated fixed-step clock for contact and dust.
            if (debris != null) time = debris.Time;
            double heat = Pow(Clamp(1 - time / 0.8, 0, 1), 1.4);
            painter.Clear();
            if (pass != PixelFxRenderPass.Front)
            {
                double fade = Clamp((time - (3.2 - 0.9)) / 0.75, 0, 1);
                fade = 1 - fade * fade * (3 - 2 * fade);
                if (time > 0.02) DrawCrater(heat, fade);
                cracks.Draw(painter, time, heat, fade, full);
                DrawRing(time, true);
                debris?.DrawBack(painter);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                DrawRing(time, false);
                DrawFronts(time);
                debris?.DrawFront(painter);
                DrawFlash(time, heat);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawCrater(double heat, double fade)
        {
            for (int x = -12; x <= 11; x++)
            {
                double offset = Abs(x + 0.5) / 12;
                int depth = (int)Floor(2.6 * (1 - offset * offset) + 0.5);
                if (offset < 0.84)
                {
                    for (int y = 0; y < depth; y++) painter.Dot(x, y, GroundSlamCracks.Soot, fade);
                    painter.Dot(x, depth, heat > 0.5 ? colors[5] : heat > 0.2 ? colors[4] : stone.Lip, fade);
                }
                else
                {
                    painter.Dot(x, -1, offset > 0.93 ? stone.Light : stone.Lip, fade);
                    if (offset < 0.9) painter.Dot(x, -2, stone.Light, fade * 0.8);
                }
            }
        }

        internal static double OutCubic(double u) => 1 - Pow(1 - u, 3);
        internal static double Front(double time) => 7 + 97 * OutCubic(Clamp((time - 0.015) / Wave, 0, 1));
        internal static double CrestHeight(double u) => 6 + 28 * Pow(1 - u, 0.75);

        private void DrawRing(double time, bool back)
        {
            double u = Clamp((time - 0.015) / (Wave + 0.1), 0, 1);
            if (time < 0.015 || u >= 0.9) return;
            double radius = Front(time) + 1, thickness = Max(1, 4 * Pow(1 - u, 0.8));
            double opacity = u < 0.55 ? 1.1 : (0.9 - u) / 0.35;
            var ramp = u < 0.35 ? (colors[3], colors[4], colors[5], colors[6])
                : (stone.Body, stone.Light, colors[4], colors[5]);
            int count = (int)Ceiling(radius * (2 * PI) * 1.2);
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * (2 * PI);
                double sin = Sin(angle);
                if ((sin < 0) != back) continue;
                double cos = Cos(angle);
                for (double offset = thickness; offset >= 0; offset -= 0.5)
                {
                    double depth = offset / thickness;
                    uint ink = depth < 0.15 ? (time < 0.09 ? White : ramp.Item4)
                        : depth < 0.4 ? ramp.Item3 : depth < 0.7 ? ramp.Item2 : ramp.Item1;
                    painter.Dot(cos * (radius - offset), 0.5 + sin * (radius - offset) * 0.16,
                        ink, opacity * (depth < 0.4 ? 1.2 : 1.15 - depth));
                }
            }
        }

        private void DrawFronts(double time)
        {
            double u = Clamp((time - 0.03) / (Wave + 0.16), 0, 1);
            if (time < 0.03 || u >= 1) return;
            double height = CrestHeight(u), width = 1.5 + 5 * Pow(1 - u, 0.9);
            double radiusX = Max(2, height * 0.55), radiusY = height * 0.8;
            double opacity = u < 0.7 ? 1 : (1 - u) / 0.3;
            var ramp = u < 0.14 ? (White, colors[6], colors[5]) : u < 0.34 ? (colors[6], colors[5], colors[4])
                : u < 0.55 ? (colors[5], colors[4], stone.Lip) : (stone.Lip, stone.Light, stone.Dust);
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * Front(time);
                double left = side > 0 ? x - radiusX - width - 1 : x - 1;
                double right = side > 0 ? x + 1 : x + radiusX + width + 1;
                painter.Shade(left, -height - 1, right, 0, (x, side, height, width, radiusX, radiusY, opacity, ramp),
                    static (px, py, s) =>
                    {
                        double ey = (py + s.height * 0.2) / s.radiusY;
                        if (Abs(ey) >= 1) return default;
                        double shape = 1 - ey * ey;
                        double edge = s.radiusX * Sqrt(shape) - s.radiusX;
                        double q = (edge - (px - s.x) * s.side) / Max(0.5, s.width * Pow(shape, 0.45));
                        if (q < 0 || q > 1) return default;
                        return (q < 0.28 ? s.ramp.Item1 : q < 0.62 ? s.ramp.Item2 : s.ramp.Item3,
                            s.opacity * (ey < -0.8 ? 0.7 : 1) * (q > 0.7 ? 1.25 - q * 0.8 : 1));
                    });
                painter.Glow(x, -height * 0.3, 8 + height * 0.7, colors[4], 0.5 * (1 - u) * opacity);
            }
        }

        private void DrawFlash(double time, double heat)
        {
            if (time < 0.035)
            {
                painter.Shade(-30, -4.5, 30, 4.5, colors, static (x, y, ramp) =>
                {
                    double dx = x / 30, dy = y / 4.5;
                    double d = Sqrt(dx * dx + dy * dy);
                    return d > 1 ? default : (d > 0.8 ? ramp[4] : d > 0.5 ? ramp[6] : White, 1);
                });
                ImpactShapes.DrawStar(painter, rays, -1, 1, 1, colors[4], colors[6], White, White, 2.4);
            }
            else if (time < 0.07)
                ImpactShapes.DrawStar(painter, rays, -1, 1.25, 0.5, colors[3], colors[5], colors[6], White, 2.4);

            if (time < 0.14) painter.GlowLine(-80, 0, 80, 0, 10, colors[5], 0.55 * (1 - time / 0.14));
            if (heat > 0) painter.Glow(0, 1, 46, colors[4], 0.35 * heat);
            DrawLight(-12, 96, colors[4], 0.6, 0.6, time);
            DrawLight(-4, 50, White, 0.9, 0.14, time);
        }

        private void DrawLight(double y, double radius, uint color, double intensity, double life, double time)
        {
            if (time >= life) return;
            double remaining = (life - time) / life;
            painter.Glow(0, y, radius * (0.55 + 0.45 * remaining), color, intensity * remaining * remaining);
        }
    }
}
