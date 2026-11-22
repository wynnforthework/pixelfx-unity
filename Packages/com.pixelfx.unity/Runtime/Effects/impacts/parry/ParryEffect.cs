using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX impacts/parry. See the package license and notices.
    internal sealed class ParryEffect
    {
        internal const int Width = 200, Height = 112, OriginX = 100, OriginY = 96;
        internal const float Duration = 1;
        private const double Y = -30, Step = 1.0 / 120, Tau = 2 * PI;
        private readonly PixelPainter painter = new PixelPainter(Width, Height, OriginX, OriginY);
        private readonly uint[] colors;
        private readonly (double Angle, double Length, double Width)[] rays = new (double, double, double)[8];
        private readonly (double Angle, double Gap, double Drift)[] arcs = new (double, double, double)[6];
        private readonly double[] edges = new double[2];
        private readonly Particle[] initialParticles, particles;
        private int steps;
        private double time;

        private enum ParticleKind { Spark, Mote, Glint }

        private struct Particle
        {
            internal ParticleKind Kind;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Delay, Seed;
            internal uint Color;
        }

        private ParryEffect(PixelFxSettings settings, bool full)
        {
            painter.LightStrength = settings.glow;
            colors = Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            var rng = new PixelFxRandom(settings.seed);
            for (int i = 0; i < rays.Length; i++)
            {
                double angle = i * PI / 4 + rng.Range(-0.04, 0.04);
                bool shortRay = i % 2 != 0;
                double length = shortRay ? rng.Range(6, 8) : i % 4 == 0 ? rng.Range(19, 22) : rng.Range(14, 17);
                rays[i] = (angle, length, shortRay ? 1.2 : 2.2);
            }
            for (int i = 0; i < arcs.Length; i++)
                arcs[i] = (i / (double)arcs.Length * Tau + rng.Range(-0.1, 0.1), rng.Range(0.55, 0.8), rng.Range(0.1, 0.35));
            edges[0] = -0.62 + rng.Range(-0.08, 0.08);
            edges[1] = PI + 0.62 + rng.Range(-0.08, 0.08);

            initialParticles = full ? CreateParticles(ref rng) : Array.Empty<Particle>();
            particles = (Particle[])initialParticles.Clone();
        }

        private Particle[] CreateParticles(ref PixelFxRandom rng)
        {
            const int sparkCount = 24, moteCount = 8, glintCount = 3;
            var result = new Particle[sparkCount + moteCount + glintCount];
            int index = 0;
            for (int i = 0; i < sparkCount; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                double angle = -PI / 2 + side * rng.Range(0.3, 1.3);
                double speed = rng.Range(130, 290);
                result[index++] = Spawn(ref rng, ParticleKind.Spark, side * rng.Range(1, 3), Y,
                    Cos(angle) * speed, Sin(angle) * speed, rng.Range(0.45, 0.85), White, 1,
                    i > 17 ? rng.Range(0.02, 0.07) : 0);
            }
            for (int i = 0; i < moteCount; i++)
            {
                double angle = rng.Range(0, Tau), speed = rng.Range(40, 110);
                result[index++] = Spawn(ref rng, ParticleKind.Mote, 0, Y, Cos(angle) * speed,
                    Sin(angle) * speed - 20, rng.Range(0.3, 0.55), colors[5 + (int)rng.Range(0, 2)],
                    0.6, rng.Range(0, 0.04));
            }
            for (int i = 0; i < glintCount; i++)
            {
                double angle = rng.Range(0, Tau), distance = rng.Range(12, 26);
                result[index++] = Spawn(ref rng, ParticleKind.Glint, Cos(angle) * distance, Y + Sin(angle) * distance,
                    0, -3, rng.Range(0.22, 0.34), White, rng.Range(1.6, 2.6), rng.Range(0.1, 0.3));
            }
            return result;
        }

        private static Particle Spawn(ref PixelFxRandom rng, ParticleKind kind, double x, double y,
            double vx, double vy, double life, uint color, double size, double delay) =>
            new Particle { Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life,
                MaxLife = life, Color = color, Size = size, Delay = delay, Seed = rng.Range(0, 100) };

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new ParryEffect(timeline.Settings, timeline.Quality == PixelFxQuality.Full);
            return (pixels, _, time, _) => effect.Render(pixels, time);
        }

        private void Advance(double elapsed)
        {
            int target = (int)Floor(elapsed * 120 + 1e-8);
            if (target < steps)
            {
                initialParticles.CopyTo(particles, 0);
                steps = 0;
                time = 0;
            }
            while (steps < target)
            {
                foreach (ref var particle in particles.AsSpan())
                    StepParticle(ref particle);
                steps++;
                time += Step;
            }
        }

        private static void StepParticle(ref Particle p)
        {
            if (p.Delay > 0)
            {
                p.Delay -= Step;
                return;
            }
            if (p.Life <= 0 || (p.Life -= Step) <= 0) return;

            switch (p.Kind)
            {
                case ParticleKind.Spark:
                    p.Vy += 950 * 0.45 * Step;
                    p.Vx -= p.Vx * 2.2 * Step;
                    p.Vy -= p.Vy * 2.2 * Step;
                    break;
                case ParticleKind.Mote:
                    p.Vx -= p.Vx * 0.6 * Step;
                    p.Vy -= p.Vy * 0.6 * Step;
                    break;
                case ParticleKind.Glint:
                    p.Vx -= p.Vx * 1.6 * Step;
                    p.Vy -= p.Vy * 1.6 * Step + 5 * Step;
                    break;
            }
            p.X += p.Vx * Step;
            double nextY = p.Y + p.Vy * Step;
            if (p.Kind == ParticleKind.Spark && p.Vy > 0 && p.Y <= 0 && nextY >= 0)
            {
                p.Vy *= -0.45;
                p.Vx *= 0.7;
                p.Y = -0.25;
            }
            else p.Y = nextY;
        }

        private void Render(byte[] pixels, double elapsed)
        {
            Advance(elapsed);
            painter.Clear();
            DrawRing();
            DrawParticles();
            if (time < 0.058) DrawEdges();
            if (time < 0.025) DrawStar(1, 1, colors[4], White, White, White, 2.6);
            else if (time < 0.058) DrawStar(1.25, 0.65, colors[2], colors[5], colors[6], White, 2.6);
            else if (time < 0.092) DrawStar(1.4, 0.3, colors[2], colors[4], colors[5], colors[6], 2);

            double pop = Exp(-time / 0.05);
            if (pop > 0.02)
            {
                painter.Glow(0, Y, 30, colors[4], 0.5 * pop);
                painter.Glow(0, Y, 10, White, 0.4 * pop);
            }
            DrawLight(60, colors[4], 0.65, 0.4);
            DrawLight(22, White, 0.8, 0.08);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawLight(double radius, uint color, double intensity, double lifetime)
        {
            if (time >= lifetime) return;
            double remaining = (lifetime - time) / lifetime;
            painter.Glow(0, Y, radius * (0.55 + 0.45 * remaining), color, intensity * remaining * remaining);
        }

        private void DrawRing()
        {
            double progress = (time - 0.03) / 0.36;
            if (progress < 0 || progress >= 1) return;
            double radius = 8 + 24 * (1 - Pow(1 - progress, 3));
            double spread = Min(1, Max(0, (progress - 0.15) / (0.6 - 0.15)));
            double breakup = spread * spread * (3 - 2 * spread);
            double thickness = Max(1, 2.6 * Pow(1 - progress, 1.2));
            int shade = progress < 0.15 ? 6 : progress < 0.35 ? 5 : progress < 0.6 ? 4 : 3;
            double alpha = progress < 0.6 ? 1 : (1 - progress) / 0.4;
            foreach (var arc in arcs)
            {
                double span = Tau / arcs.Length * (1 - arc.Gap * breakup) + 0.02;
                double arcRadius = radius * (1 + arc.Drift * breakup);
                int samples = Max(4, (int)Ceiling(arcRadius * span * 1.8));
                for (int i = 0; i <= samples; i++)
                {
                    double u = i / (double)samples, angle = arc.Angle + (u - 0.5) * span;
                    double halfWidth = Max(0.5, thickness * (1 - breakup * Pow(Abs(2 * u - 1), 1.6))) / 2;
                    double cos = Cos(angle), sin = Sin(angle);
                    for (double offset = -halfWidth; offset <= halfWidth + 0.01; offset += 0.5)
                    {
                        double y = Y + sin * (arcRadius + offset);
                        if (y < 0) painter.Dot(cos * (arcRadius + offset), y,
                            colors[offset > -0.26 ? shade : Max(2, shade - 2)], alpha);
                    }
                }
            }
            if (progress < 0.5)
                painter.GlowRing(0, Y, radius, 5, colors[4], 0.3 * (1 - progress / 0.5));
        }

        private void DrawEdges()
        {
            double length = time < 0.025 ? 26 : 32, intensity = time < 0.025 ? 1 : 0.6;
            foreach (double angle in edges)
            {
                double cos = Cos(angle), sin = Sin(angle);
                for (double d = -length; d <= length; d += 0.5)
                {
                    double u = Abs(d) / length;
                    painter.Dot(cos * d, Y + sin * d, u < 0.4 ? White : u < 0.75 ? colors[5] : colors[4],
                        u < 0.6 ? 1 : intensity * (1.3 - u));
                }
                painter.GlowLine(-cos * length, Y - sin * length, cos * length, Y + sin * length, 4, colors[5], 0.35 * intensity);
            }
        }

        private void DrawStar(double scale, double thickness, uint rim, uint body, uint inner, uint core, double heart)
        {
            // Draw the rim of every ray before the body or core so intersecting rays stay bright.
            for (int pass = 0; pass < 3; pass++)
            foreach (var ray in rays)
            {
                double cos = Cos(ray.Angle), sin = Sin(ray.Angle), length = ray.Length * scale;
                double end = pass == 2 ? length * 0.55 : length;
                for (double d = 0; d <= end; d += 0.5)
                {
                    double width = ray.Width * thickness * (1 - d / length) * Min(1, 0.5 + d / 3);
                    double halfWidth = pass == 0 ? width + 0.8 : pass == 1 ? width : width * 0.45;
                    uint color = pass == 0 ? rim : pass == 2 ? core : d < length * 0.35 ? inner : body;
                    for (double offset = -halfWidth; offset <= halfWidth; offset += 0.5)
                        painter.Dot(cos * d - sin * offset, Y + sin * d + cos * offset, color);
                }
            }
            double radius = heart * scale * Sqrt(thickness);
            painter.Circle(0, Y, radius + 0.8, inner);
            painter.Circle(0, Y, radius, core);
        }

        private void DrawParticles()
        {
            foreach (var p in particles)
            {
                if (p.Life <= 0 || p.Delay > 0) continue;
                double life = p.Life / p.MaxLife;
                if (p.Kind == ParticleKind.Glint)
                {
                    DrawGlint(p, life);
                    continue;
                }

                double speed = Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
                if (speed == 0) speed = 1;
                bool spark = p.Kind == ParticleKind.Spark;
                double length = spark ? Min(9, 1 + speed * 0.022) : Min(16, 0.5 + speed * 0.026) * p.Size;
                double dx = p.Vx / speed * length, dy = p.Vy / speed * length;
                uint head = spark ? colors[life > 0.3 ? 4 : 3] : p.Color;
                uint tail = spark ? (life > 0.5 ? head : colors[2]) : Scale(p.Color, 0.55);
                double opacity = spark ? 1 : Min(1, life / 0.35);
                double tailEnd = spark ? 0.4 : 0.35, headStart = spark ? 0.45 : 0.4;
                painter.Line(p.X - dx, p.Y - dy, p.X - dx * tailEnd, p.Y - dy * tailEnd, tail, opacity);
                painter.Line(p.X - dx * headStart, p.Y - dy * headStart, p.X, p.Y, head, opacity);
                painter.Glow(p.X, p.Y, spark ? 3 : 2.5 + p.Size, head, (spark ? 0.45 : 0.4) * life);
            }
        }

        private void DrawGlint(Particle p, double life)
        {
            double twinkle = 0.6 + 0.4 * Sin(time * (8 + p.Seed % 5) + p.Seed);
            int arms = (int)Floor(p.Size * twinkle * Min(1, life * 3) + 0.5);
            if (arms < 1) return;
            double x = Floor(p.X), y = Floor(p.Y);
            uint tip = Scale(p.Color, 0.75);
            for (int d = 1; d <= arms; d++)
            {
                uint color = d > arms * 0.6 ? tip : p.Color;
                painter.Dot(x + d, y, color);
                painter.Dot(x - d, y, color);
                painter.Dot(x, y + d, color);
                painter.Dot(x, y - d, color);
            }
            painter.Dot(x, y, White);
            painter.Glow(p.X, p.Y, 4 + arms * 2, p.Color, 0.5 * life * twinkle);
        }
    }
}
