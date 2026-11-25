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

        private struct Feather
        {
            internal double X, Y, Vx, Vy, Angle, Length, Width, Life, MaxLife, Sink, Delay;
            internal double Side, Phase, Frequency, Amplitude, Spin, Tilt, Age;
            internal bool Back, Landed;
        }

        private readonly Feather[] initialFeathers = new Feather[6], feathers = new Feather[6];
        private readonly List<Particle> particles = new List<Particle>(96);
        private double hitAge = double.PositiveInfinity, shutAge = double.PositiveInfinity, liftAge = double.PositiveInfinity;

        private void CreateFeathers()
        {
            for (int i = 0; i < feathers.Length; i++)
            {
                var f = new Feather
                {
                    X = random.Range(-46, 46), Y = random.Range(-150, -100),
                    Vx = random.Range(-8, 8), Vy = random.Range(0, 8), Angle = random.Range(-0.4, 0.4),
                    Length = random.Range(7, 9), Width = random.Range(1.9, 2.3), Life = random.Range(1.4, 1.7),
                    Sink = random.Range(26, 36), Back = i % 3 == 0, Delay = Hit + 0.1 + i * 0.16 + random.Range(0, 0.08),
                    Side = Chance(0.5) ? -1 : 1, Phase = random.Range(0, Tau), Frequency = random.Range(3.2, 4.6),
                    Amplitude = random.Range(6, 11),
                };
                // Upstream Feathers.add samples a default sink before applying the authored value.
                random.Range(14, 24);
                f.Spin = random.Range(-9, 9);
                f.Tilt = random.Range(-0.35, 0.35);
                f.MaxLife = f.Life;
                initialFeathers[i] = feathers[i] = f;
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
            for (int i = 0; i < feathers.Length; i++)
            {
                ref var f = ref feathers[i];
                if (f.Delay > 0)
                {
                    f.Delay -= Step;
                    continue;
                }
                if (f.Life <= 0 || (f.Life -= Step) <= 0) continue;
                f.Age += Step;
                double flat = Floor(f.Angle / PI + 0.5) * PI;
                if (f.Landed)
                {
                    f.Angle += (flat - f.Angle) * Min(1, Step * 10);
                    continue;
                }

                f.Vx -= f.Vx * 3.4 * Step;
                f.Vy -= f.Vy * 3.4 * Step;
                double drift = Smooth(0.1, 0.6, f.Age), phase = f.Age * f.Frequency + f.Phase;
                double sine = Sin(phase), cosine = Cos(phase);
                f.X += (f.Vx + sine * f.Amplitude * drift * 0.9) * Step;
                f.Y += (f.Vy + f.Sink * drift * (cosine * cosine * 1.5 - 0.2)) * Step;
                double rest = flat + f.Tilt + sine * 0.6;
                f.Angle += f.Spin * Step * (1 - drift) + (rest - f.Angle) * Min(1, Step * 5 * drift);
                if (f.Y >= -0.5)
                {
                    f.Y = -0.5;
                    f.Landed = true;
                    f.Life = Min(f.Life, 0.6);
                }
            }
        }

        private void DrawParticles(bool back)
        {
            foreach (var f in feathers)
                if (f.Delay <= 0 && f.Life > 0 && f.Back == back) DrawFeather(f);

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

        private void DrawFeather(Feather f)
        {
            double alpha = Min(1, Min(f.Life / 0.45, f.Age / 0.06));
            if (alpha > 0.02)
            {
                double ca = Cos(f.Angle), sa = Sin(f.Angle);
                double x = f.X - ca * f.Length / 2, y = f.Y - sa * f.Length / 2;
                double endX = x + ca * f.Length, endY = y + sa * f.Length, pad = f.Width + 1.2;
                bool hot = f.Age < 0.06;
                uint edge = colors[hot ? 5 : 3], lead = hot ? White : colors[6];
                uint first = hot ? White : colors[6], second = hot ? White : colors[5], third = colors[hot ? 6 : 4];

                for (int py = (int)Floor(Min(y, endY) - pad); py <= Ceiling(Max(y, endY) + pad); py++)
                for (int px = (int)Floor(Min(x, endX) - pad); px <= Ceiling(Max(x, endX) + pad); px++)
                {
                    double dx = px + 0.5 - x, dy = py + 0.5 - y, u = (dx * ca + dy * sa) / f.Length;
                    if (u < 0 || u > 1.05) continue;
                    double v = (-dx * sa + dy * ca) * f.Side, av = Abs(v);
                    double width = f.Width * Sqrt(Clamp((u - 0.2) / 0.22))
                        * Sqrt(Max(0, 1 - Pow(Max(0, (u - 0.55) / 0.47), 2)));
                    double limit = v < 0 ? width * 0.42 : width, fade = u < 0.72 ? 1 : 1 - (u - 0.72) * 1.6;
                    if (av < 0.5 && u < 0.9) painter.Dot(px, py, u < 0.5 ? White : first, alpha);
                    else if (av > limit + 0.05 || width < 0.5) continue;
                    else if (limit - av < 1)
                    {
                        if (v > 0) painter.Dot(px, py, u > 0.85 ? third : edge, alpha * fade);
                        else painter.Dot(px, py, lead, alpha);
                    }
                    else if (u < 0.72) painter.Dot(px, py, u < 0.38 ? first : second, alpha);
                    else painter.BlendPixel(px, py, third, alpha * fade);
                }
            }
            painter.Glow(f.X, f.Y, 4 + f.Length * 0.5, colors[5], 0.22 * alpha * (0.4 + 0.6 * f.Life / f.MaxLife));
        }
    }
}
