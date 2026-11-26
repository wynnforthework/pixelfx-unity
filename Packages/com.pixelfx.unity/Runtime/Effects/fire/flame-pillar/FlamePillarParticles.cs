using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class FlamePillarEffect
    {
        private enum Kind { Fire, Smoke, Spark, Ember, Grit, Dust }

        [System.Flags]
        private enum Flags { None = 0, Smokes = 1, Big = 2 }

        private struct Particle
        {
            internal Kind Kind;
            internal Flags Flags;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Delay, Seed;
            internal uint Ink;
        }

        private struct Rock
        {
            internal double X, Y, Vx, Vy, Life, Angle, Spin, Size;
            internal bool Rest, Dusted;
        }

        private readonly List<Particle> particles = new List<Particle>(256);
        private readonly Rock[] rocks = new Rock[18];
        private int steps;
        private bool erupted;
        private double warmLife, whiteLife;
        private const double Gravity = 950;
        private static readonly uint Outline = Hex(0x1a1422);
        private static readonly uint[] SmokeRamp =
            { Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e) };

        private void Advance(double sample)
        {
            int target = (int)Floor(sample * 120 + 1e-8);
            if (target < steps)
            {
                time = warmLife = whiteLife = 0;
                steps = 0;
                erupted = false;
                random = initialRandom;
                particles.Clear();
                System.Array.Clear(rocks, 0, rocks.Length);
            }
            while (steps < target)
            {
                steps++;
                time += Step;
                bool burst = !erupted && time >= Erupt;
                if (burst)
                {
                    erupted = true;
                    warmLife = 0.6;
                    whiteLife = 0.1;
                }
                if (full)
                {
                    Emit(burst);
                    TickRocks();
                    TickParticles();
                }
                warmLife = Max(0, warmLife - Step);
                whiteLife = Max(0, whiteLife - Step);
            }
        }

        private bool Chance(double probability) => random.Range(0, 1) < probability;
        private int Side() => Chance(0.5) ? -1 : 1;
        private int RangeInt(int min, int max) => (int)Floor(random.Range(min, max + 1));

        private void Spawn(Kind kind, double x, double y, double vx, double vy, double life,
            uint ink = 0, double size = 1, double delay = 0, Flags flags = Flags.None)
        {
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life, Ink = ink,
                Size = size, Delay = delay, Flags = flags, Seed = random.Range(0, 100)
            });
        }

        private void Emit(bool burst)
        {
            if (time < Erupt) EmitCharge();
            if (burst) EmitEruption();

            double h = ColumnHeight(), w = ColumnWidth();
            if (time > Erupt && time < Gone && h > 4)
            {
                if (Chance(0.9 * Step * 60))
                {
                    int side = Side();
                    double y = -random.Range(4, h * 0.9);
                    Spawn(Kind.Fire, side * w * random.Range(0.7, 1.1), y, side * random.Range(15, 40),
                        -random.Range(30, 70), random.Range(0.25, 0.5), size: random.Range(1.6, 3.2),
                        flags: Chance(0.35) ? Flags.Smokes : Flags.None);
                }
                if (Chance(0.7 * Step * 60))
                    Spawn(Kind.Fire, random.Range(-w, w), -h + random.Range(0, 6), random.Range(-20, 20),
                        -random.Range(30, 80), random.Range(0.35, 0.7), size: random.Range(3, 5.5),
                        flags: Chance(0.6) ? Flags.Smokes | (time < Fall - 0.3 ? Flags.Big : Flags.None) : Flags.None);
                if (time < Fall && Chance(0.5 * Step * 60))
                    Spawn(Kind.Ember, random.Range(-w, w) * 0.6, -h + 4, random.Range(-60, 60), -random.Range(50, 110),
                        random.Range(0.7, 1.3), size: Chance(0.2) ? 2 : 1);
            }
            if (time >= Fall && time < Gone - 0.1 && Chance(1.6 * Step * 60))
                Spawn(Kind.Fire, random.Range(-w - 2, w + 2), -h - random.Range(0, 8), random.Range(-30, 30),
                    -random.Range(10, 50), random.Range(0.4, 0.75), size: random.Range(3.5, 6.5), flags: Flags.Smokes);
        }

        private void EmitCharge()
        {
            if (Chance(0.6 * Step * 60 * Smooth(0.15, Erupt, time)))
                Spawn(Kind.Fire, random.Range(-5, 5), -1, random.Range(-6, 6), -random.Range(20, 45),
                    random.Range(0.15, 0.3), size: random.Range(1, 2.2));
            if (!Chance(0.5 * Step * 60 * Smooth(0, Erupt, time))) return;
            var crack = cracks[RangeInt(0, cracks.Length - 1)];
            int index = RangeInt(0, crack.VisibleCount(time) - 1);
            var (x, y) = crack.Points[index];
            if (Chance(0.5))
                Spawn(Kind.Fire, x, y - 1, random.Range(-5, 5), -random.Range(15, 35),
                    random.Range(0.15, 0.3), size: random.Range(0.8, 1.6));
            else
                Spawn(Kind.Ember, x, y - 1, random.Range(-15, 15), -random.Range(30, 70), random.Range(0.5, 1));
        }

        private void EmitEruption()
        {
            for (int i = 0; i < 70; i++)
            {
                double a = -PI / 2 + random.Range(-1.1, 1.1), speed = random.Range(100, 260);
                Spawn(Kind.Spark, random.Range(-4, 4), -2, Cos(a) * speed, Sin(a) * speed, random.Range(0.25, 0.7));
            }
            for (int i = 0; i < rocks.Length; i++)
                rocks[i] = new Rock
                {
                    X = random.Range(-6, 6), Y = -2, Vx = random.Range(-110, 110), Vy = -random.Range(150, 330),
                    Life = random.Range(1.6, 2.4), Angle = random.Range(0, Tau), Spin = random.Range(-16, 16), Size = RangeInt(1, 3),
                };
            for (int i = 0; i < 60; i++)
            {
                double a = -PI / 2 + random.Range(-0.9, 0.9), speed = random.Range(80, 260);
                double x = random.Range(-5, 5), life = random.Range(1, 1.8);
                int shade = RangeInt(0, 3);
                Spawn(Kind.Grit, x, -1, Cos(a) * speed, Sin(a) * speed, life,
                    shade == 0 ? Stone.Light : shade == 3 ? Stone.Shade : Stone.Body, Chance(0.4) ? 2 : 1);
            }
            for (int i = 0; i < 14; i++)
            {
                int side = Side();
                Spawn(Kind.Smoke, side * random.Range(2, 10), -random.Range(1, 4), side * random.Range(40, 110),
                    -random.Range(2, 10), random.Range(1, 1.8), Stone.Dust, random.Range(4, 7), random.Range(0, 0.06), Flags.Big);
            }
        }

        private void TickRocks()
        {
            for (int i = 0; i < rocks.Length; i++)
            {
                ref var rock = ref rocks[i];
                if (rock.Life <= 0 || (rock.Life -= Step) <= 0 || rock.Rest) continue;
                rock.Vy += Gravity * Step;
                rock.Vx -= rock.Vx * 0.4 * Step;
                rock.Vy -= rock.Vy * 0.4 * Step;
                rock.X += rock.Vx * Step;
                rock.Y += rock.Vy * Step;
                rock.Angle += rock.Spin * Step;
                if (rock.Y < 0 || rock.Vy <= 0) continue;
                rock.Y = -0.01;
                if (rock.Vy > 70)
                {
                    rock.Vy *= -0.3;
                    rock.Vx *= 0.7;
                    rock.Spin *= 0.6;
                    continue;
                }
                rock.Rest = true;
                rock.Vx = rock.Vy = 0;
            }
            for (int i = 0; i < rocks.Length; i++)
            {
                ref var rock = ref rocks[i];
                if (rock.Life <= 0 || !rock.Rest || rock.Dusted) continue;
                rock.Dusted = true;
                for (int j = 0; j < 3; j++)
                    Spawn(Kind.Dust, rock.X + random.Range(-1.5, 1.5), -0.5, Side() * random.Range(8, 35),
                        -random.Range(2, 18), random.Range(0.3, 0.6), Stone.Dust, random.Range(1.3, 2.6));
            }
        }

        private void TickParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var q = particles[i];
                if (q.Delay > 0)
                {
                    q.Delay -= Step;
                    particles[i] = q;
                    continue;
                }
                bool big = (q.Flags & Flags.Big) != 0;
                if ((q.Life -= Step) <= 0)
                {
                    if ((q.Flags & Flags.Smokes) != 0)
                        Spawn(Kind.Smoke, q.X, q.Y, q.Vx * 0.6, q.Vy * 0.6 - 6,
                            big ? random.Range(1.3, 2.3) : random.Range(0.8, 1.4),
                            size: q.Size * (big ? 0.85 : 0.7), flags: big ? Flags.Big : Flags.None);
                    particles.RemoveAt(i);
                    continue;
                }
                AdvanceVelocity(ref q, big);
                q.X += q.Vx * Step;
                double nextY = q.Y + q.Vy * Step;
                if (q.Vy > 0 && q.Y <= 0 && nextY >= 0)
                {
                    if (q.Kind == Kind.Spark)
                    {
                        q.Vy *= -0.45;
                        q.Vx *= 0.7;
                        nextY = -0.25;
                    }
                    else if (q.Kind == Kind.Grit)
                    {
                        if (q.Vy > 130 && Chance(0.4))
                        {
                            q.Vy *= -0.25;
                            q.Vx = q.Vx * 0.5 + random.Range(-10, 10);
                        }
                        else
                        {
                            q.Vx = q.Vy = 0;
                            q.Life = Min(q.Life, 0.5);
                        }
                        nextY = -0.25;
                    }
                }
                q.Y = nextY;
                particles[i] = q;
            }
        }

        private void AdvanceVelocity(ref Particle q, bool big)
        {
            switch (q.Kind)
            {
                case Kind.Fire:
                    q.Vy -= 95 * Step;
                    q.Vx += Sin(time * 17 + q.Seed) * 30 * Step;
                    q.Vx -= q.Vx * 5 * Step;
                    q.Vy -= q.Vy * 3.2 * Step;
                    break;
                case Kind.Smoke:
                    q.Vy -= (big ? 23 : 16) * Step;
                    q.Vx += Sin(time * 1.3 + q.Seed) * 8 * Step;
                    q.Vx -= q.Vx * 1.6 * Step;
                    q.Vy -= q.Vy * 1.2 * Step;
                    break;
                case Kind.Ember:
                    q.Vy += (-30 + Sin(time * 3 + q.Seed) * 20) * Step;
                    q.Vx += Cos(time * 2.3 + q.Seed) * 45 * Step;
                    q.Vx -= q.Vx * 1.6 * Step;
                    q.Vy -= q.Vy * 1.6 * Step;
                    break;
                case Kind.Dust:
                    q.Vx -= q.Vx * 4 * Step;
                    q.Vy -= q.Vy * 4 * Step + 5 * Step;
                    break;
                case Kind.Spark:
                    q.Vy += Gravity * 0.45 * Step;
                    q.Vx -= q.Vx * 2.2 * Step;
                    q.Vy -= q.Vy * 2.2 * Step;
                    break;
                case Kind.Grit:
                    q.Vy += Gravity * Step;
                    q.Vx -= q.Vx * 0.8 * Step;
                    break;
            }
        }

        private void DrawParticles(bool back)
        {
            if (!back)
                foreach (var q in particles)
                    if (q.Kind == Kind.Fire && q.Delay <= 0)
                        FireDrawing.Puff(painter, q.X, q.Y, q.Life / q.MaxLife, q.Size, q.Seed, heat);

            foreach (var q in particles)
            {
                if (q.Delay > 0 || q.Kind == Kind.Fire || back != (q.Kind == Kind.Smoke || q.Kind == Kind.Dust)) continue;
                double k = q.Life / q.MaxLife;
                uint hot = heat[k > 0.6 ? 1 : k > 0.3 ? 2 : 4];
                switch (q.Kind)
                {
                    case Kind.Smoke:
                        DrawSmoke(q, k);
                        break;
                    case Kind.Dust:
                        painter.SoftDisc(q.X, q.Y, q.Size * (0.6 + (1 - k) * 0.9), q.Ink, Pow(k, 1.3) * 0.7, 0);
                        break;
                    case Kind.Ember:
                        if (k < 0.25 && Sin(time * 40 + q.Seed) < 0) break;
                        painter.Dot(q.X, q.Y, hot);
                        if (q.Size > 1.5) painter.Dot(q.X, q.Y + 1, heat[5]);
                        painter.Glow(q.X, q.Y, 3 + q.Size, hot, 0.35 * k);
                        break;
                    case Kind.Spark:
                        double speed = Sqrt(q.Vx * q.Vx + q.Vy * q.Vy);
                        if (speed == 0) speed = 1;
                        double length = Min(9, 1 + speed * 0.022), ux = q.Vx / speed, uy = q.Vy / speed;
                        painter.Line(q.X - ux * length, q.Y - uy * length, q.X - ux * length * 0.4, q.Y - uy * length * 0.4,
                            k > 0.5 ? hot : heat[5]);
                        painter.Line(q.X - ux * length * 0.45, q.Y - uy * length * 0.45, q.X, q.Y, hot);
                        painter.Glow(q.X, q.Y, 3, hot, 0.45 * k);
                        break;
                    case Kind.Grit:
                        if (k <= 0.125) break;
                        painter.Dot(q.X, q.Y, q.Ink);
                        if (q.Size < 2) break;
                        painter.Dot(q.X + 1, q.Y, q.Ink);
                        painter.Dot(q.X, q.Y + 1, Scale(q.Ink, 0.8));
                        painter.Dot(q.X + 1, q.Y + 1, Scale(q.Ink, 0.7));
                        break;
                }
            }
        }

        private void DrawSmoke(Particle q, double k)
        {
            double age = 1 - k;
            bool big = (q.Flags & Flags.Big) != 0;
            double r = q.Size * (0.65 + Min(1, age * 2.5) * 0.45 + age * (big ? 0.5 : 0.4));
            double a = Min(1, age * 12) * Min(1, k * 2.4) * (big ? 0.7 : 0.58);
            int index = Min(4, (int)((q.Ink == 0 ? age * 1.05 : age) * SmokeRamp.Length));
            uint ink = q.Ink == 0 ? SmokeRamp[index] : Mix(q.Ink, SmokeRamp[index], age);
            painter.SoftDisc(q.X, q.Y, r, ink, a * 0.45, 0);
            painter.SoftDisc(q.X + r * 0.05, q.Y + r * 0.06, r * 0.74, ink, a * 0.62, 0);
            if (age < 0.5)
                painter.SoftDisc(q.X - r * 0.22, q.Y - r * 0.3, r * 0.5, Mix(ink, White, 0.12), a * 0.4 * (1 - age * 2), 0);
        }

        private void DrawRocks()
        {
            foreach (var rock in rocks)
            {
                if (rock.Life <= 0) continue;
                double r = 0.6 + rock.Size * 0.75;
                double lx = Cos(rock.Angle) * r * 0.4, ly = Sin(rock.Angle) * r * 0.4;
                painter.Shade(rock.X - r - 1, rock.Y - r - 1, rock.X + r + 1, rock.Y + r + 1,
                    (rock, r, lx, ly), static (x, y, s) =>
                    {
                        double dx = x - s.rock.X, dy = y - s.rock.Y;
                        double distance = Sqrt(dx * 1.15 * (dx * 1.15) + dy * dy);
                        if (distance > s.r + 0.9) return default;
                        double lit = dx * s.lx + dy * s.ly;
                        uint ink = distance > s.r ? Outline : lit < -0.4 ? Stone.Light : lit > 0.6 ? Stone.Shade : Stone.Body;
                        return (ink, Min(1, s.rock.Life / 0.5));
                    });
            }
        }
    }
}
