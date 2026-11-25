using System;
using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed class FeatherBurstEffect
    {
        internal const int Width = 160, Height = 160, OriginX = 80, OriginY = 60;
        internal const float Duration = 3;
        private const double Tau = 2 * PI, Step = 1.0 / 120;
        private static readonly uint WarmGlow = Hex(0xfff0c0);
        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly bool full;
        private readonly (double X, double Y, double Start, double End, double Life)[] lobes = new (double, double, double, double, double)[11];
        private readonly FeatherParticle[] initialFeathers = new FeatherParticle[30];
        private readonly List<FeatherParticle> feathers = new List<FeatherParticle>(30);
        private readonly List<Particle> initialParticles = new List<Particle>(36);
        private readonly List<Particle> particles = new List<Particle>(48);
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private double time, warmLife = 0.45, whiteLife = 0.1;
        private int tick;

        private struct Particle
        {
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Seed;
            internal uint Ink;
            internal bool Flake;
        }

        private FeatherBurstEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            full = timeline.Quality == PixelFxQuality.Full;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            random = new PixelFxRandom(settings.seed);
            for (int i = 0; i < lobes.Length; i++)
            {
                double angle = i / 11.0 * Tau + random.Range(-0.25, 0.25), distance = random.Range(5, 14);
                lobes[i] = (Cos(angle) * distance, Sin(angle) * distance * 0.8,
                    random.Range(2.5, 4), random.Range(6, 9.5), random.Range(0.45, 0.8));
            }

            for (int i = 0; i < 20; i++)
            {
                double angle = i / 20.0 * Tau + random.Range(-0.15, 0.15), speed = random.Range(110, 230);
                double sine = Sin(angle), cosine = Cos(angle);
                var f = new FeatherParticle
                {
                    X = cosine * 3, Y = sine * 3, Vx = cosine * speed, Vy = sine * speed * (sine < 0 ? 0.7 : 0.9) - 20,
                    Angle = angle + random.Range(-0.5, 0.5), Length = random.Range(10, 14), Width = random.Range(2.6, 3.4),
                    Life = random.Range(1.9, 2.6), Delay = random.Range(0, 0.03), Sink = random.Range(26, 38),
                };
                initialFeathers[i] = FeatherParticle.Initialize(f, ref random, random.Range(8, 13));
            }
            for (int i = 20; i < initialFeathers.Length; i++)
            {
                double angle = random.Range(0, Tau), speed = random.Range(50, 130);
                var f = new FeatherParticle
                {
                    Vx = Cos(angle) * speed, Vy = Sin(angle) * speed - 20, Angle = random.Range(0, Tau),
                    Length = random.Range(6, 8), Width = random.Range(1.8, 2.2), Life = random.Range(1.6, 2.4),
                    Back = true, Sink = random.Range(18, 28),
                };
                initialFeathers[i] = FeatherParticle.Initialize(f, ref random);
            }
            if (full)
            {
                for (int i = 0; i < 26; i++)
                {
                    double angle = random.Range(0, Tau), speed = random.Range(30, 140);
                    Spawn(random.Range(-3, 3), random.Range(-3, 3), Cos(angle) * speed, Sin(angle) * speed - 15,
                        random.Range(1.4, 2.6), Chance(0.6) ? White : colors[4], random.Range(0.6, 1.6), true);
                }
                for (int i = 0; i < 10; i++)
                {
                    double angle = random.Range(0, Tau), speed = random.Range(60, 160);
                    Spawn(0, 0, Cos(angle) * speed, Sin(angle) * speed, random.Range(0.3, 0.6),
                        Chance(0.5) ? White : WarmGlow, random.Range(0.8, 2.2));
                }
                initialParticles.AddRange(particles);
            }
            initialRandom = random;
            Reset();
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new FeatherBurstEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Reset()
        {
            tick = 0;
            time = 0;
            warmLife = 0.45;
            whiteLife = 0.1;
            random = initialRandom;
            feathers.Clear();
            for (int i = 0; i < initialFeathers.Length; i++)
                if (full || i % 2 == 0) feathers.Add(initialFeathers[i]);
            particles.Clear();
            particles.AddRange(initialParticles);
        }

        private bool Chance(double probability) => random.Range(0, 1) < probability;
        private static double OutCubic(double u) => 1 - Pow(1 - u, 3);

        private void Spawn(double x, double y, double vx, double vy, double life, uint ink, double size, bool flake = false)
        {
            particles.Add(new Particle
            {
                X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life, Ink = ink,
                Size = size, Seed = random.Range(0, 1) * 100, Flake = flake,
            });
        }

        private void Advance(double sample)
        {
            int target = (int)Floor(sample * 120 + 0.00001);
            if (target < tick) Reset();
            while (tick < target)
            {
                tick++;
                time += Step;
                warmLife -= Step;
                whiteLife -= Step;
                if (full && time > 0.3 && time < 2.2 && feathers.Count > 0 && Chance(0.14))
                {
                    var f = feathers[(int)random.Range(0, feathers.Count)];
                    if (f.Delay <= 0)
                        Spawn(f.X + random.Range(-2, 2), f.Y + random.Range(-1, 1), 0, 0, random.Range(0.2, 0.4),
                            Chance(0.5) ? White : WarmGlow, random.Range(1, 2.2));
                }
                for (int i = particles.Count - 1; i >= 0; i--)
                {
                    var q = particles[i];
                    q.Life -= Step;
                    if (q.Life <= 0)
                    {
                        particles.RemoveAt(i);
                        continue;
                    }
                    if (q.Flake)
                    {
                        q.Vx -= q.Vx * 2.2 * Step;
                        q.Vy -= q.Vy * 2.2 * Step - 35 * Step;
                        q.X += Sin(time * 2.5 + q.Seed) * 7 * Step;
                    }
                    else
                    {
                        q.Vx -= q.Vx * 1.6 * Step;
                        q.Vy -= q.Vy * 1.6 * Step + 5 * Step;
                    }
                    q.X += q.Vx * Step;
                    q.Y += q.Vy * Step;
                    particles[i] = q;
                }
                for (int i = feathers.Count - 1; i >= 0; i--)
                {
                    var f = feathers[i];
                    FeatherParticle.Tick(ref f, Step, double.PositiveInfinity);
                    if (f.Life <= 0) feathers.RemoveAt(i);
                    else feathers[i] = f;
                }
            }
        }

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            Advance(sample);
            painter.Clear();
            if (pass != PixelFxRenderPass.Front)
            {
                DrawFeathers(true);
                DrawPuff();
            }
            if (pass != PixelFxRenderPass.Back)
            {
                DrawPop();
                DrawFeathers(false);
                DrawParticles();
                DrawLight(50, WarmGlow, 0.75, warmLife / 0.45);
                DrawLight(20, White, 0.7, whiteLife / 0.1);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawFeathers(bool back)
        {
            foreach (var f in feathers)
                if (f.Back == back && f.Delay <= 0) FeatherParticle.Draw(painter, f, colors);
        }

        private void DrawPuff()
        {
            foreach (var (startX, startY, startRadius, endRadius, life) in lobes)
            {
                double u = time / life;
                if (u >= 1) continue;
                double expansion = 0.4 + OutCubic(u) * 1.3, x = startX * expansion, y = startY * expansion - u * 5;
                double radius = (startRadius + (endRadius - startRadius) * OutCubic(Min(1, u * 3))) * (1 - u * u);
                if (radius < 0.6) continue;
                painter.Circle(x, y, radius + 0.8, colors[2]);
                painter.Shade(x - radius, y - radius, x + radius, y + radius,
                    (X: x, Y: y, Radius: radius, Colors: colors), static (px, py, s) =>
                    {
                        double dx = px - s.X, dy = py - s.Y;
                        double d = Sqrt(dx * dx + dy * dy) / s.Radius;
                        if (d > 1) return default;
                        double light = Sin(Atan2(dy, dx) + 0.8);
                        return (d > 0.55 && light > 0.3 ? s.Colors[3] : d < 0.45 && light < -0.2 ? White : s.Colors[4], 1);
                    });
            }
        }

        private void DrawPop()
        {
            if (time < 0.07) painter.Circle(0, 0, 9 * (1 - time / 0.14), White);
            if (time < 0.25)
            {
                double u = time / 0.25;
                double radius = 6 + 24 * OutCubic(u), width = Max(1, 2.5 * (1 - u));
                painter.Shade(-radius, -radius, radius, radius,
                    (Radius: radius, Inner: radius - width, Width: width, Alpha: 1.1 - u, Ink: Mix(colors[4], WarmGlow, 0.4)),
                    static (x, y, s) =>
                    {
                        double distance = x * x + y * y;
                        if (distance > s.Radius * s.Radius || distance < s.Inner * s.Inner) return default;
                        return ((Sqrt(distance) - s.Inner) / s.Width > 0.5 ? White : s.Ink, s.Alpha);
                    });
            }
            if (time < 0.2) Sparkle(0, 0, 10 * (1 - time / 0.2), White, WarmGlow, 0.6);
            double fade = Min(1, time / 1.3);
            painter.Glow(0, 4, 46, WarmGlow, 0.28 * (1 - fade * fade * (3 - 2 * fade)));
        }

        private void DrawLight(double radius, uint ink, double strength, double k)
        {
            if (k > 0) painter.Glow(0, 0, radius * (0.55 + 0.45 * k), ink, strength * k * k);
        }

        private void DrawParticles()
        {
            foreach (var q in particles)
            {
                double k = q.Life / q.MaxLife;
                if (q.Flake)
                {
                    double alpha = Min(1, k * 3);
                    painter.Dot(q.X, q.Y, q.Ink, alpha);
                    if (q.Size <= 1.3) continue;
                    uint edge = Scale(q.Ink, 0.8);
                    painter.Dot(q.X + 1, q.Y, edge, alpha * 0.8);
                    painter.Dot(q.X - 1, q.Y, edge, alpha * 0.8);
                    painter.Dot(q.X, q.Y + 1, edge, alpha * 0.8);
                    painter.Dot(q.X, q.Y - 1, edge, alpha * 0.8);
                    continue;
                }
                double twinkle = 0.6 + 0.4 * Sin(time * (8 + q.Seed % 5) + q.Seed);
                if (q.Size < 1)
                {
                    if (twinkle * k > 0.25) painter.Dot(q.X, q.Y, twinkle > 0.8 ? White : q.Ink);
                    painter.Glow(q.X, q.Y, 3, q.Ink, 0.4 * k * twinkle);
                }
                else Sparkle(q.X, q.Y, q.Size * twinkle * Min(1, k * 3), q.Ink, Scale(q.Ink, 0.75), 0.5 * k * twinkle);
            }
        }

        private void Sparkle(double x, double y, double arm, uint ink, uint tip, double glow)
        {
            int length = (int)Floor(arm + 0.5);
            if (length < 1) return;
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= length; d++)
            {
                uint c = d > length * 0.6 ? tip : ink;
                painter.Dot(px + d, py, c);
                painter.Dot(px - d, py, c);
                painter.Dot(px, py + d, c);
                painter.Dot(px, py - d, c);
            }
            for (int d = 1; d <= Min(2, length / 3); d++)
            {
                uint c = d == 1 ? ink : tip;
                painter.Dot(px + d, py + d, c);
                painter.Dot(px + d, py - d, c);
                painter.Dot(px - d, py + d, c);
                painter.Dot(px - d, py - d, c);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + length * 2, ink, glow);
        }
    }
}
