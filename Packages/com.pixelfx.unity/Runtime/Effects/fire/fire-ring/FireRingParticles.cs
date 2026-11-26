using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class FireRingEffect
    {
        private const double Step = ImpactParticles.Step;
        private static readonly uint[] SmokeRamp =
            { Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e) };

        private enum Kind { Streak, Flame, Spark, Ember, Smoke }

        private struct Particle
        {
            internal Kind Kind;
            internal double X, Y, Vx, Vy, Life, Duration, Size, Seed;
            internal uint Ink;
            internal bool Back, Smokes;
        }

        private readonly List<Particle> particles = new List<Particle>(256);
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private int steps;
        private bool burst;
        private double warmLife, whiteLife;

        private void Advance(double sample)
        {
            int target = (int)Floor(sample * 120 + 1e-8);
            if (target < steps)
            {
                random = initialRandom;
                particles.Clear();
                steps = 0;
                time = warmLife = whiteLife = 0;
                burst = false;
            }
            while (steps < target)
            {
                steps++;
                time += Step;
                bool flash = !burst && time >= Charge;
                if (flash)
                {
                    burst = true;
                    warmLife = 0.5;
                    whiteLife = 0.08;
                }
                if (full)
                {
                    Emit(flash);
                    UpdateParticles();
                }
                warmLife = Max(0, warmLife - Step);
                whiteLife = Max(0, whiteLife - Step);
            }
        }

        private void Emit(bool flash)
        {
            if (time < Charge - 0.05 && Chance(0.9 * Step * 60))
            {
                double a = random.Range(0, Tau), distance = random.Range(26, 54), life = random.Range(0.18, 0.26);
                double speed = distance * 3 / (Exp(3 * life) - 1);
                double x = Cos(a) * distance, y = Min(-2, -6 + Sin(a) * distance * 0.45);
                double length = Sqrt(x * x + (y + 4) * (y + 4));
                if (length == 0) length = 1;
                Spawn(Kind.Streak, x, y, -x / length * speed, (-4 - y) / length * speed,
                    life, Chance(0.5) ? heat[2] : heat[3]);
            }
            if (time < Charge && Chance(0.5 * Step * 60 * (time / Charge)))
                Spawn(Kind.Flame, random.Range(-4, 4), -1, random.Range(-8, 8), -random.Range(20, 40),
                    random.Range(0.12, 0.24), size: random.Range(1, 2.2));
            if (flash)
                for (int i = 0; i < 40; i++)
                {
                    double a = random.Range(0, Tau), speed = random.Range(80, 220);
                    Spawn(Kind.Spark, Cos(a) * 3, -2, Cos(a) * speed, Sin(a) * speed * 0.4 - random.Range(40, 120),
                        random.Range(0.2, 0.5));
                }
            if (time <= Charge || time >= Charge + Spread + 1.2) return;
            for (int i = 0; i < 3; i++)
            {
                if (!Chance(0.75 * Step * 60)) continue;
                var site = sites[(int)random.Range(0, sites.Length)];
                double age = time - site.Start;
                if (age < 0 || age > site.Life * 0.7) continue;
                Spawn(Kind.Flame, site.X + random.Range(-1, 1), site.Y - site.Height * 0.6,
                    random.Range(-5, 5), -random.Range(20, 40), random.Range(0.2, 0.4),
                    size: random.Range(1.2, 2.4), back: site.Back, smokes: Chance(0.5));
                if (Chance(0.12))
                    Spawn(Kind.Ember, site.X, site.Y - site.Height * 0.5, random.Range(-15, 15), -random.Range(30, 70),
                        random.Range(0.6, 1.3), heat[3], back: site.Back);
            }
        }

        private bool Chance(double probability) => random.Range(0, 1) < probability;

        private void Spawn(Kind kind, double x, double y, double vx, double vy, double life,
            uint ink = 0, double size = 1, bool back = false, bool smokes = false)
        {
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life, Duration = life,
                Ink = ink, Size = size, Back = back, Smokes = smokes, Seed = random.Range(0, 1) * 100,
            });
        }

        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var q = particles[i];
                q.Life -= Step;
                if (q.Life <= 0)
                {
                    if (q.Smokes)
                        Spawn(Kind.Smoke, q.X, q.Y, q.Vx * 0.6, q.Vy * 0.6 - 6,
                            random.Range(0.8, 1.4), size: q.Size * 0.7, back: true);
                    particles.RemoveAt(i);
                    continue;
                }
                switch (q.Kind)
                {
                    case Kind.Streak:
                        q.Vx += q.Vx * 3 * Step;
                        q.Vy += q.Vy * 3 * Step;
                        break;
                    case Kind.Flame:
                        q.Vy -= 95 * Step;
                        q.Vx += Sin(time * 17 + q.Seed) * 30 * Step;
                        q.Vx -= q.Vx * 5 * Step;
                        q.Vy -= q.Vy * 3.2 * Step;
                        break;
                    case Kind.Spark:
                        q.Vy += 950 * 0.45 * Step;
                        q.Vx -= q.Vx * 2.2 * Step;
                        q.Vy -= q.Vy * 2.2 * Step;
                        break;
                    case Kind.Ember:
                        q.Vy += (-30 + Sin(time * 3 + q.Seed) * 20) * Step;
                        q.Vx += Cos(time * 2.3 + q.Seed) * 45 * Step;
                        q.Vx -= q.Vx * 1.6 * Step;
                        q.Vy -= q.Vy * 1.6 * Step;
                        break;
                    case Kind.Smoke:
                        q.Vy -= 16 * Step;
                        q.Vx += Sin(time * 1.3 + q.Seed) * 8 * Step;
                        q.Vx -= q.Vx * 1.6 * Step;
                        q.Vy -= q.Vy * 1.2 * Step;
                        break;
                }
                q.X += q.Vx * Step;
                double nextY = q.Y + q.Vy * Step;
                if (q.Kind == Kind.Spark && q.Vy > 0 && q.Y <= 0 && nextY >= 0)
                {
                    q.Vy = -q.Vy * 0.45;
                    q.Vx *= 0.7;
                    nextY = -0.25;
                }
                q.Y = nextY;
                particles[i] = q;
            }
        }

        private void DrawParticles(bool back)
        {
            if (back) DrawParticleGroup(true, false);
            DrawParticleGroup(back, true);
            if (!back) DrawParticleGroup(false, false);
        }

        private void DrawParticleGroup(bool back, bool flames)
        {
            foreach (var q in particles)
            {
                if (q.Back != back || (q.Kind == Kind.Flame) != flames) continue;
                double k = q.Life / q.Duration, x = q.X, y = q.Y;
                uint hot = k > 0.6 ? heat[1] : k > 0.3 ? heat[2] : heat[4];
                switch (q.Kind)
                {
                    case Kind.Flame:
                        FireDrawing.Puff(painter, x, y, k, q.Size, q.Seed, heat);
                        break;
                    case Kind.Smoke:
                        DrawSmoke(q, k);
                        break;
                    case Kind.Spark:
                    case Kind.Streak:
                        DrawStreak(q, k, hot);
                        break;
                    case Kind.Ember:
                        if (k < 0.25 && Sin(time * 40 + q.Seed) < 0) break;
                        painter.Dot(x, y, hot);
                        painter.Glow(x, y, 3 + q.Size, hot, 0.35 * k);
                        break;
                }
            }
        }

        private void DrawStreak(Particle q, double k, uint hot)
        {
            double speed = Sqrt(q.Vx * q.Vx + q.Vy * q.Vy);
            if (speed == 0) speed = 1;
            double ux = q.Vx / speed, uy = q.Vy / speed;
            if (q.Kind == Kind.Streak)
            {
                double length = Max(1, Min(7, speed / 30));
                for (double d = length; d >= 0; d -= 0.5)
                    painter.Dot(q.X - ux * d, q.Y - uy * d, d < 1 ? White : d < length * 0.5 ? q.Ink : Scale(q.Ink, 0.6),
                        d < length * 0.5 ? 1 : 1.2 - d / length);
                painter.Glow(q.X, q.Y, 4, q.Ink, 0.4);
                return;
            }
            double span = Min(9, 1 + speed * 0.022), dx = ux * span, dy = uy * span;
            painter.Line(q.X - dx, q.Y - dy, q.X - dx * 0.4, q.Y - dy * 0.4, k > 0.5 ? hot : heat[5]);
            painter.Line(q.X - dx * 0.45, q.Y - dy * 0.45, q.X, q.Y, hot);
            painter.Glow(q.X, q.Y, 3, hot, 0.45 * k);
        }

        private void DrawSmoke(Particle q, double k)
        {
            double age = 1 - k, r = q.Size * (0.65 + Min(1, age * 2.5) * 0.45 + age * 0.4);
            double alpha = Min(1, age * 12) * Min(1, k * 2.4) * 0.58;
            uint color = SmokeRamp[Min(4, (int)Floor(age * 1.05 * SmokeRamp.Length))];
            painter.SoftDisc(q.X, q.Y, r, color, alpha * 0.45, 0);
            painter.SoftDisc(q.X + r * 0.05, q.Y + r * 0.06, r * 0.74, color, alpha * 0.62, 0);
            if (age < 0.5)
                painter.SoftDisc(q.X - r * 0.22, q.Y - r * 0.3, r * 0.5, Mix(color, White, 0.12), alpha * 0.4 * (1 - age * 2), 0);
        }
    }
}
