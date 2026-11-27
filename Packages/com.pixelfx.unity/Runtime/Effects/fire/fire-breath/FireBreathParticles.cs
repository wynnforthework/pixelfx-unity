using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;
using static PixelFx.FireDrawing;

namespace PixelFx
{
    internal sealed partial class FireBreathEffect
    {
        private struct Billow
        {
            internal double Born, Edge, Speed, FireLife, SmokeLife, Size, HeatOffset, X;
            internal bool Smokes;
        }

        private struct Streak
        {
            internal double Born, Edge, Speed, Life, Length;
        }

        private struct Spark
        {
            internal double Born, Distance, Edge, Speed, Life;
        }

        private struct Ember
        {
            internal double Born, X, Y, Vx, Vy, Life, Phase;
            internal bool Large;
        }

        private static readonly uint[] SmokeRamp =
            { Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e) };
        private readonly Billow[] billows = new Billow[260];
        private readonly List<(double Age, double X, double Y, int Index)> flames =
            new List<(double, double, double, int)>(260);
        private Streak[] streaks;
        private Spark[] sparks;
        private Ember[] embers;

        private void InitializeParticles()
        {
            for (int i = 0; i < billows.Length; i++)
            {
                var r = new PixelFxRandom(unchecked(seed + 100), i);
                double born = r.Range(0, Duration), edge = r.Range(-1, 1);
                double life = r.Range(0.42, 0.8) * (1 - 0.3 * Abs(edge));
                bool smokes = r.Range(0, 1) < 0.2 + 0.4 * Abs(edge);
                billows[i] = new Billow
                {
                    Born = born, Edge = edge, FireLife = life, Smokes = smokes, Speed = r.Range(0.75, 1.1),
                    SmokeLife = r.Range(0.5, 0.85), Size = r.Range(3, 5.2), HeatOffset = r.Range(0, 1), X = r.Range(4, 10),
                };
            }
            if (!full) return;

            streaks = new Streak[70];
            for (int i = 0; i < streaks.Length; i++)
            {
                var r = new PixelFxRandom(unchecked(seed + 250), i);
                streaks[i] = new Streak
                {
                    Born = r.Range(0, Duration), Edge = r.Range(-0.6, 0.6), Speed = r.Range(330, 470),
                    Life = r.Range(0.12, 0.26), Length = r.Range(4, 9),
                };
            }
            sparks = new Spark[40];
            for (int i = 0; i < sparks.Length; i++)
            {
                var r = new PixelFxRandom(unchecked(seed + 300), i);
                sparks[i] = new Spark
                {
                    Born = r.Range(0, Duration), Distance = r.Range(4, 60), Edge = r.Range(-1, 1),
                    Speed = r.Range(140, 280), Life = r.Range(0.25, 0.55),
                };
            }
            embers = new Ember[18];
            for (int i = 0; i < embers.Length; i++)
            {
                var r = new PixelFxRandom(unchecked(seed + 400), i);
                embers[i] = new Ember
                {
                    Born = r.Range(0, Duration), X = r.Range(50, 100), Y = r.Range(-14, 6), Vx = r.Range(10, 40),
                    Vy = -r.Range(20, 50), Life = r.Range(0.8, 1.4), Large = r.Range(0, 1) < 0.2, Phase = r.Range(0, 6.3),
                };
            }
        }

        private void DrawCone(double time)
        {
            flames.Clear();
            for (int i = 0; i < billows.Length; i++)
            {
                var q = billows[i];
                double life = q.FireLife + (full && q.Smokes ? 0.85 : 0);
                for (double age = Wrap(time - q.Born); age < life; age += Duration)
                {
                    double smokeAge = age - q.FireLife;
                    if (smokeAge > q.SmokeLife) continue;
                    double born = time - age;
                    double angle = q.Edge * Spread + Sway(born) - 0.04, speed = Speed * q.Speed * Roar(born);
                    double travel = Min(age, q.FireLife);
                    var (x, vx) = Drift(q.X, Cos(angle) * speed, 0, 2, travel);
                    var (y, vy) = Drift(q.Edge * 1.5, Sin(angle) * speed, -100, 2.4, travel);
                    if (smokeAge < 0)
                    {
                        flames.Add((age, x, y, i));
                        continue;
                    }
                    var (sx, _) = Drift(x, vx * 0.5, 0, 1.8, smokeAge);
                    var (sy, _) = Drift(y, vy * 0.6 - 8, -26, 1.2, smokeAge);
                    DrawSmoke(sx, sy, 1 - smokeAge / q.SmokeLife, q.Size * 1.1);
                }
            }

            flames.Sort(static (a, b) =>
            {
                int ageOrder = b.Age.CompareTo(a.Age);
                return ageOrder != 0 ? ageOrder : a.Index.CompareTo(b.Index);
            });
            foreach (var (age, x, y, index) in flames)
            {
                var q = billows[index];
                double u = age / q.FireLife, edge = Abs(q.Edge), size = q.Size * (0.75 + 1.6 * u);
                double temperature = Min(0.999, Pow(u, 1.5) * (0.85 + 0.2 * edge) + 0.3 * edge * edge);
                double tear = Max(0, Min(1, (u - 0.32) / 0.2));
                Puff(painter, x, y, 1 - temperature, size * (1 - 0.55 * tear), q.HeatOffset, heat, 0.1, -0.5, -0.86);
                if (tear <= 0) continue;
                int stream = (int)Floor(q.HeatOffset * 1e5);
                double length = size * (1.5 + 0.5 * NoiseAt(time, 4, stream)) * (1.2 - temperature * 0.6) * tear;
                Tongue(painter, x, y + size * 0.3, length, size * 0.5 * (1.1 - temperature * 0.5),
                    -length * 0.4 + NoiseAt(time, 2.5, stream + 1) * 2, time, stream, heat,
                    temperature < 0.45 ? HotLayers : temperature < 0.72 ? BackLayers : CoolLayers, Duration);
            }
        }

