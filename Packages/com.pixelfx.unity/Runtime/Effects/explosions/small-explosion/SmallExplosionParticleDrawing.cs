using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class SmallExplosionParticles
    {
        private static readonly uint[] Hot = { Hex(0xfff3a6), Hex(0xffe26b), Hex(0xff8a2a), Hex(0xf2561d) };
        private static readonly uint[] FireRamp =
        {
            White, Hex(0xfff3a6), Hex(0xffe26b), Hex(0xffc23d), Hex(0xff8a2a),
            Hex(0xf2561d), Hex(0xc9281f), Hex(0x8a1f17), Hex(0x4a1712),
        };
        private static readonly uint[] SmokeRamp = { Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e) };
        private static readonly uint Outline = Hex(0x1a1422);

        internal void DrawBack(PixelPainter painter)
        {
            DrawParticles(painter, true);
            foreach (var b in blazes)
            {
                double k = b.Life / b.MaxLife;
                painter.Dot(b.X, -0.25, k > 0.4 ? (Sin(b.Life * 31 + b.X) > 0 ? Hot[1] : Hot[2]) : Hot[3]);
                painter.Glow(b.X, -0.25, 6 + 5 * k * b.Power, Hot[2], 0.3 * k * b.Power);
            }
        }

        internal void DrawFront(PixelPainter painter)
        {
            foreach (var rock in rocks)
            {
                if (rock.Life <= 0) continue;
                double radius = 0.6 + rock.Size * 0.75;
                var light = (X: Cos(rock.Angle) * radius * 0.4, Y: Sin(rock.Angle) * radius * 0.4);
                painter.Shade(rock.X - radius - 1, rock.Y - radius - 1, rock.X + radius + 1, rock.Y + radius + 1,
                    (rock, radius, light, stone), static (x, y, s) =>
                    {
                        double dx = x - s.rock.X, dy = y - s.rock.Y;
                        double distance = Sqrt(dx * 1.15 * (dx * 1.15) + dy * dy);
                        if (distance > s.radius + 0.9) return default;
                        double lit = dx * s.light.X + dy * s.light.Y;
                        uint ink = distance > s.radius ? Outline : lit < -0.4 ? s.stone.Light : lit > 0.6 ? s.stone.Shade : s.stone.Body;
                        return (ink, Min(1, s.rock.Life / 0.5));
                    });
            }
            DrawParticles(painter, false);
        }

        private void DrawParticles(PixelPainter p, bool back)
        {
            foreach (var q in particles)
            {
                bool behind = q.Kind == Kind.Smoke || q.Kind == Kind.Dust || (q.Flags & Flags.Back) != 0;
                if (q.Delay > 0 || behind != back) continue;
                double k = q.Life / q.MaxLife;
                switch (q.Kind)
                {
                    case Kind.Dust:
                        p.SoftDisc(q.X, q.Y, q.Size * (0.6 + (1 - k) * 0.9), q.Color, Pow(k, 1.3) * 0.7, 0);
                        break;
                    case Kind.Smoke:
                        DrawSmoke(p, q, k);
                        break;
                    case Kind.Fire:
                        DrawFire(p, q, k);
                        break;
                    case Kind.Spark:
                        DrawSpark(p, q, k);
                        break;
                    case Kind.Ember:
                        if (k < 0.25 && Sin(time * 40 + q.Seed) < 0) break;
                        uint ink = SparkColor(k);
                        p.Dot(q.X, q.Y, ink);
                        p.Glow(q.X, q.Y, 3 + q.Size, ink, 0.35 * k);
                        break;
                    case Kind.Brand:
                        p.Circle(q.X, q.Y, 1.6, Hot[3]);
                        p.Dot(q.X, q.Y, Sin(time * 40 + q.Seed) > 0 ? Hot[0] : Hot[1]);
                        p.Dot(q.X + 1, q.Y, Hot[1]);
                        p.Glow(q.X, q.Y, 7, Hot[2], 0.45);
                        break;
                    case Kind.Grit:
                        if (k <= 0.125) break;
                        p.Dot(q.X, q.Y, q.Color);
                        if (q.Size < 2) break;
                        p.Dot(q.X + 1, q.Y, q.Color);
                        p.Dot(q.X, q.Y + 1, Scale(q.Color, 0.8));
                        p.Dot(q.X + 1, q.Y + 1, Scale(q.Color, 0.7));
                        break;
                }
            }
        }

        private uint SparkColor(double life) => colors[life > 0.6 ? 6 : life > 0.3 ? 5 : 3];

        private void DrawSpark(PixelPainter p, Particle q, double life)
        {
            double speed = Sqrt(q.Vx * q.Vx + q.Vy * q.Vy);
            if (speed == 0) speed = 1;
            double length = Min(9, 1 + speed * 0.022);
            double dx = q.Vx / speed * length, dy = q.Vy / speed * length;
            uint ink = SparkColor(life);
            p.Line(q.X - dx, q.Y - dy, q.X - dx * 0.4, q.Y - dy * 0.4, life > 0.5 ? ink : colors[2]);
            p.Line(q.X - dx * 0.45, q.Y - dy * 0.45, q.X, q.Y, ink);
            p.Glow(q.X, q.Y, 3, ink, 0.45 * life);
        }

        private static void DrawSmoke(PixelPainter p, Particle q, double life)
        {
            double age = 1 - life;
            bool big = (q.Flags & Flags.Big) != 0;
            double r = q.Size * (0.65 + Min(1, age * 2.5) * 0.45 + age * (big ? 0.5 : 0.4));
            double a = Min(1, age * 12) * Min(1, life * 2.4) * (big ? 0.7 : 0.58);
            int index = Min(4, (int)((q.Color != 0 ? age : age * 1.05) * SmokeRamp.Length));
            uint ink = q.Color != 0 ? Mix(q.Color, SmokeRamp[index], age) : SmokeRamp[index];
            p.SoftDisc(q.X, q.Y, r, ink, a * 0.45, 0);
            p.SoftDisc(q.X + r * 0.05, q.Y + r * 0.06, r * 0.74, ink, a * 0.62, 0);
            if (age < 0.5)
                p.SoftDisc(q.X - r * 0.22, q.Y - r * 0.3, r * 0.5, Mix(ink, White, 0.12), a * 0.4 * (1 - age * 2), 0);
        }

        private static void DrawFire(PixelPainter p, Particle q, double life)
        {
            double age = 1 - life;
            int index = Min(7, (int)(Min(0.999, Pow(age, 0.62) + q.Seed % 1 * 0.1) * 8));
            double r = q.Size * (0.55 + 0.45 * Min(1, age * 7)) * (1 - age * 0.55);
            uint ink = FireRamp[index];
            if (index == 7)
            {
                p.SoftDisc(q.X, q.Y, r, ink, Min(1, life * 3) * 0.85, 0);
                return;
            }
            p.Circle(q.X, q.Y, r, FireRamp[index + 2]);
            p.Circle(q.X - r * 0.16, q.Y - r * 0.22, r * 0.78, ink);
            if (r > 1.6) p.Circle(q.X, q.Y - r * 0.9, r * 0.42, ink);
            if (index <= 3 && r > 1.8)
                p.Circle(q.X - r * 0.28, q.Y - r * 0.34, r * 0.36, FireRamp[Max(0, index - 1)]);
            if (index < 6) p.Glow(q.X, q.Y, r * 2.4 + 2, ink, 0.18 * life);
        }
    }
}
