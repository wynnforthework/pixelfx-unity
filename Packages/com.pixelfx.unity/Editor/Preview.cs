using System;
using UnityEngine;

namespace PixelFx.Editor
{
    internal sealed class Preview : IDisposable
    {
        private readonly PixelFxTimeline timeline;
        private readonly PixelFxSurface[] surfaces;
        internal float Duration => timeline.Duration;
        internal float PlaybackSpeed => timeline.Settings.playbackSpeed;

        internal Preview(string id, PixelFxSettings settings, float distance, bool splash, PixelFxQuality quality)
        {
            var target = Vector3.right * distance;
            var context = new PixelFxContext { LandingPoint = target, Quality = quality };
            if (splash)
                context.Hits = new[] { new PixelFxHit(target), new PixelFxHit(target + new Vector3(2.15f, 0.75f), 0.5f) };
            timeline = new PixelFxTimeline(id, settings, context, previewActor: true);
            surfaces = new PixelFxSurface[timeline.SurfaceCount];
            for (int i = 0; i < surfaces.Length; i++) surfaces[i] = new PixelFxSurface(timeline, false);
        }

        internal Texture2D CaptureThumbnail(float thumbnailTime)
        {
            if (timeline.Id == PixelFxLibrary.FireballThrow)
                thumbnailTime = timeline.CastTime + timeline.FlightTime + 0.16f;
            float time = thumbnailTime / PlaybackSpeed;
            var surface = surfaces[0];
            if (!timeline.Sample(Mathf.Min(time, Duration - 0.0001f), 0, out var stage, out float localTime, out _, out _, out float strength))
                timeline.Sample(0, 0, out stage, out localTime, out _, out _, out strength);
            surface.Draw(stage, localTime, strength);
            var texture = new Texture2D(surface.Width, surface.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.LoadRawTextureData(surface.Texture.GetRawTextureData());
            texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            foreach (var surface in surfaces) surface.Dispose();
        }
    }
}
