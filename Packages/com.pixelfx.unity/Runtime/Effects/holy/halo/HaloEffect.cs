using System;
using UnityEngine;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed class HaloEffect
    {
        internal const int Width = 56, Height = 80, OriginX = 28, OriginY = 70;
        internal const float Duration = 2;
        private const double Tau = 2 * PI, RingY = -46, RadiusX = 9.5, RadiusY = 2.6;
        private readonly PixelFxTimeline timeline;
        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly (double Born, double Life, double Angle, double Distance, double Lift)[] stars;
        private readonly (double Born, double Life, double Angle, double Width, double Fall)[] dust;

        private HaloEffect(PixelFxTimeline timeline)
        {
            this.timeline = timeline;
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            if (timeline.Quality != PixelFxQuality.Full) return;

            int starSeed = unchecked(settings.seed + 3);
            stars = new (double, double, double, double, double)[6];
            for (int i = 0; i < stars.Length; i++)
            {
                var timing = new PixelFxRandom(starSeed, i, 911);
                var random = new PixelFxRandom(starSeed, i);
                stars[i] = (timing.Range(0, 1) * Duration, timing.Range(0.3, 0.5),
                    random.Range(0, Tau), random.Range(1.1, 1.5), random.Range(1, 4));
            }
            dust = new (double, double, double, double, double)[16];
            for (int i = 0; i < dust.Length; i++)
            {
                var timing = new PixelFxRandom(settings.seed, i, 911);
                var random = new PixelFxRandom(settings.seed, i);
                dust[i] = (timing.Range(0, 1) * Duration, timing.Range(0.8, 1.5),
                    random.Range(0, Tau), random.Range(0.8, 1), random.Range(7, 14));
            }
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new HaloEffect(timeline);
            return (pixels, _, time, _) => effect.Render(pixels, time);
        }

        private void Render(byte[] pixels, double time)
        {
            time %= Duration;
            double phase = time / Duration * Tau;
            double y = RingY - (Sin(phase) > 0.55 ? 1 : 0);
            double glint = PI / 2 + phase;
            painter.Clear();
            if (timeline.ActorMask != null && timeline.ActorMask.TryProject(timeline.ActorToSurface, out var projection))
                DrawHeadLight(projection);
            DrawRing(y, phase, glint);
            if (stars != null) DrawParticles(y, time);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawHeadLight(PixelFxActorMask.Projection projection)
        {
            for (int x = -OriginX; x < Width - OriginX; x++)
            for (int y = -OriginY; y < Height - OriginY; y++)
            {
                var c = projection.Sample(x + 0.5, y + 0.5, rowLimit: 9);
                if (!CatchesLight(c)) continue;
                painter.Dot(x, y, Mix(Rgb(c.r, c.g, c.b), colors[6], 0.45));
                var below = projection.Sample(x + 0.5, y + 1.5);
                if (CatchesLight(below))
                    painter.Dot(x, y + 1, Mix(Rgb(below.r, below.g, below.b), colors[5], 0.18));
                for (int d = 0; d <= 4; d++)
                    painter.Light(x, y + d, colors[4], 0.3 * (1 - d / 5.0));
                break;
            }
        }

        private static bool CatchesLight(Color32 c) =>
            c.a != 0 && (0.299 * c.r + 0.587 * c.g + 0.114 * c.b) / 255 >= 0.15;

        private void DrawRing(double centerY, double phase, double glint)
        {
            const int samples = 92;
            for (int i = 0; i < samples; i++)
            {
                double angle = i / (double)samples * Tau, depth = Sin(angle);
                double x = Cos(angle) * RadiusX, y = centerY + depth * RadiusY;
                bool shimmer = Sin(angle * 3 - phase * 2) > 0.7;
                bool hot = Abs(((angle - glint + PI) % Tau + Tau) % Tau - PI) < 0.3;
                if (depth >= 0)
                {
                    painter.Dot(x, y, hot || shimmer ? White : colors[6]);
                    painter.Dot(x, y + 1, hot ? colors[6] : colors[4]);
                }
                else painter.Dot(x, y, hot ? White : colors[shimmer ? 6 : 5], 0.9);
            }

            painter.Glow(0, centerY, 20, colors[4], 0.4);
            painter.Glow(0, centerY + 1, 9, White, 0.16);
            double gDepth = Sin(glint), gx = Cos(glint) * RadiusX, gy = centerY + gDepth * RadiusY;
            if (gDepth > -0.2) DrawSparkle(gx, gy + 0.5, 1.5 + 2.5 * Max(0, gDepth), 0.45);
            else painter.Glow(gx, gy, 5, colors[6], 0.3);
            painter.GlowLine(0, centerY + 2, 0, centerY + 9, 9, colors[4], 0.16);
        }

        private static double Age(double time, double born) => ((time - born) % Duration + Duration) % Duration;

        private void DrawParticles(double centerY, double time)
        {
            foreach (var (born, life, angle, distance, lift) in stars)
            {
                double age = Age(time, born);
                if (age >= life) continue;
                double pulse = Sin(age / life * PI);
                DrawSparkle(Cos(angle) * RadiusX * distance, centerY + Sin(angle) * RadiusY * distance - lift,
                    1 + 2 * pulse, 0.4 * pulse);
            }
            foreach (var (born, life, angle, width, fall) in dust)
            {
                double age = Age(time, born);
                if (age >= life) continue;
                double u = age / life;
                double x = Cos(angle) * RadiusX * width + Sin(u * 5 + angle) * 1.2;
                double y = centerY + Sin(angle) * RadiusY + 1 + u * fall;
                double twinkle = 0.6 + 0.4 * Sin(age * 18 + angle * 7);
                double pulse = Sin(u * PI) * twinkle;
                painter.Dot(x, y, twinkle > 0.85 ? White : colors[6], Min(1, pulse * 1.5));
                painter.Light(x, y, colors[5], 0.5 * pulse);
            }
        }

        private void DrawSparkle(double x, double y, double arm, double light)
        {
            int count = (int)Floor(arm + 0.5);
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= count; d++)
            {
                uint ink = d > count * 0.6 ? colors[6] : White;
                painter.Dot(px + d, py, ink);
                painter.Dot(px - d, py, ink);
                painter.Dot(px, py + d, ink);
                painter.Dot(px, py - d, ink);
            }
            if (count >= 3)
            {
                painter.Dot(px + 1, py + 1, White);
                painter.Dot(px + 1, py - 1, White);
                painter.Dot(px - 1, py + 1, White);
                painter.Dot(px - 1, py - 1, White);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + count * 2, White, light);
        }
    }
}
