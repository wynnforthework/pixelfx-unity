using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX impacts/hit-spark. See the package license and notices.
    internal sealed class HitSparkEffect
    {
        internal const int Width = 136, Height = 80, OriginX = 44, OriginY = 64;
        internal const float Duration = 0.5f;
        private const double Aim = 0.1, Y = -20, Step = 1.0 / 120;
        private readonly PixelPainter painter = new PixelPainter(Width, Height, OriginX, OriginY);
        private readonly (double Angle, double Length, double Width)[] rays;
        private readonly uint[] colors;
        private readonly Particle[] initial, particles;
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private int steps;

        private struct Particle
        {
            internal double X, Y, Vx, Vy, Life, MaxLife, Delay;
            internal uint Color;
            internal bool Spark;
        }

        private HitSparkEffect(PixelFxSettings settings, bool full)
        {
            painter.LightStrength = settings.glow;
            colors = System.Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            var rng = new PixelFxRandom(settings.seed);
            double angle = Aim + rng.Range(-0.08, 0.08);
            rays = new[]
            {
                (angle, rng.Range(33, 37), 2.2),
                (angle + PI, rng.Range(8, 10), 2.2),
                (angle - PI / 2, rng.Range(15, 18), 2.6),
                (angle + PI / 2, rng.Range(10, 12), 2.3),
                (angle - PI / 4, rng.Range(5, 7), 1.0),
            };
            initial = full ? new Particle[15] : System.Array.Empty<Particle>();
            for (int i = 0; i < initial.Length; i++)
            {
                bool spark = i < 9;
                double a = Aim + (spark ? rng.Range(-0.6, 0.6) - 0.15 : rng.Range(-1.1, 0.5) - 0.3);
                double speed = spark ? rng.Range(170, 360) : rng.Range(70, 170);
                double vy = Sin(a) * speed - (spark ? 40 : rng.Range(30, 80));
                double life = rng.Range(spark ? 0.25 : 0.35, 0.45);
                double delay = spark && i > 6 ? rng.Range(0.01, 0.03) : 0;
                int gritColor = spark ? 0 : (int)rng.Range(0, 4);
                uint color = spark ? White : colors[gritColor == 0 ? 5 : gritColor == 3 ? 3 : 4];
                rng.Range(0, 100); // Upstream spawn consumes a random particle seed.
                initial[i] = new Particle { X = 1, Y = Y, Vx = Cos(a) * speed, Vy = vy,
                    Life = life, MaxLife = life, Delay = delay, Color = color, Spark = spark };
            }
            particles = (Particle[])initial.Clone();
            random = initialRandom = rng;
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new HitSparkEffect(timeline.Settings, timeline.Quality == PixelFxQuality.Full);
            return (pixels, _, time, _) => effect.Render(pixels, time);
        }

        private void Advance(double time)
        {
            int target = (int)Floor(time * 120 + 1e-8);
            if (target < steps)
            {
                initial.CopyTo(particles, 0);
                random = initialRandom;
                steps = 0;
            }
            while (steps < target)
            {
                for (int i = particles.Length - 1; i >= 0; i--)
                {
                    ref var p = ref particles[i];
                    if (p.Delay > 0) { p.Delay -= Step; continue; }
                    if (p.Life <= 0 || (p.Life -= Step) <= 0) continue;
                    p.Vy += 950 * (p.Spark ? 0.45 : 1) * Step;
                    p.Vx -= p.Vx * (p.Spark ? 2.2 : 0.8) * Step;
                    if (p.Spark) p.Vy -= p.Vy * 2.2 * Step;
                    double nextY = p.Y + p.Vy * Step;
                    p.X += p.Vx * Step;
                    if (p.Vy <= 0 || p.Y > 0 || nextY < 0) { p.Y = nextY; continue; }
                    p.Y = -0.25;
                    if (p.Spark) { p.Vy *= -0.45; p.Vx *= 0.7; }
                    else if (p.Vy > 130 && random.Range(0, 1) < 0.4)
                    {
                        p.Vy *= -0.25;
                        p.Vx = p.Vx * 0.5 + random.Range(-10, 10);
                    }
                    else p.Vx = p.Vy = 0;
                }
                steps++;
            }
        }

        private void Render(byte[] pixels, double time)
        {
            Advance(time);
            painter.Clear();
            foreach (var p in particles)
            {
                if (p.Life <= 0 || p.Delay > 0) continue;
                double k = p.Life / p.MaxLife;
                if (!p.Spark)
                {
                    if (k > 0.125) painter.Dot(p.X, p.Y, p.Color);
                    continue;
                }
                double speed = Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
                double length = Min(9, 1 + speed * 0.022);
                double x = length * p.Vx / Max(1e-9, speed), y = length * p.Vy / Max(1e-9, speed);
                uint head = colors[k > 0.6 ? 6 : k > 0.3 ? 5 : 4];
                painter.Line(p.X - x, p.Y - y, p.X - x * 0.4, p.Y - y * 0.4, k > 0.5 ? head : colors[2]);
                painter.Line(p.X - x * 0.45, p.Y - y * 0.45, p.X, p.Y, head);
                painter.Glow(p.X, p.Y, 3, head, 0.45 * k);
            }
            if (time < 0.058)
            {
                bool first = time < 0.025;
                foreach (var ray in rays)
                    DrawRay(ray.Angle, ray.Length * (first ? 1 : 1.2), ray.Width * (first ? 1 : 0.55), first ? 0 : 0.12);
                int radius = first ? 4 : 3;
                for (int dy = -radius - 1; dy <= radius + 1; dy++)
                for (int dx = -radius - 1; dx <= radius + 1; dx++)
                {
                    int distance = Abs(dx) + Abs(dy);
                    if (distance <= radius + 1)
                        painter.Dot(dx, Y + dy, distance <= radius ? White : colors[3]);
                }
            }
            else if (time < 0.092) DrawRay(rays[0].Angle, rays[0].Length * 1.3, 0.6, 0.4, true);

            double pop = Exp(-time / 0.045);
            if (pop > 0.02)
            {
                painter.Glow(0, Y, 22, colors[4], 0.3 * pop);
                painter.Glow(0, Y, 7, White, 0.15 * pop);
                painter.GlowLine(0, Y, Cos(Aim) * 26, Y + Sin(Aim) * 26, 4, colors[5], 0.25 * pop);
            }
            DrawLight(0, Y, 38, colors[4], 0.35, 0.18, time);
            DrawLight(Cos(Aim) * 10, Y + Sin(Aim) * 10, 22, colors[5], 0.45, 0.10, time);
            DrawLight(0, -2, 26, colors[3], 0.22, 0.22, time);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawRay(double angle, double length, double width, double start, bool cooling = false)
        {
            double cos = Cos(angle), sin = Sin(angle);
            uint hot = colors[cooling ? 4 : 5], warm = colors[cooling ? 3 : 4];
            for (double distance = start * length; distance <= length; distance += 0.5)
            {
                double u = distance / length, half = width * Pow(1 - u, 1.6);
                double x = cos * distance, y = Y + sin * distance;
                painter.Dot(x, y, u < 0.3 ? White : u < 0.62 ? hot : u < 0.88 ? warm : colors[3]);
                for (double offset = 0.5; offset <= half; offset += 0.5)
                {
                    uint edge = u < 0.3 && offset < 1 ? hot : warm;
                    painter.Dot(x + sin * offset, y - cos * offset, edge);
                    painter.Dot(x - sin * offset, y + cos * offset, edge);
                }
            }
        }

        private void DrawLight(double x, double y, double radius, uint color, double strength, double life, double time)
        {
            if (time >= life) return;
            double k = 1 - time / life;
            painter.Glow(x, y, radius * (0.55 + 0.45 * k), color, strength * k * k);
        }
    }
}
