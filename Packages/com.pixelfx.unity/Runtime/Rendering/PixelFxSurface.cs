using System;
using Unity.Profiling;
using UnityEngine;

namespace PixelFx
{
    internal delegate void PixelFxFrameRenderer(byte[] pixels, PixelFxStage stage, double time, double strength);

    internal sealed class PixelFxSurface : IDisposable
    {
        private static readonly ProfilerMarker Raster = new ProfilerMarker("PixelFX.Raster");
        private static readonly ProfilerMarker Upload = new ProfilerMarker("PixelFX.Upload");

        internal readonly Texture2D Texture;
        internal readonly Sprite Sprite;
        internal readonly int Width, Height, OriginX, OriginY;
        private readonly PixelFxFrameRenderer renderFrame;
        private readonly byte[] pixels;
        private int lastFrame = -1;
        private PixelFxStage lastStage;

        internal PixelFxSurface(PixelFxTimeline timeline, bool needsSprite)
        {
            var definition = timeline.Definition;
            (Width, Height, OriginX, OriginY) = definition.Canvas;
            pixels = new byte[Width * Height * 4];
            renderFrame = definition.CreateRenderer(timeline);
            Texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "PixelFX " + timeline.Id,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (needsSprite)
            {
                Sprite = Sprite.Create(Texture, new Rect(0, 0, Width, Height),
                    new Vector2(OriginX / (float)Width, 1 - OriginY / (float)Height), 32, 0, SpriteMeshType.FullRect);
                Sprite.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        internal bool Render(PixelFxStage stage, float time, float strength)
        {
            int frame = Mathf.FloorToInt(Mathf.Max(0, time) * 30 + 0.0001f);
            if (stage == lastStage && frame == lastFrame) return false;
            using (Raster.Auto())
                renderFrame(pixels, stage, frame / 30.0, strength);
            lastStage = stage;
            lastFrame = frame;
            return true;
        }

        internal void Submit()
        {
            using (Upload.Auto())
            {
                Texture.LoadRawTextureData(pixels);
                Texture.Apply(false, false);
            }
        }

        internal void Draw(PixelFxStage stage, float time, float strength)
        {
            if (Render(stage, time, strength)) Submit();
        }

        public void Dispose()
        {
            Release(Sprite);
            Release(Texture);
        }

        private static void Release(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
            else UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
