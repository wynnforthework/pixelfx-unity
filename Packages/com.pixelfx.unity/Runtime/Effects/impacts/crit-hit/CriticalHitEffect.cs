using System;
using static System.Math;
using static PixelFx.ImpactParticles;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX impacts/crit-hit. See the package license and notices.
    internal sealed class CriticalHitEffect
    {
        internal const int Width = 216, Height = 136, OriginX = 84, OriginY = 106;
        internal const float Duration = 0.9f;
        private const double Y = -26, Aim = 0.12, Tau = 2 * PI;
        private readonly PixelPainter painter = new PixelPainter(Width, Height, OriginX, OriginY);
        private readonly uint[] colors;
        private readonly (double Angle, double Length, double Width)[] rays = new (double, double, double)[14];
        private readonly (double Angle, double Gap, double Drift, double Width)[] arcs = new (double, double, double, double)[7];
        private readonly ImpactParticles particles;
        private readonly CriticalHitShards shards;
        private static readonly (double X, double Y, double Scale)[] FlareAxes =
        {
            (1, 0, 1), (0, 1, 0.45), (0.7, 0.7, 0.16), (0.7, -0.7, 0.16)
        };

        private CriticalHitEffect(PixelFxSettings settings, bool full)
        {
            painter.LightStrength = settings.glow;
            colors = Array.ConvertAll(settings.palette, color => Hex(PixelFxLibrary.Pack(color)));
            var rng = new PixelFxRandom(settings.seed);
            CreateRays(ref rng);
            for (int i = 0; i < arcs.Length; i++)
            {
                double offset = i / (double)arcs.Length * Tau;
                double angle = Aim + offset;
                double forward = Max(0, Cos(angle - Aim));
                arcs[i] = (angle, rng.Range(0.55, 0.85), rng.Range(0.08, 0.4) + 0.15 * forward, 0.7 + 0.5 * forward);
            }
            if (full)
                particles = CreateParticles(ref rng, out shards);
        }

        private void CreateRays(ref PixelFxRandom rng)
        {
            for (int i = 0; i < rays.Length; i++)
            {
                double angle = Aim + i / (double)rays.Length * Tau + rng.Range(-0.18, 0.18);
                double alignment = Cos(angle - Aim), jag = i % 2 == 0 ? 1 : 0.5;
                double length = rng.Range(11, 15) * (1 + 1.4 * Pow(Max(0, alignment), 3)
                    + 0.6 * Pow(Max(0, -alignment), 3)) * jag;
                rays[i] = (angle, length, rng.Range(1.8, 2.5) * Sqrt(jag));
            }

            int longest = 0, second = 1;
            if (rays[second].Length > rays[longest].Length) (longest, second) = (second, longest);
            for (int i = 2; i < rays.Length; i++)
            {
                if (rays[i].Length > rays[longest].Length)
                    (longest, second) = (i, longest);
                else if (rays[i].Length > rays[second].Length)
                    second = i;
            }
            rays[longest].Length *= 1.3;
            rays[second].Length *= 1.3;
        }

        private ImpactParticles CreateParticles(ref PixelFxRandom rng, out CriticalHitShards fragments)
        {
            const int sparkCount = 36, moteCount = 12, glintCount = 5;
            var result = new Particle[sparkCount + moteCount + glintCount];
            int index = 0;
            for (int i = 0; i < sparkCount; i++)
            {
                bool back = rng.Range(0, 1) < 0.22;
                double angle = (back ? Aim + PI : Aim) + rng.Range(-1, 1) * (back ? 0.6 : 0.8);
                double speed = rng.Range(170, 430) * (back ? 0.6 : 1);
                result[index++] = Spawn(ref rng, ParticleKind.Spark, 0, Y, Cos(angle) * speed, Sin(angle) * speed - 50,
                    rng.Range(0.15, 0.5), White, 1, rng.Range(0, 1) < 0.3 ? rng.Range(0, 0.04) : 0);
            }
            fragments = new CriticalHitShards(ref rng, Y, Aim, colors);
            for (int i = 0; i < moteCount; i++)
            {
                double angle = Aim - PI / 2 + rng.Range(-1.4, 1.4), speed = rng.Range(35, 110);
                double cos = Cos(angle), sin = Sin(angle);
                result[index++] = Spawn(ref rng, ParticleKind.Mote, cos * 4, Y + sin * 4, cos * speed, sin * speed - 15,
                    rng.Range(0.35, 0.7), colors[(int)rng.Range(0, 3) == 0 ? 6 : 5], 0.6, rng.Range(0, 0.05));
            }
            for (int i = 0; i < glintCount; i++)
            {
                double angle = rng.Range(0, Tau), distance = rng.Range(14, 34);
                result[index++] = Spawn(ref rng, ParticleKind.Glint, Cos(angle) * distance, Y + Sin(angle) * distance * 0.8,
                    0, -4, rng.Range(0.22, 0.38), rng.Range(0, 1) < 0.5 ? colors[6] : White,
                    rng.Range(1.6, 3), rng.Range(0.08, 0.32));
            }
            return new ImpactParticles(result, colors[7], colors[6], colors[5], colors[3]);
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new CriticalHitEffect(timeline.Settings, timeline.Quality == PixelFxQuality.Full);
            return (pixels, _, time, _) => effect.Render(pixels, time);
        }

        private void Render(byte[] pixels, double time)
        {
            painter.Clear();
            DrawRing(time);
            shards?.Draw(painter, time);
            particles?.Advance(time);
            particles?.Draw(painter);
            double flare = (time - 0.04) / 0.28;
            if (flare > 0 && flare < 1)
            {
                DrawFlare(64 * Pow(1 - flare, 1.3), 1 - flare);
                painter.Glow(0, Y, 56, colors[5], 0.4 * (1 - flare));
            }
            if (time < 0.025) DrawStar(1, 1, 6, 7, 7, 7);
            else if (time < 0.058) DrawStar(1.3, 1, 2, 5, 6, 7);
            else if (time < 0.092) DrawStar(1.05, 0.72, 2, 4, 5, 6);
            else if (time < 0.125) DrawStar(1.22, 0.3, 1, 2, 3, 4);
            double pop = Exp(-time / 0.05);
            if (pop > 0.02)
            {
                painter.Glow(0, Y, 44, colors[5], 0.4 * pop);
                painter.Glow(0, Y, 12, White, 0.6 * pop);
            }
            DrawLight(Y, 84, colors[5], 0.6, 0.5, time);
            DrawLight(Y, 30, White, 0.7, 0.1, time);
            DrawLight(-4, 44, colors[4], 0.3, 0.45, time);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawStar(double scale, double thickness, int rim, int body, int inner, int core) =>
            ImpactShapes.DrawStar(painter, rays, Y, scale, thickness, colors[rim], colors[body], colors[inner], colors[core], 2.6);

        private void DrawRing(double time)
        {
            double u = (time - 0.03) / 0.37;
            if (u < 0 || u >= 1) return;
            double radius = 10 + 30 * (1 - Pow(1 - u, 3));
            double spread = Min(1, Max(0, (u - 0.12) / (0.6 - 0.12)));
            double breakup = spread * spread * (3 - 2 * spread);
            double thickness = Max(1, 3.6 * Pow(1 - u, 1.2));
            int shade = u < 0.12 ? 7 : u < 0.3 ? 6 : u < 0.5 ? 5 : u < 0.75 ? 4 : 3;
            double alpha = u < 0.6 ? 1 : (1 - u) / 0.4;
            ImpactShapes.DrawBrokenRing(painter, arcs, Y, radius, thickness, breakup, colors[shade], colors[Max(2, shade - 2)], alpha);
            if (u < 0.6) painter.GlowRing(0, Y, radius, 6, colors[5], 0.35 * Pow(1 - u / 0.6, 2));
        }

        private void DrawFlare(double length, double intensity)
        {
            foreach (var axis in FlareAxes)
            {
                double extent = length * axis.Scale;
                for (int side = -1; side <= 1; side += 2)
                for (int d = 1; d <= extent; d++)
                {
                    double u = d / extent;
                    painter.Dot(side * axis.X * d, Y + side * axis.Y * d, u < 0.35 ? White : colors[u < 0.7 ? 6 : 5],
                        u < 0.7 ? 1 : intensity * (1.6 - u));
                    if (axis.Y == 0 && u < 0.42)
                    {
                        uint color = colors[u < 0.2 ? 6 : 5];
                        double alpha = u < 0.3 ? 1 : intensity;
                        painter.Dot(side * d, Y - 1, color, alpha);
                        painter.Dot(side * d, Y + 1, color, alpha);
                    }
                }
            }
            painter.GlowLine(-length, Y, length, Y, 6, colors[6], 0.5 * intensity);
            painter.GlowLine(0, Y - length * 0.45, 0, Y + length * 0.45, 4, colors[6], 0.3 * intensity);
        }

        private void DrawLight(double y, double radius, uint color, double intensity, double lifetime, double time)
        {
            if (time >= lifetime) return;
            double remaining = (lifetime - time) / lifetime;
            painter.Glow(0, y, radius * (0.55 + 0.45 * remaining), color, intensity * remaining * remaining);
        }
    }
}
