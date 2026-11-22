using System;
using UnityEngine;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed class BlessingEffect
    {
        internal const int Width = 80, Height = 128, OriginX = 40, OriginY = 108;
        internal const float Duration = 3;
        private const double Top = -108, Tau = PI * 2;
        private static readonly (double Offset, double Width, int Frequency, double Phase)[] Rays =
        {
            (-0.62, 0.16, 1, 0.3), (-0.2, 0.2, 2, 1.7), (0.22, 0.14, 1, 4.1), (0.6, 0.18, 2, 2.6)
        };

        private readonly PixelFxTimeline timeline;
        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly (double Born, double Q, double Velocity, double Sway, double Phase, double Size)[] sparkles;
        private readonly (double Born, double Life, double Y, double Drift, double Q)[] motes;
        private readonly double[] landings;
        private int maskVersion = -1;
        private Matrix4x4 maskTransform;

        private BlessingEffect(PixelFxTimeline timeline)
        {
            this.timeline = timeline;
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            if (timeline.Quality != PixelFxQuality.Full) return;

            if (timeline.ActorMask != null)
            {
                sparkles = new (double, double, double, double, double, double)[20];
                landings = new double[sparkles.Length];
                for (int i = 0; i < sparkles.Length; i++)
                {
                    var timing = new PixelFxRandom(settings.seed, i, 911);
                    var random = new PixelFxRandom(settings.seed, i);
                    sparkles[i] = (timing.Range(0, 1) * Duration, random.Range(-0.9, 0.9), random.Range(22, 34),
                        random.Range(1, 3), random.Range(0, Tau), random.Range(0.8, 2.4));
                }
            }

            motes = new (double, double, double, double, double)[16];
            int seed = unchecked(settings.seed + 9);
            for (int i = 0; i < motes.Length; i++)
            {
                var timing = new PixelFxRandom(seed, i, 911);
                var random = new PixelFxRandom(seed, i);
                motes[i] = (timing.Range(0, 1) * Duration, timing.Range(1.2, 2.4),
                    random.Range(0, 90), random.Range(6, 14), random.Range(-1, 1));
            }
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new BlessingEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Render(byte[] pixels, double time, PixelFxRenderPass pass)
        {
            time %= Duration;
            double phase = time / Duration * Tau;
            painter.Clear();
            if (pass != PixelFxRenderPass.Front) DrawCone(phase);
            if (pass != PixelFxRenderPass.Back)
            {
                var mask = timeline.ActorMask;
                PixelFxActorMask.Projection projection = default;
                bool hasActor = mask != null && mask.TryProject(timeline.ActorToSurface, out projection);
                if (hasActor) DrawActorHighlight(projection);
                DrawFloor();
                if (hasActor && sparkles != null)
                {
                    UpdateLandings(mask, projection);
                    DrawFallingSparkles(time);
                }
                if (motes != null) DrawMotes(time, phase);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private static double HalfWidth(double y) => 5 + 15 * ((y - Top) / -Top);

        private static double Smooth(double start, double end, double value)
        {
            double t = Min(1, Max(0, (value - start) / (end - start)));
            return t * t * (3 - 2 * t);
        }

        private static double Ray(double q, double phase)
        {
            double intensity = 0.3 * Max(0, 1 - q * q);
            foreach (var (offset, width, frequency, shift) in Rays)
            {
                double d = (q - offset) / width;
                intensity += Exp(-(d * d)) * (0.55 + 0.45 * Sin(phase * frequency + shift)) * 0.6;
            }
            return intensity * Smooth(1.25, 0.75, Abs(q));
        }

        private void DrawCone(double phase)
        {
            for (int y = (int)Top; y <= 1; y++)
            {
                double halfWidth = HalfWidth(y + 0.5);
                double depth = 0.6 + 0.4 * Smooth(Top, 0, y + 0.5);
                for (int x = -24; x <= 24; x++)
                {
                    double intensity = Ray((x + 0.5) / halfWidth, phase) * depth;
                    if (intensity < 0.02) continue;
                    painter.BlendPixel(x, y, colors[6], intensity * 0.13);
                    painter.Light(x, y, colors[4], intensity * 0.22);
                }
            }
            for (int i = 1; i <= 2; i++)
            {
                var (offset, _, frequency, shift) = Rays[i];
                double variation = 0.5 + 0.5 * Sin(phase * frequency + shift);
                for (int y = (int)Top; y < -1; y++)
                {
                    double pulse = 0.5 + 0.5 * Sin(y * 0.11 - phase * 3 + shift);
                    painter.Dot(offset * HalfWidth(y), y, pulse > 0.85 ? White : colors[6],
                        (0.1 + 0.45 * pulse) * variation * Smooth(Top, Top + 20, y));
                }
            }
            painter.Glow(0, -4, 30, colors[4], 0.22);
        }

        private void DrawActorHighlight(PixelFxActorMask.Projection projection)
        {
            for (int x = -OriginX; x < Width - OriginX; x++)
            for (int y = -OriginY; y < Height - OriginY; y++)
            {
                var c = projection.Sample(x + 0.5, y + 0.5);
                if (c.a == 0 || (0.299 * c.r + 0.587 * c.g + 0.114 * c.b) / 255 < 0.15) continue;
                painter.Dot(x, y, Mix(Rgb(c.r, c.g, c.b), colors[6], 0.35));
                break;
            }
            painter.Glow(0, -30, 20, colors[5], 0.22);
        }

        private void DrawFloor()
        {
            for (int y = 0; y <= 3; y++)
            for (int x = -24; x <= 24; x++)
            {
                double dx = x / 24.0, dy = y / 4.0;
                double distance = Sqrt(dx * dx + dy * dy);
                if (distance >= 1) continue;
                uint ink = distance < 0.3 && y == 0 ? White : distance < 0.6 ? colors[5] :
                    distance < 0.85 ? colors[4] : colors[3];
                painter.Dot(x, y, ink, y == 0 ? Sqrt(1 - distance) : (1 - distance) * 0.7);
            }
        }

        private void UpdateLandings(PixelFxActorMask mask, PixelFxActorMask.Projection projection)
        {
            var transform = timeline.ActorToSurface;
            if (maskVersion == mask.Version && maskTransform.Equals(transform)) return;
            maskVersion = mask.Version;
            maskTransform = transform;
            for (int i = 0; i < sparkles.Length; i++)
            {
                landings[i] = double.PositiveInfinity;
                for (double age = 0; age < Duration; age += 0.02)
                {
                    var (x, y) = SparklePosition(i, age);
                    if (y < -1 && projection.Sample(Floor(x) + 0.5, Floor(y) + 0.5).a == 0) continue;
                    landings[i] = age;
                    break;
                }
            }
        }

        private (double X, double Y) SparklePosition(int i, double age)
        {
            var (_, q, velocity, sway, phase, _) = sparkles[i];
            double y = Top + 6 + velocity * age;
            return (q * HalfWidth(y) + Sin(age * 2.2 + phase) * sway, y);
        }

        private static double Age(double time, double born) => ((time - born) % Duration + Duration) % Duration;

        private void DrawFallingSparkles(double time)
        {
            for (int i = 0; i < sparkles.Length; i++)
            {
                var (born, _, _, _, phase, size) = sparkles[i];
                double age = Age(time, born), fade = Min(1, age / 0.3);
                if (age > landings[i])
                {
                    double w = (age - landings[i]) / 0.14;
                    if (w < 1)
                    {
                        var (lx, ly) = SparklePosition(i, landings[i]);
                        DrawSparkle(lx, ly, 3 * (1 - w) + 1, White, colors[6], 0.5 * (1 - w));
                    }
                    continue;
                }
                var (x, y) = SparklePosition(i, age);
                double twinkle = 0.6 + 0.4 * Sin(age * (7 + i % 4) + phase);
                if (size > 1.2)
                    DrawSparkle(x, y, size * twinkle * fade, twinkle > 0.85 ? White : colors[6], colors[5], 0.35 * fade);
                else
                {
                    painter.Dot(x, y, twinkle > 0.8 ? White : colors[6], fade);
                    painter.Glow(x, y, 3, colors[5], 0.35 * fade);
                }
            }
        }

        private void DrawMotes(double time, double phase)
        {
            foreach (var (born, life, startY, drift, q) in motes)
            {
                double age = Age(time, born);
                if (age >= life) continue;
                double u = age / life, y = Top + 10 + startY + u * drift;
                double x = q * HalfWidth(y) + Sin(age * 1.7 + q * 5) * 1.5;
                double intensity = Sin(u * PI) * Ray(q, phase);
                if (intensity > 0.12)
                    painter.Dot(x, y, intensity > 0.45 ? White : colors[6], Min(1, intensity * 2.2));
            }
        }

        private void DrawSparkle(double x, double y, double arm, uint core, uint tip, double light)
        {
            int count = (int)Floor(arm + 0.5);
            if (count < 1) return;
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= count; d++)
            {
                uint ink = d > count * 0.6 ? tip : core;
                painter.Dot(px + d, py, ink);
                painter.Dot(px - d, py, ink);
                painter.Dot(px, py + d, ink);
                painter.Dot(px, py - d, ink);
            }
            if (count >= 3)
            {
                painter.Dot(px + 1, py + 1, core);
                painter.Dot(px + 1, py - 1, core);
                painter.Dot(px - 1, py + 1, core);
                painter.Dot(px - 1, py - 1, core);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + count * 2, core, light);
        }
    }
}
