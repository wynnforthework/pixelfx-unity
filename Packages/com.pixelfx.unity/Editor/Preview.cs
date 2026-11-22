using System;
using UnityEditor;
using UnityEngine;

namespace PixelFx.Editor
{
    internal sealed class Preview : IDisposable
    {
        private readonly PixelFxTimeline timeline;
        private readonly PixelFxSurface[] surfaces;
        private readonly Vector3 target;
        internal float Duration => timeline.Duration;
        internal float PlaybackSpeed => timeline.Settings.playbackSpeed;

        internal Preview(string id, PixelFxSettings settings, float distance, bool splash, PixelFxQuality quality)
        {
            target = Vector3.right * distance;
            var context = new PixelFxContext { LandingPoint = target, Quality = quality };
            if (splash)
                context.Hits = new[] { new PixelFxHit(target), new PixelFxHit(target + new Vector3(2.15f, 0.75f), 0.5f) };
            timeline = new PixelFxTimeline(id, settings, context);
            surfaces = new PixelFxSurface[timeline.SurfaceCount];
            for (int i = 0; i < surfaces.Length; i++) surfaces[i] = new PixelFxSurface(timeline, false);
        }

        internal void Draw(Rect rect, float time)
        {
            EditorGUI.DrawRect(rect, new Color(0.087f, 0.105f, 0.14f));
            GUI.BeginGroup(rect);
            try
            {
                bool staged = timeline.Id == PixelFxLibrary.FireballThrow;
                float zoom = Mathf.Min(rect.height / 6.5f, rect.width / (staged ? target.x + 7 : 9));
                var origin = new Vector2(staged ? (rect.width - target.x * zoom) * 0.5f : rect.width * 0.37f, rect.height * 0.66f);
                if (staged)
                {
                    Marker(origin);
                    foreach (var hit in timeline.Hits)
                        Marker(origin + new Vector2(hit.Position.x, -hit.Position.y) * zoom);
                }
                for (int i = 0; i < surfaces.Length; i++)
                {
                    var surface = surfaces[i];
                    if (!timeline.Sample(time, i, out var stage, out float localTime, out var point, out _, out float strength)) continue;
                    surface.Draw(stage, localTime, strength);
                    float z = zoom * timeline.Settings.scale / 32;
                    GUI.DrawTexture(new Rect(origin.x + point.x * zoom - surface.OriginX * z,
                        origin.y - point.y * zoom - surface.OriginY * z, surface.Width * z, surface.Height * z), surface.Texture);
                }
            }
            finally { GUI.EndGroup(); }
        }

        private static void Marker(Vector2 pos)
        {
            var color = new Color(0.25f, 0.33f, 0.42f);
            EditorGUI.DrawRect(new Rect(pos.x - 5, pos.y, 11, 1), color);
            EditorGUI.DrawRect(new Rect(pos.x, pos.y - 5, 1, 11), color);
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
