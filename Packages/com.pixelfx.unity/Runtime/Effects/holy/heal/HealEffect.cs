using System;
using System.Collections.Generic;
using UnityEngine;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed partial class HealEffect
    {
        internal const int Width = 80, Height = 104, OriginX = 40, OriginY = 92;
        internal const float Duration = 1.9f;
        private const int PetalCount = 8;
        private const double Tau = 2 * PI, Squash = 0.3, Radius = 16;
        private const double LiftStart = 0.16, LiftEnd = 0.74, LiftTop = -43;
        private const double Step = 1.0 / 120;
        private readonly PixelFxTimeline timeline;
        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly (double Angle, double Start, double Width)[] petals;
        private readonly Rider[] riders;
        private readonly List<Star> stars;
        private readonly PixelFxRandom particleSeed;
        private PixelFxRandom random;
        private double time, burstAge = double.PositiveInfinity;
        private int tick, lastRow = 1;

        private HealEffect(PixelFxTimeline timeline)
        {
            this.timeline = timeline;
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            random = new PixelFxRandom(settings.seed);
            petals = new (double, double, double)[PetalCount];
            for (int i = 0; i < petals.Length; i++)
                petals[i] = (i / (double)PetalCount * Tau + 0.2, 0.02 + (i * 3 % PetalCount) * 0.035, random.Range(4.2, 5.2));

            if (timeline.Quality == PixelFxQuality.Full)
            {
                riders = CreateRiders();
                if (timeline.ActorMask != null) stars = new List<Star>(44);
            }
            particleSeed = random;
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new HealEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            var mask = timeline.ActorMask;
            PixelFxActorMask.Projection projection = default;
            bool hasActor = pass != PixelFxRenderPass.Back && mask != null
                && mask.TryProject(timeline.ActorToSurface, out projection);
            Advance(sample, hasActor, projection);
            painter.Clear();

            if (pass != PixelFxRenderPass.Front)
            {
                double pool = Smooth(0, 0.2, time) * (1 - Smooth(1.3, 1.75, time));
                painter.Glow(0, -2, 30, colors[4], 0.3 * pool);
                DrawPetals(true);
                DrawHoop(true);
                if (riders != null) DrawRiders(true);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                DrawRestoration(hasActor, projection);
                DrawPetals(false);
                DrawHoop(false);
                if (time >= LiftEnd && time < LiftEnd + 0.25)
                    Sparkle(0, LiftTop, 8 * (1 - (time - LiftEnd) / 0.25), White, colors[5], 0.6);
                if (riders != null) DrawRiders(false);
                if (stars != null) DrawStars();
                DrawBurstGlow(26, colors[5], 0.55, 0.3);
                DrawBurstGlow(10, White, 0.45, 0.08);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private static double Progress(double start, double end, double t) => Max(0, Min(1, (t - start) / (end - start)));
        private static double Smooth(double start, double end, double t)
        {
            double u = Progress(start, end, t);
            return u * u * (3 - 2 * u);
        }

        private static double HoopY(double t) => LiftTop * (1 - Cos(PI * Progress(LiftStart, LiftEnd, t))) / 2;

        private void DrawPetals(bool back)
        {
            for (int i = 0; i < petals.Length; i++)
            {
                var (angle, start, width) = petals[i];
                double g = angle + time * 0.4, sin = Sin(g);
                if ((sin < 0) != back) continue;
                double open = Progress(start, start + 0.3, time) - 1;
                double close = Progress(1.25 + i * 0.02, 1.6 + i * 0.02, time);
                double bloom = (1 + 2.8 * Pow(open, 3) + 1.8 * Pow(open, 2))
                    * (1 - (2.4 * close * close * close - 1.4 * close * close));
                if (bloom <= 0.02) continue;

                double cos = Cos(g), reach = 6 + 9 * Min(1.2, bloom);
                double bx = cos * 6, by = sin * 6 * Squash;
                double tx = cos * reach, ty = sin * reach * Squash - (i % 2 == 0 ? 9 : 7) * bloom;
                double dx = tx - bx, dy = ty - by;
                double length = Sqrt(dx * dx + dy * dy);
                if (length < 1.5) continue;
                double ux = dx / length, uy = dy / length;
                double w = width * Min(1, bloom) * (0.35 + 0.65 * Abs(sin));
                double pad = w + 1;
                for (int y = (int)Floor(Min(by, ty) - pad); y <= Ceiling(Max(by, ty) + pad); y++)
                for (int x = (int)Floor(Min(bx, tx) - pad); x <= Ceiling(Max(bx, tx) + pad); x++)
                {
                    dx = x + 0.5 - bx;
                    dy = y + 0.5 - by;
                    double u = (dx * ux + dy * uy) / length;
                    if (u < 0 || u > 1) continue;
                    double halfWidth = w * Pow(Sin(PI * Pow(u, 0.6)), 0.7) + 0.2;
                    double v = Abs(-dx * uy + dy * ux) / halfWidth;
                    if (v > 1) continue;
                    if (u > 0.9) painter.Dot(x, y, White);
                    else if (v > 0.7) painter.Dot(x, y, colors[u < 0.3 ? 3 : u < 0.7 ? 5 : 6]);
                    else if (v < 0.2 && u > 0.15 && u < 0.85)
                        painter.Dot(x, y, colors[u < 0.55 ? 5 : 6], back ? 0.75 : 0.85);
                    else painter.BlendPixel(x, y, colors[u < 0.45 ? 3 : 4], back ? 0.6 : 0.45);
                }
                painter.Glow(tx, ty, 5, colors[5], 0.35 * Min(1, bloom));
            }
        }

        private void DrawHoop(bool back)
        {
            if (time < LiftStart || time > LiftEnd + 0.12) return;
            double y = HoopY(time), radius = Radius - 4 * Progress(LiftStart, LiftEnd, time);
            if (time > LiftEnd) radius *= 1 - (time - LiftEnd) / 0.12;
            DrawHoopArc(y, radius, radius * Squash, back, false);
            if (back) return;
            DrawHoopArc(y + 1, radius - 0.5, radius * Squash, false, true);
            for (int i = -2; i <= 2; i++) painter.Glow(i * radius * 0.45, y, 8, colors[5], 0.32);
        }

        private void DrawHoopArc(double y, double rx, double ry, bool back, bool lowerRim)
        {
            if (rx < 0.5) return;
            int samples = (int)Ceiling(Max(rx, ry) * Tau * 1.5) + 6;
            for (int i = 0; i < samples; i++)
            {
                double g = i / (double)samples * Tau, sin = Sin(g);
                if ((sin < 0) != back) continue;
                painter.Dot(Cos(g) * rx, y + sin * ry, lowerRim ? colors[4] : sin > 0.5 ? White : colors[6],
                    back ? 0.8 : 1);
            }
        }

        private void DrawRestoration(bool hasActor, PixelFxActorMask.Projection projection)
        {
            if (time >= LiftEnd + 0.35) return;
            double line = time < LiftStart ? 1 : HoopY(time);
            bool done = time >= LiftEnd;
            if (hasActor)
            {
                double drain = 0.68 * Smooth(0, 0.08, time);
                double afterglow = 0.45 * (1 - Progress(LiftEnd, LiftEnd + 0.35, time));
                for (int y = -OriginY; y < Height - OriginY; y++)
                for (int x = -OriginX; x < Width - OriginX; x++)
                {
                    Color32 source = projection.Sample(x + 0.5, y + 0.5);
                    if (source.a == 0) continue;
                    uint ink = Rgb(source.r, source.g, source.b);
                    double d = y - line;
                    if (!done && d < -0.5)
                    {
                        double luma = (0.299 * source.r + 0.587 * source.g + 0.114 * source.b) / 255 * 0.88;
                        painter.Dot(x, y, Mix(ink, Rgb(luma * 236, luma * 226, luma * 222), drain));
                    }
                    else if (!done && d < 2.5) painter.Dot(x, y, d < 1 ? White : colors[6]);
                    else
                    {
                        double warmth = done ? afterglow : 0.5 * Exp(-(d - 2.5) / 5);
                        if (warmth > 0.05) painter.Dot(x, y, Mix(ink, colors[5], warmth));
                    }
                }
            }
            if (time > LiftStart && !done) painter.Glow(0, line, 18, colors[5], 0.35);
        }

        private void DrawBurstGlow(double radius, uint color, double intensity, double life)
        {
            double k = 1 - burstAge / life;
            if (k > 0) painter.Glow(0, LiftTop, radius * (0.55 + 0.45 * k), color, intensity * k * k);
        }
    }
}