        private void DrawSmoke(double x, double y, double k, double size)
        {
            double age = 1 - k, radius = size * (0.65 + Min(1, age * 2.5) * 0.45 + age * 0.5);
            double alpha = Min(1, age * 12) * Min(1, k * 2.4) * 0.7 * 0.7;
            uint ink = Mix(smokeInk, SmokeRamp[Min(4, (int)Floor(age * SmokeRamp.Length))], age);
            painter.SoftDisc(x, y, radius, ink, alpha * 0.45, 0);
            painter.SoftDisc(x + radius * 0.05, y + radius * 0.06, radius * 0.74, ink, alpha * 0.62, 0);
            if (age < 0.5)
                painter.SoftDisc(x - radius * 0.22, y - radius * 0.3, radius * 0.5, Mix(ink, White, 0.12),
                    alpha * 0.4 * (1 - age * 2), 0);
        }

        private void DrawStreaks(double time)
        {
            foreach (var q in streaks)
            {
                double age = Wrap(time - q.Born);
                if (age >= q.Life) continue;
                double angle = q.Edge * Spread - 0.03;
                var (x, vx) = Drift(6, Cos(angle) * q.Speed, 0, 2, age);
                var (y, vy) = Drift(q.Edge * 2, Sin(angle) * q.Speed, -60, 2, age);
                double speed = Sqrt(vx * vx + vy * vy), length = q.Length * (speed / 400);
                bool young = 1 - age / q.Life > 0.5;
                painter.Line(x - vx / speed * length, y - vy / speed * length, x, y,
                    (Head: heat[young ? 0 : 1], Tail: heat[young ? 2 : 3]),
                    static (u, c) => (u > 0.6 ? c.Head : c.Tail, 1.0));
            }
        }

        private void DrawSparks(double time)
        {
            foreach (var q in sparks)
            {
                double age = Wrap(time - q.Born);
                if (age >= q.Life) continue;
                double angle = q.Edge * Spread * 1.8 - 0.12;
                var (x, vx) = Drift(q.Distance, Cos(angle) * q.Speed, 0, 2, age);
                var (y, vy) = Drift(q.Edge * q.Distance * Spread, Sin(angle) * q.Speed - 30, 190, 2, age);
                double k = 1 - age / q.Life, speed = Sqrt(vx * vx + vy * vy);
                double length = Min(9, 1 + speed * 0.022), ux = vx / speed, uy = vy / speed;
                uint ink = heat[k > 0.6 ? 1 : k > 0.3 ? 2 : 4];
                painter.Line(x - ux * length, y - uy * length, x - ux * length * 0.4, y - uy * length * 0.4,
                    k > 0.5 ? ink : heat[5]);
                painter.Line(x - ux * length * 0.45, y - uy * length * 0.45, x, y, ink);
                painter.Glow(x, y, 3, ink, 0.45 * k);
            }
        }

        private void DrawEmbers(double time)
        {
            foreach (var q in embers)
            {
                double age = Wrap(time - q.Born);
                if (age >= q.Life) continue;
                double k = 1 - age / q.Life;
                if (k < 0.25 && Sin(age * 40 + q.Phase) < 0) continue;
                var (x, _) = Drift(q.X, q.Vx, 0, 1.6, age);
                var (y, _) = Drift(q.Y, q.Vy, -30, 1.6, age);
                x += 5 * (Sin(age * 2.3 + q.Phase) - Sin(q.Phase));
                uint ink = heat[k > 0.6 ? 1 : k > 0.3 ? 2 : 4];
                if (q.Large)
                {
                    uint rim = heat[k > 0.45 ? 4 : 6];
                    painter.Dot(x - 1, y, rim);
                    painter.Dot(x + 1, y, rim);
                    painter.Dot(x, y - 1, rim);
                    painter.Dot(x, y + 1, rim);
                }
                painter.Dot(x, y, ink);
                painter.Glow(x, y, q.Large ? 8 : 4, ink, (q.Large ? 0.4 : 0.35) * k);
            }
        }

        private static (double Position, double Velocity) Drift(double x, double v, double a, double drag, double t)
        {
            double decay = Exp(-drag * t), terminal = a / drag;
            return (x + terminal * t + (v - terminal) * (1 - decay) / drag, terminal + (v - terminal) * decay);
        }
    }
}
