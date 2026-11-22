using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelFx
{
    [DisallowMultipleComponent]
    public sealed class PixelFxPlayer : MonoBehaviour
    {
        public static PixelFxQuality DefaultQuality { get; set; } = PixelFxQuality.Full;
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder;
        private readonly List<Playback> active = new List<Playback>();
        private readonly List<PixelFxPlayback> finished = new List<PixelFxPlayback>();
        private bool stopping;

        public PixelFxPlayback Play(PixelFxVariant variant, PixelFxContext context = null)
        {
            if (variant == null) throw new ArgumentNullException(nameof(variant));
            return Play(variant.EffectId, variant.Settings, context);
        }

        public PixelFxPlayback Play(string effectId, PixelFxSettings settings = null, PixelFxContext context = null)
        {
            var timeline = new PixelFxTimeline(effectId, settings, context ?? new PixelFxContext { Position = transform.position });
            var handle = new PixelFxPlayback(timeline.Duration);
            if (!isActiveAndEnabled || stopping)
            {
                handle.Stop();
                return handle;
            }
            var entry = new Playback(timeline, handle, transform, sortingLayer, sortingOrder);
            active.Add(entry);
            handle.Completed += _ => Remove(entry);
            entry.Draw();
            return handle;
        }

        private void Update() => Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            finished.Clear();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var entry = active[i];
                entry.Time += deltaTime;
                if (entry.Time >= entry.Handle.Duration) finished.Add(entry.Handle);
                else entry.Draw();
            }
            foreach (var handle in finished) handle.Finish(false);
            finished.Clear();
        }

        private void Remove(Playback entry)
        {
            if (active.Remove(entry)) entry.Dispose();
        }

        private void OnDisable()
        {
            stopping = true;
            try
            {
                while (active.Count > 0) active[active.Count - 1].Handle.Stop();
            }
            finally { stopping = false; }
        }

        private sealed class Playback : IDisposable
        {
            internal readonly PixelFxPlayback Handle;
            internal float Time;
            private readonly PixelFxTimeline timeline;
            private readonly PixelFxSurface[] surfaces;
            private readonly SpriteRenderer[] renderers;

            internal Playback(PixelFxTimeline timeline, PixelFxPlayback handle, Transform parent, string layer, int order)
            {
                this.timeline = timeline;
                Handle = handle;
                surfaces = new PixelFxSurface[timeline.SurfaceCount];
                renderers = new SpriteRenderer[timeline.SurfaceCount];
                for (int i = 0; i < surfaces.Length; i++)
                {
                    var obj = new GameObject("PixelFX " + timeline.Id);
                    obj.transform.SetParent(parent, true);
                    renderers[i] = obj.AddComponent<SpriteRenderer>();
                    renderers[i].sortingLayerName = layer;
                    renderers[i].sortingOrder = order;
                    surfaces[i] = new PixelFxSurface(timeline, true);
                    renderers[i].sprite = surfaces[i].Sprite;
                    obj.transform.localScale = Vector3.one * timeline.Settings.scale;
                }
            }

            internal void Draw()
            {
                for (int i = 0; i < surfaces.Length; i++)
                {
                    var renderer = renderers[i];
                    renderer.enabled = timeline.Sample(Time, i, out var stage, out float localTime,
                        out Vector3 point, out float angle, out float strength);
                    if (!renderer.enabled) continue;
                    renderer.transform.SetPositionAndRotation(point, Quaternion.Euler(0, 0, angle));
                    surfaces[i].Draw(stage, localTime, strength);
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < surfaces.Length; i++)
                {
                    renderers[i].enabled = false;
                    surfaces[i].Dispose();
                    if (Application.isPlaying) Destroy(renderers[i].gameObject);
                    else DestroyImmediate(renderers[i].gameObject);
                }
            }
        }
    }
}
