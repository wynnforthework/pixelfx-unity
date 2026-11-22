using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // The spark, mote and glint subset of the upstream impact particle simulator.
    internal sealed class ImpactParticles
    {
        internal const double Step = 1.0 / 120;
        private readonly Particle[] initialParticles, particles;
        private readonly uint hot, warm, cool, tailColor;
        private int steps;
        private double time;

        internal enum ParticleKind { Spark, Mote, Glint }

        internal struct Particle
        {
            internal ParticleKind Kind;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Delay, Seed;
            internal uint Color;
        }

        internal ImpactParticles(Particle[] initialParticles, uint hot, uint warm, uint cool, uint tailColor)
        {
            this.initialParticles = initialParticles;
            particles = (Particle[])initialParticles.Clone();
            this.hot = hot;
            this.warm = warm;
            this.cool = cool;
            this.tailColor = tailColor;
        }

        internal static Particle Spawn(ref PixelFxRandom rng, ParticleKind kind, double x, double y,
            double vx, double vy, double life, uint color, double size, double delay) =>
            new Particle { Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life,
                MaxLife = life, Color = color, Size = size, Delay = delay, Seed = rng.Range(0, 100) };

        internal void Advance(double elapsed)
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

        internal void Draw(PixelPainter painter)
        {
            foreach (var p in particles)
            {
                if (p.Life <= 0 || p.Delay > 0) continue;
                double life = p.Life / p.MaxLife;
                if (p.Kind == ParticleKind.Glint)
                {
                    DrawGlint(painter, p, life);
                    continue;
                }

                double speed = Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
                if (speed == 0) speed = 1;
                bool spark = p.Kind == ParticleKind.Spark;
                double length = spark ? Min(9, 1 + speed * 0.022) : Min(16, 0.5 + speed * 0.026) * p.Size;
                double dx = p.Vx / speed * length, dy = p.Vy / speed * length;
                uint head = spark ? (life > 0.6 ? hot : life > 0.3 ? warm : cool) : p.Color;
                uint tail = spark ? (life > 0.5 ? head : tailColor) : Scale(p.Color, 0.55);
                double opacity = spark ? 1 : Min(1, life / 0.35);
                double tailEnd = spark ? 0.4 : 0.35, headStart = spark ? 0.45 : 0.4;
                painter.Line(p.X - dx, p.Y - dy, p.X - dx * tailEnd, p.Y - dy * tailEnd, tail, opacity);
                painter.Line(p.X - dx * headStart, p.Y - dy * headStart, p.X, p.Y, head, opacity);
                painter.Glow(p.X, p.Y, spark ? 3 : 2.5 + p.Size, head, (spark ? 0.45 : 0.4) * life);
            }
        }

        private void DrawGlint(PixelPainter painter, Particle p, double life)
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
            if (arms >= 3)
            {
                painter.Dot(x - 1, y - 1, p.Color);
                painter.Dot(x - 1, y + 1, p.Color);
                painter.Dot(x + 1, y - 1, p.Color);
                painter.Dot(x + 1, y + 1, p.Color);
            }
            painter.Dot(x, y, White);
            painter.Glow(p.X, p.Y, 4 + arms * 2, p.Color, 0.5 * life * twinkle);
        }
    }
}
