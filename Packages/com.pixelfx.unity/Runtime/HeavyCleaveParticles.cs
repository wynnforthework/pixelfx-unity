using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class HeavyCleaveEffect
    {
        private enum Kind { Streak, Ember, Spark, Smoke, Dust, Grit }
        private struct Particle
        {
            internal Kind Kind;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Delay, Seed;
            internal uint Color;
        }
        private struct Rock
        {
            internal double X, Y, Vx, Vy, Life, Angle, Spin, Size;
            internal bool Rest;
        }
        private readonly List<Particle> particles = new List<Particle>(192);
        private readonly List<Rock> rocks = new List<Rock>(6);
        private static readonly uint[] SmokeRamp =
        {
            Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e),
        };

        private void Spawn(Kind kind, double x, double y, double vx, double vy,
            double life, uint color = White, double size = 1, double delay = 0)
        {
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life,
                MaxLife = life, Color = color, Size = size, Delay = delay, Seed = Next() * 100,
            });
        }

        private void SpawnImpact()
        {
            for (int i = 0; i < RoundJs(26 * sparkScale); i++)
            {
                double a = -PI / 2 + Range(-1.2, 1.35), speed = Range(140, 360);
                Spawn(Kind.Spark, impactX, -1, Cos(a) * speed, Sin(a) * speed - 40, Range(0.15, 0.5));
            }
            for (int i = 0; i < 20; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                Spawn(Kind.Smoke, impactX + side * Range(0, 8), -Range(2, 6), side * Range(45, 135),
                    -Range(2, 12), Range(1, 1.45), dust, Range(5, 8.5), Range(0, 0.07));
            }
            for (int i = 0; i < 4; i++)
                Spawn(Kind.Smoke, impactX + Range(-4, 4), -Range(3, 8), Range(-15, 15),
                    -Range(40, 90), Range(1, 1.45), dust, Range(4, 6.5), Range(0.02, 0.1));
            for (int i = 0; i < 8; i++)
                Spawn(Kind.Dust, impactX + Range(-4, 4), -Range(1, 4), Range(-60, 60),
                    -Range(8, 30), Range(0.35, 0.7), dust, Range(2, 3.5));
            for (int i = 0; i < 44; i++)
            {
                double a = -PI / 2 + Range(-0.7, 1), speed = Range(60, 230);
                double x = impactX + Range(-3, 3), life = Range(1, 1.5);
                int shade = (int)Range(0, 4);
                Spawn(Kind.Grit, x, -1, Cos(a) * speed, Sin(a) * speed, life,
                    shade == 0 ? stoneLight : shade == 3 ? stoneShade : stone, Chance(0.35) ? 2 : 1);
            }
            for (int i = 0; i < 6; i++)
                rocks.Add(new Rock
                {
                    X = impactX + Range(-3, 3), Y = -2, Vx = Range(-50, 110), Vy = -Range(120, 260),
                    Life = Range(1.15, 1.5), Angle = Range(0, Tau), Spin = Range(-14, 14), Size = (int)Range(1, 3),
                });
            for (int i = 0; i < 16; i++)
                Spawn(Kind.Ember, impactX + Range(-8, 8), -Range(1, 6), Range(-50, 60),
                    -Range(30, 110), Range(0.7, 1.5), palette[4], Range(1, 2));
        }

        private void UpdateParticles(bool impact)
        {
            if (time < Raise - 0.04 && Chance(0.35))
            {
                double a = Range(0, Tau), distance = Range(14, 26), life = Range(0.14, 0.2);
                double speed = distance * 3 / (Exp(3 * life) - 1);
                Spawn(Kind.Streak, glintX + Cos(a) * distance, glintY + Sin(a) * distance,
                    -Cos(a) * speed, -Sin(a) * speed, life, Chance(0.5) ? palette[5] : palette[4]);
            }
            if (time > Raise && time < ImpactTime && Chance(0.7))
            {
                double u = span * Head(time) * Range(0.6, 1);
                var point = OnSwing(Radius - Range(0, 6), u);
                Spawn(Kind.Ember, point.X, point.Y, Range(-20, 20), Range(-40, -10),
                    Range(0.5, 1.1), palette[4], Range(1, 2));
            }
            if (impact) SpawnImpact();
            if (time >= ImpactTime && time < ImpactTime + 0.6 && Chance(0.12))
            {
                var crack = cracks[(int)Range(0, cracks.Count)];
                var point = crack.Points[(int)Range(0, crack.Points.Count)];
                Spawn(Kind.Ember, point.X, point.Y - 1, Range(-12, 12), -Range(20, 50), Range(0.5, 0.9), palette[4]);
            }
            UpdateRocks();
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                if (p.Delay > 0)
                {
                    p.Delay -= Step;
                    particles[i] = p;
                    continue;
                }
                p.Life -= Step;
                if (p.Life <= 0)
                {
                    particles.RemoveAt(i);
                    continue;
                }
                switch (p.Kind)
                {
                    case Kind.Streak:
                        p.Vx += p.Vx * 3 * Step;
                        p.Vy += p.Vy * 3 * Step;
                        break;
                    case Kind.Ember:
                        p.Vy += (-30 + Sin(time * 3 + p.Seed) * 20) * Step;
                        p.Vx += Cos(time * 2.3 + p.Seed) * 45 * Step;
                        p.Vx -= p.Vx * 1.6 * Step;
                        p.Vy -= p.Vy * 1.6 * Step;
                        break;
                    case Kind.Smoke:
                        p.Vy -= 23 * Step;
                        p.Vx += Sin(time * 1.3 + p.Seed) * 8 * Step;
                        p.Vx -= p.Vx * 1.6 * Step;
                        p.Vy -= p.Vy * 1.2 * Step;
                        break;
                    case Kind.Dust:
                        p.Vx -= p.Vx * 4 * Step;
                        p.Vy -= p.Vy * 4 * Step + 5 * Step;
                        break;
                    case Kind.Spark:
                        p.Vy += 950 * 0.45 * Step;
                        p.Vx -= p.Vx * 2.2 * Step;
                        p.Vy -= p.Vy * 2.2 * Step;
                        break;
                    case Kind.Grit:
                        p.Vy += 950 * Step;
                        p.Vx -= p.Vx * 0.8 * Step;
                        break;
                }
                p.X += p.Vx * Step;
                double nextY = p.Y + p.Vy * Step;
                if ((p.Kind == Kind.Spark || p.Kind == Kind.Grit) && p.Y <= 0 && nextY >= 0 && p.Vy > 0)
                {
                    nextY = -0.25;
                    if (p.Kind == Kind.Spark)
                    {
                        p.Vy *= -0.45;
                        p.Vx *= 0.7;
                    }
                    else if (p.Vy > 130 && Chance(0.4))
                    {
                        p.Vy *= -0.25;
                        p.Vx = p.Vx * 0.5 + Range(-10, 10);
                    }
                    else
                    {
                        p.Vx = p.Vy = 0;
                        p.Life = Min(p.Life, 0.5);
                    }
                }
                p.Y = nextY;
                particles[i] = p;
            }
        }

        private void UpdateRocks()
        {
            int landed = 0;
            for (int i = rocks.Count - 1; i >= 0; i--)
            {
                var r = rocks[i];
                r.Life -= Step;
                if (r.Life <= 0)
                {
                    rocks.RemoveAt(i);
                    landed = (landed & ((1 << i) - 1)) | ((landed >> (i + 1)) << i);
                    continue;
                }
                if (!r.Rest)
                {
                    r.Vy += 950 * Step;
                    r.Vx -= r.Vx * 0.4 * Step;
                    r.Vy -= r.Vy * 0.4 * Step;
                    r.X += r.Vx * Step;
                    r.Y += r.Vy * Step;
                    r.Angle += r.Spin * Step;
                    if (r.Y >= 0 && r.Vy > 0)
                    {
                        r.Y = -0.01;
                        if (r.Vy > 70)
                        {
                            r.Vy *= -0.3;
                            r.Vx *= 0.7;
                            r.Spin *= 0.6;
                        }
                        else
                        {
                            r.Rest = true;
                            landed |= 1 << i;
                        }
                    }
                }
                rocks[i] = r;
            }
            for (int i = 0; i < rocks.Count; i++)
            {
                if ((landed & (1 << i)) == 0) continue;
                for (int j = 0; j < 2; j++)
                    Spawn(Kind.Dust, rocks[i].X + Range(-1.5, 1.5), -0.5, (Chance(0.5) ? -1 : 1) * Range(8, 30),
                        -Range(2, 12), Range(0.3, 0.6), dust, Range(1.3, 2.4));
            }
        }

        private void DrawParticles(bool back)
        {
            foreach (var p in particles)
            {
                if (p.Delay > 0 || (p.Kind == Kind.Smoke || p.Kind == Kind.Dust) != back) continue;
                double k = p.Life / p.MaxLife;
                uint hot = palette[k > 0.6 ? 6 : k > 0.3 ? 5 : 4];
                switch (p.Kind)
                {
                    case Kind.Spark:
                    case Kind.Streak:
                        DrawStreak(p, k, hot);
                        break;
                    case Kind.Ember:
                        if (k < 0.25 && Sin(time * 40 + p.Seed) < 0) break;
                        painter.Dot(p.X, p.Y, hot);
                        if (p.Size > 1.5) painter.Dot(p.X, p.Y + 1, palette[3]);
                        painter.Glow(p.X, p.Y, 3 + p.Size, hot, 0.35 * k);
                        break;
                    case Kind.Grit:
                        if (k <= 0.125) break;
                        painter.Dot(p.X, p.Y, p.Color);
                        if (p.Size < 2) break;
                        painter.Dot(p.X + 1, p.Y, p.Color);
                        painter.Dot(p.X, p.Y + 1, Scale(p.Color, 0.8));
                        painter.Dot(p.X + 1, p.Y + 1, Scale(p.Color, 0.7));
                        break;
                    case Kind.Dust:
                        painter.Disc(p.X, p.Y, p.Size * (0.6 + (1 - k) * 0.9), p.Color, Pow(k, 1.3) * 0.7);
                        break;
                    case Kind.Smoke:
                        double age = 1 - k;
                        double r = p.Size * (0.65 + Min(1, age * 2.5) * 0.45 + age * 0.5);
                        double a = Min(1, age * 12) * Min(1, k * 2.4) * 0.7;
                        uint c = Mix(p.Color, SmokeRamp[Min(4, (int)(age * 5))], age);
                        painter.Disc(p.X, p.Y, r, c, a * 0.45);
                        painter.Disc(p.X + r * 0.05, p.Y + r * 0.06, r * 0.74, c, a * 0.62);
                        if (age < 0.5)
                            painter.Disc(p.X - r * 0.22, p.Y - r * 0.3, r * 0.5, Mix(c, White, 0.12), a * 0.4 * (1 - age * 2));
                        break;
                }
            }
        }

        private void DrawStreak(Particle p, double k, uint hot)
        {
            double speed = Length(p.Vx, p.Vy);
            double ux = speed > 0 ? p.Vx / speed : 0, uy = speed > 0 ? p.Vy / speed : 0;
            if (p.Kind == Kind.Spark)
            {
                double len = Min(9, 1 + speed * 0.022);
                painter.Line(p.X - ux * len, p.Y - uy * len, p.X - ux * len * 0.4, p.Y - uy * len * 0.4, k > 0.5 ? hot : palette[3]);
                painter.Line(p.X - ux * len * 0.45, p.Y - uy * len * 0.45, p.X, p.Y, hot);
                painter.Glow(p.X, p.Y, 3, hot, 0.45 * k);
                return;
            }
            double length = Max(1, Min(7, speed / 30));
            for (double d = length; d >= 0; d -= 0.5)
                painter.Dot(p.X - ux * d, p.Y - uy * d,
                    d < 1 ? White : d < length * 0.5 ? p.Color : Scale(p.Color, 0.6),
                    d < length * 0.5 ? 1 : 1.2 - d / length);
            painter.Glow(p.X, p.Y, 4, p.Color, 0.4);
        }

        private void DrawRocks()
        {
            foreach (var rock in rocks)
            {
                double r = 0.6 + rock.Size * 0.75, fade = Min(1, rock.Life / 0.5);
                double lx = Cos(rock.Angle) * r * 0.4, ly = Sin(rock.Angle) * r * 0.4;
                painter.Shade(rock.X - r - 1, rock.Y - r - 1, rock.X + r + 1, rock.Y + r + 1, (x, y) =>
                {
                    double dx = x - rock.X, dy = y - rock.Y, d = Length(dx * 1.15, dy);
                    if (d > r + 0.9) return default;
                    double lit = dx * lx + dy * ly;
                    return (d > r ? Hex(0x1a1422) : lit < -0.4 ? stoneLight : lit > 0.6 ? stoneShade : stone, fade);
                });
            }
        }
    }
}
