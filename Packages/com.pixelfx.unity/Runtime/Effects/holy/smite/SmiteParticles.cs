using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Particle and debris recipes adapt Sprite Fusion VFX, MIT, Copyright (c) 2026 Hugo Duprez.
    internal sealed partial class SmiteEffect
    {
        private const double Step = 1.0 / 120;
        private static readonly uint Stone = Hex(0x5a5066), Outline = Hex(0x1a1422);
        private static readonly uint[] Stones = { Scale(Stone, 0.75), Stone, Mix(Scale(Stone, 1.4), White, 0.1) };
        private static readonly uint[] Smoke = { Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e) };
        private enum Kind { Star, Mote, Spark, Grit, Smoke, Ember }

        private struct Particle
        {
            internal Kind Type;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Seed, Delay;
            internal uint Ink;
            internal bool Big;
        }

        private struct Chip
        {
            internal double X, Y, Vx, Vy, Life, Angle, Spin, Size;
            internal bool Rest;
        }

        private readonly List<Particle> particles = new List<Particle>(144);
        private readonly List<Chip> chips = new List<Chip>(14);

        private bool Chance(double p) => random.Range(0, 1) < p;
        private int Sign() => Chance(0.5) ? -1 : 1;

        private void Advance(double sample)
        {
            int target = (int)Floor(sample * 120 + 0.00001);
            if (target < tick)
            {
                tick = 0;
                time = 0;
                hitAge = double.PositiveInfinity;
                random = initialRandom;
                particles.Clear();
                chips.Clear();
            }
            while (tick < target)
            {
                tick++;
                double before = time;
                time += Step;
                bool impact = before < Hit && time >= Hit;
                hitAge = impact ? Step : hitAge + Step;
                if (!full) continue;
                Emit(impact);
                TickChips();
                TickParticles();
            }
        }

        private void Spawn(Kind type, double x, double y, double vx, double vy, double life,
            uint ink = White, double size = 1, double delay = 0, bool big = false)
        {
            particles.Add(new Particle
            {
                Type = type, X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life, Ink = ink,
                Size = size, Seed = random.Range(0, 1) * 100, Delay = delay, Big = big,
            });
        }

        private void Emit(bool impact)
        {
            if (time < Mark && Chance(0.25))
                Spawn(Kind.Star, random.Range(-10, 10), -random.Range(0, 18), 0, -random.Range(4, 12),
                    random.Range(0.2, 0.4), Chance(0.5) ? White : colors[6], random.Range(0.5, 1.2));
            if (time > Mark && time < Hit && TipY(time) > Top + 50 && Chance(0.6))
                Spawn(Kind.Mote, Sign() * random.Range(4, 7), TipY(time) - random.Range(4, 30),
                    random.Range(-20, 20), -random.Range(10, 40), random.Range(0.15, 0.3), colors[6], 0.7);
            if (impact) EmitImpact();
            if (time > Hit && time < Hit + 1.3 && Chance(0.5))
            {
                var path = cracks[(int)random.Range(0, cracks.Length)].Main;
                var (x, y) = path[(int)random.Range(0, path.Length)];
                if (Chance(0.75))
                    Spawn(Kind.Ember, x, y - 1, random.Range(-8, 8), -random.Range(15, 35), random.Range(0.5, 0.9), colors[5]);
                else
                    Spawn(Kind.Star, x, y - 2, 0, -random.Range(4, 12), random.Range(0.3, 0.6),
                        Chance(0.5) ? White : colors[6], random.Range(0.8, 1.8));
            }
        }

        private void EmitImpact()
        {
            for (int i = 0; i < 60; i++)
            {
                double a = -PI / 2 + random.Range(-1.25, 1.25), speed = random.Range(110, 280);
                Spawn(Kind.Spark, random.Range(-3, 3), -2, Cos(a) * speed, Sin(a) * speed, random.Range(0.25, 0.75));
            }
            for (int i = 0; i < 16; i++)
            {
                double a = -PI / 2 + random.Range(-1, 1), speed = random.Range(60, 160);
                Spawn(Kind.Star, random.Range(-4, 4), -4, Cos(a) * speed, Sin(a) * speed, random.Range(0.4, 0.9),
                    Chance(0.5) ? White : colors[6], random.Range(0.8, 2.4));
            }
            for (int i = 0; i < 14; i++)
            {
                double a = -PI / 2 + random.Range(-1.2, 1.2), speed = random.Range(90, 230);
                chips.Add(new Chip
                {
                    X = random.Range(-5, 5), Y = -2, Vx = Cos(a) * speed, Vy = Sin(a) * speed,
                    Life = random.Range(1.2, 1.8), Angle = random.Range(0, Tau), Spin = random.Range(-14, 14),
                    Size = Floor(random.Range(1, 4)),
                });
            }
            for (int i = 0; i < 34; i++)
            {
                double a = -PI / 2 + random.Range(-1, 1), speed = random.Range(60, 220);
                Spawn(Kind.Grit, random.Range(-4, 4), -1, Cos(a) * speed, Sin(a) * speed, random.Range(0.9, 1.5),
                    Stones[(int)random.Range(0, Stones.Length)], Chance(0.4) ? 2 : 1);
            }
            uint dust = Mix(Stone, Hex(0x8a8094), 0.4);
            for (int i = 0; i < 9; i++)
            {
                int side = Sign();
                Spawn(Kind.Smoke, side * random.Range(2, 10), -random.Range(1, 4), side * random.Range(50, 130),
                    -random.Range(2, 12), random.Range(0.7, 1.2), Mix(dust, colors[4], 0.18),
                    random.Range(3.5, 6), random.Range(0, 0.06), true);
            }
            for (int i = 0; i < 4; i++)
                Spawn(Kind.Smoke, random.Range(-4, 4), -random.Range(2, 8), random.Range(-8, 8),
                    -random.Range(20, 45), random.Range(1.2, 1.8), Mix(dust, colors[5], 0.25),
                    random.Range(4, 6), random.Range(0.05, 0.3));
        }

        private void TickChips()
        {
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                var c = chips[i];
                c.Life -= Step;
                if (c.Life <= 0)
                {
                    chips.RemoveAt(i);
                    continue;
                }
                if (!c.Rest)
                {
                    c.Vy += 950 * Step;
                    c.Vx -= c.Vx * 0.4 * Step;
                    c.Vy -= c.Vy * 0.4 * Step;
                    c.X += c.Vx * Step;
                    c.Y += c.Vy * Step;
                    c.Angle += c.Spin * Step;
                    if (c.Y >= 0 && c.Vy > 0)
                    {
                        c.Y = -0.01;
                        if (c.Vy > 70)
                        {
                            c.Vy *= -0.35;
                            c.Vx *= 0.7;
                            c.Spin *= 0.6;
                        }
                        else
                        {
                            c.Rest = true;
                            c.Vx = c.Vy = 0;
                        }
                    }
                }
                chips[i] = c;
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
                q.Life -= Step;
                if (q.Life <= 0)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                switch (q.Type)
                {
                    case Kind.Star:
                        q.Vx -= q.Vx * 1.6 * Step;
                        q.Vy -= q.Vy * 1.6 * Step + 5 * Step;
                        break;
                    case Kind.Mote:
                        q.Vx -= q.Vx * 0.6 * Step;
                        q.Vy -= q.Vy * 0.6 * Step;
                        break;
                    case Kind.Spark:
                        q.Vy += 950 * 0.45 * Step;
                        q.Vx -= q.Vx * 2.2 * Step;
                        q.Vy -= q.Vy * 2.2 * Step;
                        break;
                    case Kind.Grit:
                        q.Vy += 950 * Step;
                        q.Vx -= q.Vx * 0.8 * Step;
                        break;
                    case Kind.Smoke:
                        q.Vy -= (q.Big ? 23 : 16) * Step;
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
                }

                q.X += q.Vx * Step;
                double nextY = q.Y + q.Vy * Step;
                if ((q.Type == Kind.Spark || q.Type == Kind.Grit) && q.Vy > 0 && q.Y <= 0 && nextY >= 0)
                {
                    q.Y = -0.25;
                    if (q.Type == Kind.Spark)
                    {
                        q.Vy *= -0.45;
                        q.Vx *= 0.7;
                    }
                    else if (q.Vy > 130 && Chance(0.4))
                    {
                        q.Vy *= -0.25;
                        q.Vx = q.Vx * 0.5 + random.Range(-10, 10);
                    }
                    else
                    {
                        q.Vx = q.Vy = 0;
                        q.Life = Min(q.Life, 0.5);
                    }
                }
                else q.Y = nextY;
                particles[i] = q;
            }
        }

        private void DrawChips()
        {
            foreach (var c in chips)
            {
                double radius = 0.6 + c.Size * 0.7, alpha = Min(1, c.Life / 0.5);
                double lx = Cos(c.Angle) * radius * 0.4, ly = Sin(c.Angle) * radius * 0.4;
                painter.Shade(c.X - radius - 1, c.Y - radius - 1, c.X + radius + 1, c.Y + radius + 1,
                    (c.X, c.Y, Radius: radius, Alpha: alpha, Lx: lx, Ly: ly), static (x, y, state) =>
                    {
                        double dx = x - state.X, dy = y - state.Y, wideX = dx * 1.2;
                        double distance = Sqrt(wideX * wideX + dy * dy);
                        if (distance > state.Radius + 0.9) return default;
                        double lit = dx * state.Lx + dy * state.Ly;
                        return (distance > state.Radius ? Outline : Stones[lit < -0.3 ? 2 : lit > 0.5 ? 0 : 1], state.Alpha);
                    });
            }
        }

        private void DrawParticles(bool back)
        {
            foreach (var q in particles)
            {
                if (q.Delay > 0 || (q.Type == Kind.Smoke) != back) continue;
                double k = q.Life / q.MaxLife, x = q.X, y = q.Y;
                switch (q.Type)
                {
                    case Kind.Smoke:
                        DrawSmoke(q, k);
                        break;
                    case Kind.Spark:
                    case Kind.Mote:
                        DrawTrail(q, k);
                        break;
                    case Kind.Grit:
                        if (k <= 0.125) break;
                        painter.Dot(x, y, q.Ink);
                        if (q.Size >= 2)
                        {
                            painter.Dot(x + 1, y, q.Ink);
                            painter.Dot(x, y + 1, Scale(q.Ink, 0.8));
                            painter.Dot(x + 1, y + 1, Scale(q.Ink, 0.7));
                        }
                        break;
                    case Kind.Ember:
                        if (k < 0.25 && Sin(time * 40 + q.Seed) < 0) break;
                        uint ink = k > 0.6 ? White : colors[k > 0.3 ? 6 : 5];
                        painter.Dot(x, y, ink);
                        painter.Glow(x, y, 4, ink, 0.35 * k);
                        break;
                    case Kind.Star:
                        double tw = 0.6 + 0.4 * Sin(time * (8 + q.Seed % 5) + q.Seed);
                        if (q.Size < 1)
                        {
                            if (tw * k > 0.25) painter.Dot(x, y, tw > 0.8 ? White : q.Ink);
                            painter.Glow(x, y, 3, q.Ink, 0.4 * k * tw);
                        }
                        else Sparkle(x, y, q.Size * tw * Min(1, k * 3), q.Ink, Scale(q.Ink, 0.75), 0.5 * k * tw);
                        break;
                }
            }
        }

        private void DrawSmoke(Particle q, double k)
        {
            double age = 1 - k, radius = q.Size * (0.65 + Min(1, age * 2.5) * 0.45 + age * (q.Big ? 0.5 : 0.4));
            double alpha = Min(1, age * 12) * Min(1, k * 2.4) * (q.Big ? 0.7 : 0.58);
            uint ink = Mix(q.Ink, Smoke[Min(4, (int)Floor(age * Smoke.Length))], age);
            painter.SoftDisc(q.X, q.Y, radius, ink, alpha * 0.45, 0);
            painter.SoftDisc(q.X + radius * 0.05, q.Y + radius * 0.06, radius * 0.74, ink, alpha * 0.62, 0);
            if (age < 0.5)
                painter.SoftDisc(q.X - radius * 0.22, q.Y - radius * 0.3, radius * 0.5,
                    Mix(ink, White, 0.12), alpha * 0.4 * (1 - age * 2), 0);
        }

        private void DrawTrail(Particle q, double k)
        {
            double speed = Sqrt(q.Vx * q.Vx + q.Vy * q.Vy);
            if (speed == 0) speed = 1;
            double ux = q.Vx / speed, uy = q.Vy / speed, x = q.X, y = q.Y;
            if (q.Type == Kind.Spark)
            {
                uint ink = k > 0.6 ? White : colors[k > 0.3 ? 6 : 5];
                double len = Min(9, 1 + speed * 0.022);
                painter.Line(x - ux * len, y - uy * len, x - ux * len * 0.4, y - uy * len * 0.4, k > 0.5 ? ink : colors[3]);
                painter.Line(x - ux * len * 0.45, y - uy * len * 0.45, x, y, ink);
                painter.Glow(x, y, 3, ink, 0.45 * k);
            }
            else
            {
                double len = Min(16, 0.5 + speed * 0.026) * q.Size, alpha = Min(1, k / 0.35);
                painter.Line(x - ux * len, y - uy * len, x - ux * len * 0.35, y - uy * len * 0.35, Scale(q.Ink, 0.55), alpha);
                painter.Line(x - ux * len * 0.4, y - uy * len * 0.4, x, y, q.Ink, alpha);
                painter.Glow(x, y, 2.5 + q.Size, q.Ink, 0.4 * k);
            }
        }

        private void Sparkle(double x, double y, double arm, uint ink, uint tip, double glow)
        {
            int length = (int)Floor(arm + 0.5);
            if (length < 1) return;
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= length; d++)
            {
                uint c = d > length * 0.6 ? tip : ink;
                painter.Dot(px + d, py, c);
                painter.Dot(px - d, py, c);
                painter.Dot(px, py + d, c);
                painter.Dot(px, py - d, c);
            }
            for (int d = 1; d <= Min(2, length / 3); d++)
            {
                uint c = d == 1 ? ink : tip;
                painter.Dot(px + d, py + d, c);
                painter.Dot(px + d, py - d, c);
                painter.Dot(px - d, py + d, c);
                painter.Dot(px - d, py - d, c);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + length * 2, ink, glow);
        }
    }
}
