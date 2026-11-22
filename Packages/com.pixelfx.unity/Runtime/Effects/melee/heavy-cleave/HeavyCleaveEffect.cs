using System.Collections.Generic;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    internal sealed partial class HeavyCleaveEffect
    {
        internal const int Width = 208, Height = 116, OriginX = 68, OriginY = 96;
        internal const double Duration = 1.9, ImpactTime = 0.335;
        private const double Raise = 0.2, SwingTime = 0.135, Hold = 0.05, Trail = 0.22;
        private const double Step = 1.0 / 120, Tau = PI * 2;
        private const double CenterX = 3, CenterY = -22, Tilt = 0.32, Squash = 0.88, StartAngle = -2.45;
        private const double Radius = 37;
        private static readonly double CosTilt = Cos(Tilt), SinTilt = Sin(Tilt);
        private static readonly uint[] DefaultPalette = { 0x2a0c10, 0x5c1414, 0xa3241a, 0xe0481c, 0xff8a2a, 0xffc23d, 0xfff0a0, 0xffffff };
        private static readonly double[] SpikeAngles = { -PI, -2.7, -2.15, -1.62, -1.05, -0.45, 0 };
        private static readonly double[] ShardAngles = { -2.5, -1.9, -1.2, -0.6 };
        private readonly List<Crack> cracks = new List<Crack>(20);
        private readonly (double Angle, double Length, double Width)[] spikes = new (double, double, double)[7];
        private readonly (double Angle, double Radius, double Velocity, double Arc)[] shards = new (double, double, double, double)[4];
        private readonly uint[] palette = new uint[8];
        private readonly PixelPainter painter;
        private readonly double span, impactX, glintX, glintY;
        private uint stone, stoneLight, stoneShade, stoneDark, dust;
        private uint random;
        private int steps;
        private double time;
        private double widthScale = 1, sparkScale = 1;
        private bool particlesEnabled = true;

        internal HeavyCleaveEffect()
        {
            painter = new PixelPainter(Width, Height, OriginX, OriginY);
            while (OnSwing(Radius, span).Y < -0.5 && span < 4) span += 0.005;
            impactX = OnSwing(Radius - 2, span).X;
            (glintX, glintY) = OnSwing(Radius - 3, 0);
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new HeavyCleaveEffect();
            var settings = timeline.Settings;
            var colors = System.Array.ConvertAll(settings.palette, color => PixelFxLibrary.Pack(color));
            bool full = timeline.Quality == PixelFxQuality.Full;
            void Reset() => effect.Reset(settings.seed, colors, 0x4a3f69, settings.bladeWidth, settings.sparks, full);
            Reset();
            double previousTime = 0;
            return (pixels, _, time, _) =>
            {
                if (time < previousTime) Reset();
                effect.Advance(time);
                effect.Render(pixels, true, settings.glow);
                previousTime = time;
            };
        }

        internal void Reset(int seed, uint[] colors, uint stoneColor,
            double bladeWidth = 1, double sparks = 1, bool full = true)
        {
            widthScale = bladeWidth;
            sparkScale = sparks;
            particlesEnabled = full;
            random = unchecked((uint)seed);
            if (random == 0) random = 1;
            time = 0;
            steps = 0;
            particles.Clear();
            rocks.Clear();
            cracks.Clear();
            for (int i = 0; i < palette.Length; i++)
                palette[i] = Hex(colors != null && colors.Length == 8 ? colors[i] : DefaultPalette[i]);
            stone = Hex(stoneColor);
            stoneLight = Mix(Scale(stone, 1.35), White, 0.08);
            stoneShade = Scale(stone, 0.64);
            stoneDark = Scale(stone, 0.42);
            dust = Mix(Scale(stone, 1.2), Hex(0x968c96), 0.3);
            BuildCracks();
            for (int i = 0; i < spikes.Length; i++)
                spikes[i] = (SpikeAngles[i] + Range(-0.08, 0.08),
                    (i == 0 || i == 6 ? 20 : i == 3 ? 15 : 9) + Range(0, 4), 1.6 + Range(0, 0.6));
            for (int i = 0; i < shards.Length; i++)
                shards[i] = (ShardAngles[i] + Range(-0.15, 0.15), 8 + Range(0, 3), Range(80, 130), Range(0.35, 0.6));
        }

        internal void Advance(double elapsed)
        {
            int target = (int)Floor(Min(Duration, elapsed) * 120 + 1e-8);
            while (steps < target)
            {
                double before = time;
                steps++;
                time += Step;
                if (particlesEnabled) UpdateParticles(before < ImpactTime && time >= ImpactTime);
            }
        }

        internal void Render(byte[] rgba, bool bottomUp, double lightStrength)
        {
            painter.Clear();
            painter.LightStrength = lightStrength;
            double age = time - ImpactTime;
            if (age >= 0) DrawFloor(age);
            DrawParticles(true);
            if (time < Raise + 0.03) DrawGlint();
            if (age >= 0)
            {
                DrawCracks(age);
                DrawRing(age, false);
            }
            DrawArc(Radius * 0.76, 5 * widthScale, time - 0.02, false);
            DrawArc(Radius, 16 * widthScale, time, true);
            double tail = span * Smooth(ImpactTime + Hold, ImpactTime + Hold + Trail, time);
            double trace = 0.8 * (1 - Clamp((time - ImpactTime - Hold) / (Trail + 0.25)));
            if (tail > 0.02 && trace > 0)
                for (double u = 0; u <= tail; u += 0.6 / (Radius - 0.5))
                {
                    var point = OnSwing(Radius - 0.5, u);
                    painter.Dot(point.X, point.Y, palette[3], trace * (0.4 + 0.6 * u / span));
                }
            if (time > Raise && time < ImpactTime + 0.05)
            {
                var point = OnSwing(Radius - 5, span * Head(time));
                painter.Glow(point.X, point.Y, 40, palette[4], 0.5);
            }
            if (age >= 0) DrawImpact(age);
            DrawRocks();
            DrawParticles(false);
            if (age >= 0)
            {
                DrawLight(-6, 26, White, 0.9, 0.1, age + Step);
                DrawLight(-12, 64, palette[4], 0.5, 0.8, age + Step);
            }
            painter.Composite(rgba, bottomUp, true);
        }

        private static (double X, double Y) OnSwing(double radius, double u)
        {
            double a = StartAngle + u, x = Cos(a) * radius, y = Sin(a) * radius * Squash;
            return (CenterX + x * CosTilt - y * SinTilt, CenterY + x * SinTilt + y * CosTilt);
        }

        private void DrawArc(double radius, double width, double t, bool rim)
        {
            double head = span * Head(t);
            double gone = Smooth(ImpactTime + Hold, ImpactTime + Hold + Trail, t);
            double tail = span * gone;
            if (head < 0.02 || tail >= head - 0.02) return;
            double bound = radius + 3;
            painter.Shade(CenterX - bound, CenterY - bound, CenterX + bound, CenterY + bound, (x, y) =>
            {
                double dx = x - CenterX, dy = y - CenterY;
                double planeX = dx * CosTilt + dy * SinTilt, planeY = (-dx * SinTilt + dy * CosTilt) / Squash;
                double r = Sqrt(planeX * planeX + planeY * planeY);
                if (r > radius || r < radius - width) return default;
                double u = Atan2(planeY, planeX) - StartAngle;
                if (u < 0) u += Tau;
                if (u < tail || u > head) return default;
                double v = (u - tail) / (head - tail);
                double w = width * (1 - 0.75 * gone) * Pow(Sin(PI * Pow(v, 2.2)), 0.6);
                if (w < 0.4) return default;
                double q = (radius - r) / w;
                if (q > 1) return default;
                double age = Max(0, t - PassedAt(u / span));
                int index = rim && q < 0.22 && age < 0.12 ? 0 :
                    RoundJs(q * 7 * 0.62 + age * 7 + Max(0, t - ImpactTime) * 10 - v * 0.8 + (rim ? 0.7 : 1.7));
                return (palette[7 - Max(0, Min(7, index))], Min(1, v / 0.35) * (1.2 - gone));
            });
            if (!rim) return;
            for (double u = tail; u <= head; u += 0.35)
            {
                var point = OnSwing(radius - width * 0.4, u);
                double strength = 0.26 * Exp(-Max(0, t - PassedAt(u / span)) / 0.2) * ((u - tail) / (head - tail));
                painter.Glow(point.X, point.Y, 7 + width * 0.5, palette[4], strength);
            }
        }

        private void DrawGlint()
        {
            double t = Clamp(time / 0.1) - 1;
            double g = (1 + 2.70158 * t * t * t + 1.70158 * t * t) * (1 + 0.15 * Sin(time * 60));
            if (time > Raise) g *= 1 - (time - Raise) / 0.03;
            int arm = RoundJs(1 + 7 * g);
            if (arm < 1) return;
            double x = Floor(glintX), y = Floor(glintY);
            for (int d = 1; d <= arm; d++)
            {
                uint c = d > arm * 0.6 ? palette[4] : palette[6];
                painter.Dot(x + d, y, c);
                painter.Dot(x - d, y, c);
                painter.Dot(x, y + d, c);
                painter.Dot(x, y - d, c);
            }
            for (int d = 1; d <= (arm >= 6 ? 2 : arm >= 3 ? 1 : 0); d++)
            {
                uint c = d == 1 ? palette[6] : palette[4];
                painter.Dot(x + d, y + d, c);
                painter.Dot(x - d, y + d, c);
                painter.Dot(x + d, y - d, c);
                painter.Dot(x - d, y - d, c);
            }
            painter.Dot(x, y, White);
            painter.Glow(glintX, glintY, 4 + arm * 2, palette[6], 0.5);
            painter.Glow(glintX, glintY, 22, palette[4], 0.3 * g);
        }

        private void DrawFloor(double age)
        {
            double fade = 1 - Smooth(1, 1.5, age);
            double heat = Pow(Clamp(1 - age / 0.8), 1.4);
            for (int y = 0; y <= 2; y++)
            for (int x = -9; x <= 9; x++)
            {
                double d = Length((x + 0.5) / 9, (y + 0.5) / 3.5);
                if (d > 1) continue;
                double t = heat * (1.2 - d) - Hash(x, y, 3) * 0.3;
                if (t > 0.08)
                    painter.Dot(impactX + x, y, palette[Max(1, Min(7, RoundJs(t * 9)))], fade);
                else
                    painter.Dot(impactX + x, y, d < 0.5 ? stoneDark : stoneShade,
                        (d < 0.9 - Hash(x, y, 9) * 0.3 ? 1 : 1 - d) * fade);
            }
            DrawRing(age, true);
        }

        private void DrawRing(double age, bool far)
        {
            double k = age / 0.45;
            if (k >= 1) return;
            double radius = 6 + 46 * OutCubic(k), thickness = Max(1, 3 * (1 - k));
            int samples = (int)Ceiling(radius * Tau * 1.2);
            for (int j = 0; j < samples; j++)
            {
                double a = j / (double)samples * Tau, s = Sin(a), c = Cos(a);
                if ((s < 0) != far) continue;
                for (double o = thickness; o >= 0; o -= 0.5)
                {
                    double u = o / thickness;
                    uint ink = u < 0.15 ? palette[6] : u < 0.4 ? palette[4] : u < 0.7 ? stoneLight : stone;
                    painter.Dot(impactX + c * (radius - o), 0.5 + s * (radius - o) * 0.16,
                        ink, (1.15 - k) * (u < 0.4 ? 1.2 : 1.15 - u));
                }
            }
        }

        private void DrawImpact(double age)
        {
            if (age < 0.07)
            {
                bool initial = age < 0.035;
                double scale = initial ? 1 : 1.25, thin = initial ? 1 : 0.55;
                for (int pass = 0; pass < 3; pass++)
                foreach (var spike in spikes)
                {
                    double c = Cos(spike.Angle), s = Sin(spike.Angle), length = spike.Length * scale;
                    for (double d = 0; d <= length; d += 0.5)
                    {
                        if (pass == 2 && d > length * 0.55) break;
                        double w = spike.Width * thin * (1 - d / length) * Min(1, 0.5 + d / 3);
                        double half = pass == 0 ? w + 0.8 : pass == 1 ? w : w * 0.45;
                        uint ink = pass == 0 ? palette[initial ? 5 : 4] :
                            pass == 2 ? initial ? White : palette[7] :
                            palette[d < length * 0.35 && initial ? 7 : 6];
                        for (double o = -half; o <= half; o += 0.5)
                            painter.Dot(impactX + c * d - s * o, -1 + s * d + c * o, ink);
                    }
                }
                double radius = 2.4 * scale * Sqrt(thin);
                painter.Circle(impactX, -1, radius + 0.8, palette[initial ? 7 : 6]);
                painter.Circle(impactX, -1, radius, initial ? White : palette[7]);
            }
            if (age >= 0.05 && age < 0.16)
            {
                double k = (age - 0.05) / 0.11;
                foreach (var shard in shards)
                {
                    double r = shard.Radius + shard.Velocity * (age - 0.05), arc = shard.Arc * (1 - 0.4 * k);
                    int samples = (int)Ceiling(r * arc * 2.2);
                    for (int j = 0; j <= samples; j++)
                    {
                        double u = j / (double)samples - 0.5, a = shard.Angle + u * arc, w = 0.7 * (1 - Abs(u) * 2);
                        for (double o = -w; o <= w + 0.01; o += 0.5)
                            painter.Dot(impactX + Cos(a) * (r + o), -1 + Sin(a) * (r + o),
                                palette[k < 0.4 ? 6 : k < 0.7 ? 5 : 4], 1 - k);
                    }
                }
            }
            if (age >= 0.2) return;
            double pop = Exp(-age / 0.05);
            painter.Glow(impactX, -6, 30, palette[5], 0.8 * pop);
            painter.Glow(impactX, -2, 10, White, 0.7 * pop);
        }

        private void DrawLight(double y, double size, uint ink, double strength, double duration, double age)
        {
            if (age >= duration) return;
            double k = 1 - age / duration;
            painter.Glow(impactX, y, size * (0.55 + 0.45 * k), ink, strength * k * k);
        }

        private sealed class Crack
        {
            internal readonly List<(double X, double Y)> Points = new List<(double, double)>();
            internal double Offset;
            internal bool Fork;
        }

        private void BuildCracks()
        {
            for (int i = 0; i < 4; i++)
            {
                int side = Chance(0.7) ? 1 : -1;
                double limit = 40 * Range(0.45, 1) * (i < 2 ? 1 : 0.7);
                double x = impactX + side * Range(0, 1.5), y = 0;
                var crack = new Crack();
                crack.Points.Add((x, y));
                while (Abs(x - impactX) < limit && y < 7)
                {
                    x += side * Range(1.5, 3.5);
                    y = Min(7, Max(0, y + Range(-0.6, 1.6) * (Abs(x - impactX) > 3 ? 1 : 0.3)));
                    crack.Points.Add((RoundJs(x), RoundJs(y)));
                    if (!Chance(0.18) || crack.Points.Count <= 3 || y >= 5) continue;
                    var fork = new Crack { Fork = true, Offset = Abs(x - impactX) / 40 };
                    fork.Points.Add((RoundJs(x), RoundJs(y)));
                    double fx = x, fy = y;
                    for (int j = (int)Range(1, 4); j > 0; j--)
                    {
                        fx += side * Range(1, 3);
                        fy += Range(0.8, 2);
                        fork.Points.Add((RoundJs(fx), RoundJs(Min(8, fy))));
                    }
                    cracks.Add(fork);
                }
                cracks.Add(crack);
            }
        }

        private void DrawCracks(double age)
        {
            double heat = Pow(Clamp(1 - age / 1.2), 1.2), fade = 1 - Smooth(1.15, 1.5, age);
            foreach (var crack in cracks)
            {
                double reach = 40 * OutCubic(Clamp((age - crack.Offset * 0.1) / 0.13)) * (crack.Fork ? 0.3 : 1);
                double d = 0;
                for (int i = 1; i < crack.Points.Count && d < reach; i++)
                {
                    var a = crack.Points[i - 1];
                    var b = crack.Points[i];
                    double len = Length(b.X - a.X, b.Y - a.Y);
                    if (len == 0) continue;
                    double f = Min(1, (reach - d) / len);
                    double x = a.X + (b.X - a.X) * f, y = a.Y + (b.Y - a.Y) * f;
                    double h = heat * (1 - d / 46) - (crack.Fork ? 0.12 : 0);
                    uint col = h > 0.72 ? White : h > 0.08 ? palette[Max(1, Min(6, RoundJs(h * 8)))] : stoneDark;
                    if (y > 0.5) painter.Line(a.X + 1, a.Y, x + 1, y, stoneDark, fade);
                    painter.Line(a.X, a.Y, x, y, col, fade);
                    if (h > 0.25 && (int)d % 6 < 2) painter.Glow(x, y, 6, palette[4], 0.2 * h * fade);
                    d += len;
                }
            }
            if (heat > 0) painter.Glow(impactX, -2, 34, palette[4], 0.32 * heat * fade);
        }

        private double Next()
        {
            unchecked
            {
                uint t = random += 0x6d2b79f5;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        private static uint HashPart(uint h, int n)
        {
            unchecked
            {
                h = (h ^ (uint)n) * 0x85ebca6b;
                h ^= h >> 13;
                h *= 0xc2b2ae35;
                return h ^ (h >> 16);
            }
        }

        private static double Hash(int x, int y, int seed) =>
            HashPart(HashPart(HashPart(0x9e3779b9, x), y), seed) / 4294967296.0;
        private double Range(double a, double b) => a + (b - a) * Next();
        private bool Chance(double probability) => Next() < probability;
        private static int RoundJs(double value) => (int)Floor(value + 0.5);
        private static double Clamp(double t) => Max(0, Min(1, t));
        private static double Length(double x, double y) => Sqrt(x * x + y * y);
        private static double Smooth(double a, double b, double t)
        {
            t = Clamp((t - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
        private static double OutCubic(double t) => 1 - Pow(1 - t, 3);
        private static double Head(double t) => Pow(Clamp((t - Raise) / SwingTime), 2);
        private static double PassedAt(double fraction) => Raise + SwingTime * Sqrt(Clamp(fraction));
    }
}
