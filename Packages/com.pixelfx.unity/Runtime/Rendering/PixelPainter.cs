using System;
using static System.Math;

namespace PixelFx
{
    // Coordinates use pixel units with Y down, as in the upstream renderer.
    internal sealed class PixelPainter
    {
        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        private readonly uint[] color, light;
        private readonly int width, height, originX, originY;

        internal PixelPainter(int width, int height, int originX, int originY)
        {
            this.width = width;
            this.height = height;
            this.originX = originX;
            this.originY = originY;
            color = new uint[width * height];
            light = new uint[color.Length];
        }

        internal double LightStrength { get; set; } = 1;
        internal void Clear()
        {
            Array.Clear(color, 0, color.Length);
            Array.Clear(light, 0, light.Length);
        }

        internal void Dot(double x, double y, uint ink, double alpha = 1)
        {
            Put((int)Floor(x) + originX, (int)Floor(y) + originY, ink, alpha);
        }

        internal void Shade(double left, double top, double right, double bottom,
            Func<double, double, (uint Ink, double Alpha)> sample)
        {
            Shade(left, top, right, bottom, sample, static (x, y, shader) => shader(x, y));
        }

        internal void Shade<T>(double left, double top, double right, double bottom, T state,
            Func<double, double, T, (uint Ink, double Alpha)> sample)
        {
            int x0 = Max(0, (int)Ceiling(left + originX - 0.5));
            int x1 = Min(width - 1, (int)Floor(right + originX - 0.5));
            int y0 = Max(0, (int)Ceiling(top + originY - 0.5));
            int y1 = Min(height - 1, (int)Floor(bottom + originY - 0.5));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var pixel = sample(x - originX + 0.5, y - originY + 0.5, state);
                if (pixel.Ink != 0) Put(x, y, pixel.Ink, pixel.Alpha);
            }
        }

        private void Put(int x, int y, uint ink, double alpha)
        {
            if ((uint)x >= width || (uint)y >= height) return;
            if (alpha <= (Bayer[((y & 3) << 2) | (x & 3)] + 0.5) / 16) return;
            color[y * width + x] = ink;
        }

        internal void Line(double x0, double y0, double x1, double y1, uint ink, double alpha = 1) =>
            Line(x0, y0, x1, y1, (ink, alpha), static (_, style) => style);

        internal void Line<T>(double x0, double y0, double x1, double y1, T state,
            Func<double, T, (uint Ink, double Alpha)> sample)
        {
            int x = (int)Floor(x0) + originX, y = (int)Floor(y0) + originY;
            int endX = (int)Floor(x1) + originX, endY = (int)Floor(y1) + originY;
            int dx = Abs(endX - x), dy = -Abs(endY - y);
            int stepX = x < endX ? 1 : -1, stepY = y < endY ? 1 : -1;
            int error = dx + dy, length = Max(1, Max(dx, -dy)), index = 0;
            while (true)
            {
                var pixel = sample(index++ / (double)length, state);
                Put(x, y, pixel.Ink, pixel.Alpha);
                if (x == endX && y == endY) return;
                int twice = error * 2;
                if (twice >= dy) { error += dy; x += stepX; }
                if (twice <= dx) { error += dx; y += stepY; }
            }
        }

        internal void Circle(double x, double y, double radius, uint ink, double alpha = 1)
        {
            if (radius < 0.6) { Dot(x, y, ink, alpha); return; }
            Disc(x, y, radius, ink, alpha, false);
        }

