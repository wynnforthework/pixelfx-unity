using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Dust and the chunk subset of upstream Bits, with reset-and-replay on backward seeks.
    internal sealed class BluntHitDebris
    {
        private const double Step = 1.0 / 120;
        private const int RingCount = 30, PuffCount = RingCount + 5, ChunkCount = 5;
        private static readonly uint Dust = Hex(0xb7a8a6), Stone = Hex(0x6e5a5a), Outline = Hex(0x1a1422);
        private static readonly uint StoneLight = Mix(Scale(Stone, 1.35), White, 0.08), StoneShade = Scale(Stone, 0.64);
        private static readonly double LightAngle = Atan2(-1, -1);
        private readonly Puff[] initialPuffs = new Puff[PuffCount], puffs = new Puff[PuffCount];
        private readonly List<Puff> kicks = new List<Puff>(ChunkCount * 4);
        private readonly Chunk[] initialChunks = new Chunk[ChunkCount], chunks = new Chunk[ChunkCount];
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private int steps;
        private double simulationTime;

        private struct Puff
        {
            internal double X, Y, Vx, Vy, Radius, Life, MaxLife, Start, Lift;
        }

        private struct Chunk
        {
            internal double X, Y, Vx, Vy, Angle, Spin, Life, Size;
            internal double[] Shape;
            internal bool Resting;
        }

        internal BluntHitDebris(PixelFxRandom rng)
        {
            const double y = BluntHitEffect.CenterY;
            for (int i = 0; i < RingCount; i++)
            {
                double angle = i / (double)RingCount * (2 * PI) + rng.Range(-0.12, 0.12);
                double cos = Cos(angle), sin = Sin(angle), side = Abs(cos);
                double speed = rng.Range(40, 140) * (0.6 + 0.5 * side);
                initialPuffs[i] = CreatePuff(cos * 9, y + sin * 6, cos * speed, sin * speed * 0.5,
                    rng.Range(2.4, 6.2) * (0.75 + 0.35 * side), rng.Range(0.35, 0.85), rng.Range(0.03, 0.1), rng.Range(4, 16));
            }
            for (int i = RingCount; i < PuffCount; i++)
                initialPuffs[i] = CreatePuff(rng.Range(-6, 6), y - rng.Range(3, 6), rng.Range(-30, 30), -rng.Range(20, 45),
                    rng.Range(3.5, 5.5), rng.Range(0.6, 0.85), rng.Range(0.05, 0.1), rng.Range(14, 24));
            for (int i = 0; i < ChunkCount; i++)
            {
                double side = rng.Range(0, 1) < 0.5 ? -1 : 1;
                double angle = -PI / 2 + side * rng.Range(0.5, 1.3), speed = rng.Range(70, 140);
                var chunk = new Chunk
                {
                    X = side * rng.Range(4, 8), Y = y + rng.Range(-3, 3), Vx = Cos(angle) * speed, Vy = Sin(angle) * speed,
                    Life = rng.Range(0.65, 0.85), Angle = rng.Range(0, 2 * PI), Spin = side * rng.Range(8, 16),
                    Size = rng.Range(0, 1) < 0.4 ? 2 : 1, Shape = new double[6]
                };
                for (int j = 0; j < chunk.Shape.Length; j++) chunk.Shape[j] = rng.Range(0.7, 1.12);
                initialChunks[i] = chunk;
            }
            initialRandom = rng;
            Reset();
        }

        private static Puff CreatePuff(double x, double y, double vx, double vy, double radius, double life, double start, double lift) =>
            new Puff { X = x, Y = y, Vx = vx, Vy = vy, Radius = radius, Life = life, MaxLife = life, Start = start, Lift = lift };

        private void Reset()
        {
            initialPuffs.CopyTo(puffs, 0);
            initialChunks.CopyTo(chunks, 0);
            random = initialRandom;
            kicks.Clear();
            steps = 0;
            simulationTime = 0;
        }

        private void Advance(double time)
        {
            int target = (int)Floor(time * 120 + 1e-8);
            if (target < steps) Reset();
            while (steps < target)
            {
                steps++;
                simulationTime += Step;
                for (int i = 0; i < puffs.Length; i++)
                    if (simulationTime >= puffs[i].Start) StepPuff(ref puffs[i], 4.5);
                for (int i = 0; i < chunks.Length; i++)
                {
                    ref var chunk = ref chunks[i];
                    double previousVy = chunk.Vy;
                    StepChunk(ref chunk);
                    if (chunk.Life > 0 && previousVy > 60 && chunk.Vy <= 0)
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            double side = random.Range(0, 1) < 0.5 ? -1 : 1;
                            kicks.Add(CreatePuff(chunk.X, -1, side * random.Range(8, 22),
                                -random.Range(3, 10), random.Range(1.2, 2), 0.4, 0, 0));
                        }
                    }
                }
                for (int i = 0; i < kicks.Count; i++)
                {
                    var puff = kicks[i];
                    StepPuff(ref puff, 4);
                    kicks[i] = puff;
                }
            }
        }

        private static void StepPuff(ref Puff puff, double drag)
        {
            puff.Life -= Step;
            puff.Vx -= puff.Vx * drag * Step;
            puff.Vy -= puff.Vy * drag * Step + puff.Lift * Step;
            puff.X += puff.Vx * Step;
            puff.Y += puff.Vy * Step;
        }

        private static void StepChunk(ref Chunk chunk)
        {
            if ((chunk.Life -= Step) <= 0 || chunk.Resting) return;
            chunk.Vy += 950 * Step;
            chunk.Vx -= chunk.Vx * 0.6 * Step;
            chunk.Vy -= chunk.Vy * 0.6 * Step;
            chunk.X += chunk.Vx * Step;
            chunk.Y += chunk.Vy * Step;
            chunk.Angle += chunk.Spin * Step;
            if (chunk.Y < 0 || chunk.Vy <= 0) return;
            chunk.Y = -0.01;
            if (chunk.Vy > 70)
            {
                chunk.Vy *= -0.28;
                chunk.Vx *= 0.7;
                chunk.Spin *= 0.6;
                return;
            }
            chunk.Resting = true;
            chunk.Vx = chunk.Vy = 0;
        }

        internal void Draw(PixelPainter painter, double time, uint warm)
        {
            Advance(time);
            uint dust = Mix(Dust, warm, Min(1, Max(0, 1 - (simulationTime - 0.03) / 0.25)) * 0.55);
            uint highlight = Mix(dust, White, 0.55);
            foreach (var puff in puffs) DrawPuff(painter, puff, dust, highlight);
            foreach (var puff in kicks) DrawPuff(painter, puff, dust, highlight);
            foreach (var chunk in chunks)
                if (chunk.Life > 0) DrawChunk(painter, chunk);
        }

        private void DrawPuff(PixelPainter painter, Puff puff, uint dust, uint highlight)
        {
            if (simulationTime < puff.Start || puff.Life <= 0) return;
            double life = puff.Life / puff.MaxLife;
            double radius = puff.Radius * (0.7 + (1 - life) * 0.85);
            double alpha = Pow(life, 1.25) * Min(1, (simulationTime - puff.Start) / 0.03);
            painter.SoftDisc(puff.X, puff.Y, radius, dust, 0.58 * alpha, 0.75);
            painter.SoftDisc(puff.X - radius * 0.22, puff.Y - radius * 0.32, radius * 0.58, highlight, 0.48 * alpha, 1);
        }

        private static void DrawChunk(PixelPainter painter, Chunk chunk)
        {
            double radius = 0.75 + chunk.Size * 0.8, bound = radius + 2;
            painter.Shade(chunk.X - bound, chunk.Y - bound, chunk.X + bound, chunk.Y + bound,
                (Chunk: chunk, Radius: radius, Cos: Cos(chunk.Angle), Sin: Sin(chunk.Angle), Fade: Min(1, chunk.Life / 0.5)),
                static (x, y, state) =>
                {
                    var rock = state.Chunk;
                    double dx = x - rock.X, dy = y - rock.Y;
                    double u = dx * state.Cos + dy * state.Sin, v = -dx * state.Sin + dy * state.Cos;
                    double distance = Sqrt(u * u + v * v), angle = Atan2(v, u);
                    if (angle < 0) angle += 2 * PI;
                    int count = rock.Shape.Length;
                    double facet = angle / (2 * PI) * count;
                    int index = (int)Floor(facet) % count;
                    double fraction = facet - Floor(facet);
                    double edge = state.Radius * (rock.Shape[index] * (1 - fraction)
                        + rock.Shape[(index + 1) % count] * fraction);
                    if (distance > edge + 0.85) return default;
                    if (distance > edge) return (Outline, state.Fade);
                    double light = Cos((index + 0.5) / count * (2 * PI) + rock.Angle - LightAngle);
                    uint color = distance < edge * 0.42
                        ? light > 0.55 ? StoneLight : Stone
                        : light > 0.35 ? StoneLight : light < -0.4 ? StoneShade : Stone;
                    return (color, state.Fade);
                });
        }
    }
}
