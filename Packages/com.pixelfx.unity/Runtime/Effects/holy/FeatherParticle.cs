using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Flutter and vane rasterization from Patch PixelFX holy/_lib.js at 5294518.
    internal struct FeatherParticle
    {
        internal double X, Y, Vx, Vy, Angle, Length, Width, Life, MaxLife, Sink, Delay;
        internal double Side, Phase, Frequency, Amplitude, Spin, Tilt, Age;
        internal bool Back, Landed;

        internal static FeatherParticle Initialize(FeatherParticle f, ref PixelFxRandom random, double? amplitude = null, double? side = null, bool useDefaultSink = false)
        {
            double defaultSide = random.Range(0, 1) < 0.5 ? -1 : 1;
            f.Side = side ?? defaultSide;
            f.Phase = random.Range(0, 2 * PI);
            f.Frequency = random.Range(3.2, 4.6);
            double defaultAmplitude = random.Range(6, 11);
            f.Amplitude = amplitude ?? defaultAmplitude;
            // Preserve the upstream random sequence even when the emitter supplies its own sink.
            double defaultSink = random.Range(14, 24);
            if (useDefaultSink) f.Sink = defaultSink;
            f.Spin = random.Range(-9, 9);
            f.Tilt = random.Range(-0.35, 0.35);
            f.MaxLife = f.Life;
            return f;
        }

        internal static void Tick(ref FeatherParticle f, double dt, double floor)
        {
            if (f.Delay > 0)
            {
                f.Delay -= dt;
                return;
            }
            if (f.Life <= 0 || (f.Life -= dt) <= 0) return;
            f.Age += dt;
            double flat = Floor(f.Angle / PI + 0.5) * PI;
            if (f.Landed)
            {
                f.Angle += (flat - f.Angle) * Min(1, dt * 10);
                return;
            }

            f.Vx -= f.Vx * 3.4 * dt;
            f.Vy -= f.Vy * 3.4 * dt;
            double u = Min(1, Max(0, (f.Age - 0.1) / (0.6 - 0.1)));
            double drift = u * u * (3 - 2 * u), phase = f.Age * f.Frequency + f.Phase;
            double sine = Sin(phase), cosine = Cos(phase);
            f.X += (f.Vx + sine * f.Amplitude * drift * 0.9) * dt;
            f.Y += (f.Vy + f.Sink * drift * (cosine * cosine * 1.5 - 0.2)) * dt;
            double rest = flat + f.Tilt + sine * 0.6;
            f.Angle += f.Spin * dt * (1 - drift) + (rest - f.Angle) * Min(1, dt * 5 * drift);
            if (f.Y >= floor - 0.5)
            {
                f.Y = floor - 0.5;
                f.Landed = true;
                f.Life = Min(f.Life, 0.6);
            }
        }

        internal static void Draw(PixelPainter painter, FeatherParticle f, uint[] colors, int paletteStart = 0)
        {
            double alpha = Min(1, Min(f.Life / 0.45, f.Age / 0.06));
            double ca = Cos(f.Angle), sa = Sin(f.Angle);
            DrawVane(painter, f.X - ca * f.Length / 2, f.Y - sa * f.Length / 2,
                f.Angle, f.Length, f.Width, colors, paletteStart, alpha, f.Side, f.Age < 0.06, 0.2);
            painter.Glow(f.X, f.Y, 4 + f.Length * 0.5, colors[paletteStart + 3], 0.22 * alpha * (0.4 + 0.6 * f.Life / f.MaxLife));
        }

        internal static void DrawVane(PixelPainter painter, double x, double y, double angle, double length,
            double vaneWidth, uint[] colors, int paletteStart, double alpha, double side, bool hot, double quill = 0.02)
        {
            if (length < 1.5 || alpha <= 0.02) return;
            double ca = Cos(angle), sa = Sin(angle);
            double endX = x + ca * length, endY = y + sa * length, pad = vaneWidth + 1.2;
            uint edge = colors[paletteStart + (hot ? 3 : 1)], lead = hot ? White : colors[paletteStart + 4];
            uint first = hot ? White : colors[paletteStart + 4], second = hot ? White : colors[paletteStart + 3], third = colors[paletteStart + (hot ? 4 : 2)];

            for (int py = (int)Floor(Min(y, endY) - pad); py <= Ceiling(Max(y, endY) + pad); py++)
            for (int px = (int)Floor(Min(x, endX) - pad); px <= Ceiling(Max(x, endX) + pad); px++)
            {
                double dx = px + 0.5 - x, dy = py + 0.5 - y, u = (dx * ca + dy * sa) / length;
                if (u < 0 || u > 1.05) continue;
                double v = (-dx * sa + dy * ca) * side, av = Abs(v);
                double width = vaneWidth * Sqrt(Min(1, Max(0, (u - quill) / 0.22)))
                    * Sqrt(Max(0, 1 - Pow(Max(0, (u - 0.55) / 0.47), 2)));
                double limit = v < 0 ? width * 0.42 : width, fade = u < 0.72 ? 1 : 1 - (u - 0.72) * 1.6;
                if (av < 0.5 && u < 0.9) painter.Dot(px, py, u < 0.5 ? White : first, alpha);
                else if (av > limit + 0.05 || width < 0.5) continue;
                else if (limit - av < 1)
                {
                    if (v > 0) painter.Dot(px, py, u > 0.85 ? third : edge, alpha * fade);
                    else painter.Dot(px, py, lead, alpha);
                }
                else if (u < 0.72) painter.Dot(px, py, u < 0.38 ? first : second, alpha);
                else painter.BlendPixel(px, py, third, alpha * fade);
            }
        }
    }
}
