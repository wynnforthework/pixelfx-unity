using System;
using static System.Math;
using static PixelFx.PixelPainter;
using static PixelFx.ImpactParticles;

namespace PixelFx
{
    // Port of Patch PixelFX impacts/parry. See the package license and notices.
    internal sealed class ParryEffect
    {
        internal const int Width = 200, Height = 112, OriginX = 100, OriginY = 96;
        internal const float Duration = 1;
        private const double Y = -30, Tau = 2 * PI;
        private readonly PixelPainter painter = new PixelPainter(Width, Height, OriginX, OriginY);
        private readonly uint[] colors;
        private readonly (double Angle, double Length, double Width)[] rays = new (double, double, double)[8];
        private readonly (double Angle, double Gap, double Drift, double Width)[] arcs = new (double, double, double, double)[6];
        private readonly double[] edges = new double[2];
        private readonly ImpactParticles particles;
        private ParryEffect(PixelFxSettings settings, bool full)
        {
            painter.LightStrength = settings.glow;
            colors = Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            var rng = new PixelFxRandom(settings.seed);
            for (int i = 0; i < rays.Length; i++)
            {
                double angle = i * PI / 4 + rng.Range(-0.04, 0.04);
                bool shortRay = i % 2 != 0;
                double length = shortRay ? rng.Range(6, 8) : i % 4 == 0 ? rng.Range(19, 22) : rng.Range(14, 17);
                rays[i] = (angle, length, shortRay ? 1.2 : 2.2);
            }
            for (int i = 0; i < arcs.Length; i++)
                arcs[i] = (i / (double)arcs.Length * Tau + rng.Range(-0.1, 0.1), rng.Range(0.55, 0.8), rng.Range(0.1, 0.35), 1);
            edges[0] = -0.62 + rng.Range(-0.08, 0.08);
            edges[1] = PI + 0.62 + rng.Range(-0.08, 0.08);

            if (full) particles = new ImpactParticles(CreateParticles(ref rng), colors[4], colors[4], colors[3], colors[2]);
        }

        private Particle[] CreateParticles(ref PixelFxRandom rng)
        {
            const int sparkCount = 24, moteCount = 8, glintCount = 3;
            var result = new Particle[sparkCount + moteCount + glintCount];
            int index = 0;
            for (int i = 0; i < sparkCount; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                double angle = -PI / 2 + side * rng.Range(0.3, 1.3);
                double speed = rng.Range(130, 290);
                result[index++] = Spawn(ref rng, ParticleKind.Spark, side * rng.Range(1, 3), Y,
                    Cos(angle) * speed, Sin(angle) * speed, rng.Range(0.45, 0.85), White, 1,
                    i > 17 ? rng.Range(0.02, 0.07) : 0);
            }
            for (int i = 0; i < moteCount; i++)
            {
                double angle = rng.Range(0, Tau), speed = rng.Range(40, 110);
                result[index++] = Spawn(ref rng, ParticleKind.Mote, 0, Y, Cos(angle) * speed,
                    Sin(angle) * speed - 20, rng.Range(0.3, 0.55), colors[5 + (int)rng.Range(0, 2)],
                    0.6, rng.Range(0, 0.04));
            }
            for (int i = 0; i < glintCount; i++)
            {
                double angle = rng.Range(0, Tau), distance = rng.Range(12, 26);
                result[index++] = Spawn(ref rng, ParticleKind.Glint, Cos(angle) * distance, Y + Sin(angle) * distance,
                    0, -3, rng.Range(0.22, 0.34), White, rng.Range(1.6, 2.6), rng.Range(0.1, 0.3));
            }
            return result;
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new ParryEffect(timeline.Settings, timeline.Quality == PixelFxQuality.Full);
            return (pixels, _, time, _) => effect.Render(pixels, time);
        }

        private void Render(byte[] pixels, double elapsed)
        {
            particles?.Advance(elapsed);
            double time = elapsed;
            painter.Clear();
            DrawRing(time);
            particles?.Draw(painter);
            if (time < 0.058) DrawEdges(time);
            if (time < 0.025) ImpactShapes.DrawStar(painter, rays, Y, 1, 1, colors[4], White, White, White, 2.6);
            else if (time < 0.058) ImpactShapes.DrawStar(painter, rays, Y, 1.25, 0.65, colors[2], colors[5], colors[6], White, 2.6);
            else if (time < 0.092) ImpactShapes.DrawStar(painter, rays, Y, 1.4, 0.3, colors[2], colors[4], colors[5], colors[6], 2);

            double pop = Exp(-time / 0.05);
            if (pop > 0.02)
            {
                painter.Glow(0, Y, 30, colors[4], 0.5 * pop);
                painter.Glow(0, Y, 10, White, 0.4 * pop);
            }
            DrawLight(60, colors[4], 0.65, 0.4, time);
            DrawLight(22, White, 0.8, 0.08, time);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawLight(double radius, uint color, double intensity, double lifetime, double time)
        {
            if (time >= lifetime) return;
            double remaining = (lifetime - time) / lifetime;
            painter.Glow(0, Y, radius * (0.55 + 0.45 * remaining), color, intensity * remaining * remaining);
        }

        private void DrawRing(double time)
        {
            double progress = (time - 0.03) / 0.36;
            if (progress < 0 || progress >= 1) return;
            double radius = 8 + 24 * (1 - Pow(1 - progress, 3));
            double spread = Min(1, Max(0, (progress - 0.15) / (0.6 - 0.15)));
            double breakup = spread * spread * (3 - 2 * spread);
            double thickness = Max(1, 2.6 * Pow(1 - progress, 1.2));
            int shade = progress < 0.15 ? 6 : progress < 0.35 ? 5 : progress < 0.6 ? 4 : 3;
            double alpha = progress < 0.6 ? 1 : (1 - progress) / 0.4;
            ImpactShapes.DrawBrokenRing(painter, arcs, Y, radius, thickness, breakup, colors[shade], colors[Max(2, shade - 2)], alpha);
            if (progress < 0.5)
                painter.GlowRing(0, Y, radius, 5, colors[4], 0.3 * (1 - progress / 0.5));
        }

        private void DrawEdges(double time)
        {
            double length = time < 0.025 ? 26 : 32, intensity = time < 0.025 ? 1 : 0.6;
            foreach (double angle in edges)
            {
                double cos = Cos(angle), sin = Sin(angle);
                for (double d = -length; d <= length; d += 0.5)
                {
                    double u = Abs(d) / length;
                    painter.Dot(cos * d, Y + sin * d, u < 0.4 ? White : u < 0.75 ? colors[5] : colors[4],
                        u < 0.6 ? 1 : intensity * (1.3 - u));
                }
                painter.GlowLine(-cos * length, Y - sin * length, cos * length, Y + sin * length, 4, colors[5], 0.35 * intensity);
            }
        }
    }
}
