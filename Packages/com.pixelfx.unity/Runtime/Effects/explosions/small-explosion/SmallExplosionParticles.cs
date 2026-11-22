using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class SmallExplosionParticles
    {
        private const double Step = ImpactParticles.Step, Tau = 2 * PI, Gravity = 950;
        private enum Kind { Fire, Smoke, Spark, Ember, Brand, Grit, Dust }

        [System.Flags]
        private enum Flags { None = 0, Smokes = 1, Big = 2, Back = 4 }

        private struct Particle
        {
            internal Kind Kind;
            internal Flags Flags;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Delay, Seed;
            internal uint Color;
        }

        private struct Rock
        {
            internal double X, Y, Vx, Vy, Life, Angle, Spin, Size;
            internal bool Rest, Dusted;
        }

        private struct Blaze
        {
            internal double X, Life, MaxLife, Accumulator, Power;
        }

        private readonly List<Particle> particles = new List<Particle>(512);
        private readonly List<Blaze> blazes = new List<Blaze>(12);
        private readonly Particle[] initialParticles;
        private readonly Rock[] initialRocks, rocks;
        private readonly uint[] colors;
        private readonly SmallExplosionEffect.Stone stone;
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private int steps;
        private double time;

        internal SmallExplosionParticles(int seed, uint[] colors, SmallExplosionEffect.Stone stone)
        {
            random = new PixelFxRandom(seed);
            this.colors = colors;
            this.stone = stone;
            initialRocks = new Rock[26];
            rocks = new Rock[initialRocks.Length];
            Seed();
            initialParticles = particles.ToArray();
            initialRocks.CopyTo(rocks, 0);
            initialRandom = random;
        }

        private double Next() => random.Range(0, 1);
        private bool Chance(double probability) => Next() < probability;
        private int Side() => Chance(0.5) ? -1 : 1;
        private static int Round(double n) => (int)Floor(n + 0.5);

        private void Spawn(Kind kind, double x, double y, double vx, double vy, double life,
            uint color = 0, double size = 1, double delay = 0, Flags flags = Flags.None)
        {
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life,
                Color = color, Size = size, Delay = delay, Flags = flags, Seed = Next() * 100,
            });
        }

        private void Seed()
        {
            const double rad = SmallExplosionEffect.Radius, y = SmallExplosionEffect.CenterY;
            const double power = 0.9, earth = 1.6;
            for (int i = 0; i < 5; i++)
                Spawn(Kind.Fire, random.Range(-2.5, 2.5), y + random.Range(-2.5, 2.5),
                    random.Range(-20, 20), random.Range(-40, -10), random.Range(0.18, 0.3),
                    size: random.Range(rad * 0.4, rad * 0.55));
            for (int i = 0; i < Round(28 * power); i++)
            {
                double a = random.Range(0, Tau), d = random.Range(0, rad * 0.55), speed = random.Range(40, 210) * power;
                Spawn(Kind.Fire, Cos(a) * d, y + Sin(a) * d, Cos(a) * speed, Sin(a) * speed * 0.8 - 40,
                    random.Range(0.55, 1.15), size: random.Range(rad * 0.22, rad * 0.46),
                    flags: Chance(0.45) ? Flags.Smokes | Flags.Big : Flags.None);
            }
            for (int i = 0; i < 5; i++)
            {
                double a = random.Range(0, Tau), d = random.Range(rad * 0.1, rad * 0.5);
                Spawn(Kind.Smoke, Cos(a) * d, y + Sin(a) * d, Cos(a) * 30, Sin(a) * 20 - 30,
                    random.Range(0.9, 1.5), Rgb(34, 29, 38), random.Range(rad * 0.15, rad * 0.26),
                    random.Range(0.06, 0.2), Flags.Big);
            }
            for (int i = 0; i < 5; i++)
                Spawn(Kind.Fire, random.Range(-rad * 0.25, rad * 0.25), y, random.Range(-12, 12), random.Range(-180, -100),
                    random.Range(0.8, 1.3), size: random.Range(rad * 0.24, rad * 0.36), delay: random.Range(0, 0.1),
                    flags: Chance(0.6) ? Flags.Smokes | Flags.Big : Flags.None);
            for (int i = 0; i < Round(10 * power); i++)
            {
                double a = random.Range(0, Tau), d = random.Range(0, rad * 0.7), speed = random.Range(15, 100);
                Spawn(Kind.Smoke, Cos(a) * d, y + Sin(a) * d, Cos(a) * speed, Sin(a) * speed * 0.6 - 35,
                    random.Range(1.8, 2.8), size: random.Range(rad * 0.26, rad * 0.44), delay: random.Range(0.12, 0.45), flags: Flags.Big);
            }
            for (int i = 0; i < Round(80 * power); i++)
            {
                double a = random.Range(0, Tau), speed = random.Range(140, 380) * power;
                Spawn(Kind.Spark, 0, y, Cos(a) * speed, Sin(a) * speed - 70, random.Range(0.18, 0.6));
            }
            for (int i = 0; i < Round(40 * power); i++)
            {
                double a = random.Range(0, Tau), speed = random.Range(30, 150);
                Spawn(Kind.Ember, Cos(a) * rad * 0.4, y + Sin(a) * rad * 0.4, Cos(a) * speed,
                    Sin(a) * speed - 75, random.Range(1.2, 2.6), colors[3]);
            }
            // Upstream evaluates the random bound each time it checks this loop.
            for (int i = 0; i < Round(random.Range(9, 14) * Max(0, power - 0.55)); i++)
            {
                double a = -PI / 2 + random.Range(-1.35, 1.35), speed = random.Range(170, 330);
                Spawn(Kind.Brand, 0, y, Cos(a) * speed, Sin(a) * speed, random.Range(0.7, 1.3));
            }
            for (int i = 0; i < Round(rad * 0.5); i++)
            {
                int side = Side();
                Spawn(Kind.Smoke, side * random.Range(0, rad * 0.6), -random.Range(1, 5 + rad * 0.04),
                    side * random.Range(3, 9.5) * rad, -random.Range(2, 15), random.Range(1.1, 2), stone.Dust,
                    random.Range(4, 7) + rad / 6, random.Range(0, 0.06), Flags.Big);
            }
            for (int i = 0; i < initialRocks.Length; i++)
            {
                initialRocks[i] = new Rock
                {
                    X = random.Range(-0.4, 0.4) * rad, Y = -3, Vx = random.Range(-9, 9) * rad * 0.25,
                    Vy = -random.Range(7, 16) * rad * 0.9, Life = random.Range(1.8, 2.6),
                    Angle = random.Range(0, Tau), Spin = random.Range(-16, 16), Size = Floor(random.Range(1, 4)),
                };
            }
            for (int i = 0; i < Round(earth * rad * 4); i++)
            {
                double u = random.Range(-1, 1), a = -PI / 2 + u * 0.5;
                double speed = random.Range(5, 15) * rad * (1 - 0.45 * Abs(u));
                double life = random.Range(1.2, 2.2);
                int shade = (int)random.Range(0, 4);
                Spawn(Kind.Grit, u * 0.25 * rad, -1, Cos(a) * speed, Sin(a) * speed, life,
                    shade == 0 ? stone.Light : shade == 3 ? stone.Shade : stone.Body, Chance(0.45) ? 2 : 1);
            }
            for (int i = 0; i < Round(earth * 12); i++)
                Spawn(Kind.Smoke, random.Range(-0.3, 0.3) * rad, -random.Range(2, rad * 0.6), random.Range(-15, 15),
                    -random.Range(60, 190) * earth * 0.6, random.Range(1.6, 2.6), stone.Dust,
                    random.Range(5, 9) + rad / 7, random.Range(0, 0.15), Flags.Big);
        }

        internal void Advance(double t)
        {
            int target = (int)Floor(t * 120 + 1e-8);
            if (target < steps)
            {
                random = initialRandom;
                particles.Clear();
                particles.AddRange(initialParticles);
                initialRocks.CopyTo(rocks, 0);
                blazes.Clear();
                time = steps = 0;
            }
            while (steps < target)
            {
                steps++;
                time += Step;
                AdvanceRocks();
                AdvanceBlazes();
                for (int i = particles.Count - 1; i >= 0; i--)
                {
                    var p = particles[i];
                    if (AdvanceParticle(ref p)) particles[i] = p;
                    else particles.RemoveAt(i);
                }
            }
        }

        private void AdvanceRocks()
        {
            for (int index = 0; index < rocks.Length; index++)
            {
                ref var r = ref rocks[index];
                if (r.Life <= 0 || (r.Life -= Step) <= 0 || r.Rest) continue;
                r.Vy += Gravity * Step;
                r.Vx -= r.Vx * 0.4 * Step;
                r.Vy -= r.Vy * 0.4 * Step;
                r.X += r.Vx * Step;
                r.Y += r.Vy * Step;
                r.Angle += r.Spin * Step;
                if (r.Y < 0 || r.Vy <= 0) continue;
                r.Y = -0.01;
                if (r.Vy > 70)
                {
                    r.Vy *= -0.3;
                    r.Vx *= 0.7;
                    r.Spin *= 0.6;
                    continue;
                }
                r.Rest = true;
                r.Vx = r.Vy = 0;
            }
            // Dust uses the shared RNG after every rock has advanced, in emission order.
            for (int index = 0; index < rocks.Length; index++)
            {
                ref var r = ref rocks[index];
                if (r.Life <= 0 || !r.Rest || r.Dusted) continue;
                r.Dusted = true;
                for (int i = 0; i < 3; i++)
                    Spawn(Kind.Dust, r.X + random.Range(-1.5, 1.5), -0.5, Side() * random.Range(10, 55) * 0.6,
                        -random.Range(2, 22), random.Range(0.3, 0.65), stone.Dust, random.Range(1.3, 2.8));
            }
        }

        private void AdvanceBlazes()
        {
            for (int i = blazes.Count - 1; i >= 0; i--)
            {
                var b = blazes[i];
                if ((b.Life -= Step) <= 0)
                {
                    blazes.RemoveAt(i);
                    continue;
                }
                double k = b.Life / b.MaxLife, intensity = b.Power * (k > 0.7 ? 1 : 0.35 + k / 0.7 * 0.65);
                for (b.Accumulator += Step * (6 + 16 * intensity); b.Accumulator > 1; b.Accumulator--)
                {
                    double size = random.Range(1.2, 2.8) * intensity;
                    Spawn(Kind.Fire, b.X + random.Range(-1.5, 1.5), -0.25 - size * 0.6,
                        random.Range(-6, 6), random.Range(-30, -12), random.Range(0.28, 0.6),
                        size: size, flags: Flags.Back | (Chance(0.3) ? Flags.Smokes : Flags.None));
                    if (Chance(0.06))
                        Spawn(Kind.Ember, b.X, -1.75, random.Range(-15, 15), random.Range(-45, -15),
                            random.Range(0.6, 1.4), Hot[2], flags: Flags.Back);
                }
                blazes[i] = b;
            }
        }

        private bool AdvanceParticle(ref Particle p)
        {
            if (p.Delay > 0)
            {
                p.Delay -= Step;
                return true;
            }
            bool big = (p.Flags & Flags.Big) != 0;
            if ((p.Life -= Step) <= 0)
            {
                if (p.Kind == Kind.Fire && (p.Flags & Flags.Smokes) != 0)
                    Spawn(Kind.Smoke, p.X, p.Y, p.Vx * 0.6, p.Vy * 0.6 - 6,
                        big ? random.Range(1.3, 2.3) : random.Range(0.8, 1.4),
                        size: p.Size * (big ? 0.85 : 0.7), flags: big ? Flags.Big : Flags.None);
                return false;
            }
            switch (p.Kind)
            {
                case Kind.Fire:
                    p.Vy -= 95 * Step;
                    p.Vx += Sin(time * 17 + p.Seed) * 30 * Step;
                    p.Vx -= p.Vx * 5 * Step;
                    p.Vy -= p.Vy * 3.2 * Step;
                    break;
                case Kind.Smoke:
                    p.Vy -= (big ? 23 : 16) * Step;
                    p.Vx += Sin(time * 1.3 + p.Seed) * 8 * Step;
                    p.Vx -= p.Vx * 1.6 * Step;
                    p.Vy -= p.Vy * 1.2 * Step;
                    break;
                case Kind.Ember:
                    p.Vy += (-30 + Sin(time * 3 + p.Seed) * 20) * Step;
                    p.Vx += Cos(time * 2.3 + p.Seed) * 45 * Step;
                    p.Vx -= p.Vx * 1.6 * Step;
                    p.Vy -= p.Vy * 1.6 * Step;
                    break;
                case Kind.Dust:
                    p.Vx -= p.Vx * 4 * Step;
                    p.Vy -= p.Vy * 4 * Step + 5 * Step;
                    break;
                case Kind.Spark:
                    p.Vy += Gravity * 0.45 * Step;
                    p.Vx -= p.Vx * 2.2 * Step;
                    p.Vy -= p.Vy * 2.2 * Step;
                    break;
                case Kind.Grit:
                    p.Vy += Gravity * Step;
                    p.Vx -= p.Vx * 0.8 * Step;
                    break;
                case Kind.Brand:
                    p.Vy += Gravity * 0.8 * Step;
                    p.Vx -= p.Vx * 0.9 * Step;
                    if (Chance(0.9 * Min(1, Step * 60)))
                        Spawn(Kind.Fire, p.X, p.Y, p.Vx * 0.1 + random.Range(-7, 7), p.Vy * 0.1 - 10,
                            random.Range(0.16, 0.32), size: random.Range(0.9, 1.7) * (0.5 + p.Life / p.MaxLife * 0.6),
                            flags: Chance(0.4) ? Flags.Smokes : Flags.None);
                    break;
            }
            double nextY = p.Y + p.Vy * Step;
            p.X += p.Vx * Step;
            if (p.Vy > 0 && p.Y <= 0 && nextY >= 0)
            {
                switch (p.Kind)
                {
                    case Kind.Brand:
                        double life = random.Range(0.9, 1.5), power = random.Range(0.55, 0.85);
                        blazes.Add(new Blaze { X = p.X, Life = life, MaxLife = life, Power = power, Accumulator = Next() });
                        return false;
                    case Kind.Grit:
                        if (p.Vy > 130 && Chance(0.4))
                        {
                            p.Vy *= -0.25;
                            p.Vx = p.Vx * 0.5 + random.Range(-10, 10);
                        }
                        else
                        {
                            p.Vx = p.Vy = 0;
                            p.Life = Min(p.Life, 0.5);
                        }
                        nextY = -0.25;
                        break;
                    case Kind.Spark:
                        p.Vy *= -0.45;
                        p.Vx *= 0.7;
                        nextY = -0.25;
                        break;
                }
            }
            p.Y = nextY;
            return true;
        }
    }
}
