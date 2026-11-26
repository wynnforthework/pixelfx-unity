using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class FireTornadoEffect
    {
        private static readonly uint[] SmokeRamp =
            { Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e) };

        private struct Ember
        {
            internal double Born, Height, Rise, Angle, Spin, Distance, Life;
        }

        private struct Smoke
        {
            internal double Born, Angle, Life, Size, Vy;
        }

        private struct ShedFlame
        {
            internal double Born, Height, Side, Speed, Life, Size, HeatOffset;
        }

        private Ember[] embers;
        private Smoke[] smoke;
        private ShedFlame[] shedFlames;

        private void InitializeParticles()
        {
            embers = new Ember[30];
            for (int i = 0; i < embers.Length; i++)
            {
                var random = new PixelFxRandom(unchecked(seed + 500), i);
                embers[i] = new Ember
                {
                    Born = random.Range(0, Duration), Height = random.Range(0.02, 0.7), Rise = random.Range(0.12, 0.3),
                    Angle = random.Range(0, Tau), Spin = random.Range(4, 7), Distance = random.Range(4, 12),
                    Life = random.Range(1.2, 2.4),
                };
            }
            smoke = new Smoke[20];
            for (int i = 0; i < smoke.Length; i++)
            {
                var random = new PixelFxRandom(unchecked(seed + 300), i);
                smoke[i] = new Smoke
                {
                    Born = random.Range(0, Duration), Angle = random.Range(0, Tau), Life = random.Range(1.2, 2),
                    Size = random.Range(2.5, 4), Vy = -random.Range(12, 22),
                };
            }
            shedFlames = new ShedFlame[70];
            for (int i = 0; i < shedFlames.Length; i++)
            {
                var random = new PixelFxRandom(unchecked(seed + 200), i);
                shedFlames[i] = new ShedFlame
                {
                    Born = random.Range(0, Duration), Height = random.Range(0.05, 0.95), Side = random.Range(0, 1) < 0.5 ? -1 : 1,
                    Speed = random.Range(20, 50), Life = random.Range(0.25, 0.5), Size = random.Range(1.4, 2.8),
                    HeatOffset = random.Range(0, 1),
                };
            }
        }

        private void DrawEmbers(double time, bool front)
        {
            foreach (var ember in embers)
            for (double age = Wrap(time - ember.Born); age < ember.Life; age += Duration)
            {
                double angle = ember.Angle + ember.Spin * age, sin = Sin(angle), cos = Cos(angle);
                if ((sin >= 0) != front) continue;
                double v = ember.Height + ember.Rise * age, capped = Min(1, v);
                double radius = Radius(capped) + ember.Distance + age * 4, k = 1 - age / ember.Life;
                double x = Center(capped, time / Duration) + cos * radius, y = -v * FunnelHeight + sin * radius * 0.28;
                double vx = -sin * radius * ember.Spin, vy = cos * radius * ember.Spin * 0.28;
                double speed = Sqrt(vx * vx + vy * vy), length = Min(5, 1 + speed * 0.025);
                uint ink = heat[k > 0.6 ? 1 : k > 0.3 ? 2 : 4];
                painter.Line(x - vx / speed * length, y - vy / speed * length, x, y,
                    (Head: ink, Tail: heat[front ? 6 : 7]), static (u, c) => (u > 0.6 ? c.Head : c.Tail, 1.0));
                painter.Glow(x, y, front ? 4 : 3, ink, (front ? 0.4 : 0.15) * k);
            }
        }

        private void DrawSmoke(double time)
        {
            foreach (var q in smoke)
            {
                double age = Wrap(time - q.Born);
                if (age >= q.Life) continue;
                double radius = Radius(1) * (0.8 + age * 0.5), angle = q.Angle + age * 3;
                double x = Center(1, (time - age) / Duration) + Cos(angle) * radius;
                double y = -FunnelHeight - 2 + q.Vy * age + Sin(angle) * radius * 0.25;
                double k = 1 - age / q.Life, progress = 1 - k;
                double r = q.Size * (0.65 + Min(1, progress * 2.5) * 0.45 + progress * 0.4);
                double a = Min(1, progress * 12) * Min(1, k * 2.4) * 0.58;
                uint ink = Mix(smokeInk, SmokeRamp[Min(4, (int)Floor(progress * SmokeRamp.Length))], progress);
                painter.SoftDisc(x, y, r, ink, a * 0.45, 0);
                painter.SoftDisc(x + r * 0.05, y + r * 0.06, r * 0.74, ink, a * 0.62, 0);
                if (progress < 0.5)
                    painter.SoftDisc(x - r * 0.22, y - r * 0.3, r * 0.5, Mix(ink, White, 0.12), a * 0.4 * (1 - progress * 2), 0);
            }
        }

        private void DrawShedFlames(double time)
        {
            foreach (var q in shedFlames)
            {
                double age = Wrap(time - q.Born);
                if (age >= q.Life) continue;
                double drag = 1 - Exp(-3 * age), terminalY = -60.0 / 3;
                double x = Center(q.Height, (time - age) / Duration) + q.Side * Radius(q.Height)
                    + q.Side * q.Speed * drag / 3;
                double y = -q.Height * FunnelHeight + terminalY * age + (-q.Speed * 0.6 - terminalY) * drag / 3;
                FireDrawing.Puff(painter, x, y, 1 - age / q.Life, q.Size, q.HeatOffset, heat, 0.1, -q.Side * 0.6, -0.8);
            }
        }
    }
}
