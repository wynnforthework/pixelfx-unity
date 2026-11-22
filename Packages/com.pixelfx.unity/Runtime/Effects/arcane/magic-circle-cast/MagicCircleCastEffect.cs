using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Patch PixelFX 5294518. The light column adapts Sprite Fusion Holy Light,
    // MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed partial class MagicCircleCastEffect
    {
        internal const int Width = 200, Height = 220, OriginX = 100, OriginY = 192;
        internal const float Duration = 3.9f;
        private const double Tau = 2 * PI, Squash = 0.3, Flash = 1.52, Break = 2.42, Gone = 3.45;
        private const double Outer = 40, Second = 35, Inner = 24, RuneRadius = 30;
        private const int Top = 192, BeamWidth = 11, RuneCount = 8;
        private const double Step = ImpactParticles.Step;
        private static readonly Edge[] Edges = MakeEdges();
        private static readonly string[] Glyphs =
        {
            ".#.#.#.#..#.#.#", "#.##.#.#.#.##.#", "##.#.###.#..#..", "#..#.#####.#..#",
            ".###...#...###.", "###.#.###.#..#.", "#.#####.#..#..#", ".#..##.#.##..#.",
        };
        private readonly PixelPainter painter;
        private readonly uint[] colors, columnColors;
        private readonly (double Offset, double Width, double Frequency)[] strands = new (double, double, double)[9];
        private readonly bool full;
        private readonly int seed;
        private double time;

        private MagicCircleCastEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            seed = settings.seed;
            full = timeline.Quality == PixelFxQuality.Full;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, value => Hex(PixelFxLibrary.Pack(value)));
            columnColors = new[] { White, colors[6], colors[5], colors[4], colors[3] };
            var rng = new PixelFxRandom(seed);
            for (int i = 0; i < strands.Length; i++)
                strands[i] = (rng.Range(-0.85, 0.85), rng.Range(0.04, 0.1), rng.Range(1, 3));
            initialRandom = rng;
            ResetSimulation();
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new MagicCircleCastEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            Advance(sample);
            painter.Clear();
            double lvl = Level(time);
            if (pass != PixelFxRenderPass.Front)
            {
                if (time < Gone)
                {
                    double k = lvl * Smooth(0.04, 0.62, time);
                    painter.Glow(0, 0, 52, colors[4], 0.28 * k);
                    painter.Glow(-24, 1, 28, colors[5], 0.12 * k);
                    painter.Glow(24, 1, 28, colors[5], 0.12 * k);
                }
                DrawFloor(lvl, true);
                DrawColumn();
            }
            if (pass != PixelFxRenderPass.Back)
            {
                DrawFloor(lvl, false);
                DrawPens();
                DrawFlash();
                if (full) DrawParticles();
                DrawFlashLights();
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawFloor(double lvl, bool far)
        {
            Shock(0, 24, 80, 6, 0.5, far);
            Shock(0.08, 16, 56, 3, 0.4, far);
            if (time < Gone)
            {
                Ring(Outer, lvl, far);
                Ring(Second, lvl, far);
                Ring(Inner, lvl, far);
                for (int j = 0; j < 32; j++)
                {
                    double angle = (j + 0.5) * Tau / 32, y = Sin(angle) * Squash, x = Cos(angle);
                    double age = time - OuterAt(angle, 0.1, 0.48) - 0.03;
                    if ((y < 0) != far || age < 0) continue;
                    painter.Line(x * Second, y * Second, x * Outer, y * Outer, LineColor(lvl, age),
                        Clamp(lvl / 0.3) * (j % 2 == 0 ? 1 : 0.6));
                }
                foreach (var edge in Edges) DrawEdge(edge, lvl, far);
            }
            for (int k = 0; k < RuneCount; k++)
            {
                double angle = RuneAngle(k), depth = Sin(angle), fade = 1 - Clamp((time - RuneOut(k)) / 0.25);
                if ((depth < 0) != far || time < InnerAt(angle) || fade <= 0) continue;
                double x = Cos(angle) * RuneRadius, y0 = depth * RuneRadius * Squash;
                double lit = time - (0.9 + k * (1.3 - 0.9) / (RuneCount - 1));
                if (lit < 0)
                {
                    Glyph(k, x, y0 - 1, colors[3], 1);
                    continue;
                }
                double t = Clamp(lit / 0.2);
                double rise = 3 * (t >= 1 ? 1 : t <= 0 ? 0 : 1 + 3.4 * Pow(t - 1, 3) + 2.4 * Pow(t - 1, 2));
                double y = y0 - 1 - rise + (lit > 0.2 ? Sin(time * 5 + k) * 0.5 : 0);
                bool hot = lit < 0.07 || time > Flash && time < Flash + 0.07;
                Glyph(k, x, y + 1, hot ? colors[6] : colors[4], fade);
                Glyph(k, x, y, hot || lvl > 0.8 ? White : colors[6], fade);
                if (lit < 0.1) Sparkle(x, y - 2, 4 * (1 - lit / 0.1) + 1, colors[6], colors[5], 0.5);
                painter.Glow(x, y - 2, 8, colors[5], (0.3 + 0.3 * lvl) * fade);
                painter.Glow(x, y0, 6, colors[4], 0.2 * fade);
            }
        }

        private void Ring(double radius, double lvl, bool far)
        {
            double ry = radius * Squash;
            painter.Shade(-radius - 1, -ry - 1, radius + 1, ry + 1, (Effect: this, radius, ry, lvl, far),
                static (x, y, s) =>
                {
                    double nx = x / s.radius, ny = y / s.ry, distance = Sqrt(nx * nx + ny * ny);
                    if ((ny < 0) != s.far || distance < 1e-6) return default;
                    double gradient = Sqrt(Pow(nx / s.radius, 2) + Pow(ny / s.ry, 2)) / distance;
                    if (Abs(distance - 1) / gradient > 0.5) return default;
                    double angle = Atan2(ny, nx), t = s.Effect.time;
                    double start = s.radius == Inner ? InnerAt(angle) : s.radius == Outer
                        ? OuterAt(angle, 0.04, 0.42) : OuterAt(angle, 0.1, 0.48);
                    if (t < start) return default;
                    double run = (t - 0.9) * 5 + Pow(t - 0.9, 2) * 4;
                    bool chase = s.radius == Outer && t > 0.9 && t < Break
                        && (AngleDistance(angle - run) < 0.22 || AngleDistance(angle - run - PI) < 0.22);
                    return (chase ? White : s.Effect.LineColor(s.lvl, t - start), Clamp(s.lvl / 0.3));
                });
        }

        private void DrawEdge(Edge edge, double lvl, bool far)
        {
            double lo = 0, hi = Clamp((time - edge.Start) / EdgeDuration);
            bool startFar = edge.Y0 < 0;
            if (startFar != (edge.Y1 < 0))
            {
                double crossing = edge.Y0 / (edge.Y0 - edge.Y1);
                if (startFar == far) hi = Min(hi, crossing);
                else lo = crossing;
            }
            else if (startFar != far) return;
            if (hi <= lo) return;
            var a = edge.At(lo);
            var b = edge.At(hi);
            painter.Line(a.X, a.Y, b.X, b.Y, (Effect: this, lo, hi, edge.Start, lvl),
                static (q, s) => (s.Effect.LineColor(s.lvl, s.Effect.time - (s.Start + Lerp(s.lo, s.hi, q) * EdgeDuration)),
                    Clamp(s.lvl / 0.3)));
        }

        private void Glyph(int rune, double x, double y, uint ink, double alpha)
        {
            double left = Floor(x + 0.5) - 1, top = Floor(y + 0.5) - 4;
            for (int i = 0; i < 15; i++)
                if (Glyphs[rune][i] == '#')
                    painter.Dot(left + i % 3, top + i / 3, ink, alpha);
        }

        private void DrawPens()
        {
            if (time > 0.04 && time < 0.42)
            {
                double angle = (time - 0.04) / (0.42 - 0.04) * PI;
                Pen(Cos(PI / 2 - angle) * Outer, Sin(PI / 2 - angle) * Outer * Squash);
                Pen(Cos(PI / 2 + angle) * Outer, Sin(PI / 2 + angle) * Outer * Squash);
            }
            if (time > 0.3 && time < 0.6)
            {
                double angle = -PI / 2 + (time - 0.3) / (0.6 - 0.3) * Tau;
                Pen(Cos(angle) * Inner, Sin(angle) * Inner * Squash);
            }
            foreach (var edge in Edges)
            {
                if (time <= edge.Start || time >= edge.Start + EdgeDuration) continue;
                var point = edge.At((time - edge.Start) / EdgeDuration);
                Pen(point.X, point.Y);
            }
        }

        private void Pen(double x, double y)
        {
            Sparkle(x, y, 2, colors[6], colors[5], 0.5);
            painter.Glow(x, y, 7, colors[6], 0.45);
        }

        private double Level(double t)
        {
            if (t < 0.9) return 0.55;
            if (t < Flash) return 0.55 + 0.4 * Smooth(0.9, Flash, t);
            if (t < Flash + 0.07) return 1.2;
            return t < Break ? 0.92 : 0.92 * (1 - Smooth(Break, Gone, t));
        }

        private uint LineColor(double lvl, double age) => age < 0.05 || age >= 0.16 && lvl > 1 ? White
            : age < 0.16 || lvl > 0.8 ? colors[6] : lvl > 0.45 ? colors[5] : lvl > 0.28 ? colors[4] : colors[3];

        private static double OuterAt(double a, double start, double end) => Lerp(start, end, AngleDistance(a - PI / 2) / PI);
        private static double InnerAt(double a) => Lerp(0.3, 0.6, (((a + PI / 2) % Tau + Tau) % Tau) / Tau);
        private static double AngleDistance(double a) => Abs(Atan2(Sin(a), Cos(a)));
        private static double RuneAngle(int k) => -PI / 2 + (k + 0.5) * Tau / RuneCount;
        private static double RuneOut(int k) => Break + 0.2 + k * 0.08;
        private static double Clamp(double x) => Max(0, Min(1, x));
        private static double Lerp(double a, double b, double k) => a + (b - a) * k;
        private static double OutCubic(double x) => 1 - Pow(1 - x, 3);
        private static double Smooth(double a, double b, double x)
        {
            double k = Clamp((x - a) / (b - a));
            return k * k * (3 - 2 * k);
        }

        private const double EdgeDuration = (0.88 - 0.52) / 3;
        private readonly struct Edge
        {
            internal readonly double X0, Y0, X1, Y1, Start;
            internal Edge(int index, double angle)
            {
                double a = angle + index * Tau / 3, b = angle + ((index + 1) % 3) * Tau / 3;
                X0 = Cos(a) * Inner;
                Y0 = Sin(a) * Inner;
                X1 = Cos(b) * Inner;
                Y1 = Sin(b) * Inner;
                Start = 0.52 + index * (0.88 - 0.52) / 3;
            }
            internal (double X, double Y) At(double k) => (Lerp(X0, X1, k), Lerp(Y0, Y1, k) * Squash);
        }

        private static Edge[] MakeEdges()
        {
            var edges = new Edge[6];
            for (int i = 0; i < 3; i++)
            {
                edges[i * 2] = new Edge(i, -PI / 2);
                edges[i * 2 + 1] = new Edge(i, PI / 2);
            }
            return edges;
        }
    }
}
