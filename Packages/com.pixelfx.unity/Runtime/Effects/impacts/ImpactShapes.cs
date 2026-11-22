using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Rasterizers shared by the impacts derived from the upstream impacts/_lib.js.
    internal static class ImpactShapes
    {
        internal static void DrawStar(PixelPainter painter, (double Angle, double Length, double Width)[] rays,
            double y, double scale, double thickness, uint rim, uint body, uint inner, uint core, double heart)
        {
            // Draw the rim of every ray before the body or core so intersecting rays stay bright.
            for (int pass = 0; pass < 3; pass++)
            foreach (var ray in rays)
            {
                double cos = Cos(ray.Angle), sin = Sin(ray.Angle), length = ray.Length * scale;
                double end = pass == 2 ? length * 0.55 : length;
                for (double d = 0; d <= end; d += 0.5)
                {
                    double width = ray.Width * thickness * (1 - d / length) * Min(1, 0.5 + d / 3);
                    double halfWidth = pass == 0 ? width + 0.8 : pass == 1 ? width : width * 0.45;
                    uint color = pass == 0 ? rim : pass == 2 ? core : d < length * 0.35 ? inner : body;
                    for (double offset = -halfWidth; offset <= halfWidth; offset += 0.5)
                        painter.Dot(cos * d - sin * offset, y + sin * d + cos * offset, color);
                }
            }
            double radius = heart * scale * Sqrt(thickness);
            painter.Circle(0, y, radius + 0.8, inner);
            painter.Circle(0, y, radius, core);
        }

        internal static void DrawBrokenRing(PixelPainter painter, (double Angle, double Gap, double Drift, double Width)[] arcs,
            double centerY, double radius, double thickness, double breakup, uint outer, uint inner, double alpha)
        {
            foreach (var arc in arcs)
            {
                double span = (2 * PI) / arcs.Length * (1 - arc.Gap * breakup) + 0.02;
                double arcRadius = radius * (1 + arc.Drift * breakup);
                int samples = Max(4, (int)Ceiling(arcRadius * span * 1.8));
                for (int i = 0; i <= samples; i++)
                {
                    double u = i / (double)samples, angle = arc.Angle + (u - 0.5) * span;
                    double halfWidth = Max(0.5, thickness * arc.Width * (1 - breakup * Pow(Abs(2 * u - 1), 1.6))) / 2;
                    double cos = Cos(angle), sin = Sin(angle);
                    for (double offset = -halfWidth; offset <= halfWidth + 0.01; offset += 0.5)
                    {
                        double y = centerY + sin * (arcRadius + offset);
                        if (y < 0) painter.Dot(cos * (arcRadius + offset), y,
                            offset > -0.26 ? outer : inner, alpha);
                    }
                }
            }
        }
    }
}
