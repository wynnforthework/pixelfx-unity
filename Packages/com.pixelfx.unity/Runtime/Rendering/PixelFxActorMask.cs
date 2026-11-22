using System;
using UnityEngine;

namespace PixelFx
{
    // Reads a Simple SpriteRenderer's artwork on the sprite's native pixel grid.
    internal sealed class PixelFxActorMask
    {
        private Sprite sprite;
        private Texture2D texture;
        private Color32[] pixels;
        private int width, height;
        private bool flipX, flipY;

        internal bool Update(SpriteRenderer actor) =>
            actor != null && actor.drawMode == SpriteDrawMode.Simple
                ? Update(actor.sprite, actor.flipX, actor.flipY)
                : Update(null, false, false);

        internal bool Update(Sprite next, bool horizontalFlip, bool verticalFlip)
        {
            flipX = horizontalFlip;
            flipY = verticalFlip;
            var nextTexture = next != null ? next.texture : null;
            if (nextTexture == null || !nextTexture.isReadable || next.associatedAlphaSplitTexture != null)
            {
                sprite = null;
                texture = null;
                pixels = null;
                return false;
            }
            if (next == sprite && nextTexture == texture) return true;

            sprite = next;
            texture = nextTexture;
            pixels = null;
            width = Mathf.RoundToInt(sprite.rect.width);
            height = Mathf.RoundToInt(sprite.rect.height);
            pixels = ReadSpritePixels();
            return true;
        }

        private Color32[] ReadSpritePixels()
        {
            var result = new Color32[width * height];
            var vertices = sprite.vertices;
            var uv = sprite.uv;
            var triangles = sprite.triangles;
            Vector2 pivot = sprite.pivot;
            float ppu = sprite.pixelsPerUnit;

            // Mapping through the geometry and UVs handles atlas trimming and rotation.
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector2 a = vertices[ia] * ppu + pivot;
                Vector2 b = vertices[ib] * ppu + pivot;
                Vector2 c = vertices[ic] * ppu + pivot;
                Vector2 ab = b - a, ac = c - a;
                float area = Cross(ab, ac);
                if (area == 0) continue;

                int left = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                int right = Mathf.Min(width, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                int bottom = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                int top = Mathf.Min(height, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                {
                    var offset = new Vector2(x + 0.5f, y + 0.5f) - a;
                    float u = Cross(offset, ac) / area, v = Cross(ab, offset) / area;
                    if (u < -1e-5f || v < -1e-5f || u + v > 1.00001f) continue;
                    Vector2 point = uv[ia] + (uv[ib] - uv[ia]) * u + (uv[ic] - uv[ia]) * v;
                    int tx = Mathf.Clamp(Mathf.FloorToInt(point.x * texture.width), 0, texture.width - 1);
                    int ty = Mathf.Clamp(Mathf.FloorToInt(point.y * texture.height), 0, texture.height - 1);
                    result[y * width + x] = texture.GetPixel(tx, ty);
                }
            }
            return result;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // The supplied point is the renderer's pivot in painter coordinates, where Y points down.
        internal void DrawFill<T>(PixelPainter painter, double pivotX, double pivotY, double pixelsPerUnit,
            T state, Func<Color32, T, uint> recolor, double opacity = 1)
        {
            if (pixels == null || pixelsPerUnit <= 0 || opacity <= 0) return;
            var grid = GridAt(pivotX, pivotY, pixelsPerUnit);
            painter.Shade(grid.X, grid.Y, grid.X + width * grid.Step, grid.Y + height * grid.Step,
                (Mask: this, Grid: grid, State: state, Recolor: recolor, Opacity: opacity), static (x, y, s) =>
                {
                    var source = s.Mask.Sample(x, y, s.Grid);
                    return source.a == 0 ? default : (s.Recolor(source, s.State), s.Opacity);
                });
        }

        internal void DrawOutline(PixelPainter painter, double pivotX, double pivotY, double pixelsPerUnit,
            uint ink, double opacity = 1)
        {
            if (pixels == null || pixelsPerUnit <= 0 || opacity <= 0) return;
            var grid = GridAt(pivotX, pivotY, pixelsPerUnit);
            double step = grid.Step;
            painter.Shade(grid.X - step, grid.Y - step, grid.X + (width + 1) * step, grid.Y + (height + 1) * step,
                (Mask: this, Grid: grid, Ink: ink, Opacity: opacity), static (x, y, s) =>
                {
                    var (column, row) = CellAt(x, y, s.Grid);
                    if (s.Mask.SourcePixel(column, row).a != 0) return default;
                    bool adjacent = s.Mask.SourcePixel(column - 1, row).a != 0 || s.Mask.SourcePixel(column + 1, row).a != 0
                        || s.Mask.SourcePixel(column, row - 1).a != 0 || s.Mask.SourcePixel(column, row + 1).a != 0;
                    return adjacent ? (s.Ink, s.Opacity) : default;
                });
        }

        private (double X, double Y, double Step) GridAt(double pivotX, double pivotY, double pixelsPerUnit)
        {
            double step = pixelsPerUnit / sprite.pixelsPerUnit;
            Vector2 pivot = sprite.pivot;
            return (pivotX + (flipX ? pivot.x - width : -pivot.x) * step,
                pivotY + (flipY ? -pivot.y : pivot.y - height) * step, step);
        }

        private static (int Column, int Row) CellAt(double x, double y, (double X, double Y, double Step) grid) =>
            ((int)Math.Floor((x - grid.X) / grid.Step), (int)Math.Floor((y - grid.Y) / grid.Step));

        private Color32 Sample(double x, double y, (double X, double Y, double Step) grid)
        {
            var (column, row) = CellAt(x, y, grid);
            return SourcePixel(column, row);
        }

        private Color32 SourcePixel(int column, int row)
        {
            if ((uint)column >= (uint)width || (uint)row >= (uint)height) return default;
            int x = flipX ? width - 1 - column : column;
            int y = flipY ? row : height - 1 - row;
            return pixels[y * width + x];
        }
    }
}
