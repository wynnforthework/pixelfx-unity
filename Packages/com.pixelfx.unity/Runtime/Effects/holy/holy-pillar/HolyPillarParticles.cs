using System;
using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class HolyPillarEffect
    {
        private enum Kind { Streak, Spark, Ember, Star }

        private struct Particle
        {
            internal Kind Kind;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Seed;
            internal uint Ink;
            internal bool Back;
        }

        private readonly FeatherParticle[] initialFeathers = new FeatherParticle[6], feathers = new FeatherParticle[6];
        private readonly List<Particle> particles = new List<Particle>(96);
        private double hitAge = double.PositiveInfinity, shutAge = double.PositiveInfinity, liftAge = double.PositiveInfinity;

        private void CreateFeathers()
        {
            for (int i = 0; i < feathers.Length; i++)
            {
                var f = new FeatherParticle
                {
                    X = random.Range(-46, 46), Y = random.Range(-150, -100),
                    Vx = random.Range(-8, 8), Vy = random.Range(0, 8), Angle = random.Range(-0.4, 0.4),
                    Length = random.Range(7, 9), Width = random.Range(1.9, 2.3), Life = random.Range(1.4, 1.7),
                    Sink = random.Range(26, 36), Back = i % 3 == 0, Delay = Hit + 0.1 + i * 0.16 + random.Range(0, 0.08),
                };
                initialFeathers[i] = feathers[i] = FeatherParticle.Initialize(f, ref random);
            }
        }

        private void Advance(double sample)
        {
            int target = (int)Floor(sample * 120 + 0.00001);
            if (target < tick)
            {
                time = 0;
                tick = 0;
                hitAge = shutAge = liftAge = double.PositiveInfinity;
                random = initialRandom;
                particles.Clear();
                Array.Copy(initialFeathers, feathers, feathers.Length);
            }

            while (tick < target)
            {
                tick++;
                double previous = time;
                time += Step;
                bool hit = previous < Hit && time >= Hit, shut = previous < Shut && time >= Shut;
                hitAge = hit ? Step : hitAge + Step;
                shutAge = shut ? Step : shutAge + Step;
                liftAge = previous < Lift && time >= Lift ? Step : liftAge + Step;
                if (!full) continue;

                Emit(hit, shut);
                TickParticles();
                TickFeathers();
            }
        }

        private bool Chance(double value) => random.Range(0, 1) < value;

        private void Spawn(Kind kind, double x, double y, double vx, double vy, double life,
            uint ink = White, double size = 1, bool back = false)
        {
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life,
                Ink = ink, Size = size, Seed = random.Range(0, 1) * 100, Back = back,
            });
        }

        private void Emit(bool hit, bool shut)
        {
            if (time < Open + 0.1 && Chance(0.5))
            {
                double angle = random.Range(0, Tau), distance = random.Range(22, 34);
                double x = Cos(angle) * distance, y = SunY + Sin(angle) * distance;
                double life = random.Range(0.18, 0.28), speed = (distance - 10) * 3 / (Exp(3 * life) - 1);
                Spawn(Kind.Streak, x, y, -x / distance * speed, (SunY - y) / distance * speed,
                    life, colors[Chance(0.5) ? 6 : 5]);
            }
            if (hit)
                for (int i = 0; i < 26; i++)
                {
                    double angle = -PI / 2 + random.Range(-1.2, 1.2), speed = random.Range(70, 190);
                    Spawn(Kind.Spark, random.Range(-3, 3), -2, Cos(angle) * speed, Sin(angle) * speed, random.Range(0.25, 0.6));
                }
            if (time > Hit && time < Shut + 0.1 && Chance(0.35))
            {
                double angle = random.Range(0, Tau), radius = Radius * (Chance(0.5) ? 1 : 0.72);
                double y = Sin(angle);
                Spawn(Kind.Ember, Cos(angle) * radius, y * radius * Squash - 1, random.Range(-6, 6),
                    -random.Range(12, 30), random.Range(0.5, 0.8), colors[5], back: y < 0);
            }
            if (time > Hit && time < Fold && Chance(0.05))
                Spawn(Kind.Star, random.Range(-1, 1) * 40, -random.Range(20, 160), 0, random.Range(2, 8),
                    random.Range(0.3, 0.6), Chance(0.5) ? White : colors[6], random.Range(0.8, 1.8));
            if (shut)
                for (int i = 0; i < 12; i++)
                    Spawn(Kind.Star, random.Range(-4, 4), -random.Range(10, 170), random.Range(-25, 25),
                        random.Range(-10, 10), random.Range(0.4, 0.8), Chance(0.5) ? White : colors[6], random.Range(0.8, 2));
        }

        private void TickParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var q = particles[i];
                q.Life -= Step;
                if (q.Life <= 0)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                switch (q.Kind)
                {
                    case Kind.Streak:
                        q.Vx += q.Vx * 3 * Step;
                        q.Vy += q.Vy * 3 * Step;
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
                    case Kind.Star:
                        q.Vx -= q.Vx * 1.6 * Step;
                        q.Vy -= q.Vy * 1.6 * Step + 5 * Step;
                        break;
                }
                q.X += q.Vx * Step;
                double nextY = q.Y + q.Vy * Step;
                if (q.Kind == Kind.Spark && q.Vy > 0 && q.Y <= 0 && nextY >= 0)
                {
                    q.Vy *= -0.45;
                    q.Vx *= 0.7;
                    q.Y = -0.25;
                }
                else q.Y = nextY;
                particles[i] = q;
            }
        }

        private void TickFeathers()
        {
            for (int i = 0; i < feathers.Length; i++) FeatherParticle.Tick(ref feathers[i], Step, 0);
        }

        private void DrawParticles(bool back)
        {
            foreach (var f in feathers)
                if (f.Delay <= 0 && f.Life > 0 && f.Back == back) FeatherParticle.Draw(painter, f, colors, 2);

            foreach (var q in particles)
            {
                if (q.Back != back) continue;
                double k = q.Life / q.MaxLife, x = q.X, y = q.Y;
                switch (q.Kind)
                {
                    case Kind.Streak:
                    case Kind.Spark:
                        DrawTrail(q, k);
                        break;
                    case Kind.Ember:
                        if (k < 0.25 && Sin(time * 40 + q.Seed) < 0) break;
                        uint ink = k > 0.6 ? White : colors[k > 0.3 ? 6 : 5];
                        painter.Dot(x, y, ink);
                        painter.Glow(x, y, 3 + q.Size, ink, 0.35 * k);
                        break;
                    case Kind.Star:
                        double twinkle = 0.6 + 0.4 * Sin(time * (8 + q.Seed % 5) + q.Seed);
                        if (q.Size < 1)
                        {
                            if (twinkle * k > 0.25) painter.Dot(x, y, twinkle > 0.8 ? White : q.Ink);
                            painter.Glow(x, y, 3, q.Ink, 0.4 * k * twinkle);
                        }
                        else Sparkle(x, y, q.Size * twinkle * Min(1, k * 3), q.Ink, Scale(q.Ink, 0.75), 0.5 * k * twinkle);
                        break;
                }
            }
        }

        private void DrawTrail(Particle q, double k)
        {
            double speed = Sqrt(q.Vx * q.Vx + q.Vy * q.Vy);
            if (speed == 0) speed = 1;
            double x = q.X, y = q.Y, ux = q.Vx / speed, uy = q.Vy / speed;
            if (q.Kind == Kind.Streak)
            {
                double length = Max(1, Min(7, speed / 30));
                for (double d = length; d >= 0; d -= 0.5)
                    painter.Dot(x - ux * d, y - uy * d, d < 1 ? White : d < length * 0.5 ? q.Ink : Scale(q.Ink, 0.6),
                        d < length * 0.5 ? 1 : 1.2 - d / length);
                painter.Glow(x, y, 4, q.Ink, 0.4);
            }
            else
            {
                uint head = k > 0.6 ? White : colors[k > 0.3 ? 6 : 5];
                double length = Min(9, 1 + speed * 0.022);
                painter.Line(x - ux * length, y - uy * length, x - ux * length * 0.4, y - uy * length * 0.4,
                    k > 0.5 ? head : colors[3]);
                painter.Line(x - ux * length * 0.45, y - uy * length * 0.45, x, y, head);
                painter.Glow(x, y, 3, head, 0.45 * k);
            }
        }
    }
}