        internal void Triangle((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, uint ink)
        {
            Shade(Min(a.X, Min(b.X, c.X)), Min(a.Y, Min(b.Y, c.Y)),
                Max(a.X, Max(b.X, c.X)), Max(a.Y, Max(b.Y, c.Y)), (A: a, B: b, C: c, Ink: ink),
                static (x, y, triangle) =>
                {
                    bool inside = Crosses(x, y, triangle.A, triangle.C) ^ Crosses(x, y, triangle.B, triangle.A)
                        ^ Crosses(x, y, triangle.C, triangle.B);
                    return (inside ? triangle.Ink : 0, 1);
                });
        }

        private static bool Crosses(double x, double y, (double X, double Y) a, (double X, double Y) b) =>
            (a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X;

        // Soft dust uses straight-alpha blending and a quadratic radial fade.
        internal void SoftDisc(double x, double y, double radius, uint ink, double alpha, double falloff)
        {
            if (alpha <= 0.004) return;
            x += originX;
            y += originY;
            if (radius < 0.5)
            {
                Blend((int)Floor(x), (int)Floor(y), ink, alpha);
                return;
            }
            Bounds(x, y, radius, out int left, out int right, out int top, out int bottom);
            double radiusSquared = radius * radius;
            for (int row = top; row <= bottom; row++)
            for (int col = left; col <= right; col++)
            {
                double dx = col + 0.5 - x, dy = row + 0.5 - y;
                double distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > radiusSquared) continue;
                double distance = Sqrt(distanceSquared) / radius;
                Blend(col, row, ink, alpha * (1 - distance * distance * falloff));
            }
        }

        private void Blend(int x, int y, uint ink, double alpha)
        {
            if (alpha <= 0.004 || (uint)x >= width || (uint)y >= height) return;
            int index = y * width + x;
            uint under = color[index];
            double outAlpha = alpha + (under >> 24) / 255.0 * (1 - alpha);
            double over = alpha / outAlpha, below = 1 - over;
            color[index] = (Rgb(Channel(ink, 0) * over + Channel(under, 0) * below,
                Channel(ink, 8) * over + Channel(under, 8) * below,
                Channel(ink, 16) * over + Channel(under, 16) * below) & 0xffffff) | ((uint)(outAlpha * 255) << 24);
        }

        internal void Disc(double x, double y, double radius, uint ink, double alpha, bool blend = true)
        {
            if (alpha <= 0.004) return;
            x += originX;
            y += originY;
            Bounds(x, y, radius, out int left, out int right, out int top, out int bottom);
            double radiusSquared = radius * radius;
            for (int row = top; row <= bottom; row++)
            for (int col = left; col <= right; col++)
            {
                double dx = col + 0.5 - x, dy = row + 0.5 - y;
                if (dx * dx + dy * dy > radiusSquared) continue;
                int index = row * width + col;
                uint under = color[index];
                double oldAlpha = (under >> 24) / 255.0;
                double outAlpha = alpha + oldAlpha * (1 - alpha);
                if (blend)
                    color[index] = (Mix(under, ink, alpha / outAlpha) & 0xffffff) | ((uint)(outAlpha * 255) << 24);
                else Put(col, row, ink, alpha);
            }
        }

        internal void Glow(double x, double y, double radius, uint ink, double strength)
        {
            if (radius < 1 || strength <= 0.004) return;
            x += originX;
            y += originY;
            Bounds(x, y, radius, out int left, out int right, out int top, out int bottom);
            int r = Channel(ink, 0), g = Channel(ink, 8), b = Channel(ink, 16);
            for (int row = top; row <= bottom; row++)
            {
                double dy = (row + 0.5 - y) / radius;
                for (int col = left; col <= right; col++)
                {
                    double dx = (col + 0.5 - x) / radius, distance = dx * dx + dy * dy;
                    if (distance >= 1) continue;
                    double falloff = 1 - Sqrt(distance);
                    AddLight(row * width + col, r, g, b, strength * falloff * falloff * LightStrength);
                }
            }
        }

        internal void GlowLine(double x0, double y0, double x1, double y1, double radius, uint ink, double strength)
        {
            if (radius < 1 || strength <= 0.004) return;
            x0 += originX;
            x1 += originX;
            y0 += originY;
            y1 += originY;
            double vx = x1 - x0, vy = y1 - y0;
            double lengthSquared = Max(1e-9, vx * vx + vy * vy);
            int left = Max(0, (int)Floor(Min(x0, x1) - radius));
            int right = Min(width - 1, (int)Ceiling(Max(x0, x1) + radius));
            int top = Max(0, (int)Floor(Min(y0, y1) - radius));
            int bottom = Min(height - 1, (int)Ceiling(Max(y0, y1) + radius));
            int r = Channel(ink, 0), g = Channel(ink, 8), b = Channel(ink, 16);
            for (int row = top; row <= bottom; row++)
            for (int col = left; col <= right; col++)
            {
                double x = col + 0.5 - x0, y = row + 0.5 - y0;
                double u = Min(1, Max(0, (x * vx + y * vy) / lengthSquared));
                double dx = x - u * vx, dy = y - u * vy;
                double distance = Sqrt(dx * dx + dy * dy) / radius;
                if (distance >= 1) continue;
                double falloff = 1 - distance;
                AddLight(row * width + col, r, g, b, strength * falloff * falloff * LightStrength);
            }
        }

        internal void GlowRing(double x, double y, double radius, double width, uint ink, double strength)
        {
            if (width < 1 || strength <= 0.004) return;
            x += originX;
            y += originY;
            Bounds(x, y, radius + width, out int left, out int right, out int top, out int bottom);
            int r = Channel(ink, 0), g = Channel(ink, 8), b = Channel(ink, 16);
            for (int row = top; row <= bottom; row++)
            for (int col = left; col <= right; col++)
            {
                double dx = col + 0.5 - x, dy = row + 0.5 - y;
                double falloff = 1 - Abs(Sqrt(dx * dx + dy * dy) - radius) / width;
                if (falloff > 0)
                    AddLight(row * this.width + col, r, g, b, strength * falloff * falloff * LightStrength);
            }
        }

        private void AddLight(int index, int r, int g, int b, double strength)
        {
            if (strength <= 0.004) return;
            uint under = light[index];
            light[index] = Rgb(Min(255, Channel(under, 0) + r * strength),
                Min(255, Channel(under, 8) + g * strength), Min(255, Channel(under, 16) + b * strength)) & 0xffffff;
        }

        private void Bounds(double x, double y, double radius, out int left, out int right, out int top, out int bottom)
        {
            left = Max(0, (int)Floor(x - radius));
            right = Min(width - 1, (int)Ceiling(x + radius));
            top = Max(0, (int)Floor(y - radius));
            bottom = Min(height - 1, (int)Ceiling(y + radius));
        }

        internal void FadeLightEdges()
        {
            const int margin = 8;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int edge = Min(Min(x, width - 1 - x), Min(y, height - 1 - y));
                int index = y * width + x;
                uint ink = light[index];
                if (edge >= margin || ink == 0) continue;
                double u = (edge + 0.5) / margin;
                double k = u * u * (3 - 2 * u);
                light[index] = Rgb(Floor(Channel(ink, 0) * k + 0.5), Floor(Channel(ink, 8) * k + 0.5),
                    Floor(Channel(ink, 16) * k + 0.5)) & 0xffffff;
            }
        }

