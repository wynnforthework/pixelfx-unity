using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed class GroundSlamCracks
    {
        internal static readonly uint Soot = Hex(0x0d0a12);
        private readonly Crack[] cracks = new Crack[10];
        private readonly List<(Point Point, double Life, double Phase)> embers = new List<(Point, double, double)>();
        private readonly uint[] colors;
        private readonly uint lip;

        private readonly struct Point
        {
            internal readonly double X, Y, Distance;
            internal Point(double x, double y, double distance) => (X, Y, Distance) = (x, y, distance);
        }

        private sealed class Crack
        {
            internal double Length, Delay;
            internal readonly List<Point[]> Paths = new List<Point[]>();
        }

        internal GroundSlamCracks(ref PixelFxRandom rng, uint[] colors, uint lip)
        {
            this.colors = colors;
            this.lip = lip;
            for (int i = 0; i < cracks.Length; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                double dip = 0.06 + Pow(rng.Range(0, 1), 1.7) * 0.55;
                double length = rng.Range(22, 58) * (1 - 0.5 * dip / (0.55 + 0.06));
                double angle = side > 0 ? dip : PI - dip;
                var root = new Point(side * rng.Range(2, 9), rng.Range(0.5, 1.5), 0);
                var main = Walk(ref rng, root, angle, length, 0.55, 2, 4);
                var crack = new Crack { Length = length, Delay = rng.Range(0, 0.035) };
                crack.Paths.Add(main);
                for (int j = 2; j < main.Length - 1; j++)
                {
                    if (rng.Range(0, 1) >= 0.7 * 0.4) continue;
                    double forkAngle = angle + (rng.Range(0, 1) < 0.5 ? -1 : 1) * rng.Range(0.45, 1);
                    double forkLength = rng.Range(3, 9) * (1 - 0.5 * j / main.Length);
                    crack.Paths.Add(Walk(ref rng, main[j], forkAngle, forkLength, 0.7, 1.5, 3));
                }
                cracks[i] = crack;
            }
        }

        internal void SeedEmbers(ref PixelFxRandom rng)
        {
            foreach (var crack in cracks)
            foreach (var point in crack.Paths[0])
                if (point.Distance > 3 && point.Distance < 32 && rng.Range(0, 1) < 0.25)
                    embers.Add((point, rng.Range(0.8, 2.2), rng.Range(0, 2 * PI)));
        }

        private static Point[] Walk(ref PixelFxRandom rng, Point start, double angle, double length,
            double jitter, double minStep, double maxStep)
        {
            var points = new List<Point> { start };
            double x = start.X, y = start.Y, distance = 0, heading = angle;
            while (distance < length - 0.01)
            {
                double step = Min(length - distance, rng.Range(minStep, maxStep));
                heading += rng.Range(-jitter, jitter) + (angle - heading) * 0.45;
                x += Cos(heading) * step;
                y = Clamp(y + Sin(heading) * step, 0.5, 22);
                distance += step;
                points.Add(new Point(x, y, start.Distance + distance));
            }
            return points.ToArray();
        }

        internal void Draw(PixelPainter painter, double time, double heat, double fade, bool full)
        {
            if (fade <= 0) return;
            foreach (var crack in cracks)
            {
                double growth = crack.Length * GroundSlamEffect.OutCubic(Clamp((time - crack.Delay) / 0.1, 0, 1));
                foreach (var path in crack.Paths)
                for (int i = 1; i < path.Length; i++)
                {
                    var start = path[i - 1];
                    if (start.Distance >= growth) break;
                    var end = path[i];
                    double delta = end.Distance - start.Distance;
                    double fraction = end.Distance > growth ? (growth - start.Distance) / delta : 1;
                    double ex = start.X + (end.X - start.X) * fraction;
                    double ey = start.Y + (end.Y - start.Y) * fraction;
                    painter.Line(start.X, start.Y + 1, ex, ey + 1, (start, ex, ey, lip, fade), static (u, s) =>
                    {
                        int x = (int)Floor(s.start.X + (s.ex - s.start.X) * u + 0.5);
                        int y = (int)Floor(s.start.Y + (s.ey - s.start.Y) * u + 0.5);
                        return (s.lip, PixelFxRandom.At(x, y, 5) < 0.3 ? s.fade * 0.8 : 0);
                    });
                    painter.Line(start.X, start.Y, ex, ey, (start.Distance, span: delta * fraction, heat, colors, fade),
                        static (u, s) =>
                        {
                            double h = HeatAt(s.heat, s.Distance + s.span * u);
                            uint ink = h > 0.75 ? White : h > 0.5 ? s.colors[5] : h > 0.3 ? s.colors[4]
                                : h > 0.12 ? s.colors[3] : Soot;
                            return (ink, s.fade);
                        });
                    double localHeat = HeatAt(heat, start.Distance);
                    if (localHeat > 0.12)
                        painter.Glow((start.X + ex) / 2, (start.Y + ey) / 2, 4 + localHeat * 4,
                            colors[4], 0.22 * localHeat * fade);
                }
            }
            if (!full || time < 0.1) return;
            foreach (var ember in embers)
            {
                double life = 1 - (time - 0.1) / ember.Life;
                if (life <= 0) continue;
                double hot = life * (0.6 + 0.4 * Sin(time * 23 + ember.Phase));
                painter.Dot(ember.Point.X, ember.Point.Y, colors[hot > 0.55 ? 5 : hot > 0.25 ? 4 : 3]);
                painter.Glow(ember.Point.X, ember.Point.Y, 4, colors[4], 0.35 * hot);
            }
        }

        private static double HeatAt(double heat, double distance) => heat * Clamp(1.2 - distance / 40, 0, 1);
    }
}
