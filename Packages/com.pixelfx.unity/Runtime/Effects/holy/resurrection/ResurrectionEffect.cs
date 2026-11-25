using System;
using UnityEngine;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX 5294518. The lit floor line adapts Sprite Fusion's heal,
    // MIT License, Copyright (c) 2026 Hugo Duprez. See NOTICE.txt.
    internal sealed partial class ResurrectionEffect
    {
        internal const int Width = 192, Height = 160, OriginX = 96, OriginY = 140;
        internal const float Duration = 4.8f;
        private const double Tau = 2 * PI, Top = -140, Step = 1.0 / 120;
        private const double Pour = 0.34, ClimbStart = 0.5, ClimbEnd = 1.15;
        private const double HaloStart = 1.24, HaloEnd = 1.5, HaloY = -47, HaloRx = 9.5, HaloRy = 2.6;
        private static readonly uint[] Ash = { Hex(0x16131c), Hex(0x26222f), Hex(0x3a3546), Hex(0x524c5e), Hex(0x6e6878), Hex(0x8c8796) };
        private readonly PixelFxTimeline timeline;
        private readonly PixelPainter painter;
        private readonly uint[] colors;
        private readonly bool full;
        private readonly PixelFxRandom initialRandom;
        private PixelFxRandom random;
        private int tick;
        private double time;
        private readonly float[] profile = new float[121];
        private readonly (double Offset, double Width, double Brightness, double Frequency, double Phase)[] rays =
            new (double, double, double, double, double)[6];
        private readonly int[] cores = new int[2];
        private readonly (double X, double Y, double Speed, double Amplitude, double Phase, double Frequency)[] motes =
            new (double, double, double, double, double, double)[34];
        private readonly Color32[] actor = new Color32[Width * Height];
        private int actorTop, maskVersion = -1;
        private Matrix4x4 maskTransform;
        private bool hasMask;

        private ResurrectionEffect(PixelFxTimeline timeline)
        {
            this.timeline = timeline;
            var settings = timeline.Settings;
            full = timeline.Quality == PixelFxQuality.Full;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            colors = Array.ConvertAll(settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            random = new PixelFxRandom(settings.seed);
            for (int i = 0; i < rays.Length; i++)
                rays[i] = (-0.75 + i * 0.3 + random.Range(-0.08, 0.08), random.Range(0.1, 0.2),
                    random.Range(0.55, 1), random.Range(1.5, 3.5), random.Range(0, Tau));
            var order = new[] { 0, 1, 2, 3, 4, 5 };
            Array.Sort(order, (a, b) => rays[b].Brightness.CompareTo(rays[a].Brightness));
            cores[0] = order[0];
            cores[1] = order[1];
            for (int i = 0; i < motes.Length; i++)
                motes[i] = (random.Range(-1, 1), random.Range(0, 1), random.Range(4, 11),
                    random.Range(0.6, 2.4), random.Range(0, Tau), random.Range(0.6, 1.4));
            CreateQuills();
            initialRandom = random;
            actorTop = -38;
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline)
        {
            var effect = new ResurrectionEffect(timeline);
            return (pixels, _, time, _, pass) => effect.Render(pixels, time, pass);
        }

        private static double Clamp(double u) => Max(0, Min(1, u));
        private static double Seg(double t, double a, double b) => Clamp((t - a) / (b - a));
        private static double Lerp(double a, double b, double u) => a + (b - a) * u;
        private static double Smooth(double a, double b, double u)
        {
            u = Seg(u, a, b);
            return u * u * (3 - 2 * u);
        }
        private static double OutCubic(double u) => 1 - Pow(1 - u, 3);
        private static double OutBack(double u, double s) =>
            u <= 0 || u >= 1 ? u : 1 + (s + 1) * Pow(u - 1, 3) + s * Pow(u - 1, 2);
        private static int Round(double u) => (int)Floor(u + 0.5);
        private double ClimbY() => Lerp(1, actorTop - 2, -(Cos(PI * Seg(time, ClimbStart, ClimbEnd)) - 1) / 2);
        private double ShaftStrength() => Smooth(0, 0.12, time) * (1 - Smooth(3, 3.75, time));
        private double HalfWidth(double y) => Lerp(8, 25, (y - Top) / -Top) *
            (Lerp(0.4, 1, OutCubic(Seg(time, 0, 0.6))) * Lerp(1, 0.12, Pow(Seg(time, 2.9, 3.6), 3)));

        private void Render(byte[] pixels, double sample, PixelFxRenderPass pass)
        {
            bool front = pass != PixelFxRenderPass.Back;
            if (front || full) UpdateMask();
            Advance(sample);
            painter.Clear();
            if (pass != PixelFxRenderPass.Front)
            {
                DrawShaft();
                DrawPool(true);
                DrawGroundRing(true);
                DrawWing(-1);
                DrawWing(1);
                DrawLoose(true);
            }
            if (front)
            {
                DrawPool(false);
                DrawGroundRing(false);
                DrawBody();
                DrawWingAccent();
                DrawHalo();
                if (time >= ClimbEnd && time < ClimbEnd + 0.22)
                    DrawFlare(22 * (1 - (time - ClimbEnd) / 0.22), 1 - (time - ClimbEnd) / 0.22);
                DrawLoose(false);
                if (full) DrawParticles();
                DrawLights();
            }
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void UpdateMask()
        {
            var mask = timeline.ActorMask;
            if (mask == null || (mask.Version == maskVersion && maskTransform.Equals(timeline.ActorToSurface))) return;
            maskVersion = mask.Version;
            maskTransform = timeline.ActorToSurface;
            hasMask = mask.TryProject(maskTransform, out var projection);
            if (!hasMask)
            {
                actorTop = -38;
                return;
            }

            actorTop = Height - OriginY;
            for (int y = -OriginY; y < Height - OriginY; y++)
            for (int x = -OriginX; x < Width - OriginX; x++)
            {
                var c = projection.Sample(x + 0.5, y + 0.5);
                actor[(y + OriginY) * Width + x + OriginX] = c;
                if (c.a != 0) actorTop = Min(actorTop, y);
            }
            if (actorTop == Height - OriginY) actorTop = -38;
        }

        private Color32 SampleActor(int x, int y)
        {
            x += OriginX;
            y += OriginY;
            return hasMask && (uint)x < Width && (uint)y < Height ? actor[y * Width + x] : default;
        }

        private bool IsEdge(int x, int y) =>
            SampleActor(x - 1, y).a == 0 || SampleActor(x + 1, y).a == 0 ||
            SampleActor(x, y - 1).a == 0 || SampleActor(x, y + 1).a == 0;

        private bool IsOutline(int x, int y) =>
            SampleActor(x - 1, y).a != 0 || SampleActor(x + 1, y).a != 0 ||
            SampleActor(x, y - 1).a != 0 || SampleActor(x, y + 1).a != 0;

        private void DrawShaft()
        {
            double k = ShaftStrength();
            if (k <= 0) return;
            for (int i = 0; i < profile.Length; i++)
            {
                double q = (i - 60) / 40.0, intensity = 0;
                foreach (var r in rays)
                {
                    double d = (q - r.Offset) / r.Width;
                    intensity += r.Brightness * Exp(-d * d) * (0.72 + 0.28 * Sin(time * r.Frequency + r.Phase));
                }
                profile[i] = (float)((intensity * 0.6 + 0.35 * Max(0, 1 - q * q)) * Smooth(1.25, 0.7, Abs(q)));
            }

            double front = Lerp(Top, 1, Pow(Seg(time, 0, Pour), 2)), w0 = HalfWidth(0) * 1.3;
            for (int y = (int)Top; y <= Ceiling(Min(front, 0.5)); y++)
            {
                double py = y + 0.5, w = HalfWidth(py);
                double depth = (0.75 + 0.25 * Smooth(Top, 0, py)) * Smooth(front, front - 10, py);
                for (int x = (int)Floor(-w0 - 1); x <= Ceiling(w0 + 1); x++)
                {
                    double intensity = ProfileAt((x + 0.5) / w) * k * depth;
                    if (intensity <= 0.01) continue;
                    painter.BlendPixel(x, y, colors[6], intensity * 0.24);
                    painter.Light(x, y, colors[4], intensity * 0.3);
                }
            }
            foreach (int i in cores)
            {
                var r = rays[i];
                double shimmer = 0.75 + 0.25 * Sin(time * r.Frequency + r.Phase);
                for (int y = (int)Top; y < Min(front, 0); y++)
                {
                    double pulse = 0.5 + 0.5 * Sin(y * 0.13 - time * 11 + r.Phase);
                    painter.Dot(r.Offset * HalfWidth(y), y, pulse > 0.8 ? White : colors[6],
                        k * shimmer * (0.15 + 0.55 * pulse) * Smooth(front, front - 10, y));
                }
            }

            if (time < Pour + 0.05 && front > Top + 14)
            {
                double y = Min(front, 0);
                painter.Glow(0, y, 14, colors[5], 0.7 * k);
                for (int d = -6; d <= 0; d++)
                    painter.Dot(0, y + d, d > -3 ? White : colors[6], 1 + d / 7.0);
            }
            if (!full) return;
            foreach (var m in motes)
            {
                double y = Top + ((m.Y * -Top + time * m.Speed) % -Top + -Top) % -Top;
                if (y > front) continue;
                double w = HalfWidth(y), x = m.X * w + Sin(time * m.Frequency + m.Phase) * m.Amplitude;
                double intensity = ProfileAt(x / w) * k;
                if (intensity < 0.25) continue;
                painter.Dot(x, y, intensity > 0.75 ? White : colors[intensity > 0.5 ? 6 : 5], Min(1, intensity * 1.4));
                painter.Light(x, y, colors[5], intensity * 0.5);
            }
        }

        private double ProfileAt(double q)
        {
            int i = Round(q * 40) + 60;
            return (uint)i < profile.Length ? profile[i] : 0;
        }

        private void DrawPool(bool back)
        {
            double k = ShaftStrength() * Smooth(Pour - 0.05, Pour + 0.1, time), w = HalfWidth(0) * 1.25;
            if (k <= 0) return;
            if (back)
            {
                painter.Glow(0, -2, w * 1.6, colors[4], 0.3 * k);
                return;
            }
            for (int y = 0; y <= 3; y++)
            for (int x = (int)Floor(-w); x <= w; x++)
            {
                double u = Sqrt(Pow(x / w, 2) + Pow(y / 4.0, 2));
                if (u >= 1) continue;
                double a = k * (y == 0 ? Sqrt(1 - u) * 1.2 : (1 - u) * 0.9);
                painter.Dot(x, y, u < 0.3 && y == 0 ? White : colors[u < 0.6 ? 5 : u < 0.85 ? 4 : 3], a);
            }
        }

        private void DrawGroundRing(bool back)
        {
            if (time <= Pour || time >= Pour + 0.4) return;
            double phase = Seg(time, Pour, Pour + 0.4), radius = 4 + 32 * OutCubic(phase), width = 2.5 * (1 - phase) + 0.5;
            int count = (int)Ceiling(radius * Tau * 1.2);
            for (int i = 0; i < count; i++)
            {
                double angle = i / (double)count * Tau, sine = Sin(angle);
                if ((sine < 0) != back) continue;
                double cosine = Cos(angle);
                for (double offset = width; offset >= 0; offset -= 0.5)
                {
                    double u = offset / width;
                    uint ink = u < 0.15 && time < Pour + 0.06 ? White : colors[u < 0.15 ? 7 : u < 0.4 ? 6 : u < 0.7 ? 5 : 4];
                    painter.Dot(cosine * (radius - offset), sine * (radius - offset) * 0.18, ink,
                        (1.1 - phase) * (u < 0.4 ? 1.2 : 1.15 - u));
                }
            }
        }

        private void DrawBody()
        {
            if (time >= ClimbEnd + 0.6) return;
            double line = ClimbY(), u = time - ClimbEnd;
            if (hasMask)
            {
                for (int y = -OriginY; y < Height - OriginY; y++)
                for (int x = -OriginX; x < Width - OriginX; x++)
                {
                    var c = SampleActor(x, y);
                    if (c.a == 0) continue;
                    double d = y - line;
                    uint ink = Rgb(c.r, c.g, c.b);
                    if (u >= 0)
                    {
                        if (u < 0.45) painter.Dot(x, y, u < 0.06 ? White : Mix(ink, colors[5], 0.55 * Pow(1 - u / 0.45, 2)));
                    }
                    else if (d < -0.5)
                    {
                        double luma = (0.299 * c.r + 0.587 * c.g + 0.114 * c.b) / 255;
                        painter.Dot(x, y, Ash[Min(5, (int)Floor((luma * 1.1) * 6))]);
                    }
                    else if (d < 1.5) painter.Dot(x, y, d < 0.5 ? White : colors[6]);
                    else
                    {
                        double warmth = Exp(-(d - 1.5) / 4) * 0.7;
                        if (warmth > 0.08) painter.Dot(x, y, Mix(ink, colors[5], warmth));
                    }
                }
                if (u < 0.25)
                    for (int y = -OriginY; y < Height - OriginY; y++)
                    {
                        double d = y - line;
                        if (u < 0 && (d < -0.5 || d > 12)) continue;
                        for (int x = -OriginX; x < Width - OriginX; x++)
                        {
                            if (SampleActor(x, y).a != 0 || !IsOutline(x, y)) continue;
                            if (u < 0) painter.Dot(x, y, d < 1 ? White : colors[d < 5 ? 5 : 4], d < 1 ? 1 : 1.1 - d / 12);
                            else painter.Dot(x, y, u < 0.08 ? White : colors[5], 1 - u / 0.25);
                        }
                    }
            }
            if (time > ClimbStart && u < 0)
            {
                painter.Glow(0, line, 26, colors[4], 0.5);
                painter.Glow(0, line, 8, White, 0.3);
                painter.Glow(0, -18, 36, colors[5], 0.16 * Seg(time, ClimbStart, ClimbEnd));
            }
        }

        private void DrawWingAccent()
        {
            if (time > BeatEnd - 0.03 && time < BeatEnd + 0.45)
            {
                double u = Seg(time, BeatEnd - 0.03, BeatEnd + 0.45), x = 10 + 70 * OutCubic(u);
                for (int side = -1; side <= 1; side += 2)
                for (int d = 0; d < 12; d++)
                    painter.Dot(side * (x - d), 0, d < 2 ? White : colors[d < 5 ? 6 : 4], (1 - u) * (1 - d / 12.0) * 1.3);
            }
            double light = Smooth(WingStart, WingStart + 0.2, time) * (1 - Smooth(Scatter, Scatter + 0.3, time));
            if (light <= 0) return;
            if (hasMask)
                for (int y = -OriginY; y < -8; y++)
                for (int x = -OriginX; x < Width - OriginX; x++)
                    if (SampleActor(x, y).a == 0 && IsOutline(x, y)) painter.Light(x, y, colors[5], 0.55 * light);
            painter.Glow(0, -34, 76, colors[4], 0.14 * light);
        }

        private static double AngleDistance(double a, double b) => Abs(((a - b + PI) % Tau + Tau) % Tau - PI);

        private void DrawHalo()
        {
            double form = OutCubic(Seg(time, HaloStart, HaloEnd)), k = 1 - Smooth(3.35, 3.9, time);
            if (form <= 0 || k <= 0) return;
            bool flash = time >= HaloEnd && time < HaloEnd + 0.06;
            double glint = (time - HaloEnd) * 4.2 + PI / 2;
            for (int j = 0; j < 96; j++)
            {
                double angle = j / 96.0 * Tau, fromFront = AngleDistance(angle, PI / 2);
                if (fromFront > form * PI + 0.02) continue;
                double sine = Sin(angle), x = Cos(angle) * HaloRx * (0.9 + 0.1 * form), y = HaloY + sine * HaloRy;
                bool hot = flash || AngleDistance(angle, glint) < 0.22 || fromFront > form * PI - 0.3;
                if (sine >= 0)
                {
                    painter.Dot(x, y, hot ? White : colors[6], k);
                    painter.Dot(x, y + 1, colors[hot ? 6 : 4], k);
                }
                else painter.Dot(x, y, colors[hot ? 6 : 5], k * 0.9);
            }
            painter.Glow(0, HaloY, 20, colors[4], 0.4 * k * form);
            painter.Glow(0, HaloY + 1, 9, White, 0.2 * k * form);
            double g = Sin(glint);
            if (time > HaloEnd && g > 0.2) Sparkle(Cos(glint) * HaloRx, HaloY + g * HaloRy + 0.5, 2 + 2 * g, colors[6], colors[4], 0.35 * k);
            if (time >= HaloEnd && time < HaloEnd + 0.3) Sparkle(0, HaloY + HaloRy + 0.5, 7 * (1 - (time - HaloEnd) / 0.3), White, colors[5], 0.6);
        }

        private void DrawFlare(double length, double k)
        {
            if (length < 1) return;
            for (int i = 0; i < 8; i++)
            {
                double angle = i * PI / 4, len = length * (i % 4 == 0 ? 1 : i % 2 == 0 ? 0.6 : 0.3);
                for (double d = 0; d <= len; d += 0.5)
                {
                    double u = d / len;
                    painter.Dot(Cos(angle) * d, actorTop - 1 + Sin(angle) * d, u < 0.35 ? White : colors[u < 0.7 ? 6 : 5],
                        u < 0.6 ? 1 : k * 1.5 * (1 - u) + 0.2);
                }
            }
        }
    }
}
