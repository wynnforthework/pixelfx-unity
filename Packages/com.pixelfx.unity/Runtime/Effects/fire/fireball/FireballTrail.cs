using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed class FireballTrail
    {
        private const int PuffCount = 130;
        private static readonly uint[] SmokeRamp =
            { Hex(0x7b6f72), Hex(0x5d5660), Hex(0x433d48), Hex(0x2b2630), Hex(0x1a171e) };
        private readonly uint[] heat;
        private readonly uint smoke;
        private readonly double speed, radius;
        private readonly Puff[] puffs = new Puff[PuffCount];
        private readonly Mote[] motes = new Mote[26];
        private readonly Ember[] embers = new Ember[30];
        private readonly (double Age, double X, double Y, int Index)[] flames = new (double, double, double, int)[PuffCount];

        private struct Puff
        {
            internal double Birth, X, Y, Vx, Vy, FireLife, SmokeLife, Size, Heat;
            internal bool Smokes;
        }

        private struct Mote
        {
            internal double Birth, Angle, Speed, Life;
        }

        private struct Ember
        {
            internal double Birth, X, Y, Vx, Vy, Life, Phase;
            internal bool Big;
        }

        internal FireballTrail(int seed, uint[] heat, double speed, double radius)
        {
            this.heat = heat;
            this.speed = speed;
            this.radius = radius;
            smoke = Mix(Hex(0x5b5566), heat[6], 0.3);
            for (int i = 0; i < puffs.Length; i++)
            {
                var random = new PixelFxRandom(seed + 100, i);
                double birth = random.Range(0, FireballEffect.Period);
                double life = random.Range(0.22, 0.55);
                bool smokes = random.Range(0, 1) < 0.55;
                double x = -random.Range(2, 24);
                puffs[i] = new Puff
                {
                    Birth = birth, FireLife = life, Smokes = smokes, X = x,
                    Y = random.Range(-1, 1) * (radius + 0.1 * x),
                    Vx = speed * random.Range(0.3, 0.6), Vy = random.Range(-16, 16),
                    SmokeLife = random.Range(0.25, 0.45), Size = random.Range(1.3, 2.9),
                    Heat = random.Range(0, 1) * 0.5 + 0.5 * (-x / 24),
                };
            }
            for (int i = 0; i < motes.Length; i++)
            {
                var random = new PixelFxRandom(seed + 200, i);
                motes[i] = new Mote
                {
                    Birth = random.Range(0, FireballEffect.Period), Angle = random.Range(-2.4, 2.4),
                    Speed = random.Range(30, 70), Life = random.Range(0.08, 0.18),
                };
            }
            for (int i = 0; i < embers.Length; i++)
            {
                var random = new PixelFxRandom(seed + 300, i);
                embers[i] = new Ember
                {
                    Birth = random.Range(0, FireballEffect.Period), X = -random.Range(2, 16),
                    Y = random.Range(-3, 3), Vx = speed * random.Range(0.3, 0.8),
                    Vy = random.Range(-20, 20), Life = random.Range(0.25, 0.55),
                    Big = random.Range(0, 1) < 0.25, Phase = random.Range(0, 6.3),
                };
            }
        }

        internal void DrawTail(PixelPainter p, double time)
        {
            int count = 0;
            for (int i = 0; i < puffs.Length; i++)
            {
                var puff = puffs[i];
                double age = Age(time, puff.Birth);
                double smokeAge = age - puff.FireLife;
                if (smokeAge >= 0 && (!puff.Smokes || smokeAge > puff.SmokeLife)) continue;

                double t = Min(age, puff.FireLife);
                var (dx, vx) = Drift(0, puff.Vx, 0, 4.5, t);
                var (y, vy) = Drift(puff.Y, puff.Vy, -70, 3, t);
                double x = puff.X + dx - speed * t;
                if (smokeAge >= 0)
                {
                    var (sx, _) = Drift(x, vx - speed, speed * 2, 2, smokeAge);
                    var (sy, _) = Drift(y, vy * 0.6 - 6, -30, 1.2, smokeAge);
                    Smoke(p, sx, sy, 1 - smokeAge / puff.SmokeLife, puff.Size * 0.9);
                    continue;
                }

                // Older flames draw first. Keep emission order for equal ages.
                int slot = count++;
                while (slot > 0 && flames[slot - 1].Age < age)
                {
                    flames[slot] = flames[slot - 1];
                    slot--;
                }
                flames[slot] = (age, x, y, i);
            }
            for (int i = 0; i < count; i++)
            {
                var (age, x, y, index) = flames[i];
                var puff = puffs[index];
                double u = age / puff.FireLife;
                Flame(p, x, y, 1 - u, puff.Size * (1 + 1.3 * u), puff.Heat);
            }
        }

        internal void DrawSparks(PixelPainter p, double time, double headX, double headY)
        {
            foreach (var mote in motes)
            {
                double age = Age(time, mote.Birth);
                if (age >= mote.Life) continue;
                double c = Cos(mote.Angle), s = Sin(mote.Angle), distance = radius * 0.9 + mote.Speed * age;
                double x = headX + c * distance - speed * 0.4 * age, y = headY + s * distance;
                double k = 1 - age / mote.Life, length = 1 + 2 * k;
                p.Line(x - c * length + speed * 0.02, y - s * length, x, y, k > 0.5 ? heat[1] : heat[3]);
                p.Glow(x, y, 3, heat[2], 0.4 * k);
            }
            foreach (var ember in embers)
            {
                double age = Age(time, ember.Birth);
                if (age >= ember.Life) continue;
                var (dx, vx) = Drift(0, ember.Vx, 0, 3, age);
                var (y, vy) = Drift(ember.Y, ember.Vy, 50, 2, age);
                double x = ember.X + dx - speed * age, k = 1 - age / ember.Life;
                uint ink = k > 0.6 ? heat[1] : k > 0.3 ? heat[2] : heat[4];
                if (ember.Big)
                {
                    if (k < 0.25 && Sin(age * 40 + ember.Phase) < 0) continue;
                    uint rim = k > 0.45 ? heat[4] : heat[6];
                    p.Dot(x - 1, y, rim);
                    p.Dot(x + 1, y, rim);
                    p.Dot(x, y - 1, rim);
                    p.Dot(x, y + 1, rim);
                    p.Dot(x, y, ink);
                    p.Glow(x, y, 8, ink, 0.4 * k);
                }
                else
                {
                    vx -= speed;
                    double magnitude = Sqrt(vx * vx + vy * vy);
                    double length = Min(9, 1 + magnitude * 0.022);
                    double ux = vx / (magnitude == 0 ? 1 : magnitude), uy = vy / (magnitude == 0 ? 1 : magnitude);
                    p.Line(x - ux * length, y - uy * length, x - ux * length * 0.4, y - uy * length * 0.4,
                        k > 0.5 ? ink : heat[5]);
                    p.Line(x - ux * length * 0.45, y - uy * length * 0.45, x, y, ink);
                    p.Glow(x, y, 3, ink, 0.45 * k);
                }
            }
        }

        private void Flame(PixelPainter p, double x, double y, double k, double size, double variation)
        {
            double age = 1 - k;
            int index = Min(7, (int)Floor(Min(0.999, Pow(age, 0.62) + variation * 0.1) * 8));
            double r = size * (0.55 + 0.45 * Min(1, age * 7)) * (1 - age * 0.55);
            uint ink = heat[index];
            if (index == 7)
            {
                p.SoftDisc(x, y, r, ink, Min(1, k * 3) * 0.85, 0);
                return;
            }

            p.Circle(x, y, r, heat[Min(8, index + 2)]);
            if (r > 1.6) p.Circle(x - 0.86 * r * 1.5, y - 0.5 * r * 1.5, r * 0.3, heat[index + 1]);
            p.Circle(x - r * 0.16, y - r * 0.22, r * 0.78, ink);
            if (r > 1.6) p.Circle(x - 0.86 * r * 0.9, y - 0.5 * r * 0.9, r * 0.42, ink);
            if (index <= 3 && r > 1.8)
                p.Circle(x - r * 0.28, y - r * 0.34, r * 0.36, heat[Max(0, index - 1)]);
            if (index < 6) p.Glow(x, y, r * 2.4 + 2, ink, 0.07 * k);
        }

        private void Smoke(PixelPainter p, double x, double y, double k, double size)
        {
            double age = 1 - k;
            double r = size * (0.65 + Min(1, age * 2.5) * 0.45 + age * 0.4);
            double a = Min(1, age * 12) * Min(1, k * 2.4) * 0.58;
            uint ink = Mix(smoke, SmokeRamp[Min(4, (int)Floor(age * SmokeRamp.Length))], age);
            p.SoftDisc(x, y, r, ink, a * 0.45, 0);
            p.SoftDisc(x + r * 0.05, y + r * 0.06, r * 0.74, ink, a * 0.62, 0);
            if (age < 0.5)
                p.SoftDisc(x - r * 0.22, y - r * 0.3, r * 0.5, Mix(ink, White, 0.12), a * 0.4 * (1 - age * 2), 0);
        }

        private static double Age(double time, double birth)
        {
            double age = time - birth;
            return age < 0 ? age + FireballEffect.Period : age;
        }

        private static (double Position, double Velocity) Drift(double x, double v, double a, double drag, double time)
        {
            double e = Exp(-drag * time), terminal = a / drag;
            return (x + terminal * time + (v - terminal) * (1 - e) / drag, terminal + (v - terminal) * e);
        }
    }
}
