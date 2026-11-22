using System;
using Unity.Profiling;
using UnityEngine;

namespace PixelFx
{
    internal sealed class PixelFxSurface : IDisposable
    {
        private static readonly ProfilerMarker Raster = new ProfilerMarker("PixelFX.Raster");
        private static readonly ProfilerMarker Upload = new ProfilerMarker("PixelFX.Upload");

        internal readonly Texture2D Texture;
        internal readonly Sprite Sprite;
        internal readonly int Width, Height, OriginX, OriginY;
        private readonly PixelFxTimeline timeline;
        private readonly HeavyCleaveEffect cleave;
        private readonly PixelFxArt art;
        private readonly byte[] pixels;
        private readonly uint[] colors;
        private int lastFrame = -1;
        private PixelFxStage lastStage;

        internal PixelFxSurface(PixelFxTimeline timeline, bool needsSprite)
        {
            this.timeline = timeline;
            (Width, Height, OriginX, OriginY) = timeline.Id switch
            {
                PixelFxLibrary.HeavyCleave => (208, 116, 68, 96),
                PixelFxLibrary.Shuriken => (184, 56, 22, 28),
                _ => (260, 196, 130, 148),
            };
            pixels = new byte[Width * Height * 4];
            if (timeline.Id == PixelFxLibrary.HeavyCleave)
            {
                cleave = new HeavyCleaveEffect();
                colors = Array.ConvertAll(timeline.Settings.palette, c => PixelFxLibrary.Pack(c));
                ResetCleave();
            }
            else art = new PixelFxArt(new PixelPainter(Width, Height, OriginX, OriginY), timeline.Settings);
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

        private void ResetCleave()
        {
            var p = timeline.Settings;
            cleave.Reset(p.seed, colors, 0x4a3f69, p.bladeWidth, p.sparks, timeline.Quality == PixelFxQuality.Full);
        }

        internal bool Render(PixelFxStage stage, float time, float strength)
        {
            int frame = Mathf.FloorToInt(Mathf.Max(0, time) * 30 + 0.0001f);
            if (stage == lastStage && frame == lastFrame) return false;
            using (Raster.Auto())
            {
                double sampledTime = frame / 30.0;
                if (cleave != null)
                {
                    if (frame < lastFrame) ResetCleave();
                    cleave.Advance(sampledTime);
                    cleave.Render(pixels, true, timeline.Settings.glow);
                }
                else art.Render(pixels, stage, sampledTime, timeline.CastTime, strength, timeline.Quality == PixelFxQuality.Full);
            }
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
