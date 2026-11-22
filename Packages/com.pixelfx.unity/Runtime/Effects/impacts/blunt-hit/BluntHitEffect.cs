using System;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX impacts/blunt-hit. See the package license and notices.
    internal sealed class BluntHitEffect
    {
        internal const int Width = 128, Height = 88, OriginX = 64, OriginY = 72;
        internal const float Duration = 0.9f;
        internal const double CenterY = -22;
        private static readonly uint Warm = Hex(0xffc98a);
        private readonly PixelPainter painter = new PixelPainter(Width, Height, OriginX, OriginY);
        private readonly uint[] colors;
        private readonly double phase;
        private readonly Wedge[] wedges = new Wedge[9];
        private readonly BluntHitDebris debris;

        private struct Wedge
        {
            internal double Cos, Sin, Length, Width, Root, Start, Extend, Retract;
        }

        private BluntHitEffect(PixelFxSettings settings, bool full)
        {
            painter.LightStrength = settings.glow;
            colors = Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            var rng = new PixelFxRandom(settings.seed);
            phase = rng.Range(0, 2 * PI);
            for (int i = 0; i < wedges.Length; i++)
            {
                double angle = i / (double)wedges.Length * (2 * PI) + rng.Range(-0.2, 0.2);
                wedges[i] = new Wedge
                {
                    Cos = Cos(angle), Sin = Sin(angle), Length = rng.Range(6, 15), Width = rng.Range(3.2, 5),
                    Root = rng.Range(13, 16), Start = rng.Range(0.015, 0.04),
                    Extend = rng.Range(0.04, 0.06), Retract = rng.Range(0.09, 0.14)
                };
            }
            if (full) debris = new BluntHitDebris(rng);
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new BluntHitEffect(timeline.Settings, timeline.Quality == PixelFxQuality.Full);
            return (pixels, _, time, _) => effect.Render(pixels, time);
        }

        private void Render(byte[] pixels, double time)
        {
            painter.Clear();
            debris?.Draw(painter, time, Warm);
            if (time < 0.058) DrawFlash(time >= 0.025);
            foreach (var wedge in wedges) DrawWedge(wedge, time - wedge.Start);
            double pop = Exp(-time / 0.06);
            if (pop > 0.02) painter.Glow(0, CenterY, 28, Warm, 0.5 * pop);
            DrawLight(CenterY, 48, Warm, 0.6, 0.4, time);
            DrawLight(CenterY, 16, White, 0.6, 0.07, time);
            DrawLight(-2, 36, Warm, 0.25, 0.4, time);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawFlash(bool cooling)
        {
            double radius = cooling ? 13.5 : 11.5, bound = radius * 1.4;
            painter.Shade(-bound, CenterY - bound, bound, CenterY + bound,
                (Radius: radius, Phase: phase + (cooling ? 0.6 : 0), Cooling: cooling, Colors: colors),
                static (x, y, flash) =>
                {
                    double dy = (y - CenterY) * (flash.Cooling ? 1.25 : 1.1);
                    double angle = Atan2(dy, x);
                    double edge = flash.Radius * (1 + 0.15 * Sin(5 * angle + flash.Phase)
                        + 0.08 * Sin(9 * angle + flash.Phase * 2.3));
                    double fraction = Sqrt(x * x + dy * dy) / edge;
                    if (fraction > 1) return default;
                    uint color = flash.Cooling
                        ? fraction < 0.3 ? White : fraction < 0.72 ? flash.Colors[5] : fraction < 0.9 ? flash.Colors[4] : flash.Colors[3]
                        : fraction < 0.55 ? White : fraction < 0.86 ? flash.Colors[5] : flash.Colors[4];
                    return (color, 1);
                });
        }

        private void DrawWedge(Wedge wedge, double age)
        {
            if (age < 0 || age > wedge.Extend + wedge.Retract) return;
            double growth = age < wedge.Extend
                ? 1 - Pow(1 - age / wedge.Extend, 3)
                : 1 - Pow((age - wedge.Extend) / wedge.Retract, 2);
            double length = wedge.Length * growth;
            if (length < 1.5) return;
            double root = wedge.Root + 2 * growth, halfWidth = wedge.Width * (0.6 + 0.4 * growth) / 2;
            double startX = wedge.Cos * root, startY = CenterY + wedge.Sin * root;
            double endX = wedge.Cos * (root + length), endY = CenterY + wedge.Sin * (root + length);
            double nx = -wedge.Sin * halfWidth, ny = wedge.Cos * halfWidth;
            painter.Triangle((startX - wedge.Cos, startY - wedge.Sin),
                (endX + nx * 1.45 + wedge.Cos, endY + ny * 1.45 + wedge.Sin),
                (endX - nx * 1.45 + wedge.Cos, endY - ny * 1.45 + wedge.Sin), colors[1]);
            painter.Triangle((startX, startY), (endX + nx, endY + ny), (endX - nx, endY - ny),
                age < wedge.Extend + 0.02 ? White : colors[5]);
        }

        private void DrawLight(double y, double radius, uint color, double intensity, double lifetime, double time)
        {
            if (time >= lifetime) return;
            double remaining = (lifetime - time) / lifetime;
            painter.Glow(0, y, radius * (0.55 + 0.45 * remaining), color, intensity * remaining * remaining);
        }
    }
}
