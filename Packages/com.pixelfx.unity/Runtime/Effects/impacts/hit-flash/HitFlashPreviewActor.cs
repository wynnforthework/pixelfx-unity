using UnityEngine;
using static PixelFx.PixelPainter;

namespace PixelFx
{
    // Upstream stockActor(2) from Patch PixelFX src/core/actor.js at 5294518.
    internal static class HitFlashPreviewActor
    {
        private static readonly string[] Rows =
        {
            "...........kkk..........",
            "..........kAABk.........",
            ".........kAAABJk........",
            ".........kABBJJk........",
            ".........kBBBJCDk.......",
            ".........kBCJDDDk.......",
            ".........kkJJJDk........",
            ".......kkkkABBJCkk......",
            "......kAABCJBCCCkDk.....",
            "......kABBCJCCCmDkDk....",
            "......kBBCDDJCDDDkDk....",
            "......kABCDJDDDDDkk.....",
            ".....kAABDkDDDDJkk......",
            ".....kABCDkDDJJGkk......",
            ".....kABCkJJJGGGkk......",
            "....kJJCDkFFFGGGk.......",
            "....kAJJDkJJJGGGk.......",
            "....kJCJJkABBJGGk.......",
            ".....kJCCkBBCDJJJk......",
            ".....kCCCJBBCDJJDk......",
            ".....kCCCDJCCDkJDDk.....",
            "......kCDDJBCDkDDDk.....",
            ".......kDJJJCDkDDDkk....",
            ".......kJBCJCDkDDDkk....",
            ".......kBDJBCDkCDDkk....",
            "........kJJBCDkCDDk.....",
            "........kBBBCDkCCDDk....",
            "........kBBBCDkCCDDk....",
            ".........kBBCDkJDDDk....",
            "........kBJJJJkCJJJk....",
            "........kBBCCDkCCDDk....",
            "........kBBCDDkCDDk.....",
            "........kBBCDkCCDDk.....",
            "........kBCCDkCCDDk.....",
            "........kBCDDkCDDDk.....",
            ".......kJBCJkkkDJkkkk...",
            ".......kAJJAAAAkCCCCCk..",
            "........kkkkkkkkkkkkk...",
        };

        internal static PixelFxActorMask CreateMask()
        {
            int width = Rows[0].Length, height = Rows.Length;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                uint color = Rows[y][x] switch
                {
                    'k' => Hex(0x0e0c14),
                    'A' => Hex(0xe4e6ec),
                    'B' => Hex(0xb6b9c3),
                    'C' => Hex(0x868995),
                    'D' => Hex(0x5c5e6a),
                    'E' => Hex(0x6e707c),
                    'F' => Hex(0x4b4d58),
                    'G' => Hex(0x34353f),
                    'J' => Hex(0x2a2b33),
                    'm' => Hex(0xf53cd1),
                    _ => 0
                };
                pixels[(height - y - 1) * width + x] = new Color32(
                    (byte)color, (byte)(color >> 8), (byte)(color >> 16), (byte)(color >> 24));
            }
            return new PixelFxActorMask(pixels, width, height, 32, new Vector2(width / 2, 0));
        }
    }
}
