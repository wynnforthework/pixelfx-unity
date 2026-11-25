using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class HealingRingEffect
    {
        private enum Kind { Streak, Mote, Star, Ember }
        private struct Particle
        {
            internal Kind Kind;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Seed;
            internal uint Ink;
            internal bool Back;
        }

        private List<Particle> particles;

        private void Advance(double sample)
        {
            int target = (int)Floor(sample * 120 + 0.00001);
            if (target < step)
            {
                time = 0;
                step = 0;
                releaseAge = double.PositiveInfinity;
                random = initialRandom;
                particles?.Clear();
            }
            while (step < target)
            {
                step++;
                double previous = time;
                time += Step;
                bool release = previous < Charge && time >= Charge;
                if (time >= Charge)
                    releaseAge = release ? Step : releaseAge + Step;
                if (!full) continue;

                particles ??= new List<Particle>(100);
                Emit(previous, release);
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
                        case Kind.Mote:
                            q.Vx -= q.Vx * 0.6 * Step;
                            q.Vy -= q.Vy * 0.6 * Step;
                            break;
                        case Kind.Star:
                            q.Vx -= q.Vx * 1.6 * Step;
                            q.Vy -= q.Vy * 1.6 * Step + 5 * Step;
                            break;
                        case Kind.Ember:
                            q.Vy += (-30 + Sin(time * 3 + q.Seed) * 20) * Step;
                            q.Vx += Cos(time * 2.3 + q.Seed) * 45 * Step;
                            q.Vx -= q.Vx * 1.6 * Step;
                            q.Vy -= q.Vy * 1.6 * Step;
                            break;
                    }
                    q.X += q.Vx * Step;
                    q.Y += q.Vy * Step;
                    particles[i] = q;
                }
            }
        }

        private bool Chance(double probability) => random.Range(0, 1) < probability;

        private void Spawn(Kind kind, double x, double y, double vx, double vy, double life, uint ink,
            double size = 1, bool back = false)
        {
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life,
                Ink = ink, Size = size, Seed = random.Range(0, 1) * 100, Back = back,
            });
        }

        private void Emit(double previous, bool release)
        {
            if (time < Charge - 0.04 && Chance(0.7))
            {
                double angle = random.Range(0, Tau), distance = random.Range(24, 48);
                double x = Cos(angle) * distance, y = Sin(angle) * distance * Squash - random.Range(1, 8);
                double life = random.Range(0.16, 0.24), length = Sqrt(x * x + (y + 3) * (y + 3));
                double velocity = length * 3 / (Exp(3 * life) - 1);
                Spawn(Kind.Streak, x, y, -x / length * velocity, (-3 - y) / length * velocity,
                    life, colors[Chance(0.5) ? 6 : 5]);
            }
            if (release)
            {
                for (int i = 0; i < 10; i++)
                    Spawn(Kind.Mote, random.Range(-2, 2), -random.Range(2, 10), random.Range(-25, 25),
                        -random.Range(80, 170), random.Range(0.3, 0.6), Chance(0.5) ? White : colors[6], random.Range(0.6, 1));
                for (int i = 0; i < 16; i++)
                {
                    double angle = random.Range(PI * 1.05, PI * 1.95), speed = random.Range(40, 120);
                    Spawn(Kind.Star, 0, -3, Cos(angle) * speed, Sin(angle) * speed, random.Range(0.4, 0.8),
                        Chance(0.5) ? White : colors[6], random.Range(0.6, 2));
                }
            }
            foreach (var site in sites)
            {
                if (previous >= site.Start || time < site.Start) continue;
                for (int i = 0; i < 3; i++)
                    Spawn(Kind.Star, site.X + random.Range(-3, 3), site.Z * Squash - 1, random.Range(-10, 10),
                        -random.Range(15, 40), random.Range(0.3, 0.6), Chance(0.5) ? White : colors[6],
                        random.Range(0.5, 1.2), site.Z < 0);
            }
            if (time > Charge + 0.1 && time < 1.9 && Chance(0.35))
            {
                double radius = Front(time), angle = random.Range(0, Tau), distance = radius * Sqrt(random.Range(0, 1));
                double sine = Sin(angle);
                Spawn(Kind.Ember, Cos(angle) * distance, sine * distance * Squash - 1, random.Range(-5, 5),
                    -random.Range(10, 30), random.Range(0.5, 1), colors[5], back: sine < 0);
            }
        }

        private void DrawParticles(bool back)
        {
            if (particles == null) return;
            foreach (var q in particles)
            {
                if (q.Back != back) continue;
                double k = q.Life / q.MaxLife, x = q.X, y = q.Y;
                switch (q.Kind)
                {
                    case Kind.Star:
                        double twinkle = 0.6 + 0.4 * Sin(time * (8 + q.Seed % 5) + q.Seed);
                        if (q.Size < 1)
                        {
                            if (twinkle * k > 0.25) painter.Dot(x, y, twinkle > 0.8 ? White : q.Ink);
                            painter.Glow(x, y, 3, q.Ink, 0.4 * k * twinkle);
                        }
                        else Sparkle(x, y, q.Size * twinkle * Min(1, k * 3), q.Ink, Scale(q.Ink, 0.75), 0.5 * k * twinkle);
                        break;
                    case Kind.Ember:
                        if (k < 0.25 && Sin(time * 40 + q.Seed) < 0) break;
                        uint ink = k > 0.6 ? White : colors[k > 0.3 ? 6 : 5];
                        painter.Dot(x, y, ink);
                        painter.Glow(x, y, 3 + q.Size, ink, 0.35 * k);
                        break;
                    default:
                        DrawTravelParticle(q, k);
                        break;
                }
            }
        }

        private void DrawTravelParticle(Particle q, double k)
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
            }
            else
            {
                double length = Min(16, 0.5 + speed * 0.026) * q.Size;
                double alpha = Min(1, k / 0.35);
                painter.Line(q.X - ux * length, q.Y - uy * length, q.X - ux * length * 0.35,
                    q.Y - uy * length * 0.35, Scale(q.Ink, 0.55), alpha);
                painter.Line(q.X - ux * length * 0.4, q.Y - uy * length * 0.4, q.X, q.Y, q.Ink, alpha);
                painter.Glow(q.X, q.Y, 2.5 + q.Size, q.Ink, 0.4 * k);
            }
        }
    }
}
