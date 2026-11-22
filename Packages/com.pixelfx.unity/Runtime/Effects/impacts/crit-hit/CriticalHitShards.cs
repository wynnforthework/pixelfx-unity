using static System.Math;
using static PixelFx.ImpactParticles;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // The upstream Bits shard recipe with bouncing and settling on the local floor.
    internal sealed class CriticalHitShards
    {
        private const int Count = 10;
        private readonly Shard[] initial = new Shard[Count], shards = new Shard[Count];
        private int steps;

        private struct Shard
        {
            internal double X, Y, Vx, Vy, Angle, Spin, Life, Size;
            internal uint Color;
            internal bool Resting;
        }

        internal CriticalHitShards(ref PixelFxRandom rng, double y, double aim, uint[] colors)
        {
            for (int i = 0; i < Count; i++)
            {
                double angle = aim + rng.Range(-1, 1) * 1.1 - 0.35, speed = rng.Range(110, 280);
                initial[i] = new Shard
                {
                    Y = y, Vx = Cos(angle) * speed, Vy = Sin(angle) * speed - 40,
                    Life = rng.Range(0.55, 0.85), Angle = rng.Range(0, 2 * PI), Spin = rng.Range(-25, 25),
                    Size = rng.Range(2, 4.5), Color = colors[(int)rng.Range(0, 4) switch { 0 => 7, 3 => 5, _ => 6 }]
                };
            }
            initial.CopyTo(shards, 0);
        }

        internal void Draw(PixelPainter painter, double time)
        {
            int target = (int)Floor(time * 120 + 1e-8);
            if (target < steps)
            {
                initial.CopyTo(shards, 0);
                steps = 0;
            }
            while (steps < target)
            {
                for (int i = 0; i < shards.Length; i++) Advance(ref shards[i]);
                steps++;
            }

            foreach (var shard in shards)
            {
                if (shard.Life <= 0) continue;
                double alpha = Min(1, shard.Life / 0.4);
                double cos = Cos(shard.Angle), sin = Sin(shard.Angle), half = shard.Size / 2;
                for (double d = -half; d <= half; d += 0.5)
                    painter.Dot(shard.X + cos * d, shard.Y + sin * d, d > half - 0.6 ? White : shard.Color, alpha);
            }
        }

        private static void Advance(ref Shard shard)
        {
            if (shard.Life <= 0 || (shard.Life -= Step) <= 0 || shard.Resting) return;
            shard.Vy += 950 * 0.8 * Step;
            shard.Vx -= shard.Vx * 0.7 * Step;
            shard.Vy -= shard.Vy * 0.7 * Step;
            shard.X += shard.Vx * Step;
            shard.Y += shard.Vy * Step;
            shard.Angle += shard.Spin * Step;
            if (shard.Y < 0 || shard.Vy <= 0) return;
            shard.Y = -0.01;
            if (shard.Vy > 70)
            {
                shard.Vy *= -0.35;
                shard.Vx *= 0.7;
                shard.Spin *= 0.6;
                return;
            }
            shard.Resting = true;
            shard.Vx = shard.Vy = 0;
        }
    }
}
