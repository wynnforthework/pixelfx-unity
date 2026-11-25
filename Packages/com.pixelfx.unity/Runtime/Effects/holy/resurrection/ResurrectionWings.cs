using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class ResurrectionEffect
    {
        private const double WingStart = 1.1, BeatStart = 1.95, BeatDrop = 2.25, BeatEnd = 2.37, BeatRecover = 2.8, Scatter = 2.95;
        private static readonly (double X, double Y)[] OpenArm = { (0, 0), (7, -9), (15, -19), (20, -27) };
        private static readonly (double X, double Y)[] FoldedArm = { (0, 0), (2, -2), (3, -3), (4, -3) };
        private static readonly (double Position, double Angle, double Length, double Width, int Kind)[] Design =
        {
            (0.14, 1.35, 11, 2.6, 1), (0.26, 1.12, 14, 2.8, 1), (0.38, 0.88, 17, 3, 1),
            (0.5, 0.62, 19, 3.1, 1), (0.62, 0.35, 21, 3.2, 1), (0.72, 0.08, 23, 3.3, 1),
            (0.8, -0.2, 26, 3.4, 0), (0.85, -0.45, 29, 3.5, 0), (0.9, -0.72, 31, 3.6, 0),
            (0.95, -1, 30, 3.6, 0), (1, -1.3, 26, 3.4, 0), (0.22, 1.2, 7, 2.2, 2),
            (0.34, 0.98, 8, 2.3, 2), (0.46, 0.75, 9, 2.4, 2), (0.58, 0.5, 10, 2.5, 2),
            (0.7, 0.22, 10, 2.5, 2), (0.81, -0.1, 11, 2.6, 2), (0.92, -0.5, 11, 2.6, 2),
        };
        private static readonly double[] Flex = { 1, 0.5, 0.3 };
        private static readonly int[] TrailQuills = { 6, 8, 10 };
        private readonly Quill[] quills = new Quill[Design.Length];
        private readonly int[] drawOrder = new int[Design.Length];

        private struct Quill
        {
            internal double Position, Angle, Length, Width, Fold, Open, RightOff, LeftOff;
            internal int Kind;
            internal double Off(int side) => side > 0 ? RightOff : LeftOff;
        }

        private struct WingFeather
        {
            internal double X, Y, Angle, Length;
            internal bool Hot;
            internal (double X, double Y) Point(double length) => (X + Cos(Angle) * length, Y + Sin(Angle) * length);
        }

        private void CreateQuills()
        {
            Span<double> reach = stackalloc double[Design.Length];
            double far = 0;
            for (int i = 0; i < reach.Length; i++)
            {
                var q = Design[i];
                var (x, y) = Along(OpenArm, q.Position);
                x += Cos(q.Angle) * q.Length;
                y += Sin(q.Angle) * q.Length;
                reach[i] = Sqrt(x * x + y * y);
                far = Max(far, reach[i]);
            }
            for (int i = 0; i < quills.Length; i++)
            {
                var q = Design[i];
                double off = Scatter + (1 - reach[i] / far) * 0.2;
                quills[i] = new Quill
                {
                    Position = q.Position, Angle = q.Angle, Length = q.Length, Width = q.Width, Kind = q.Kind,
                    Fold = 1.45 + i % 6 * 0.04,
                    Open = q.Kind == 0 ? 0.06 + (q.Position - 0.8) * 0.45 : q.Kind == 1 ? q.Position * 0.06 : 0.03 + q.Position * 0.05,
                    RightOff = off + random.Range(0, 0.04), LeftOff = off + random.Range(0, 0.04),
                };
                drawOrder[i] = i;
            }
            Array.Sort(drawOrder, (a, b) => quills[b].Angle.CompareTo(quills[a].Angle));
        }

        private static (double X, double Y) Along(ReadOnlySpan<(double X, double Y)> points, double s)
        {
            Span<double> lengths = stackalloc double[3];
            double total = 0;
            for (int i = 0; i < lengths.Length; i++)
            {
                double x = points[i + 1].X - points[i].X, y = points[i + 1].Y - points[i].Y;
                lengths[i] = Sqrt(x * x + y * y);
                total += lengths[i];
            }
            double distance = s * total;
            for (int i = 0; i < lengths.Length; i++)
            {
                double length = lengths[i];
                if (distance <= length || i == lengths.Length - 1)
                {
                    double u = length == 0 ? 0 : Clamp(distance / length);
                    return (Lerp(points[i].X, points[i + 1].X, u), Lerp(points[i].Y, points[i + 1].Y, u));
                }
                distance -= length;
            }
            return points[3];
        }

        private static double Beat(double at)
        {
            if (at < BeatStart) return 0;
            if (at < BeatDrop) return -0.34 * (-(Cos(PI * Seg(at, BeatStart, BeatDrop)) - 1) / 2);
            if (at < BeatEnd) return Lerp(-0.34, 0.66, Pow(Seg(at, BeatDrop, BeatEnd), 2));
            return 0.66 * (1 - OutBack(Seg(at, BeatEnd, BeatRecover), 1.3));
        }

        private static void Arm(double at, Span<(double X, double Y)> arm)
        {
            double u = OutBack(Seg(at, WingStart, WingStart + 0.4), 1.6);
            for (int i = 0; i < arm.Length; i++)
                arm[i] = (Lerp(FoldedArm[i].X, OpenArm[i].X, u), Lerp(FoldedArm[i].Y, OpenArm[i].Y, u));
        }

        private static (double X, double Y) Place((double X, double Y) point, int side, double c, double s) =>
            (side * (4 + point.X * c - point.Y * s), -20 + point.X * s + point.Y * c);

        private WingFeather WingAt(int i, double at, int side, ReadOnlySpan<(double X, double Y)> arm, double rotation, double omega)
        {
            var q = quills[i];
            double open = OutBack(Seg(at, WingStart + q.Open, WingStart + q.Open + 0.42), 2);
            var (x, y) = Place(Along(arm, q.Position), side, Cos(rotation), Sin(rotation));
            double angle = Lerp(q.Fold, q.Angle, open) + rotation - Max(-0.5, Min(0.5, omega * 0.045 * Flex[q.Kind]));
            return new WingFeather
            {
                X = x, Y = y, Angle = side > 0 ? angle : PI - angle, Length = q.Length * Lerp(0.3, 1, open),
                Hot = at < WingStart + q.Open + 0.07 || at >= q.Off(side),
            };
        }

        private WingFeather WingAt(int i, double at, int side)
        {
            Span<(double X, double Y)> arm = stackalloc (double, double)[4];
            Arm(at, arm);
            double rotation = Beat(at);
            return WingAt(i, at, side, arm, rotation, (rotation - Beat(at - 0.03)) / 0.03);
        }

        private void DrawWing(int side)
        {
            double alpha = Smooth(WingStart, WingStart + 0.1, time);
            if (alpha <= 0) return;
            Span<(double X, double Y)> arm = stackalloc (double, double)[4];
            Arm(time, arm);
            double rotation = Beat(time), omega = (rotation - Beat(time - 0.03)) / 0.03;
            int count = 0;
            foreach (int i in drawOrder)
                if (time < quills[i].Off(side) + 0.05) count++;
            if (count == 0) return;

            for (int layer = 0; layer < 2; layer++)
            foreach (int i in drawOrder)
            {
                var q = quills[i];
                if ((q.Kind == 2) != (layer == 1) || time >= q.Off(side) + 0.05) continue;
                var f = WingAt(i, time, side, arm, rotation, omega);
                FeatherParticle.DrawVane(painter, f.X, f.Y, f.Angle, f.Length, q.Width, colors, 2, alpha, side, f.Hot);
            }
            double k = count / (double)quills.Length;
            for (int i = 0; i < arm.Length; i++) arm[i] = Place(arm[i], side, Cos(rotation), Sin(rotation));
            if (k > 0.5)
                for (int layer = 1; layer >= 0; layer--)
                for (int i = 1; i < arm.Length; i++)
                    painter.Line(arm[i - 1].X, arm[i - 1].Y + layer, arm[i].X, arm[i].Y + layer, layer == 0 ? White : colors[5], alpha);

            painter.Glow(side * 28, -36, 38, colors[4], 0.26 * alpha * k);
            double g = (time - WingStart - 0.45) * 1.3 % 1;
            if (k > 0.9 && time > WingStart + 0.45 && g < 0.8)
            {
                var (x, y) = Along(arm, g / 0.8);
                Sparkle(x, y, 2 + Sin(g / 0.8 * PI) * 2, White, colors[6], 0.4);
            }
            painter.GlowLine(arm[0].X, arm[0].Y, arm[3].X, arm[3].Y, 7, colors[5], 0.25 * alpha * k);
            if (time <= BeatDrop + 0.03 || time >= BeatEnd + 0.14) return;
            double fade = 1 - Smooth(BeatEnd, BeatEnd + 0.14, time);
            foreach (int i in TrailQuills)
            {
                var previous = WingAt(i, time, side);
                var point = previous.Point(previous.Length - 1);
                for (int j = 1; j < 10; j++)
                {
                    var f = WingAt(i, Max(BeatDrop + 0.01, time - j * 0.012), side);
                    var next = f.Point(f.Length - 1);
                    painter.Line(point.X, point.Y, next.X, next.Y, j < 3 ? White : colors[j < 6 ? 6 : 4], fade * (1.1 - j / 10.0));
                    point = next;
                }
            }
        }
    }
}
