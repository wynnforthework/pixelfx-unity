using System;
using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class ResurrectionEffect
    {
        private enum ParticleKind { Spark, Mote, Streak, Star, Ember }

        private struct Particle
        {
            internal ParticleKind Kind;
            internal double X, Y, Vx, Vy, Life, MaxLife, Size, Seed;
            internal uint Ink;
        }

        private struct Flash
        {
            internal double X, Y, Radius, Strength, Life, MaxLife;
            internal uint Ink;
        }

        private readonly List<Particle> particles = new List<Particle>(128);
        private readonly List<FeatherParticle> loose = new List<FeatherParticle>(32);
        private readonly List<Flash> flashes = new List<Flash>(5);
        private readonly bool[,] detached = new bool[2, Design.Length];
        private bool poured, restored, haloFormed, beaten;

        private void Advance(double sample)
        {
            int target = (int)Floor(sample / Step + 1e-5);
            if (target < tick)
            {
                tick = 0;
                time = 0;
                random = initialRandom;
                poured = restored = haloFormed = beaten = false;
                particles.Clear();
                flashes.Clear();
                loose.Clear();
                Array.Clear(detached, 0, detached.Length);
            }
            while (tick < target)
            {
                tick++;
                time += Step;
                bool pour = !poured && time >= Pour;
                bool restore = !restored && time >= ClimbEnd;
                bool form = !haloFormed && time >= HaloEnd;
                bool beat = !beaten && time >= BeatEnd - 0.03;
                if (full) EmitParticles(pour, restore, beat);
                if (pour)
                {
                    AddFlash(0, -12, 42, colors[5], 0.9, 0.4);
                    AddFlash(0, -6, 16, White, 0.6, 0.1);
                }
                if (restore)
                {
                    AddFlash(0, -24, 64, colors[5], 0.7, 0.5);
                    AddFlash(0, -30, 28, White, 0.7, 0.1);
                }
                if (form) AddFlash(0, HaloY, 30, colors[6], 0.8, 0.3);
                if (beat)
                {
                    AddFlash(-30, -34, 46, colors[5], 0.6, 0.4);
                    AddFlash(30, -34, 46, colors[5], 0.6, 0.4);
                }
                poured |= pour;
                restored |= restore;
                haloFormed |= form;
                beaten |= beat;
                TickFlashes();
                if (full) TickParticles();
            }
        }

        private bool Chance(double chance) => random.Range(0, 1) < chance;
        private uint Either(uint first, uint second) => Chance(0.5) ? first : second;

        private void Spawn(ParticleKind kind, double x, double y, double vx, double vy, double life, uint ink = White, double size = 1) =>
            particles.Add(new Particle
            {
                Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Life = life, MaxLife = life,
                Ink = ink, Size = size, Seed = random.Range(0, 100),
            });

        private void EmitParticles(bool pour, bool restore, bool beat)
        {
            if (time > 0.05 && time < 2.9 && Chance(0.16))
            {
                double q = random.Range(-0.9, 0.9), y = Top + random.Range(8, 24), vy = random.Range(50, 90);
                Spawn(ParticleKind.Streak, q * HalfWidth(y), y, q * 2, vy, Log(1 + 3 * (-y - 2) / vy) / 3, Either(colors[6], colors[5]));
            }
            if (time > 0.3 && time < 2.8 && Chance(0.05))
                Spawn(ParticleKind.Star, random.Range(-1, 1) * HalfWidth(-30), -random.Range(4, 90),
                    random.Range(-3, 3), random.Range(-4, 6), random.Range(0.4, 0.8), Either(colors[6], White), random.Range(0.6, 1.8));
            if (pour)
            {
                for (int i = 0; i < 14; i++)
                    Spawn(ParticleKind.Star, random.Range(-18, 18), -random.Range(0, 6), random.Range(-20, 20),
                        -random.Range(10, 50), random.Range(0.5, 1.1), Either(White, colors[6]), random.Range(0.6, 2.2));
                for (int i = 0; i < 10; i++)
                {
                    double angle = -PI / 2 + random.Range(-1.2, 1.2), speed = random.Range(60, 150);
                    Spawn(ParticleKind.Spark, random.Range(-3, 3), -1, Cos(angle) * speed, Sin(angle) * speed, random.Range(0.25, 0.5));
                }
            }
            if (hasMask && time > ClimbStart && time < ClimbEnd)
            {
                int row = Round(ClimbY());
                EmitBodyRim(row);
                EmitBodyRim(row + 1);
            }
            if (restore)
                for (int i = 0; i < 18; i++)
                {
                    double angle = random.Range(0, Tau), speed = random.Range(30, 110), c = Cos(angle), s = Sin(angle);
                    Spawn(ParticleKind.Star, c * 6, -24 + s * 12, c * speed, s * speed * 0.7 - 20,
                        random.Range(0.4, 0.9), Either(White, colors[6]), random.Range(0.6, 2));
                }
            if (time > HaloStart - 0.2 && time < HaloStart + 0.1 && Chance(0.6))
            {
                double angle = random.Range(0, Tau), distance = random.Range(14, 24);
                double x = Cos(angle) * distance, y = HaloY + Sin(angle) * distance * 0.5;
                double dx = Cos(angle + 0.6) * HaloRx - x, dy = HaloY + Sin(angle + 0.6) * HaloRy - y;
                double life = random.Range(0.16, 0.24), length = Sqrt(dx * dx + dy * dy);
                double speed = (length == 0 ? 1 : length) * 3 / (Exp(3 * life) - 1);
                length = length == 0 ? 1 : length;
                Spawn(ParticleKind.Streak, x, y, dx / length * speed, dy / length * speed, life, Either(colors[6], colors[5]));
            }
            EmitWingParticles(beat);
            if (time > 3.35 && time < 3.75 && Chance(0.4))
            {
                double angle = random.Range(0, Tau);
                Spawn(ParticleKind.Ember, Cos(angle) * HaloRx, HaloY + Sin(angle) * HaloRy,
                    random.Range(-6, 6), -random.Range(10, 25), random.Range(0.5, 0.9), colors[5]);
            }
        }

        private void EmitBodyRim(int y)
        {
            Span<int> xs = stackalloc int[Width];
            int count = 0;
            for (int x = -OriginX; x < Width - OriginX; x++)
                if (SampleActor(x, y).a != 0 && IsEdge(x, y)) xs[count++] = x;
            if (count == 0 || !Chance(0.35)) return;
            int edgeX = xs[(int)random.Range(0, count)], side = Sign(edgeX + 0.5);
            Spawn(Chance(0.6) ? ParticleKind.Mote : ParticleKind.Star, edgeX + side, y, side * random.Range(8, 30),
                -random.Range(15, 45), random.Range(0.35, 0.7), Either(colors[6], colors[5]), random.Range(0.5, 1.1));
        }

        private WingFeather PickWingFeather(int side, int kind = -1)
        {
            Span<int> indices = stackalloc int[Design.Length];
            int count = 0;
            for (int i = 0; i < quills.Length; i++)
                if ((kind < 0 || kind == quills[i].Kind) && time < quills[i].Off(side) + 0.05) indices[count++] = i;
            return WingAt(indices[(int)random.Range(0, count)], time, side);
        }

        private void EmitWingParticles(bool beat)
        {
            if (time > WingStart && time < WingStart + 0.35 && Chance(0.5))
                for (int side = -1; side <= 1; side += 2)
                {
                    var f = PickWingFeather(side);
                    var (x, y) = f.Point(f.Length);
                    Spawn(ParticleKind.Star, x, y, side * random.Range(5, 25), -random.Range(5, 25),
                        random.Range(0.3, 0.6), Either(White, colors[6]), random.Range(0.6, 1.6));
                }

            if (time > WingStart + 0.45 && time < Scatter)
                for (int side = -1; side <= 1; side += 2)
                {
                    if (Chance(0.045))
                    {
                        var f = PickWingFeather(side, 0);
                        var (x, y) = f.Point(f.Length - 2);
                        Spawn(ParticleKind.Star, x, y, 0, -random.Range(2, 8),
                            random.Range(0.3, 0.6), Either(White, colors[6]), random.Range(1, 2.4));
                    }
                    if (Chance(0.03))
                    {
                        var f = PickWingFeather(side);
                        var (x, y) = f.Point(f.Length * 0.7);
                        Spawn(ParticleKind.Ember, x, y, random.Range(-8, 8), -random.Range(8, 25),
                            random.Range(0.6, 1.1), colors[5]);
                    }
                }

            if (beat)
                for (int side = -1; side <= 1; side += 2)
                    EmitWingbeat(side);
            if (time >= Scatter)
                for (int side = -1; side <= 1; side += 2)
                    DetachQuills(side);
        }

        private void EmitWingbeat(int side)
        {
            for (int i = 0; i < 7; i++)
                Spawn(ParticleKind.Mote, side * random.Range(8, 22), -random.Range(1, 4), side * random.Range(90, 160),
                    -random.Range(0, 8), random.Range(0.25, 0.45), Either(colors[6], colors[5]), random.Range(0.6, 1));
            for (int i = 0; i < quills.Length; i++)
            {
                if (quills[i].Kind != 0) continue;
                var f = WingAt(i, time, side);
                var (x, y) = f.Point(f.Length);
                Spawn(ParticleKind.Spark, x, y, side * random.Range(30, 90), random.Range(60, 160), random.Range(0.3, 0.6));
            }
            for (int i = 0; i < 3; i++)
            {
                var f = PickWingFeather(side, 1);
                var (x, y) = f.Point(f.Length * 0.6);
                AddFeather(x, y, side * random.Range(20, 70), random.Range(10, 50), f.Angle, side, false);
            }
        }

        private void DetachQuills(int side)
        {
            int wing = side > 0 ? 0 : 1;
            for (int i = 0; i < quills.Length; i++)
            {
                var q = quills[i];
                double off = q.Off(side);
                if (detached[wing, i] || time < off) continue;
                detached[wing, i] = true;
                var f = WingAt(i, off - 0.001, side);
                double dx = Cos(f.Angle) * f.Length, dy = Sin(f.Angle) * f.Length;
                if (q.Kind != 2)
                {
                    var (x, y) = f.Point(f.Length * 0.55);
                    double ox = x - side * 4, oy = y + 20, length = Sqrt(ox * ox + oy * oy);
                    double speed = random.Range(60, 150);
                    length = length == 0 ? 1 : length;
                    AddFeather(x, y, ox / length * speed + random.Range(-15, 15), oy / length * speed * 0.5 - random.Range(20, 50),
                        f.Angle + random.Range(-0.4, 0.4), side, true);
                }
                for (int j = 0; j < 2; j++)
                    Spawn(ParticleKind.Ember, f.X + dx * random.Range(0.3, 1), f.Y + dy * random.Range(0.3, 1),
                        random.Range(-15, 15), -random.Range(10, 40), random.Range(0.6, 1.1), colors[5]);
                if (Chance(0.6))
                    Spawn(ParticleKind.Star, f.X + dx, f.Y + dy, 0, -random.Range(5, 15), random.Range(0.3, 0.6), White, random.Range(1, 2));
            }
        }

        private void AddFeather(double x, double y, double vx, double vy, double angle, int side, bool scattering)
        {
            var f = new FeatherParticle
            {
                X = x, Y = y, Vx = vx, Vy = vy, Angle = angle,
                Length = random.Range(9, 12), Width = random.Range(2.2, 2.8),
                Life = scattering ? random.Range(1, 1.45) : random.Range(1.2, 1.6),
                Back = scattering && Chance(0.35),
            };
            loose.Add(FeatherParticle.Initialize(f, ref random, side: side, useDefaultSink: true));
        }

        private void TickParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var q = particles[i];
                if ((q.Life -= Step) <= 0)
                {
                    particles.RemoveAt(i);
                    continue;
                }
                switch (q.Kind)
                {
                    case ParticleKind.Spark:
                        q.Vy += 950 * 0.45 * Step;
                        q.Vx -= q.Vx * 2.2 * Step;
                        q.Vy -= q.Vy * 2.2 * Step;
                        break;
                    case ParticleKind.Streak:
                        q.Vx += q.Vx * 3 * Step;
                        q.Vy += q.Vy * 3 * Step;
                        break;
                    case ParticleKind.Mote:
                        q.Vx -= q.Vx * 0.6 * Step;
                        q.Vy -= q.Vy * 0.6 * Step;
                        break;
                    case ParticleKind.Ember:
                        q.Vy += (-30 + Sin(time * 3 + q.Seed) * 20) * Step;
                        q.Vx += Cos(time * 2.3 + q.Seed) * 45 * Step;
                        q.Vx -= q.Vx * 1.6 * Step;
                        q.Vy -= q.Vy * 1.6 * Step;
                        break;
                    case ParticleKind.Star:
                        q.Vx -= q.Vx * 1.6 * Step;
                        q.Vy -= q.Vy * 1.6 * Step + 5 * Step;
                        break;
                }
                q.X += q.Vx * Step;
                double nextY = q.Y + q.Vy * Step;
                if (q.Kind == ParticleKind.Spark && q.Vy > 0 && q.Y <= 0 && nextY >= 0)
                {
                    q.Vy *= -0.45;
                    q.Vx *= 0.7;
                    nextY = -0.25;
                }
                q.Y = nextY;
                particles[i] = q;
            }
            for (int i = loose.Count - 1; i >= 0; i--)
            {
                var f = loose[i];
                FeatherParticle.Tick(ref f, Step, 0);
                if (f.Life <= 0) loose.RemoveAt(i);
                else loose[i] = f;
            }
        }

        private void DrawLoose(bool back)
        {
            foreach (var f in loose)
                if (f.Back == back) FeatherParticle.Draw(painter, f, colors, 2);
        }

        private void DrawParticles()
        {
            foreach (var q in particles)
            {
                double x = q.X, y = q.Y, k = q.Life / q.MaxLife;
                double speed = Sqrt(q.Vx * q.Vx + q.Vy * q.Vy);
                if (speed == 0) speed = 1;
                double ux = q.Vx / speed, uy = q.Vy / speed;
                switch (q.Kind)
                {
                    case ParticleKind.Spark:
                    {
                        uint head = k > 0.6 ? White : colors[k > 0.3 ? 6 : 5];
                        double length = Min(9, 1 + speed * 0.022);
                        painter.Line(x - ux * length, y - uy * length, x - ux * length * 0.4, y - uy * length * 0.4, k > 0.5 ? head : colors[3]);
                        painter.Line(x - ux * length * 0.45, y - uy * length * 0.45, x, y, head);
                        painter.Glow(x, y, 3, head, 0.45 * k);
                        break;
                    }
                    case ParticleKind.Mote:
                    {
                        double length = Min(16, 0.5 + speed * 0.026) * q.Size, alpha = Min(1, k / 0.35);
                        painter.Line(x - ux * length, y - uy * length, x - ux * length * 0.35, y - uy * length * 0.35, Scale(q.Ink, 0.55), alpha);
                        painter.Line(x - ux * length * 0.4, y - uy * length * 0.4, x, y, q.Ink, alpha);
                        painter.Glow(x, y, 2.5 + q.Size, q.Ink, 0.4 * k);
                        break;
                    }
                    case ParticleKind.Streak:
                    {
                        double length = Max(1, Min(7, speed / 30));
                        for (double d = length; d >= 0; d -= 0.5)
                            painter.Dot(x - ux * d, y - uy * d, d < 1 ? White : d < length * 0.5 ? q.Ink : Scale(q.Ink, 0.6),
                                d < length * 0.5 ? 1 : 1.2 - d / length);
                        painter.Glow(x, y, 4, q.Ink, 0.4);
                        break;
                    }
                    case ParticleKind.Ember:
                    {
                        if (k < 0.25 && Sin(time * 40 + q.Seed) < 0) break;
                        uint ink = k > 0.6 ? White : colors[k > 0.3 ? 6 : 5];
                        painter.Dot(Floor(x), Floor(y), ink);
                        painter.Glow(x, y, 3 + q.Size, ink, 0.35 * k);
                        break;
                    }
                    case ParticleKind.Star:
                    {
                        double twinkle = 0.6 + 0.4 * Sin(time * (8 + q.Seed % 5) + q.Seed);
                        if (q.Size < 1)
                        {
                            if (twinkle * k > 0.25) painter.Dot(Floor(x), Floor(y), twinkle > 0.8 ? White : q.Ink);
                            painter.Glow(x, y, 3, q.Ink, 0.4 * k * twinkle);
                        }
                        else Sparkle(x, y, q.Size * twinkle * Min(1, k * 3), q.Ink, Scale(q.Ink, 0.75), 0.5 * k * twinkle);
                        break;
                    }
                }
            }
        }

        private void AddFlash(double x, double y, double radius, uint ink, double strength, double life) =>
            flashes.Add(new Flash { X = x, Y = y, Radius = radius, Ink = ink, Strength = strength, Life = life, MaxLife = life });

        private void TickFlashes()
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var flash = flashes[i];
                if ((flash.Life -= Step) <= 0) flashes.RemoveAt(i);
                else flashes[i] = flash;
            }
        }

        private void DrawLights()
        {
            foreach (var flash in flashes)
            {
                double k = flash.Life / flash.MaxLife;
                painter.Glow(flash.X, flash.Y, flash.Radius * (0.55 + 0.45 * k), flash.Ink, flash.Strength * k * k);
            }
        }

        private void Sparkle(double x, double y, double length, uint ink, uint tip, double strength)
        {
            int arm = Round(length);
            if (arm < 1) return;
            int px = (int)Floor(x), py = (int)Floor(y);
            for (int d = 1; d <= arm; d++)
            {
                uint color = d > arm * 0.6 ? tip : ink;
                painter.Dot(px + d, py, color);
                painter.Dot(px - d, py, color);
                painter.Dot(px, py + d, color);
                painter.Dot(px, py - d, color);
            }
            for (int d = 1; d <= arm / 3 && d <= 2; d++)
            {
                uint color = d == 1 ? ink : tip;
                painter.Dot(px + d, py + d, color);
                painter.Dot(px + d, py - d, color);
                painter.Dot(px - d, py + d, color);
                painter.Dot(px - d, py - d, color);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + arm * 2, ink, strength);
        }
    }
}
