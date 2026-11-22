using System;
using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class MagicCircleCastEffect
    {
        private enum Kind { Ember, Streak, Spark, Star, Mote }

        private struct Particle
        {
            internal Kind Kind;
            internal double X, Y, Vx, Vy, Life, Duration, Size, Seed;
            internal uint Ink;
        }

        private readonly List<Particle> particles = new List<Particle>(256);
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private int steps, flashStep, nextRune;
        private bool broke;

        private void ResetSimulation()
        {
            random = initialRandom;
            steps = flashStep = nextRune = 0;
            broke = false;
            time = 0;
            particles.Clear();
        }

        private void Advance(double sample)
        {
            int target = (int)Floor(sample * 120 + 1e-8);
            if (target < steps) ResetSimulation();
            while (steps < target)
            {
                steps++;
                time += Step;
                bool flash = flashStep == 0 && time >= Flash;
                if (flash) flashStep = steps;
                if (!full) continue;
                Emit(flash);
                UpdateParticles();
            }
        }

        private void Emit(bool flash)
        {
            if (time > 0.04 && time < 0.88 && Chance(0.5))
            {
                var pen = GlitterPosition();
                Spawn(Kind.Ember, pen.X, pen.Y - 1, random.Range(-12, 12), random.Range(-30, -8),
                    random.Range(0.25, 0.55), colors[6]);
            }
            if (time > 1.3 - 0.1 && time < Flash - 0.05 && Chance(0.7))
            {
                double a = random.Range(0, Tau), distance = random.Range(Outer * 0.7, Outer * 1.1);
                double life = random.Range(0.16, 0.24), velocity = distance * 3 / (Exp(3 * life) - 1);
                double x = Cos(a) * distance, y = Sin(a) * distance * Squash - 1;
                double length = Sqrt(x * x + (y + 2) * (y + 2));
                if (length == 0) length = 1;
                Spawn(Kind.Streak, x, y, -x / length * velocity, (-2 - y) / length * velocity,
                    life, Chance(0.5) ? colors[6] : colors[5]);
            }
            if (flash) EmitFlash();
            if (time > 0.9 && time < Break + 0.2 && Chance(0.12))
            {
                double a = random.Range(0, Tau);
                int ring = (int)random.Range(0, 3);
                double r = ring == 0 ? Outer : ring == 1 ? Second : Inner;
                Spawn(Kind.Star, Cos(a) * r, Sin(a) * r * Squash - 0.5, 0, -random.Range(2, 8),
                    random.Range(0.25, 0.45), Chance(0.5) ? White : colors[6], random.Range(0.8, 2));
            }
            if (time > Flash && time < Break)
            {
                if (Chance(0.5))
                    Spawn(Kind.Star, random.Range(-1, 1) * BeamWidth * 1.5, -random.Range(0, 120),
                        random.Range(-10, 10), -random.Range(30, 85), random.Range(0.5, 1.1), StarInk(), random.Range(0.7, 2));
                if (Chance(0.35))
                    Spawn(Kind.Mote, random.Range(-1, 1) * BeamWidth * 0.8, -random.Range(0, 50),
                        0, -random.Range(160, 300), random.Range(0.2, 0.35), Chance(0.5) ? White : colors[6], 0.8);
            }
            if (!broke && time >= Break)
            {
                broke = true;
                for (int i = 0; i < 46; i++)
                    Spawn(Kind.Star, random.Range(-1, 1) * BeamWidth * 0.9, -random.Range(2, Top * 0.8),
                        random.Range(-12, 12), -random.Range(8, 40), random.Range(0.5, 1.15),
                        StarInk(whiteCount: 2, violetCount: 1), random.Range(0.6, 2.4));
            }
            if (time > Break && time < Break + 0.4 && Chance(0.8))
            {
                Spawn(Kind.Star, random.Range(-1, 1) * BeamWidth * 0.7, -random.Range(2, Top * 0.78),
                    random.Range(-8, 8), -random.Range(10, 50), random.Range(0.4, 0.9), StarInk(), random.Range(0.6, 1.8));
                if (Chance(0.4))
                    Spawn(Kind.Mote, random.Range(-1, 1) * BeamWidth * 0.5, -random.Range(2, Top * 0.7),
                        0, -random.Range(60, 140), random.Range(0.2, 0.4), colors[6], 0.7);
            }
            while (nextRune < RuneCount && time >= RuneOut(nextRune))
            {
                double a = RuneAngle(nextRune++), x = Cos(a) * RuneRadius, y = Sin(a) * RuneRadius * Squash - 5;
                for (int i = 0; i < 4; i++)
                    Spawn(Kind.Star, x + random.Range(-2, 2), y + random.Range(-2, 2), random.Range(-14, 14),
                        -random.Range(10, 40), random.Range(0.3, 0.55), Chance(0.5) ? White : colors[6], random.Range(0.6, 1.6));
            }
        }

        private void EmitFlash()
        {
            for (int i = 0; i < 36; i++)
            {
                int side = Side();
                double speed = random.Range(80, 175);
                Spawn(Kind.Spark, side * random.Range(0, Outer * 0.5), -1, side * speed, -random.Range(30, 180),
                    random.Range(0.25, 0.7), White);
            }
            for (int i = 0; i < 26; i++)
                Spawn(Kind.Star, random.Range(-Outer, Outer), -random.Range(1, 6), random.Range(-20, 20),
                    -random.Range(40, 130), random.Range(0.5, 1.1), StarInk(), random.Range(0.8, 2.3));
            for (int i = 0; i < 34; i++)
            {
                int side = Side();
                Spawn(Kind.Star, side * random.Range(0, 14), -random.Range(1, 4), side * random.Range(50, 140),
                    -random.Range(10, 90), random.Range(0.4, 0.95), StarInk(violetCount: 2), random.Range(0.7, 2.2));
            }
        }

        private (double X, double Y) GlitterPosition()
        {
            if (time < 0.42)
            {
                double a = PI / 2 + Side() * ((time - 0.04) / (0.42 - 0.04)) * PI;
                return (Cos(a) * Outer, Sin(a) * Outer * Squash);
            }
            if (time < 0.6)
            {
                double a = -PI / 2 + (time - 0.3) / (0.6 - 0.3) * Tau;
                return (Cos(a) * Inner, Sin(a) * Inner * Squash);
            }
            Span<int> active = stackalloc int[Edges.Length];
            int count = 0;
            for (int i = 0; i < Edges.Length; i++)
                if (time >= Edges[i].Start && time < Edges[i].Start + EdgeDuration)
                    active[count++] = i;
            int index = (int)random.Range(0, count);
            var edge = Edges[count == 0 ? 0 : active[index]];
            return edge.At(Clamp((time - edge.Start) / EdgeDuration));
        }

        private bool Chance(double probability) => random.Range(0, 1) < probability;
        private int Side() => Chance(0.5) ? -1 : 1;

        private uint StarInk(int whiteCount = 1, int violetCount = 1)
        {
            double index = random.Range(0, whiteCount + violetCount + 1);
            return index < whiteCount ? White : index < whiteCount + violetCount ? colors[6] : colors[5];
        }

        private void Spawn(Kind kind, double x, double y, double vx, double vy, double life, uint ink, double size = 1)
        {
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life, Duration = life,
                Ink = ink, Size = size, Seed = random.Range(0, 1) * 100,
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
                    particles.RemoveAt(i);
                    continue;
                }
                switch (q.Kind)
                {
                    case Kind.Spark:
                        q.Vy += 950 * 0.45 * Step;
                        q.Vx -= q.Vx * 2.2 * Step;
                        q.Vy -= q.Vy * 2.2 * Step;
                        break;
                    case Kind.Streak:
                        q.Vx += q.Vx * 3 * Step;
                        q.Vy += q.Vy * 3 * Step;
                        break;
                    case Kind.Ember:
                        q.Vy += (-30 + Sin(time * 3 + q.Seed) * 20) * Step;
                        q.Vx += Cos(time * 2.3 + q.Seed) * 45 * Step;
                        q.Vx -= q.Vx * 1.6 * Step;
                        q.Vy -= q.Vy * 1.6 * Step;
                        break;
                    case Kind.Mote:
                        q.Vx -= q.Vx * 0.6 * Step;
                        q.Vy -= q.Vy * 0.6 * Step;
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
                    q.Vy = -q.Vy * 0.45;
                    q.Vx *= 0.7;
                    nextY = -0.25;
                }
                q.Y = nextY;
                particles[i] = q;
            }
        }

        private void DrawParticles()
        {
            foreach (var q in particles)
            {
                double k = q.Life / q.Duration, x = q.X, y = q.Y;
                double speed = Sqrt(q.Vx * q.Vx + q.Vy * q.Vy);
                if (speed == 0) speed = 1;
                double ux = q.Vx / speed, uy = q.Vy / speed;
                uint hot = k > 0.6 ? White : k > 0.3 ? colors[6] : colors[5];
                switch (q.Kind)
                {
                    case Kind.Spark:
                        double sparkLength = Min(9, 1 + speed * 0.022);
                        double dx = ux * sparkLength, dy = uy * sparkLength;
                        painter.Line(x - dx, y - dy, x - dx * 0.4, y - dy * 0.4, k > 0.5 ? hot : colors[4]);
                        painter.Line(x - dx * 0.45, y - dy * 0.45, x, y, hot);
                        painter.Glow(x, y, 3, hot, 0.45 * k);
                        break;
                    case Kind.Mote:
                        double moteLength = Min(16, 0.5 + speed * 0.026) * q.Size;
                        dx = ux * moteLength;
                        dy = uy * moteLength;
                        double alpha = Min(1, k / 0.35);
                        painter.Line(x - dx, y - dy, x - dx * 0.35, y - dy * 0.35, Scale(q.Ink, 0.55), alpha);
                        painter.Line(x - dx * 0.4, y - dy * 0.4, x, y, q.Ink, alpha);
                        painter.Glow(x, y, 2.5 + q.Size, q.Ink, 0.4 * k);
                        break;
                    case Kind.Streak:
                        double length = Max(1, Min(7, speed / 30));
                        for (double d = length; d >= 0; d -= 0.5)
                            painter.Dot(x - ux * d, y - uy * d, d < 1 ? White : d < length * 0.5 ? q.Ink : Scale(q.Ink, 0.6),
                                d < length * 0.5 ? 1 : 1.2 - d / length);
                        painter.Glow(x, y, 4, q.Ink, 0.4);
                        break;
                    case Kind.Ember:
                        if (k < 0.25 && Sin(time * 40 + q.Seed) < 0) break;
                        painter.Dot(x, y, hot);
                        painter.Glow(x, y, 3 + q.Size, hot, 0.35 * k);
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
    }
}