        // Unity uploads bottom-up RGBA32. The standalone renderer can also emit top-down RGBA.
        internal void Composite(byte[] pixels, bool bottomUp, bool fadeEdges)
        {
            for (int y = 0; y < height; y++)
            {
                int output = (bottomUp ? height - y - 1 : y) * width * 4;
                for (int x = 0; x < width; x++, output += 4)
                {
                    int i = y * width + x;
                    uint ink = color[i], glow = light[i];
                    double a = ink >> 24;
                    double r = Channel(ink, 0), g = Channel(ink, 8), b = Channel(ink, 16);
                    if (glow != 0)
                    {
                        int lr = Channel(glow, 0), lg = Channel(glow, 8), lb = Channel(glow, 16);
                        double nextAlpha = Max(a, Max(lr, Max(lg, lb)));
                        double premultiply = a / 255;
                        double normalize = 255 / nextAlpha;
                        r = (r * premultiply + lr) * normalize;
                        g = (g * premultiply + lg) * normalize;
                        b = (b * premultiply + lb) * normalize;
                        a = nextAlpha;
                    }
                    if (fadeEdges)
                    {
                        int edge = Min(Min(x, width - 1 - x), Min(y, height - 1 - y));
                        a *= Min(1, edge / 4.0);
                    }
                    pixels[output] = (byte)Min(255, r);
                    pixels[output + 1] = (byte)Min(255, g);
                    pixels[output + 2] = (byte)Min(255, b);
                    pixels[output + 3] = (byte)a;
                }
            }
        }

        internal const uint White = 0xffffffff;
        internal static int Channel(uint c, int shift) => (int)((c >> shift) & 255);
        internal static uint Rgb(double r, double g, double b) =>
            0xff000000u | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
        internal static uint Hex(uint rgb) => Rgb(rgb >> 16, (rgb >> 8) & 255, rgb & 255);
        internal static uint Mix(uint from, uint to, double t) => Rgb(
            Channel(from, 0) + (Channel(to, 0) - Channel(from, 0)) * t,
            Channel(from, 8) + (Channel(to, 8) - Channel(from, 8)) * t,
            Channel(from, 16) + (Channel(to, 16) - Channel(from, 16)) * t);
        internal static uint Scale(uint c, double k) => Rgb(Min(255, Channel(c, 0) * k),
            Min(255, Channel(c, 8) * k), Min(255, Channel(c, 16) * k));
    }
}
