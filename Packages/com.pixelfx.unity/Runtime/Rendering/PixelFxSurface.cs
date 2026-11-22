using System;
using Unity.Profiling;
using UnityEngine;

namespace PixelFx
{
    internal delegate void PixelFxFrameRenderer(byte[] pixels, PixelFxStage stage, double time, double strength);
    internal delegate void PixelFxLayerRenderer(byte[] pixels, PixelFxStage stage, double time, double strength, PixelFxRenderPass pass);
    internal enum PixelFxRenderPass { Combined, Back, Front }

    internal sealed class PixelFxSurface : IDisposable
    {
        private static readonly ProfilerMarker Raster = new ProfilerMarker("PixelFX.Raster");
        private static readonly ProfilerMarker Upload = new ProfilerMarker("PixelFX.Upload");

        internal readonly Texture2D Texture;
        internal readonly Sprite Sprite;
        internal readonly int Width, Height, OriginX, OriginY;
        private readonly PixelFxLayerRenderer renderFrame;
        private readonly byte[] pixels;
        private readonly PixelFxTimeline timeline;
        private int lastActorVersion;
        private Vector3 lastChest;
        private Matrix4x4 lastActorToSurface;
        private int lastFrame = -1;
        private float lastStrength;
        private PixelFxStage lastStage;
        private PixelFxRenderPass lastPass;

        internal PixelFxSurface(PixelFxTimeline timeline, bool needsSprite)
        {
            this.timeline = timeline;
            var definition = timeline.Definition;
            (Width, Height, OriginX, OriginY) = definition.Canvas;
            pixels = new byte[Width * Height * 4];
            if (definition.CreateLayerRenderer != null)
                renderFrame = definition.CreateLayerRenderer(timeline);
            else
            {
                var combined = definition.CreateRenderer(timeline);
                renderFrame = (buffer, stage, time, strength, pass) => combined(buffer, stage, time, strength);
            }
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

        internal bool Render(PixelFxStage stage, float time, float strength, PixelFxRenderPass pass = PixelFxRenderPass.Combined)
        {
            int frame = Mathf.FloorToInt(Mathf.Max(0, time) * 30 + 0.0001f);
            int actorVersion = 0;
            Vector3 chest = default;
            Matrix4x4 actorToSurface = default;
            if (timeline.ActorMask != null && pass != PixelFxRenderPass.Back)
            {
                var binding = timeline.BindingFor(stage, 0);
                timeline.ActorMask.Update(binding.Actor);
                actorVersion = timeline.ActorMask.Version;
                actorToSurface = timeline.ActorToSurface;
                if (binding.Actor != null && binding.Anchor != null)
                    chest = binding.Actor.transform.InverseTransformPoint(binding.Anchor.position);
            }
            if (stage == lastStage && frame == lastFrame && pass == lastPass && actorVersion == lastActorVersion
                && chest == lastChest && strength == lastStrength
                && actorToSurface.Equals(lastActorToSurface)) return false;
            using (Raster.Auto())
                renderFrame(pixels, stage, frame / 30.0, strength, pass);
            lastStage = stage;
            lastFrame = frame;
            lastPass = pass;
            lastStrength = strength;
            lastActorVersion = actorVersion;
            lastChest = chest;
            lastActorToSurface = actorToSurface;
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

        internal void Draw(PixelFxStage stage, float time, float strength, PixelFxRenderPass pass = PixelFxRenderPass.Combined)
        {
            if (Render(stage, time, strength, pass)) Submit();
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
