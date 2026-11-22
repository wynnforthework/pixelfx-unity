using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed class PixelFxArt
    {
        private const double Tau = PI * 2;
        private readonly PixelPainter p;
        private readonly PixelFxSettings settings;
        private readonly uint[] c;

        internal PixelFxArt(PixelPainter painter, PixelFxSettings settings)
        {
            p = painter;
            this.settings = settings;
            c = System.Array.ConvertAll(settings.palette, value => Hex(PixelFxLibrary.Pack(value)));
        }

        internal void Render(byte[] pixels, PixelFxStage stage, double t, double castDuration, double strength, bool full)
        {
            p.Clear();
            p.LightStrength = settings.glow;
            switch (stage)
            {
                case PixelFxStage.Shuriken: Shuriken(t, full); break;
                case PixelFxStage.Cast: Cast(t, castDuration, full); break;
                case PixelFxStage.Flight: Flight(t, full); break;
                case PixelFxStage.Impact: Impact(t, strength, full); break;
            }
            p.Composite(pixels, true, true);
        }

        private void Shuriken(double t, bool full)
        {
            const double flight = 0.85;
            int rate = settings.trailRate;
            if (full && rate > 0)
            {
                int first = Max(0, (int)Ceiling((t - 0.33) * rate));
                int last = (int)Floor(Min(t, flight) * rate);
                for (int i = first; i <= last; i++)
                {
                    var random = new RandomSeed(settings.seed, i);
                    double born = i / (double)rate, age = t - born, life = random.Range(0.14, 0.33);
                    if (age < 0 || age >= life) continue;
                    double k = age / life, vx = random.Range(-37, -9), vy = random.Range(-24, 24);
                    double x = born / flight * 138 - random.Range(2, 8) + vx * age;
                    double y = -3 * Sin(PI * born / flight) + random.Range(-4, 4) + vy * age + 20 * age * age;
                    uint ink = c[k < 0.22 ? 3 : k < 0.53 ? 2 : k < 0.78 ? 1 : 0];
                    if (i % 3 == 0 && k < 0.75)
                    {
                        p.Line(x, y, x - 2.5 * (1 - k), y - vy * 0.015, ink, 1 - k);
                        p.Glow(x, y, 4.5, c[2], 0.55 * (1 - k));
                    }
                    else p.Dot(x, y, ink, 1 - k);
                }
            }
            if (t >= flight) return;
            double cx = t / flight * 138, cy = -3 * Sin(PI * t / flight);
            double angle = t * settings.spin * Tau + 0.18, alpha = Min(1, (flight - t) / 0.075);
            for (int i = 0; i < 4; i++)
            {
                double a = angle + i * Tau / 4;
                DrawBlade(cx, cy, a, alpha);
            }
            p.Circle(cx, cy, 3.9, Hex(0x1a2433), alpha);
            p.Circle(cx, cy, 2.6, Hex(0x84a3b8), alpha);
            p.Circle(cx, cy, 1.3, Hex(0x1a2433), alpha);
        }

        private static readonly (double X, double Y)[] Blade = { (2,-4.8), (12,-2), (8,1.9), (2.2,4.4) };
        private static readonly (double X, double Y)[] Face = { (2,-3.1), (10.5,-1.9), (3,2.9) };
        private static readonly (double X, double Y)[] Shadow = { (3,-0.8), (10.5,-1.9), (3,2.9) };
        private static readonly (double X, double Y)[] Bevel = { (2.3,-3.1), (10.5,-1.9), (3,-1.6) };
        private void DrawBlade(double cx, double cy, double angle, double alpha)
        {
            double co = Cos(angle), si = Sin(angle);
            p.Shade(cx - 13, cy - 13, cx + 13, cy + 13, (x, y) =>
            {
                double dx = x - cx, dy = y - cy, u = dx * co + dy * si, v = -dx * si + dy * co;
                if (Inside(Bevel, u, v)) return (Hex(0xcee6f5), alpha);
                if (Inside(Shadow, u, v)) return (Hex(0x5b7082), alpha);
                if (Inside(Face, u, v)) return (Hex(0x84a3b8), alpha);
                return Inside(Blade, u, v) ? (Hex(0x1a2433), alpha) : default;
            });
        }

        private static bool Inside((double X, double Y)[] points, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                var a = points[i];
                var b = points[j];
                if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        private void Cast(double t, double duration, bool full)
        {
            double progress = Clamp(t / Max(duration, 0.001));
            double radius = 2 + 5 * progress + Sin(16 * t) * progress;
            for (int i = 0; i < 3; i++)
            {
                double q = (progress * 3 + i / 3.0) % 1;
                Ring(0, 0, radius + 21 * (1 - q * q), 0.6 + 0.18 * i, c[5], Sin(q * PI));
            }
            if (full)
            {
                for (int i = 0; i < 52; i++)
                {
                    var rng = new RandomSeed(settings.seed, i);
                    double age = (t * rng.Range(2.1, 3.4) + rng.Range(0, 1)) % 1;
                    double angle = rng.Range(0, Tau) + age * age * 1.4;
                    double r = radius + rng.Range(14, 45) * (1 - age * age);
                    double dx = Cos(angle), dy = Sin(angle);
                    p.Line(dx * r, dy * r, dx * (r + 4 * age), dy * (r + 4 * age),
                        age > 0.66 ? c[6] : c[3], Sin(age * PI) * Min(1, progress * 5));
                }
            }
            p.Glow(0, 0, 43, c[4], 0.2 + 0.2 * progress);
            Orb(0, 0, radius, t);
        }

        private void Flight(double t, bool full)
        {
            double y = Sin(t * 13) * 0.65;
            p.Shade(-37, -9, 7, 9, (x, yy) =>
            {
                double q = Clamp(-x / 37), band = (4.5 + Sin(q * 15 - t * 23)) * (1 - q);
                double d = Abs(yy - y - Sin(q * 12 - t * 18) * q * 3);
                if (d > band) return default;
                int heat = (int)(2 + (1 - q) * 3 + (1 - d / Max(0.1, band)));
                return (c[heat], 1 - q * 0.85);
            });
            if (full)
            {
                for (int i = 0; i < 70; i++)
                {
                    var rng = new RandomSeed(settings.seed + 71, i);
                    double age = (t * rng.Range(2, 4.2) + rng.Range(0, 1)) % 1;
                    double x = -rng.Range(25, 88) * age, yy = rng.Range(-12, 12) * age + y;
                    double radius = rng.Range(0.8, 3.3) * (0.6 + age);
                    if (age > 0.62)
                        p.Disc(x, yy, radius, Mix(Hex(0x494052), c[1], 0.18), (1 - age) * 0.8);
                    else
                        p.Circle(x, yy, radius, c[age < 0.22 ? 5 : age < 0.42 ? 3 : 1], 1 - age);
                }
            }
            p.Glow(-5, y, 38, c[4], 0.38);
            Orb(0, y, 5.4, t);
        }

        private void Orb(double x, double y, double radius, double t)
        {
            p.Shade(x - radius - 2, y - radius - 2, x + radius + 2, y + radius + 2, (xx, yy) =>
            {
                double dx = xx - x, dy = yy - y, a = Atan2(dy, dx);
                double length = Sqrt(dx * dx + dy * dy);
                double rim = radius + 0.72 * Sin(a * 5 + t * 27) + 0.42 * Sin(a * 9 - t * 14);
                if (length > rim) return default;
                double heat = Clamp(1 - length / rim + 0.23 * Sin(a * 3 + length * 0.55 - t * 15));
                return (c[Min(7, 3 + (int)(heat * 6))], 1);
            });
        }

        private void Impact(double t, double strength, bool full)
        {
            double scale = settings.blastSize * Sqrt(strength), radius = 18 * scale, y = -radius * 0.3;
            if (t < 0.7)
                p.Glow(0, y, radius * 3.4, c[4], 0.62 * strength * Pow(1 - t / 0.7, 2));
            if (t < 0.1)
            {
                p.Circle(0, y, radius * (1 - t * 3), c[4]);
                p.Circle(0, y, radius * (0.79 - t * 2), c[7]);
            }
            if (t < 0.5)
            {
                double q = t / 0.5;
                Ring(0, 0, radius * (0.3 + 3.9 * (1 - Pow(1 - q, 3))), 0.16, c[5], 1 - q);
                if (t < 0.24) Ring(0, y, radius * (0.5 + t * 8), 1, c[6], 1 - t / 0.24);
            }
            double fade = Clamp((5.4 - t) / 1.2);
            if (t > 0)
                p.Shade(-radius * 1.3, -2, radius * 1.3, 4, (x, yy) =>
                {
                    if (radius <= 0 || Pow(x / (radius * 1.3), 2) + Pow(yy / 3, 2) > 1) return default;
                    return (t < 0.65 ? c[2] : Hex(0x17121d), fade);
                });
            if (!full) return;
            DrawBlastParticles(t, strength, scale, radius, y);
        }

        private void DrawBlastParticles(double t, double strength, double scale, double radius, double y)
        {
            int clouds = (int)Round(44 * strength * settings.blastSize);
            for (int i = 0; i < clouds; i++)
            {
                var rng = new RandomSeed(settings.seed + 139, i);
                double delay = rng.Range(0, 0.24), age = t - delay, life = rng.Range(1.45, 3.65);
                if (age < 0 || age >= life) continue;
                double k = age / life, a = rng.Range(0, Tau), v = rng.Range(18, 65) * scale;
                double drag = 1 - Exp(-age * 2.5);
                double x = Cos(a) * v * drag, yy = y + Sin(a) * v * 0.45 * drag - age * rng.Range(9, 25) * scale;
                double r = radius * rng.Range(0.16, 0.38) * (0.8 + k * 1.9);
                if (age < 0.54)
                {
                    p.Circle(x, yy, r, c[age < 0.24 ? 4 : 2]);
                    p.Circle(x - r * 0.18, yy - r * 0.22, r * 0.68, c[age < 0.24 ? 6 : 3]);
                }
                else p.Disc(x, yy, r, Mix(Hex(0x594c5a), Hex(0x241d2a), k), Clamp((age - 0.54) * 9) * (1 - k) * 0.54);
            }
            int sparks = (int)Round(88 * strength * settings.blastSize);
            for (int i = 0; i < sparks; i++)
            {
                var rng = new RandomSeed(settings.seed + 261, i);
                double life = rng.Range(0.22, 1.35);
                if (t >= life) continue;
                double a = rng.Range(-PI, 0), v = rng.Range(40, 180) * scale, k = 1 - t / life;
                double x = Cos(a) * v * t / (1 + t * 2.6);
                double yy = Min(-1, y + Sin(a) * v * t + 75 * t * t);
                p.Line(x, yy, x - Cos(a) * 4 * k, yy - Sin(a) * 4 * k, c[k > 0.55 ? 6 : 3], k);
            }
        }

        private void Ring(double x, double y, double radius, double squash, uint ink, double alpha)
        {
            int count = (int)Ceiling(radius * Tau * 1.2);
            for (int i = 0; i < count; i++)
            {
                double a = i * Tau / count;
                p.Dot(x + radius * Cos(a), y + radius * Sin(a) * squash, ink, alpha);
            }
        }

        private static double Clamp(double n) => Max(0, Min(1, n));

        private struct RandomSeed
        {
            private uint state;
            internal RandomSeed(int seed, int index)
            {
                unchecked
                {
                    uint h = Hash(0x9e3779b9, seed);
                    state = Hash(h, index);
                    if (state == 0) state = 1;
                }
            }

            private static uint Hash(uint h, int value)
            {
                unchecked
                {
                    h = (h ^ (uint)value) * 0x85ebca6b;
                    h ^= h >> 13;
                    h *= 0xc2b2ae35;
                    return h ^ (h >> 16);
                }
            }

            internal double Range(double a, double b)
            {
                unchecked
                {
                    uint t = state += 0x6d2b79f5;
                    t = (t ^ (t >> 15)) * (t | 1);
                    t ^= t + (t ^ (t >> 7)) * (t | 61);
                    return a + (b - a) * ((t ^ (t >> 14)) / 4294967296.0);
                }
            }
        }
    }
}
