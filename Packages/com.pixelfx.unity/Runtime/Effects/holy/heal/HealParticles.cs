using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class HealEffect
    {
        private static readonly string[] Heart = { ".#.#.", "#####", ".###.", "..#.." };

        private struct Star
        {
            internal double X, Y, Vx, Vy, Life, InitialLife, Size, Seed;
            internal uint Color;
        }

        private struct Rider
        {
            internal double Start, Angle, Radius, Y, Spin, Velocity, Life;
            internal bool Heart;
        }

        private Rider[] CreateRiders()
        {
            var result = new Rider[PetalCount * 2];
            for (int i = 0; i < PetalCount; i++)
            {
                result[i] = new Rider
                {
                    Start = 0.2 + i * 0.05, Angle = petals[i].Angle, Radius = Radius * 0.9, Y = -1,
                    Spin = i % 2 == 0 ? -4.2 : 4.2, Velocity = random.Range(46, 58),
                    Life = random.Range(0.75, 0.95), Heart = i % 2 != 0,
                };
            }
            for (int i = 0; i < PetalCount; i++)
            {
                result[i + PetalCount] = new Rider
                {
                    Start = LiftEnd + random.Range(0, 0.06), Angle = i / (double)PetalCount * Tau,
                    Radius = random.Range(12, 18), Y = LiftTop + random.Range(-2, 4),
                    Spin = i % 2 == 0 ? -3.6 : 3.6, Velocity = random.Range(14, 34),
                    Life = random.Range(0.35, 0.6), Heart = i % 2 == 0,
                };
            }
            return result;
        }

        private void Advance(double sample, bool hasActor, PixelFxActorMask.Projection projection)
        {
            int target = (int)Floor(sample * 120 + 0.00001);
            if (target < tick)
            {
                tick = 0;
                time = 0;
                burstAge = double.PositiveInfinity;
                lastRow = 1;
                random = particleSeed;
                stars?.Clear();
            }

            while (tick < target)
            {
                tick++;
                time += Step;
                if (time >= LiftEnd)
                    burstAge = double.IsPositiveInfinity(burstAge) ? Step : burstAge + Step;
                if (stars == null) continue;

                if (time > LiftStart && time < LiftEnd)
                {
                    int row = (int)Floor(HoopY(time) + 0.5);
                    if (hasActor)
                        for (int y = lastRow; y > row; y--) SpawnEdgeStar(y, projection);
                    lastRow = Min(lastRow, row);
                }

                for (int i = stars.Count - 1; i >= 0; i--)
                {
                    var q = stars[i];
                    q.Life -= Step;
                    if (q.Life <= 0)
                    {
                        stars.RemoveAt(i);
                        continue;
                    }
                    q.Vx -= q.Vx * 1.6 * Step;
                    q.Vy -= q.Vy * 1.6 * Step + 5 * Step;
                    q.X += q.Vx * Step;
                    q.Y += q.Vy * Step;
                    stars[i] = q;
                }
            }
        }

        private void SpawnEdgeStar(int y, PixelFxActorMask.Projection projection)
        {
            Span<int> edges = stackalloc int[Width];
            int count = 0;
            for (int x = -OriginX; x < Width - OriginX; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                if (projection.Sample(px, py).a == 0) continue;
                if (projection.Sample(px - 1, py).a == 0 || projection.Sample(px + 1, py).a == 0
                    || projection.Sample(px, py - 1).a == 0 || projection.Sample(px, py + 1).a == 0)
                    edges[count++] = x;
            }
            if (count == 0 || random.Range(0, 1) >= 0.45) return;
            var q = new Star
            {
                X = edges[(int)random.Range(0, count)] + random.Range(-1, 1) * 2,
                Y = y, Vx = random.Range(-4, 4), Vy = -random.Range(4, 12),
                Life = random.Range(0.3, 0.55), Color = random.Range(0, 1) < 0.5 ? White : colors[6],
                Size = random.Range(0.6, 1.6), Seed = random.Range(0, 1) * 100,
            };
            q.InitialLife = q.Life;
            stars.Add(q);
        }

        private void DrawStars()
        {
            foreach (var q in stars)
            {
                double k = q.Life / q.InitialLife;
                double twinkle = 0.6 + 0.4 * Sin(time * (8 + q.Seed % 5) + q.Seed);
                if (q.Size < 1)
                {
                    if (twinkle * k > 0.25) painter.Dot(q.X, q.Y, twinkle > 0.8 ? White : q.Color);
                    painter.Glow(q.X, q.Y, 3, q.Color, 0.4 * k * twinkle);
                }
                else Sparkle(q.X, q.Y, q.Size * twinkle * Min(1, k * 3), q.Color, Scale(q.Color, 0.75), 0.5 * k * twinkle);
            }
        }

        private static (double X, double Y, double Z) RiderPosition(Rider rider, double age)
        {
            double angle = rider.Angle + rider.Spin * age;
            double radius = rider.Radius * (1 - 0.45 * (age / rider.Life));
            return (Cos(angle) * radius, rider.Y - rider.Velocity * age, Sin(angle));
        }

        private void DrawRiders(bool back)
        {
            foreach (var rider in riders)
            {
                double age = time - rider.Start;
                if (age < 0) continue;
                if (age < rider.Life)
                {
                    var (x, y, z) = RiderPosition(rider, age);
                    if ((z < 0) != back) continue;
                    for (int j = 0; j < 7; j++)
                    {
                        double trailAge = age - j * 0.03;
                        if (trailAge < 0) break;
                        var (tx, ty, tz) = RiderPosition(rider, trailAge);
                        if ((tz < 0) != back) continue;
                        painter.Dot(tx, ty, j == 0 ? White : colors[j < 3 ? 6 : 5], (1 - j / 7.0) * (back ? 0.6 : 1));
                    }
                    painter.Glow(x, y, 4, colors[5], back ? 0.2 : 0.4);
                    continue;
                }

                double u = (age - rider.Life) / 0.36;
                if (back || u >= 1) continue;
                var (endX, endY, _) = RiderPosition(rider, rider.Life);
                double fade = 1 - Smooth(0.5, 1, u);
                if (u < 0.12) Sparkle(endX, endY, 3, White, colors[6], 0.5);
                else if (rider.Heart) DrawHeart(endX, endY - u * 6, fade);
                else Sparkle(endX, endY - u * 5, 1 + 2 * fade * (0.7 + 0.3 * Sin(age * 30)), White, colors[5], 0.45 * fade);
            }
        }

        private static bool HeartAt(int x, int y) => (uint)x < 5 && (uint)y < 4 && Heart[y][x] == '#';

        private void DrawHeart(double x, double y, double opacity)
        {
            int left = (int)Floor(x + 0.5) - 2, top = (int)Floor(y + 0.5) - 2;
            for (int j = -1; j <= 4; j++)
            for (int i = -1; i <= 5; i++)
            {
                if (HeartAt(i, j)) painter.Dot(left + i, top + j, i == 1 && j == 1 ? White : colors[j >= 2 ? 5 : 6], opacity);
                else if (HeartAt(i - 1, j) || HeartAt(i + 1, j) || HeartAt(i, j - 1) || HeartAt(i, j + 1))
                    painter.Dot(left + i, top + j, colors[3], opacity * 0.9);
            }
            painter.Glow(x, y, 6, colors[5], 0.4 * opacity);
        }

        private void Sparkle(double x, double y, double arm, uint core, uint tip, double light)
        {
            int length = (int)Floor(arm + 0.5);
            if (length < 1) return;
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= length; d++)
            {
                uint ink = d > length * 0.6 ? tip : core;
                painter.Dot(px + d, py, ink);
                painter.Dot(px - d, py, ink);
                painter.Dot(px, py + d, ink);
                painter.Dot(px, py - d, ink);
            }
            for (int d = 1; d <= Min(2, length / 3); d++)
            {
                uint ink = d == 1 ? core : tip;
                painter.Dot(px + d, py + d, ink);
                painter.Dot(px - d, py + d, ink);
                painter.Dot(px + d, py - d, ink);
                painter.Dot(px - d, py - d, ink);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + length * 2, core, light);
        }
    }
}
