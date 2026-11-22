using System;
using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;
using static PixelFx.GroundSlamEffect;

namespace PixelFx
{
    internal sealed class GroundSlamDebris
    {
        private const double Step = ImpactParticles.Step, Tau = 2 * PI;
        private static readonly uint[] SmokeRamp = { Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e) };
        private static readonly uint Outline = Hex(0x1a1422);
        private static readonly double LightAngle = Atan2(-1, -1);
        private readonly List<Particle> particles = new List<Particle>(448);
        private readonly Particle[] initialParticles;
        private readonly Rock[] initialRocks = new Rock[20], rocks = new Rock[20];
        private readonly Stone stone;
        private readonly uint[] colors, gritColors, waveGritColors;
        private readonly uint dustLit;
        private readonly PixelFxRandom initialRandom;
        private readonly double initialLeft, initialRight;
        private double left, right;
        private PixelFxRandom rng;
        private int steps;
        internal double Time { get; private set; }

        private enum Kind { Spark, Dust, Smoke, Flake, Grit }

        private struct Particle
        {
            internal Kind Kind;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Delay, Seed;
            internal uint Color;
        }

        private struct Rock
        {
            internal double X, Y, Vx, Vy, Life, Angle, Spin, Size;
            internal bool Resting;
            internal double[] OutlineRadii;
        }

        internal GroundSlamDebris(PixelFxRandom random, Stone stone, uint[] colors)
        {
            rng = random;
            this.stone = stone;
            this.colors = colors;
            dustLit = Mix(stone.Dust, White, 0.2);
            gritColors = new[] { stone.Light, stone.Body, stone.Body, stone.Shade, stone.Shade, stone.Lip };
            waveGritColors = new[] { stone.Light, stone.Body, stone.Lip };
            SeedContact();
            SeedCloud();
            initialParticles = particles.ToArray();
            initialLeft = left = rng.Range(0, 1);
            initialRight = right = rng.Range(0, 1);
            initialRandom = rng;
            initialRocks.CopyTo(rocks, 0);
        }

        private bool Chance(double chance) => rng.Range(0, 1) < chance;
        private int Side() => Chance(0.5) ? -1 : 1;
        private uint Pick(uint[] ramp) => ramp[(int)rng.Range(0, ramp.Length)];

