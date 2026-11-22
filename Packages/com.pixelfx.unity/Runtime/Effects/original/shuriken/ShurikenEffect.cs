using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed class ShurikenEffect
    {
        internal const int Width = 184, Height = 56, OriginX = 22, OriginY = 28;
        private const double Tau = PI * 2;
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
            const double flight = 0.85;
            int rate = settings.trailRate;
            if (full && rate > 0)
            {
                int first = Max(0, (int)Ceiling((t - 0.33) * rate));
                int last = (int)Floor(Min(t, flight) * rate);
                for (int i = first; i <= last; i++)
                {
                    var random = new PixelFxRandom(settings.seed, i);
                    double born = i / (double)rate, age = t - born, life = random.Range(0.14, 0.33);
                    if (age < 0 || age >= life) continue;
                    double k = age / life, vx = random.Range(-37, -9), vy = random.Range(-24, 24);
                    double x = born / flight * 138 - random.Range(2, 8) + vx * age;
                    double y = -3 * Sin(PI * born / flight) + random.Range(-4, 4) + vy * age + 20 * age * age;
                    uint ink = c[k < 0.22 ? 3 : k < 0.53 ? 2 : k < 0.78 ? 1 : 0];
                    if (i % 3 == 0 && k < 0.75)
                    {
                        p.Line(x, y, x - 2.5 * (1 - k), y - vy * 0.015, ink, 1 - k);
                        p.Glow(x, y, 4.5, c[2], 0.55 * (1 - k));
                    }
                    else p.Dot(x, y, ink, 1 - k);
                }
            }
            if (t >= flight) return;
            double cx = t / flight * 138, cy = -3 * Sin(PI * t / flight);
            double angle = t * settings.spin * Tau + 0.18, alpha = Min(1, (flight - t) / 0.075);
            for (int i = 0; i < 4; i++)
            {
                double a = angle + i * Tau / 4;
                DrawBlade(cx, cy, a, alpha);
            }
            p.Circle(cx, cy, 3.9, Hex(0x1a2433), alpha);
            p.Circle(cx, cy, 2.6, Hex(0x84a3b8), alpha);
            p.Circle(cx, cy, 1.3, Hex(0x1a2433), alpha);
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
