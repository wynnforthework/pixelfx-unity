using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed class ShurikenEffect
    {
        internal const int Width = 184, Height = 56, OriginX = 22, OriginY = 28;
        internal const double TurnTime = 0.85, ReturnTime = 0.65;
        internal const double ArrivalTime = TurnTime + ReturnTime, Duration = ArrivalTime + 0.2;
        private const double Tau = PI * 2;
        private const double TravelPixels = 138, MaxParticleLife = 0.33;
        private readonly PixelPainter p;
        private readonly PixelFxSettings settings;
        private readonly uint[] c;

        private ShurikenEffect(PixelFxSettings settings)
        {
            p = new PixelPainter(Width, Height, OriginX, OriginY);
            this.settings = settings;
            c = System.Array.ConvertAll(settings.palette, value => Hex(PixelFxLibrary.Pack(value)));
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new ShurikenEffect(timeline.Settings);
            bool full = timeline.Quality == PixelFxQuality.Full;
            return (pixels, _, time, _) => effect.Render(pixels, time, full);
        }

        private void Render(byte[] pixels, double t, bool full)
        {
            p.Clear();
            p.LightStrength = settings.glow;
            Shuriken(t, full);
            p.Composite(pixels, true, true);
        }

        private void Shuriken(double t, bool full)
        {
            var (cx, cy) = FlightPosition(t);
            if (settings.trailRate > 0)
            {
                DrawStreak(t);
                if (full) DrawTrailParticles(t);
            }
            if (t >= ArrivalTime + 0.09) return;
            double angle = t * settings.spin * Tau + 0.18, alpha = Min(1, (ArrivalTime + 0.09 - t) / 0.09);
            for (int i = 0; i < 4; i++)
            {
                double a = angle + i * Tau / 4;
                DrawBlade(cx, cy, a, alpha);
            }
            p.Circle(cx, cy, 3.9, Hex(0x1a2433), alpha);
            p.Circle(cx, cy, 2.6, Hex(0x84a3b8), alpha);
            p.Circle(cx, cy, 1.3, Hex(0x1a2433), alpha);
        }

        internal static (double X, double Y) FlightPosition(double time)
        {
            if (time < TurnTime)
            {
                double travel = Min(1, Max(0, time / TurnTime));
                double progress = travel * (2 - travel);
                return (TravelPixels * progress, -3 * Sin(PI * progress));
            }
            if (time >= ArrivalTime) return (0, 0);
            double back = (time - TurnTime) / ReturnTime;
            double progressBack = back * back * (3 - 2 * back);
            return (TravelPixels * (1 - progressBack), 6 * Sin(PI * progressBack));
        }

        private void DrawStreak(double time)
        {
            double legStart = time < TurnTime ? 0 : TurnTime;
            double length = time < TurnTime ? 0.12 : 0.06;
            double head = Min(time, ArrivalTime);
            double fade = Max(0, 1 - (time - head) / length);
            double tail = Max(legStart, time - length);
            double middle = (tail + head) / 2;
            if (tail >= head) return;
            var a = FlightPosition(tail);
            var b = FlightPosition(middle);
            var tip = FlightPosition(head);
            double opacity = fade * Min(1, settings.trailRate / 68.0);
            p.Line(a.X, a.Y, b.X, b.Y, c[1], opacity * 0.7);
            p.Line(b.X, b.Y, tip.X, tip.Y, c[2], opacity);
        }

        private void DrawTrailParticles(double time)
        {
            int rate = settings.trailRate;
            int first = Max(0, (int)Ceiling((time - MaxParticleLife) * rate));
            int last = (int)Floor(Min(time, ArrivalTime) * rate);
            for (int i = first; i <= last; i++)
            {
                var random = new PixelFxRandom(settings.seed, i);
                double born = i / (double)rate, age = time - born;
                bool returning = born >= TurnTime;
                double life = random.Range(0.14, MaxParticleLife) * (returning ? 0.42 : 1);
                if (age < 0 || age >= life) continue;
                double k = age / life, direction = returning ? -1 : 1;
                double drift = random.Range(-37, -9), vy = random.Range(-24, 24);
                var (cx, cy) = FlightPosition(born);
                double x = cx + direction * (drift * age - random.Range(2, 8));
                double y = cy + random.Range(-4, 4) + vy * age + 20 * age * age;
                uint ink = c[k < 0.22 ? 3 : k < 0.53 ? 2 : k < 0.78 ? 1 : 0];
                if (i % 3 == 0 && k < 0.75)
                {
                    p.Line(x, y, x - direction * 2.5 * (1 - k), y - vy * 0.015, ink, 1 - k);
                    p.Glow(x, y, 4.5, c[2], 0.55 * (1 - k));
                }
                else p.Dot(x, y, ink, 1 - k);
            }
        }

        private static readonly (double X, double Y)[] Blade = { (2,-4.8), (12,-2), (8,1.9), (2.2,4.4) };
        private static readonly (double X, double Y)[] Face = { (2,-3.1), (10.5,-1.9), (3,2.9) };
        private static readonly (double X, double Y)[] Shadow = { (3,-0.8), (10.5,-1.9), (3,2.9) };
        private static readonly (double X, double Y)[] Bevel = { (2.3,-3.1), (10.5,-1.9), (3,-1.6) };
        private void DrawBlade(double cx, double cy, double angle, double alpha)
        {
            double co = Cos(angle), si = Sin(angle);
            p.Shade(cx - 13, cy - 13, cx + 13, cy + 13, (x, y) =>
            {
                double dx = x - cx, dy = y - cy, u = dx * co + dy * si, v = -dx * si + dy * co;
                if (Inside(Bevel, u, v)) return (Hex(0xcee6f5), alpha);
                if (Inside(Shadow, u, v)) return (Hex(0x5b7082), alpha);
                if (Inside(Face, u, v)) return (Hex(0x84a3b8), alpha);
                return Inside(Blade, u, v) ? (Hex(0x1a2433), alpha) : default;
            });
        }

        private static bool Inside((double X, double Y)[] points, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                var a = points[i];
                var b = points[j];
                if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }
    }
}
