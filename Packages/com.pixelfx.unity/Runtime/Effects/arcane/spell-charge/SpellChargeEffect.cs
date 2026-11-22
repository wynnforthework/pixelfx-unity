using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX at 5294518. Charge and arcs derive from Sprite Fusion
    // energy charge, MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed class SpellChargeEffect
    {
        internal const int Width = 120, Height = 86, OriginX = 44, OriginY = 80;
        internal const float Duration = 1.6f;
        internal const int HandX = 15, HandY = -22;
        private const double Period = 1.6, RingLife = 0.5, Tau = 2 * PI;
        private static readonly (double Squash, double Tilt)[] RingShapes = { (1, 0), (0.38, 0.55), (1, 0), (0.42, -0.7) };
        private readonly uint[] colors;
        private readonly int seed;
        private readonly (double X, double Y)[] bolt = new (double, double)[33];
        private readonly (double X, double Y)[] fork = new (double, double)[9];

        internal SpellChargeEffect(PixelFxSettings settings)
        {
            seed = settings.seed;
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new SpellChargeEffect(timeline.Settings);
            var painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = timeline.Settings.glow };
            bool full = timeline.Quality == PixelFxQuality.Full;
            return (pixels, _, time, _) =>
            {
                painter.Clear();
                effect.Draw(painter, time, full);
                painter.FadeLightEdges();
                painter.Composite(pixels, true, false);
            };
        }

        internal void Draw(PixelPainter p, double time, bool full, PixelFxRenderPass pass = PixelFxRenderPass.Combined)
        {
            double t = time % Period, phase = t / Period;
            int arcFrame = (int)Floor(t * 30);
            double since = Period;
            for (int ring = 0; ring < RingShapes.Length; ring++)
                since = Min(since, Wrap(t - ring * Period / RingShapes.Length - RingLife));
            double swell = Exp(-Square(since / 0.07)) + 0.5 * Exp(-Square(since / 0.18));
            double pulse = Sin(phase * Tau * 4);
            double radius = 6.1 + 0.5 * pulse + 1.4 * swell;
            double x = HandX + (swell <= 0.4 ? 0 : (arcFrame & 1) != 0 ? 1 : -1);

            if (full && pass != PixelFxRenderPass.Back)
            {
                DrawSpiral(p, t, radius, streaks: true);
                DrawSpiral(p, t, radius, streaks: false);
            }
            for (int ring = 0; ring < RingShapes.Length; ring++)
            {
                for (int cycle = -1; cycle <= 0; cycle++)
                {
                    double age = t - ring * Period / RingShapes.Length - cycle * Period;
                    if (age >= 0 && age < RingLife)
                        DrawRing(p, x, radius, RingShapes[ring], age / RingLife, pass);
                }
            }
            if (pass == PixelFxRenderPass.Back) return;
            DrawOrb(p, x, radius, phase, (int)Floor(t * 15), full);
            if (full)
            {
                DrawArcs(p, x, radius, arcFrame, swell);
                DrawGlints(p, t, x, radius);
            }
            if (since < 0.07)
                Sparkle(p, x, HandY, 4 + 6 * (1 - since / 0.07), colors[6], colors[5], 0.5);

            double light = 0.85 + 0.15 * pulse + 0.2 * swell;
            p.Glow(x, HandY, 46, colors[4], 0.26 * light);
            p.GlowRing(x, HandY, radius + 3, 8, colors[5], 0.3 * light);
            p.Glow(x, HandY, radius, colors[6], 0.12);
            p.Glow(HandX, -1, 22, colors[5], 0.16 * light);
        }

        private void DrawSpiral(PixelPainter p, double t, double radius, bool streaks)
        {
            int stream = unchecked(seed * 3 + (streaks ? 1 : 2));
            uint[] palette = colors;
            for (int i = 0; i < (streaks ? 110 : 56); i++)
            {
                var timing = new PixelFxRandom(stream, i, 911);
                double age = Wrap(t - timing.Range(0, 1) * Period);
                double life = streaks ? timing.Range(0.2, 0.34) : timing.Range(0.4, 0.7);
                if (age >= life) continue;
                var random = new PixelFxRandom(stream, i);
                double angle = streaks ? random.Range(2.8, 5.75) : random.Range(2.6, 5.2);
                double distance = streaks ? random.Range(20, 52) : random.Range(12, 32);
                double spin = streaks ? random.Range(0.6, 1.3) : random.Range(1, 2.2);
                double q = age / life;
                var (x, y) = SpiralPoint(q, radius, angle, distance, spin, streaks);
                var (nextX, nextY) = SpiralPoint(Min(1, q + 0.02), radius, angle, distance, spin, streaks);
                double vx = (nextX - x) / (0.02 * life), vy = (nextY - y) / (0.02 * life);
                double speed = Sqrt(vx * vx + vy * vy);
                double ux = vx / (speed == 0 ? 1 : speed), uy = vy / (speed == 0 ? 1 : speed);
                if (streaks)
                {
                    uint ink = random.Range(0, 1) < 0.55 ? palette[6] : palette[5];
                    double alpha = Min(1, q * 4) * (1 - Smooth(0.9, 1, q));
                    double length = Max(1, Min(7, speed / 30));
                    for (double d = length; d >= 0; d -= 0.5)
                        p.Dot(x - ux * d, y - uy * d, d < 1 ? White : d < length * 0.5 ? ink : Scale(ink, 0.6),
                            (d < length * 0.5 ? 1 : 1.2 - d / length) * alpha);
                    p.Glow(x, y, 4, ink, 0.4 * (1 - Smooth(0.6, 0.9, q)) * alpha);
                }
                else
                {
                    double size = random.Range(0.6, 1);
                    int tone = (int)random.Range(0, 4);
                    uint ink = tone == 0 ? palette[5] : tone == 3 ? White : palette[6];
                    double length = Min(16, 0.5 + speed * 0.026) * size;
                    double opacity = Min(1, q * 3) * (q > 0.9 ? (1 - q) / 0.1 : 1);
                    double alpha = Min(1, opacity / 0.35);
                    if (length > 1.2)
                        p.Line(x - ux * length, y - uy * length, x - ux * length * 0.35, y - uy * length * 0.35, Scale(ink, 0.55), alpha);
                    p.Line(x - ux * length * 0.4, y - uy * length * 0.4, x, y, ink, alpha);
                    p.Glow(x, y, 2.5 + size, ink, 0.3 * (1 - Smooth(0.55, 0.85, q)) * opacity);
                }
            }
        }

        private static (double X, double Y) SpiralPoint(double q, double radius, double angle, double distance, double spin, bool streaks)
        {
            double a = angle + spin * (streaks ? q * q : q);
            double d = distance * (1 - q * q) + radius;
            return (HandX + Cos(a) * d, HandY + Sin(a) * d * (streaks ? 0.92 : 1));
        }

        private void DrawRing(PixelPainter p, double x, double radius, (double Squash, double Tilt) shape, double q,
            PixelFxRenderPass pass)
        {
            radius += 1.5 + 22 * (1 - q * q);
            int samples = (int)Ceiling(radius * Tau * 1.4);
            double c = Cos(shape.Tilt), s = Sin(shape.Tilt), alpha = Min(1, q * 3);
            uint ink = q > 0.85 ? White : q > 0.55 ? colors[6] : colors[5];
            for (int j = 0; j < samples; j++)
            {
                double angle = j / (double)samples * Tau;
                double dx = Cos(angle) * radius, dy = Sin(angle) * radius * shape.Squash;
                double px = x + dx * c - dy * s, py = HandY + dx * s + dy * c;
                if (py > -0.5) continue;
                if (pass == PixelFxRenderPass.Back && py >= HandY || pass == PixelFxRenderPass.Front && py < HandY) continue;
                p.Dot(px, py, ink, alpha);
                if (j % 10 == 0) p.Glow(px, py, 5, colors[5], 0.12 * alpha);
            }
        }

        private void DrawOrb(PixelPainter p, double x, double radius, double phase, int flickerFrame, bool full)
        {
            for (int i = 0; i < 8; i++)
            {
                double angle = i / 8.0 * Tau + phase * (Tau / 8) * 3;
                double length = (1.5 + 4.5 * Abs(LoopNoise(phase, i * 7 + 1))) * ((i & 1) == 0 ? 1 : 0.7);
                double dx = Cos(angle), dy = Sin(angle);
                double px = x + dx * (radius + 1.2), py = HandY + dy * (radius + 1.2);
                p.Line(px, py, px + dx * length, py + dy * length, (Inner: colors[6], Outer: colors[(i & 1) == 0 ? 5 : 4]),
                    static (u, color) => (u < 0.4 ? color.Inner : color.Outer, u < 0.6 ? 1 : 1.6 - u));
            }
            p.Circle(x, HandY, radius + 1.3, colors[4]);
            p.Circle(x, HandY, radius + 0.3, colors[5]);
            p.Circle(x - radius * 0.12, HandY - radius * 0.12, radius * 0.8, colors[6]);
            p.Circle(x, HandY, radius * 0.5, White);
            if (full)
            {
                var random = new PixelFxRandom(flickerFrame, 77);
                for (int i = 0; i < 3; i++)
                {
                    double angle = random.Range(0, Tau);
                    int segments = BuildBolt(ref random, bolt, x + Cos(angle) * radius * 0.4, HandY + Sin(angle) * radius * 0.4,
                        x + Cos(angle + random.Range(-0.6, 0.6)) * (radius + 0.6),
                        HandY + Sin(angle + random.Range(-0.6, 0.6)) * (radius + 0.6), 0.45);
                    for (int j = 0; j < segments; j++)
                        p.Line(bolt[j].X, bolt[j].Y, bolt[j + 1].X, bolt[j + 1].Y, colors[6],
                            static (u, ink) => (u < 0.6 ? White : ink, 1));
                }
            }
            p.Dot(x - radius * 0.5, HandY - radius * 0.62, White);
        }

        private void DrawArcs(PixelPainter p, double x, double radius, int frame, double swell)
        {
            var random = new PixelFxRandom(seed, frame / 2, 3);
            int count = (random.Range(0, 1) < 0.6 ? 1 : 0) + (swell > 0.3 ? 1 : 0);
            double freshness = (frame & 1) == 0 ? 0.8 : 0.4;
            for (int i = 0; i < count; i++)
            {
                double angle = random.Range(0, Tau), length = random.Range(5, 10) * (1 + swell * 0.4);
                double endAngle = angle + random.Range(-0.5, 0.5);
                double x0 = x + Cos(angle) * (radius + 0.5), y0 = HandY + Sin(angle) * (radius + 0.5);
                double x1 = x0 + Cos(endAngle) * length, y1 = Min(-2, y0 + Sin(endAngle) * length);
                bool canFork = random.Range(0, 1) < 0.5;
                int segments = BuildBolt(ref random, bolt, x0, y0, x1, y1, 0.35);
                int forkSegments = 0;
                if (canFork && segments > 2 && random.Range(0, 1) <= 0.7)
                {
                    int j = 1 + (int)random.Range(0, segments - 1);
                    var (px, py) = bolt[j];
                    double heading = Atan2(py - bolt[j - 1].Y, px - bolt[j - 1].X);
                    heading += (random.Range(0, 1) < 0.5 ? -1 : 1) * (0.4 + random.Range(0, 1) * 0.6);
                    double dx = x1 - x0, dy = y1 - y0;
                    double reach = Sqrt(dx * dx + dy * dy) * 0.5 * (0.4 + 0.6 * random.Range(0, 1)) * (1 - j / (segments + 1.0));
                    if (reach > 1.5)
                        forkSegments = BuildBolt(ref random, fork, px, py, px + Cos(heading) * reach,
                            py + Sin(heading) * reach, 0.35 * 1.1, Max(2, segments / 4));
                }
                for (int pass = freshness > 0.45 ? 0 : 1; pass < 3; pass++)
                {
                    DrawArcPath(p, bolt, segments, true, freshness, pass);
                    if (pass > 0) DrawArcPath(p, fork, forkSegments, false, freshness, pass);
                }
            }
        }

        private static int BuildBolt(ref PixelFxRandom random, (double X, double Y)[] points, double x0, double y0,
            double x1, double y1, double jag, int segments = 0)
        {
            if (segments == 0)
            {
                double dx = x1 - x0, dy = y1 - y0;
                int depth = (int)Max(1, Min(5, Floor(Log(Sqrt(dx * dx + dy * dy) / 1.5, 2) + 0.5)));
                segments = 1 << depth;
            }
            points[0] = (x0, y0);
            points[segments] = (x1, y1);
            for (int step = segments; step > 1; step /= 2)
            {
                for (int i = 0; i < segments; i += step)
                {
                    var a = points[i];
                    var b = points[i + step];
                    double dx = b.X - a.X, dy = b.Y - a.Y;
                    double length = Sqrt(dx * dx + dy * dy);
                    double offset = random.Range(-1, 1) * jag * length, divisor = length == 0 ? 1 : length;
                    points[i + step / 2] = ((a.X + b.X) / 2 + (-dy / divisor) * offset,
                        (a.Y + b.Y) / 2 + (dx / divisor) * offset);
                }
            }
            return segments;
        }

        private void DrawArcPath(PixelPainter p, (double X, double Y)[] path, int segments, bool main, double freshness, int pass)
        {
            for (int i = 0; i < segments; i++)
            {
                var a = path[i];
                double dx = path[i + 1].X - a.X, dy = path[i + 1].Y - a.Y;
                double length = Max(1, Sqrt(dx * dx + dy * dy));
                for (double d = 0; d <= length; d += 0.5)
                {
                    double x = a.X + dx * d / length, y = a.Y + dy * d / length;
                    switch (pass)
                    {
                        case 0: Cross(p, x, y, 1.5, colors[3], (freshness - 0.45) * 1.6); break;
                        case 1:
                            p.Dot(x + 1, y, colors[5]);
                            p.Dot(x, y + 1, colors[5]);
                            break;
                        default:
                            p.Dot(x, y, main ? White : colors[5]);
                            if (d == 0 && i % 3 == 0)
                                p.Glow(x, y, (main ? 9 : 5) * (0.5 + freshness * 0.5), colors[5], 0.3 * freshness * (main ? 1 : 0.55));
                            break;
                    }
                }
            }
        }

        private void DrawGlints(PixelPainter p, double t, double x, double radius)
        {
            int stream = unchecked(seed * 3 + 4);
            for (int i = 0; i < 7; i++)
            {
                var timing = new PixelFxRandom(stream, i, 911);
                double age = Wrap(t - timing.Range(0, 1) * Period);
                if (age >= 0.22) continue;
                var random = new PixelFxRandom(stream, i);
                double angle = random.Range(0, Tau), distance = random.Range(radius + 5, radius + 15);
                double k = Sin(age / 0.22 * PI);
                Sparkle(p, x + Cos(angle) * distance, HandY + Sin(angle) * distance * 0.9, 1 + 2.5 * k, colors[6], colors[4], 0.4 * k);
            }
        }

        private static void Cross(PixelPainter p, double x, double y, double d, uint ink, double alpha)
        {
            p.Dot(x + d, y, ink, alpha);
            p.Dot(x - d, y, ink, alpha);
            p.Dot(x, y + d, ink, alpha);
            p.Dot(x, y - d, ink, alpha);
        }

        private static void Sparkle(PixelPainter p, double x, double y, double arm, uint core, uint tip, double light)
        {
            int n = (int)Floor(arm + 0.5);
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= n; d++) Cross(p, px, py, d, d > n * 0.6 ? tip : core, 1);
            for (int d = 1; d <= n / 3 && d <= 2; d++)
            {
                uint ink = d == 1 ? core : tip;
                p.Dot(px - d, py - d, ink);
                p.Dot(px + d, py - d, ink);
                p.Dot(px - d, py + d, ink);
                p.Dot(px + d, py + d, ink);
            }
            p.Dot(px, py, White);
            p.Glow(x, y, 4 + n * 2, core, light);
        }

        private static double LoopNoise(double phase, int seed)
        {
            double x = Cos(phase * Tau) * 3 + 50, y = Sin(phase * Tau) * 3 + 50;
            int ix = (int)Floor(x), iy = (int)Floor(y);
            double ux = Smooth(0, 1, x - ix), uy = Smooth(0, 1, y - iy);
            double a = Lerp(PixelFxRandom.At(ix, iy, seed), PixelFxRandom.At(ix + 1, iy, seed), ux);
            double b = Lerp(PixelFxRandom.At(ix, iy + 1, seed), PixelFxRandom.At(ix + 1, iy + 1, seed), ux);
            return Lerp(a, b, uy) * 2 - 1;
        }

        private static double Wrap(double t) => ((t % Period) + Period) % Period;
        private static double Square(double value) => value * value;
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static double Smooth(double a, double b, double value)
        {
            double t = Max(0, Min(1, (value - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }
    }
}