        private void Spawn(Kind kind, double x, double y, double vx, double vy, double life,
            uint color = White, double size = 1, double delay = 0)
        {
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life,
                Color = color, Size = size, Delay = delay, Seed = rng.Range(0, 100)
            });
        }

        private void SeedContact()
        {
            for (int i = 0; i < 56; i++)
            {
                bool low = i < 14;
                int side = Side();
                double angle = low ? (side > 0 ? -rng.Range(0.04, 0.6) : PI + rng.Range(0.04, 0.6))
                    : -PI / 2 + rng.Range(-1.15, 1.15);
                double speed = low ? rng.Range(150, 380) : rng.Range(200, 500);
                Spawn(Kind.Spark, rng.Range(-4, 4), -1, Cos(angle) * speed, Sin(angle) * speed,
                    low ? rng.Range(0.2, 0.5) : rng.Range(0.25, 0.65), delay: low ? 0 : rng.Range(0, 0.05));
            }
            for (int i = 0; i < initialRocks.Length; i++)
            {
                int size = i < 3 ? 4 : (int)rng.Range(1, 4);
                double angle = -PI / 2 + rng.Range(-0.5, 0.5);
                double speed = size > 3 ? rng.Range(110, 190) : rng.Range(170, 380) * (size > 2 ? 0.8 : 1);
                var rock = new Rock
                {
                    X = rng.Range(-7, 7), Y = -2, Vx = Cos(angle) * speed, Vy = Sin(angle) * speed,
                    Life = rng.Range(1.9, 2.7), Angle = rng.Range(0, Tau), Spin = rng.Range(-14, 14),
                    Size = size, OutlineRadii = new double[6]
                };
                for (int j = 0; j < rock.OutlineRadii.Length; j++) rock.OutlineRadii[j] = rng.Range(0.7, 1.12);
                initialRocks[i] = rock;
            }
            for (int i = 0; i < 110; i++)
            {
                double u = rng.Range(-1, 1), angle = -PI / 2 + u * 0.65;
                double speed = rng.Range(110, 380) * (1 - 0.35 * Abs(u));
                Spawn(Kind.Grit, u * 6, -1, Cos(angle) * speed, Sin(angle) * speed, rng.Range(1, 2.2),
                    Pick(gritColors), Chance(0.35) ? 2 : 1);
            }
        }

        private void SeedCloud()
        {
            for (int i = 0; i < 28; i++)
            {
                bool core = i < 14;
                int side = Side();
                Spawn(Kind.Smoke, core ? rng.Range(-5, 5) : side * rng.Range(4, 12), -rng.Range(2, 10),
                    core ? rng.Range(-12, 12) : side * rng.Range(14, 38), -rng.Range(50, 130) * (core ? 1 : rng.Range(0.3, 0.6)),
                    rng.Range(1.9, 2.7), Chance(0.35) ? dustLit : stone.Dust,
                    core ? rng.Range(4.5, 9) : rng.Range(4, 7.5), rng.Range(0.02, 0.22));
            }
            for (int i = 0; i < 10; i++)
            {
                int side = Side();
                Spawn(Kind.Smoke, side * rng.Range(2, 14), -rng.Range(2, 6), side * rng.Range(20, 70),
                    -rng.Range(4, 20), rng.Range(1.4, 2.3), stone.Dust, rng.Range(6, 9), rng.Range(0, 0.08));
            }
            for (int i = 0; i < 26; i++)
                Spawn(Kind.Flake, rng.Range(-22, 22), -rng.Range(14, 80), rng.Range(-12, 12), rng.Range(-10, 4),
                    rng.Range(1.2, 2), Chance(0.5) ? stone.Lip : dustLit, rng.Range(0.5, 1.2), rng.Range(0.25, 1));
        }

        internal void Advance(double elapsed)
        {
            int target = (int)Floor(elapsed * 120 + 1e-8);
            if (target < steps)
            {
                rng = initialRandom;
                left = initialLeft;
                right = initialRight;
                particles.Clear();
                particles.AddRange(initialParticles);
                initialRocks.CopyTo(rocks, 0);
                Time = steps = 0;
            }
            while (steps < target)
            {
                Time += Step;
                steps++;
                double u = (Time - 0.015) / Wave;
                if (u > 0 && u < 1)
                {
                    double front = Front(Time), height = CrestHeight(u);
                    EmitWave(ref left, -1, u, front, height);
                    EmitWave(ref right, 1, u, front, height);
                }
                AdvanceRocks();
                for (int i = particles.Count - 1; i >= 0; i--)
                {
                    var particle = particles[i];
                    if (AdvanceParticle(ref particle)) particles[i] = particle;
                    else particles.RemoveAt(i);
                }
            }
        }

        private void EmitWave(ref double acc, int side, double u, double front, double height)
        {
            for (acc += Step * 120 * (1 - u * 0.7); acc > 1; acc--)
            {
                Spawn(Kind.Smoke, side * (front - rng.Range(1, 7)), -rng.Range(0.5, 3),
                    side * rng.Range(15, 70) * (1 - u), -rng.Range(2, 12), rng.Range(1, 2.1),
                    Chance(0.3) ? dustLit : stone.Dust, rng.Range(2.5, 6.5) * (1 - 0.4 * u));
                if (Chance(0.5))
                    Spawn(Kind.Dust, side * (front - rng.Range(0, 3)), -rng.Range(0.3, 0.9) * height,
                        side * rng.Range(-15, 25), -rng.Range(8, 35), rng.Range(0.3, 0.6),
                        Chance(0.5) ? dustLit : stone.Dust, rng.Range(1.2, 2.4));
                if (Chance(0.4))
                    Spawn(Kind.Grit, side * front, -1, side * rng.Range(10, 60), -rng.Range(40, 140) * (1 - u),
                        rng.Range(0.5, 1), Pick(waveGritColors));
                if (u < 0.45 && Chance(0.2))
                    Spawn(Kind.Spark, side * front, -1, side * rng.Range(60, 200), -rng.Range(30, 160), rng.Range(0.12, 0.3));
            }
        }

        private void AdvanceRocks()
        {
            foreach (ref var rock in rocks.AsSpan())
            {
                if (rock.Life <= 0 || (rock.Life -= Step) <= 0 || rock.Resting) continue;
                double previousVy = rock.Vy;
                rock.Vy += 950 * Step;
                rock.Vx -= rock.Vx * 0.4 * Step;
                rock.Vy -= rock.Vy * 0.4 * Step;
                rock.X += rock.Vx * Step;
                rock.Y += rock.Vy * Step;
                rock.Angle += rock.Spin * Step;
                if (rock.Y < 0 || rock.Vy <= 0) continue;
                rock.Y = -0.01;
                if (rock.Vy > 70)
                {
                    rock.Vy *= -0.38;
                    rock.Vx *= 0.7;
                    rock.Spin *= 0.6;
                }
                else
                {
                    rock.Resting = true;
                    rock.Vx = rock.Vy = 0;
                }
                if (previousVy <= 70) continue;
                int count = previousVy > 200 ? 3 : 2;
                for (int i = 0; i < count; i++)
                    Spawn(Kind.Dust, rock.X + rng.Range(-1.5, 1.5), -0.5, Side() * rng.Range(8, 35),
                        -rng.Range(2, 16), rng.Range(0.3, 0.65), stone.Dust,
                        rng.Range(1.3, 2.4) * (0.7 + rock.Size * 0.25));
            }
        }

        private bool AdvanceParticle(ref Particle p)
        {
            if (p.Delay > 0)
            {
                p.Delay -= Step;
                return true;
            }
            if ((p.Life -= Step) <= 0) return false;
            switch (p.Kind)
            {
                case Kind.Grit:
                    p.Vy += 950 * Step;
                    p.Vx -= p.Vx * 0.8 * Step;
                    MoveAlongFloor(ref p, grit: true);
                    return true;
                case Kind.Spark:
                    p.Vy += 950 * 0.45 * Step;
                    p.Vx -= p.Vx * 2.2 * Step;
                    p.Vy -= p.Vy * 2.2 * Step;
                    MoveAlongFloor(ref p, grit: false);
                    return true;
                case Kind.Flake:
                    p.Vx -= p.Vx * 2.2 * Step;
                    p.Vy -= p.Vy * 2.2 * Step - 35 * Step;
                    p.X += Sin(Time * 2.5 + p.Seed) * 7 * Step;
                    break;
                case Kind.Smoke:
                    p.Vy -= 23 * Step;
                    p.Vx += Sin(Time * 1.3 + p.Seed) * 8 * Step;
                    p.Vx -= p.Vx * 1.6 * Step;
                    p.Vy -= p.Vy * 1.2 * Step;
                    break;
                case Kind.Dust:
                    p.Vx -= p.Vx * 4 * Step;
                    p.Vy -= p.Vy * 4 * Step + 5 * Step;
                    break;
            }
            p.X += p.Vx * Step;
            p.Y += p.Vy * Step;
            return true;
        }

        private void MoveAlongFloor(ref Particle p, bool grit)
        {
            double nextY = p.Y + p.Vy * Step;
            p.X += p.Vx * Step;
            if (p.Vy <= 0 || p.Y > 0 || nextY < 0)
            {
                p.Y = nextY;
                return;
            }
            p.Y = -0.25;
            if (!grit)
            {
                p.Vy *= -0.45;
                p.Vx *= 0.7;
            }
            else if (p.Vy > 130 && Chance(0.4))
            {
                p.Vy *= -0.25;
                p.Vx = p.Vx * 0.5 + rng.Range(-10, 10);
            }
            else
            {
                p.Vx = p.Vy = 0;
                p.Life = Min(p.Life, 0.5);
            }
        }

        internal void DrawBack(PixelPainter painter)
        {
            foreach (var p in particles)
            {
                if (p.Delay > 0) continue;
                double life = p.Life / p.MaxLife;
                if (p.Kind == Kind.Dust)
                    painter.SoftDisc(p.X, p.Y, p.Size * (0.6 + (1 - life) * 0.9), p.Color, Pow(life, 1.3) * 0.7, 0);
                else if (p.Kind == Kind.Smoke)
                    DrawSmoke(painter, p, life);
            }
        }

        internal void DrawFront(PixelPainter painter)
        {
            foreach (var rock in rocks)
                if (rock.Life > 0) DrawRock(painter, rock);
            foreach (var p in particles)
            {
                if (p.Delay > 0) continue;
                double life = p.Life / p.MaxLife;
                switch (p.Kind)
                {
                    case Kind.Spark: DrawSpark(painter, p, life); break;
                    case Kind.Flake: painter.Dot(p.X, p.Y, p.Color, Min(1, life * 3)); break;
                    case Kind.Grit:
                        if (life <= 0.125) break;
                        painter.Dot(p.X, p.Y, p.Color);
                        if (p.Size < 2) break;
                        painter.Dot(p.X + 1, p.Y, p.Color);
                        painter.Dot(p.X, p.Y + 1, Scale(p.Color, 0.8));
                        painter.Dot(p.X + 1, p.Y + 1, Scale(p.Color, 0.7));
                        break;
                }
            }
        }

        private static void DrawSmoke(PixelPainter painter, Particle p, double life)
        {
            double age = 1 - life;
            double radius = p.Size * (0.65 + Min(1, age * 2.5) * 0.45 + age * 0.5);
            double alpha = Min(1, age * 12) * Min(1, life * 2.4) * 0.7;
            uint color = Mix(p.Color, SmokeRamp[Min(SmokeRamp.Length - 1, (int)(age * SmokeRamp.Length))], age);
            painter.SoftDisc(p.X, p.Y, radius, color, alpha * 0.45, 0);
            painter.SoftDisc(p.X + radius * 0.05, p.Y + radius * 0.06, radius * 0.74, color, alpha * 0.62, 0);
            if (age < 0.5)
                painter.SoftDisc(p.X - radius * 0.22, p.Y - radius * 0.3, radius * 0.5,
                    Mix(color, White, 0.12), alpha * 0.4 * (1 - age * 2), 0);
        }

        private void DrawSpark(PixelPainter painter, Particle p, double life)
        {
            double speed = Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
            if (speed == 0) speed = 1;
            double length = Min(9, 1 + speed * 0.022);
            double dx = p.Vx / speed * length, dy = p.Vy / speed * length;
            uint head = colors[life > 0.6 ? 6 : life > 0.3 ? 5 : 4];
            painter.Line(p.X - dx, p.Y - dy, p.X - dx * 0.4, p.Y - dy * 0.4, life > 0.5 ? head : colors[3]);
            painter.Line(p.X - dx * 0.45, p.Y - dy * 0.45, p.X, p.Y, head);
            painter.Glow(p.X, p.Y, 3, head, 0.45 * life);
        }

        private void DrawRock(PixelPainter painter, Rock rock)
        {
            double radius = 0.75 + rock.Size * 0.8, fade = Min(1, rock.Life / 0.5);
            double cos = Cos(rock.Angle), sin = Sin(rock.Angle);
            painter.Shade(rock.X - radius - 2, rock.Y - radius - 2, rock.X + radius + 2, rock.Y + radius + 2,
                (rock, radius, fade, cos, sin, stone), static (x, y, s) =>
                {
                    double dx = x - s.rock.X, dy = y - s.rock.Y;
                    double u = dx * s.cos + dy * s.sin, v = -dx * s.sin + dy * s.cos;
                    double distance = Sqrt(u * u + v * v), angle = Atan2(v, u);
                    if (angle < 0) angle += Tau;
                    var radii = s.rock.OutlineRadii;
                    double vertex = angle / Tau * radii.Length, fraction = vertex - Floor(vertex);
                    int i = (int)vertex % radii.Length;
                    double edge = s.radius * (radii[i] * (1 - fraction) + radii[(i + 1) % radii.Length] * fraction);
                    if (distance > edge + 0.85) return default;
                    if (distance > edge) return (Outline, s.fade);
                    double lit = Cos((i + 0.5) / radii.Length * Tau + s.rock.Angle - LightAngle);
                    if (distance < edge * 0.42) return (lit > 0.55 ? s.stone.Light : s.stone.Body, s.fade);
                    return (lit > 0.35 ? s.stone.Light : lit < -0.4 ? s.stone.Shade : s.stone.Body, s.fade);
                });
        }
    }
}
