using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. See NOTICE.txt.
    internal sealed class DivineShieldEffect
    {
        internal const int Width = 88, Height = 84, OriginX = 44, OriginY = 68;
        internal const float Duration = 2.4f;
        private const double Period = 2.4, Tau = 2 * PI, CenterY = -22, Radius = 29, Tilt = 0.34;
        private const int Meridians = 8;
        private static readonly double SealRadius = Sqrt(Radius * Radius - CenterY * CenterY);
        private static readonly double TiltSin = Sin(Tilt), TiltCos = Cos(Tilt);

        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly PixelFxTimeline timeline;
        private readonly double[] depth = new double[Width * Height];
        private readonly bool[] hot = new bool[Width * Height];
        private readonly (double Born, double Life, double Latitude, double Longitude, double Size)[] glints;
        private readonly (double Born, double Life, double X, double Phase)[] motes;

        private DivineShieldEffect(PixelFxTimeline timeline)
        {
            this.timeline = timeline;
            var settings = timeline.Settings;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            if (timeline.Quality != PixelFxQuality.Full) return;

            glints = new (double, double, double, double, double)[9];
            for (int i = 0; i < glints.Length; i++)
            {
                var timing = new PixelFxRandom(settings.seed, i, 911);
                var random = new PixelFxRandom(settings.seed, i);
                glints[i] = (timing.Range(0, 1) * Period, timing.Range(0.35, 0.6),
                    random.Range(-1.2, 1.3), random.Range(0, Tau), random.Range(0.6, 1));
            }
            motes = new (double, double, double, double)[12];
            int seed = unchecked(settings.seed + 1);
            for (int i = 0; i < motes.Length; i++)
            {
                var timing = new PixelFxRandom(seed, i, 911);
                var random = new PixelFxRandom(seed, i);
                motes[i] = (timing.Range(0, 1) * Period, timing.Range(1.2, 2.2),
                    random.Range(-0.7, 0.7) * Radius, random.Range(0, 6));
            }
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new DivineShieldEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private void Render(byte[] pixels, double time, PixelFxRenderPass pass)
        {
            painter.Clear();
            time %= Period;
            double phase = time / Period;
            double rotation = phase * (PI / 4);
            double sheen = -1.7 + 3.4 * Min(1, phase / 0.55);
            if (pass != PixelFxRenderPass.Front)
            {
                DrawFilm(sheen, true);
                DrawLattice(rotation, sheen, true);
                DrawSeal(true);
                painter.Glow(0, CenterY, Radius * 1.1, colors[4], 0.14);
            }
            if (pass != PixelFxRenderPass.Back)
            {
                timeline.ActorMask?.LightOutline(painter, timeline.ActorToSurface, colors[5], 0.3,
                    -OriginX, -OriginY, Width - OriginX, Height - OriginY);
                DrawFilm(sheen, false);
                DrawLattice(rotation, sheen, false);
                DrawRim(sheen);
                DrawHighlights(phase, rotation);
                DrawSeal(false);
                for (int i = -2; i <= 2; i++)
                    painter.Glow(i * SealRadius * 0.5, 0, 12, colors[4], 0.18);
                if (glints != null) DrawParticles(time, rotation);
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private static (double X, double Y, double Z) OnSphere(double latitude, double longitude)
        {
            double x = Radius * Cos(latitude) * Sin(longitude);
            double y = Radius * Sin(latitude);
            double z = Radius * Cos(latitude) * Cos(longitude);
            return (x, CenterY - (y * TiltCos - z * TiltSin), y * TiltSin + z * TiltCos);
        }

        private static double Diagonal(double x, double y) => (x - (y - CenterY)) / (Radius * 1.414);

        private void DrawFilm(double sheen, bool back)
        {
            for (int y = (int)(CenterY - Radius); y < CenterY + Radius; y++)
            for (int x = (int)-Radius; x < Radius; x++)
            {
                double dx = x + 0.5, dy = y + 0.5 - CenterY;
                double d = Sqrt(dx * dx + dy * dy) / Radius;
                if (d >= 1) continue;
                double highlight = Max(0, 1 - Abs(Diagonal(x + 0.5, y + 0.5) - sheen) / 0.22);
                double opacity = (back ? 0.07 : 0.03) + 0.26 * Pow(d, 6) + 0.2 * highlight * (back ? 0.3 : 1);
                painter.BlendPixel(x, y, colors[highlight > 0.5 && !back ? 6 : 5], opacity);
            }
        }

        private void DrawLattice(double rotation, double sheen, bool back)
        {
            Array.Fill(depth, double.PositiveInfinity);
            for (int k = 0; k < Meridians; k++)
            {
                double longitude = rotation + k * PI / 4;
                for (double latitude = -PI / 2; latitude <= PI / 2; latitude += 0.7 / Radius)
                    Record(OnSphere(latitude, longitude), sheen, back);
            }
            for (double longitude = 0; longitude < Tau; longitude += 0.7 / Radius)
                Record(OnSphere(0, longitude), sheen, back);

            for (int i = 0; i < depth.Length; i++)
            {
                double d = depth[i];
                if (double.IsPositiveInfinity(d)) continue;
                int x = i % Width - OriginX, y = i / Width - OriginY;
                if (back) painter.BlendPixel(x, y, colors[4], 0.32);
                else if (hot[i]) painter.Dot(x, y, White);
                else if (d < 0.4) painter.Dot(x, y, colors[6]);
                else painter.BlendPixel(x, y, colors[d < 0.7 ? 6 : 5], d < 0.7 ? 0.8 : 0.55);
            }
        }

        private void Record((double X, double Y, double Z) point, double sheen, bool back)
        {
            var (x, y, z) = point;
            if ((z < 0) != back) return;
            int i = ((int)Floor(y) + OriginY) * Width + (int)Floor(x) + OriginX;
            double d = Abs(z) / Radius;
            if (d >= depth[i]) return;
            hot[i] = Abs(Diagonal(x, y) - sheen) < 0.12 || (!double.IsPositiveInfinity(depth[i]) && hot[i]);
            depth[i] = d;
        }

        private void DrawSeal(bool back)
        {
            int count = (int)Ceiling(SealRadius * Tau * 1.5) + 6;
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau, s = Sin(angle);
                if ((s < 0) != back) continue;
                painter.Dot(Cos(angle) * SealRadius, s * (SealRadius * 0.26),
                    back ? colors[5] : s > 0.6 ? White : colors[6], back ? 0.8 : 1);
            }
        }

        private void DrawRim(double sheen)
        {
            int count = (int)Ceiling(Radius * Tau * 1.4);
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau, c = Cos(angle), s = Sin(angle);
                double x = c * Radius, y = CenterY + s * Radius;
                double lit = Cos(angle + 2.36);
                bool highlight = Abs(Diagonal(x, y) - sheen) < 0.15;
                painter.Dot(x, y, highlight || lit > 0.75 ? White : colors[lit > -0.2 ? 6 : 5]);
                painter.Dot(c * (Radius - 1), CenterY + s * (Radius - 1), colors[4], 0.35 + 0.3 * lit);
            }
            painter.GlowRing(0, CenterY, Radius, 6, colors[4], 0.32);
        }

        private void DrawHighlights(double phase, double rotation)
        {
            for (int k = 0; k < Meridians; k++)
            {
                var (x, y, z) = OnSphere(0, rotation + k * PI / 4);
                if (z < Radius * 0.15) continue;
                painter.Dot(x, y, White);
                if (z > Radius * 0.45)
                {
                    painter.BlendPixel(x + 1, y, colors[6], 0.7);
                    painter.BlendPixel(x - 1, y, colors[6], 0.7);
                    painter.BlendPixel(x, y + 1, colors[6], 0.7);
                    painter.BlendPixel(x, y - 1, colors[6], 0.7);
                }
                painter.Glow(x, y, 4, colors[5], 0.3);
            }
            var (poleX, poleY, _) = OnSphere(PI / 2, 0);
            double pulse = 1.5 + 1.5 * Sin(phase * Tau * 2);
            double whole = Floor(pulse);
            Sparkle(poleX, poleY, 2 + whole + (pulse - whole >= 0.5 ? 1 : 0), 0.45);
            for (int i = 0; i < 2; i++)
            {
                double angle = phase * Tau + i * PI + 0.8;
                Sparkle(Cos(angle) * Radius, CenterY + Sin(angle) * Radius, 2, 0.4);
            }
            for (double a = 3.55; a < 4.2; a += 0.04)
                painter.Dot(Cos(a) * Radius * 0.74, CenterY + Sin(a) * Radius * 0.74,
                    a > 3.7 && a < 4.05 ? White : colors[6]);
            painter.Dot(Cos(3.4) * Radius * 0.62, CenterY + Sin(3.4) * Radius * 0.62, White);
            for (double a = 0.45; a < 0.8; a += 0.05)
                painter.Dot(Cos(a) * Radius * 0.8, CenterY + Sin(a) * Radius * 0.8, colors[5], 0.6);
        }

        private void DrawParticles(double time, double rotation)
        {
            foreach (var (born, life, latitude, longitude, size) in glints)
            {
                double age = ((time - born) % Period + Period) % Period;
                if (age >= life) continue;
                var (x, y, z) = OnSphere(latitude, longitude + rotation);
                if (z < 0) continue;
                double pulse = Sin(age / life * PI);
                Sparkle(x, y, 1 + 2.5 * pulse * size, 0.45 * pulse);
            }
            foreach (var (born, life, startX, phase) in motes)
            {
                double age = ((time - born) % Period + Period) % Period;
                if (age >= life) continue;
                double u = age / life, x = startX + Sin(u * 6 + phase) * 2;
                double y = CenterY + Radius * 0.75 - u * Radius * 1.4;
                double dy = y - CenterY;
                if (x * x + dy * dy > (Radius - 2) * (Radius - 2)) continue;
                double pulse = Sin(u * PI);
                painter.Dot(x, y, colors[u < 0.5 ? 6 : 5], pulse);
                painter.Light(x, y, colors[5], 0.4 * pulse);
            }
        }

        private void Sparkle(double x, double y, double arm, double light)
        {
            int count = (int)Floor(arm + 0.5);
            double px = Floor(x), py = Floor(y);
            for (int d = 1; d <= count; d++)
            {
                uint ink = d > count * 0.6 ? colors[6] : White;
                painter.Dot(px + d, py, ink);
                painter.Dot(px - d, py, ink);
                painter.Dot(px, py + d, ink);
                painter.Dot(px, py - d, ink);
            }
            if (count >= 3)
            {
                painter.Dot(px + 1, py + 1, White);
                painter.Dot(px + 1, py - 1, White);
                painter.Dot(px - 1, py + 1, White);
                painter.Dot(px - 1, py - 1, White);
            }
            painter.Dot(px, py, White);
            painter.Glow(x, y, 4 + count * 2, White, light);
        }
    }
}
