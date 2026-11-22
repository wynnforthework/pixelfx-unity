using System;
using UnityEngine;
using static System.Math;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Port of Patch PixelFX impacts/hit-flash at 5294518. See the package license.
    internal sealed class HitFlashEffect
    {
        internal const int Width = 80, Height = 64, OriginX = 40, OriginY = 54;
        internal const float Duration = 0.5f;
        private const double PreviewChestY = -26;
        private const int PixelsPerUnit = 32;
        private readonly PixelPainter painter = new PixelPainter(Width, Height, OriginX, OriginY);
        private readonly PixelFxTimeline timeline;
        private readonly PixelFxActorMask previewMask;
        private readonly uint[] colors;
        private readonly (double Angle, double Distance, double Length, double Delay)[] lines;

        private HitFlashEffect(PixelFxTimeline timeline)
        {
            this.timeline = timeline;
            painter.LightStrength = timeline.Settings.glow;
            colors = Array.ConvertAll(timeline.Settings.palette, c => Hex(PixelFxLibrary.Pack(c)));
            if (timeline.PreviewActor && timeline.ActorMask == null) previewMask = HitFlashPreviewActor.CreateMask();
            int count = timeline.Quality == PixelFxQuality.Full ? 7 : 3;
            lines = new (double, double, double, double)[count];
            var random = new PixelFxRandom(timeline.Settings.seed);
            for (int i = 0; i < count; i++)
            {
                double angle = -PI - 0.3 + (i + random.Range(0.25, 0.75)) / count * (PI + 0.6);
                lines[i] = (angle, random.Range(15, 19), random.Range(6, 9), random.Range(0, 0.04));
            }
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var effect = new HitFlashEffect(timeline);
            return (pixels, _, time, _) => effect.Draw(pixels, time);
        }

        private static (Vector3 Foot, float Scale, float Height) ActorLayout(SpriteRenderer actor)
        {
            var sprite = actor.sprite;
            if (sprite == null) return (Vector3.zero, 1, 38f / PixelsPerUnit);
            // Use the source rectangle so the overlay pivots with the animation artwork.
            var size = sprite.rect.size / sprite.pixelsPerUnit;
            var pivot = sprite.pivot / sprite.pixelsPerUnit;
            float centerX = size.x / 2 - pivot.x;
            float bottomY = actor.flipY ? pivot.y - size.y : -pivot.y;
            float scale = Mathf.Max(1, Mathf.Max(size.x * PixelsPerUnit / 36, size.y * PixelsPerUnit / 38));
            return (new Vector3(actor.flipX ? -centerX : centerX, bottomY), scale, size.y);
        }

        internal static void PlaceOverlay(SpriteRenderer overlay, SpriteRenderer actor, float scale)
        {
            if (actor == null)
            {
                overlay.transform.localScale = Vector3.one * scale;
                return;
            }
            var layout = ActorLayout(actor);
            var actorTransform = actor.transform;
            overlay.transform.SetPositionAndRotation(actorTransform.TransformPoint(layout.Foot), actorTransform.rotation);
            overlay.transform.localScale = actorTransform.lossyScale * layout.Scale;
        }

        private void Draw(byte[] pixels, double time)
        {
            painter.Clear();
            var binding = timeline.BindingFor(PixelFxStage.Single, 0);
            var actor = binding.Actor;
            double px = 0, py = 0, ppu = PixelsPerUnit, cx = 0, cy = PreviewChestY;
            if (actor != null)
            {
                var layout = ActorLayout(actor);
                ppu /= layout.Scale;
                px = -layout.Foot.x * ppu;
                py = layout.Foot.y * ppu;
                cy = -layout.Height * (26.0 / 38) * ppu;
                if (binding.Anchor != null)
                {
                    var chest = actor.transform.InverseTransformPoint(binding.Anchor.position) - layout.Foot;
                    cx = chest.x * ppu;
                    cy = -chest.y * ppu;
                }
            }

            if (previewMask != null)
                previewMask.DrawFill(painter, 0, 0, PixelsPerUnit, 0, static (c, _) => Rgb(c.r, c.g, c.b));
            var mask = timeline.ActorMask ?? previewMask;
            DrawBody(mask, px, py, ppu, time);

            double hx = cx + timeline.HitSide * 12, hy = cy - 5;
            DrawBurst(hx, hy, time);
            DrawLines(cx, cy, time);
            double flash = Exp(-time / 0.08);
            if (flash > 0.02) painter.Glow(cx, cy, 26, time < 0.058 ? White : colors[5], 0.35 * flash);
            DrawLight(hx, hy, 34, White, 0.6, 0.1, time);
            DrawLight(cx, cy, 30, colors[4], 0.5, 0.35, time);
            painter.FadeLightEdges();
            painter.Composite(pixels, true, false);
        }

        private void DrawBody(PixelFxActorMask mask, double x, double y, double ppu, double time)
        {
            if (mask == null) return;
            if (time < 0.058)
            {
                mask.DrawFill(painter, x, y, ppu, 0, static (_, _) => White);
                mask.DrawOutline(painter, x, y, ppu, colors[time < 0.025 ? 6 : 5]);
            }
            else if (time < 0.125)
            {
                mask.DrawFill(painter, x, y, ppu, colors, static (c, ramp) =>
                {
                    double luma = (0.299 * c.r + 0.587 * c.g + 0.114 * c.b) / 255;
                    return ramp[Min(6, (int)Floor(2.5 + luma * 5))];
                });
                mask.DrawOutline(painter, x, y, ppu, colors[5]);
            }
            else if (time < 0.158)
                mask.DrawOutline(painter, x, y, ppu, White);
            else if (time < 0.36)
            {
                double cooling = 1 - (time - 0.158) / (0.36 - 0.158);
                mask.DrawFill(painter, x, y, ppu, (Red: colors[4], Cooling: cooling), static (c, state) =>
                {
                    uint source = Rgb(c.r, c.g, c.b);
                    return Mix(source, Mix(source, state.Red, 0.55), state.Cooling);
                });
                if (cooling > 0.5)
                    mask.DrawOutline(painter, x, y, ppu, colors[4], (cooling - 0.5) * 2);
            }
        }

        private void DrawBurst(double x, double y, double time)
        {
            if (time >= 0.07) return;
            bool contact = time < 0.035;
            double inner = contact ? 0 : 0.45;
            for (int i = 0; i < 8; i++)
            {
                double angle = i / 8.0 * (2 * PI) + 0.2;
                double length = (i % 2 == 0 ? 9 : 4) * (contact ? 1 : 1.3);
                double dx = Cos(angle) * length, dy = Sin(angle) * length;
                painter.Line(x + dx * inner, y + dy * inner, x + dx, y + dy,
                    (Contact: contact, Rim: colors[6], Tip: colors[4]), static (u, state) =>
                        (u < 0.45 && state.Contact ? White : u < 0.75 ? state.Rim : state.Tip, 1));
            }
            if (contact)
            {
                painter.Line(x - 1, y, x + 1, y, White);
                painter.Line(x, y - 1, x, y + 1, White);
            }
            painter.Glow(x, y, 14, colors[6], 0.5 * (1 - time / 0.07));
        }

        private void DrawLines(double x, double y, double time)
        {
            foreach (var line in lines)
            {
                double age = (time - line.Delay) / 0.16;
                if (age < 0 || age >= 1) continue;
                double length = line.Length * (1 - age);
                if (length < 1) continue;
                double distance = line.Distance - 6 * (1 - Pow(1 - age, 3));
                double dx = Cos(line.Angle), dy = Sin(line.Angle) * 1.15;
                double halfWidth = (2.8 - 1.6 * age) / 2;
                double nx = -dy * halfWidth, ny = dx * halfWidth;
                double tx = x + dx * distance, ty = y + dy * distance;
                double ex = x + dx * (distance + length), ey = y + dy * (distance + length);
                painter.Triangle((tx - dx, ty - dy), (ex + nx * 1.5 + dx, ey + ny * 1.5 + dy),
                    (ex - nx * 1.5 + dx, ey - ny * 1.5 + dy), colors[3]);
                painter.Triangle((tx, ty), (ex + nx, ey + ny), (ex - nx, ey - ny), age < 0.5 ? White : colors[6]);
            }
        }

        private void DrawLight(double x, double y, double radius, uint ink, double strength, double lifetime, double time)
        {
            if (time >= lifetime) return;
            double remaining = (lifetime - time) / lifetime;
            painter.Glow(x, y, radius * (0.55 + 0.45 * remaining), ink, strength * remaining * remaining);
        }
    }
}
