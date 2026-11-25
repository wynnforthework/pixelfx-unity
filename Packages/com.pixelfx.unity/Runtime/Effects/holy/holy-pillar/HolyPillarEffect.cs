using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed partial class HolyPillarEffect
    {
        internal const int Width = 192, Height = 232, OriginX = 96, OriginY = 204;
        internal const float Duration = 3.5f;
        private const double Tau = 2 * PI, Top = -204, Ground = -1, SunY = -166, Depth = 203;
        private const double Open = 0.3, Hit = 0.66, Fold = 2.05, Shut = 2.45, Lift = 2.75;
        private const double Radius = 26, Squash = 0.24, Step = 1.0 / 120;
        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly bool full;
        private readonly (double Angle, double Spread, double Brightness, double Phase, double Far)[] shafts = new (double, double, double, double, double)[7];
        private readonly ShaftFrame[] fan = new ShaftFrame[7];
        private readonly float[] row = new float[Width];
        private readonly byte[] core = new byte[Width];
        private readonly (double X, double Y, double Speed, double Amplitude, double Frequency, double Phase)[] dust;
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private double time;
        private int tick;

        private struct ShaftFrame
        {
            internal double Cosine, Tangent, Spread, Front, Brightness, PulseDepth;
        }

        private HolyPillarEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            full = timeline.Quality == PixelFxQuality.Full;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            random = new PixelFxRandom(settings.seed);
            double mid = (shafts.Length - 1) / 2.0;
            for (int i = 0; i < shafts.Length; i++)
            {
                double offset = i - mid, far = Abs(offset) / mid;
                shafts[i] = (offset * (0.4 / mid) + random.Range(-0.02, 0.02),
                    random.Range(0.05, 0.085) * (1 - far * 0.25), (1 - far * 0.35) * random.Range(0.8, 1),
                    random.Range(0, 1), far);
            }
            if (full)
            {
                dust = new (double, double, double, double, double, double)[72];
                for (int i = 0; i < dust.Length; i++)
                    dust[i] = (random.Range(-80, 80), random.Range(0, 1), random.Range(7, 18),
                        random.Range(0.5, 2.5), random.Range(0.6, 1.5), random.Range(0, Tau));
                CreateFeathers();
            }
            initialRandom = random;
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new HolyPillarEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private static double Clamp(double t) => Max(0, Min(1, t));
        private static double Progress(double a, double b, double t) => Clamp((t - a) / (b - a));
        private static double Smooth(double a, double b, double t)
        {
            double u = Progress(a, b, t);
            return u * u * (3 - 2 * u);
        }

        private static double OutBack(double u, double overshoot) =>
            u <= 0 || u >= 1 ? u : 1 + (overshoot + 1) * Pow(u - 1, 3) + overshoot * Pow(u - 1, 2);

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            Advance(sample);
            painter.Clear();
            double fade = Smooth(Open, Open + 0.1, time) * (1 - Smooth(Lift - 0.05, Lift + 0.05, time));
            double lift = Depth * Pow(Progress(Shut + 0.05, Lift, time), 2);
            double taper = 1 - 0.75 * Smooth(Shut, Lift, time);
            if (fade > 0) PrepareFan();
            if (pass != PixelFxRenderPass.Front)
            {
                if (fade > 0) DrawFan(fade, lift, taper);
                DrawSigil(true);
                if (full) DrawParticles(true);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                DrawSigil(false);
                if (full && fade > 0) DrawDust(fade, lift, taper);
                DrawSun();
                if (time > Hit - 0.02 && time < Hit + 0.25)
                    Sparkle(0, Ground - 2, 12 * (1 - (time - Hit) / 0.25), White, colors[5], 0.7);
                if (time > Shut && time < Shut + 0.2)
                    Sparkle(0, Ground - 2, 8 * (1 - (time - Shut) / 0.2), White, colors[5], 0.5);
                if (full) DrawParticles(false);
                DrawFlash(-30, 64, colors[5], 0.85, 0.5, hitAge);
                DrawFlash(-6, 30, White, 0.8, 0.12, hitAge);
                DrawFlash(-60, 70, colors[5], 0.7, 0.35, shutAge);
                DrawFlash(SunY, 40, White, 0.6, 0.2, liftAge);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void PrepareFan()
        {
            for (int i = 0; i < shafts.Length; i++)
            {
                var (angle, spread, brightness, phase, far) = shafts[i];
                double delay = far * 0.1;
                double opening = OutBack(Progress(Open + delay, Open + delay + 0.42, time), 1.5);
                double u = Progress(Fold - delay, Shut, time);
                double folded = u < 0.5 ? 4 * u * u * u : 1 - Pow(-2 * u + 2, 3) / 2;
                double current = angle * opening * (1 - folded);
                double pulse = (time - Hit) / 0.6 + phase;
                fan[i] = new ShaftFrame
                {
                    Cosine = Cos(current), Tangent = Tan(current), Spread = spread, Brightness = brightness,
                    Front = Depth * (1 - Pow(Progress(Open + far * 0.06, Hit + far * 0.06, time), 2)),
                    PulseDepth = Depth * (1 - (pulse - Floor(pulse))),
                };
            }
        }

        private void DrawFan(double fade, double lift, double taper)
        {
            bool pulsing = time >= Hit && time <= Shut;
            for (int y = (int)Top; y < Ground; y++)
            {
                double dy = Ground - (y + 0.5);
                Array.Clear(row, 0, Width);
                Array.Clear(core, 0, Width);
                foreach (var s in fan)
                {
                    double d = dy / s.Cosine;
                    if (d < lift || d < s.Front - 6) continue;
                    double center = s.Tangent * dy, halfWidth = (1.4 + d * s.Spread) * taper / 2 / s.Cosine;
                    double soft = Smooth(s.Front - 6, s.Front + 4, d) * Smooth(lift, lift + 6, d);
                    double phase = (d - s.PulseDepth) / 12;
                    double pulse = pulsing ? 1 + 0.9 * Exp(-(phase * phase)) : 1;
                    double intensity = s.Brightness * pulse * soft * fade;
                    int first = Max(0, (int)Floor(center - halfWidth) + OriginX);
                    int last = Min(Width - 1, (int)Ceiling(center + halfWidth) + OriginX);
                    for (int i = first; i <= last; i++)
                    {
                        double v = (i - OriginX + 0.5 - center) / halfWidth;
                        if (Abs(v) < 1) row[i] = (float)(row[i] + intensity * Pow(1 - v * v, 1.3));
                    }
                    int middle = (int)Floor(center) + OriginX;
                    if ((uint)middle < Width) core[middle] = Max(core[middle], (byte)Min(255, intensity * 200));
                }
                for (int i = 0; i < Width; i++)
                {
                    double intensity = row[i];
                    if (intensity < 0.02) continue;
                    int x = i - OriginX;
                    painter.BlendPixel(x, y, colors[6], Min(0.6, intensity * 0.38));
                    painter.Light(x, y, colors[4], Min(0.9, intensity * 0.32));
                    if (core[i] > 0) painter.Dot(x, y, core[i] > 150 ? White : colors[6], Min(1, core[i] / 180.0));
                }
            }
            if (time > Hit - 0.05) painter.Glow(0, Ground - 3, 20, colors[5], 0.5 * fade);
        }

        private void DrawDust(double fade, double lift, double taper)
        {
            foreach (var (startX, startY, speed, amplitude, frequency, phase) in dust)
            {
                double y = Top + ((startY * Depth + time * speed) % Depth + Depth) % Depth;
                double x = startX + Sin(time * frequency + phase) * amplitude, dy = Ground - y;
                if (dy <= 0) continue;
                double intensity = 0;
                foreach (var s in fan)
                {
                    double d = dy / s.Cosine;
                    if (d < lift || d < s.Front) continue;
                    double v = (x - s.Tangent * dy) / ((1.4 + d * s.Spread) * taper / 2 / s.Cosine);
                    if (Abs(v) < 1) intensity += (1 - v * v) * s.Brightness;
                }
                intensity *= fade;
                if (intensity < 0.2) continue;
                painter.Dot(x, y, intensity > 0.7 ? White : colors[6], Min(1, intensity * 1.3));
                painter.Light(x, y, colors[5], 0.4 * Min(1, intensity));
            }
        }

        private void DrawSun()
        {
            double u = Progress(Shut, Lift + 0.1, time);
            double closing = u <= 0 || u >= 1 ? u : 2.5 * u * u * u - 1.5 * u * u;
            double radius = 15 * OutBack(Progress(0.02, 0.45, time), 1.8) * (1 - closing);
            if (radius > 0.5)
            {
                double spin = time * 0.6;
                painter.Glow(0, SunY, Min(32, radius * 3), colors[4], 0.5);
                painter.Glow(0, SunY, radius * 1.4, White, 0.35);
                painter.Circle(0, SunY, radius * 0.45, White);
                DrawSunRing(radius * 0.62, spin, true);
                DrawSunRing(radius, spin, false);
                for (int j = 0; j < 16; j++)
                {
                    double angle = spin + j * Tau / 16, start = radius + 2, end = start + (j % 2 == 0 ? 6 : 3);
                    double x = Cos(angle), y = Sin(angle);
                    painter.Line(x * start, SunY + y * start, x * end, SunY + y * end,
                        (Bright: colors[6], Dim: colors[4]), static (u, c) => (u < 0.4 ? c.Bright : c.Dim, 1.1 - u));
                }
            }

            if (time > Lift && time < Lift + 0.3)
                Sparkle(0, SunY, 10 * (1 - (time - Lift) / 0.3), White, colors[5], 0.6);
        }

        private void DrawSunRing(double radius, double spin, bool inner)
        {
            if (radius < 0.5) return;
            int count = (int)Ceiling(radius * Tau * 1.5) + 6;
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau;
                uint ink = inner ? colors[6] : Sin(angle * 6 + spin * 4) > 0.5 ? White : colors[5];
                painter.Dot(Cos(angle) * radius, SunY + Sin(angle) * radius, ink);
            }
        }

        private void DrawSigil(bool back)
        {
            if (time < Hit) return;
            double burn = Progress(Hit, Hit + 0.2, time) * PI + 0.01;
            double heat = 1 - Smooth(Shut, Shut + 0.8, time), fade = 1 - Smooth(Shut + 0.4, Shut + 1, time);
            if (fade <= 0) return;
            DrawSigilRing(Radius, burn, heat, fade, back, true);
            DrawSigilRing(Radius * 0.72, burn, heat, fade, back, false);
            for (int j = 0; j < 12; j++)
            {
                double angle = time * 0.35 + j * Tau / 12;
                double y = Sin(angle);
                if ((y < 0) != back || !Burnt(angle, burn)) continue;
                double x = Cos(angle), start = Radius * 0.78, end = Radius * (j % 3 == 0 ? 0.95 : 0.88);
                painter.Line(x * start, y * start * Squash, x * end, y * end * Squash, SigilInk(angle, heat, j % 2 == 0), fade);
            }
            if (back) return;
            for (int i = -2; i <= 2; i++)
                painter.Glow(i * Radius * 0.45, -2, 16, colors[4], (0.34 - Abs(i) * 0.05) * fade * (0.5 + 0.5 * heat));
            painter.Glow(0, -1, 10, White, 0.3 * heat * fade);
        }

        private void DrawSigilRing(double radius, double burn, double heat, double fade, bool back, bool outer)
        {
            int count = (int)Ceiling(radius * Tau * 1.5) + 6;
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau, y = Sin(angle);
                if ((y < 0) != back || !Burnt(angle, burn)) continue;
                painter.Dot(Cos(angle) * radius, y * (radius * Squash), SigilInk(angle, heat, outer), fade);
            }
        }

        private static bool Burnt(double angle, double burn) =>
            Abs(((angle - PI / 2 + PI) % Tau + Tau) % Tau - PI) <= burn;

        private uint SigilInk(double angle, double heat, bool hot)
        {
            bool flicker = PixelFxRandom.At((int)Floor(angle * 40 + 0.5), (int)Floor(time * 20)) < 0.18;
            return time < Hit + 0.08 || (flicker && heat > 0.4) ? White :
                colors[heat > 0.6 ? (hot ? 6 : 5) : heat > 0.3 ? 4 : heat > 0.1 ? 3 : 2];
        }

        private void DrawFlash(double y, double radius, uint ink, double power, double life, double age)
        {
            double k = 1 - age / life;
            if (k > 0) painter.Glow(0, y, radius * (0.55 + 0.45 * k), ink, power * k * k);
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
