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

        private void LateUpdate() => Tick(Time.deltaTime);

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

        internal sealed class Playback : IDisposable
        {
            internal readonly PixelFxPlayback Handle;
            internal float Time;
            private readonly PixelFxTimeline timeline;
            private readonly (SpriteRenderer Renderer, PixelFxSurface Surface)[] primary, rear;
            private readonly Transform parent;
            private readonly Transform sortedParent;
            private readonly Transform effectSpace;
            private readonly int layerId, order;

            internal Playback(PixelFxTimeline timeline, PixelFxPlayback handle, Transform parent, string layer, int order,
                Transform sortedParent = null, Transform effectSpace = null)
            {
                this.timeline = timeline;
                Handle = handle;
                this.parent = parent;
                this.sortedParent = sortedParent;
                this.effectSpace = effectSpace;
                layerId = SortingLayer.NameToID(layer);
                this.order = order;
                primary = new (SpriteRenderer, PixelFxSurface)[timeline.SurfaceCount];
                rear = timeline.Definition.LayeredStage.HasValue && primary.Length > 0
                    ? new (SpriteRenderer, PixelFxSurface)[primary.Length]
                    : Array.Empty<(SpriteRenderer, PixelFxSurface)>();
                for (int i = 0; i < primary.Length; i++)
                    primary[i] = CreateSurface();
            }

            private (SpriteRenderer, PixelFxSurface) CreateSurface()
            {
                var obj = new GameObject("PixelFX " + timeline.Id);
                if (sortedParent != null)
                {
                    obj.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, sortedParent.gameObject.scene);
                }
                obj.transform.SetParent(effectSpace != null ? effectSpace : parent, effectSpace == null);
                obj.transform.localScale = Vector3.one * timeline.Settings.scale;
                var renderer = obj.AddComponent<SpriteRenderer>();
                var surface = new PixelFxSurface(timeline, true);
                renderer.sprite = surface.Sprite;
                return (renderer, surface);
            }

            internal void Draw()
            {
                for (int i = 0; i < primary.Length; i++)
                {
                    var (renderer, surface) = primary[i];
                    bool visible = timeline.Sample(Time, i, out var stage, out float localTime,
                        out Vector3 point, out float angle, out float strength);
                    renderer.enabled = visible;
                    var binding = visible ? timeline.BindingFor(stage, i) : default;
                    var sortingRenderer = binding.SortingRenderer;
                    bool split = sortingRenderer != null && binding.Layer == PixelFxLayer.Middle
                        && timeline.Definition.LayeredStage == stage;
                    if (!split && i < rear.Length && rear[i].Renderer != null) rear[i].Renderer.enabled = false;
                    if (!visible) continue;

                    var rotation = Quaternion.Euler(0, 0, angle);
                    bool actorOverlay = timeline.ActorMask != null;
                    SetPlacement(renderer, sortingRenderer, !actorOverlay && binding.Layer == PixelFxLayer.Lower, point, rotation);
                    if (actorOverlay)
                        HitFlashEffect.PlaceOverlay(renderer, binding.Actor, timeline.Settings.scale);
                    surface.Draw(stage, localTime, strength, split ? PixelFxRenderPass.Front : PixelFxRenderPass.Combined);
                    if (!split) continue;

                    if (rear[i].Renderer == null) rear[i] = CreateSurface();
                    var (backRenderer, backSurface) = rear[i];
                    backRenderer.enabled = true;
                    SetPlacement(backRenderer, sortingRenderer, true, point, rotation);
                    backSurface.Draw(stage, localTime, strength, PixelFxRenderPass.Back);
                }
            }

            private void SetPlacement(SpriteRenderer renderer, Renderer actor, bool behind, Vector3 point, Quaternion rotation)
            {
                if (actor == null)
                {
                    if (effectSpace == null && renderer.transform.parent != parent)
                        renderer.transform.SetParent(parent, false);
                    renderer.sortingLayerID = layerId;
                    renderer.sortingOrder = order;
                }
                else
                {
                    var group = PixelFxActorBinding.SortingGroupFor(actor.transform);
                    // Sort outside the actor's group, even if the player is on the actor itself.
                    if (effectSpace == null && renderer.transform.parent != sortedParent)
                        renderer.transform.SetParent(sortedParent, false);
                    renderer.sortingLayerID = group != null ? group.sortingLayerID : actor.sortingLayerID;
                    int actorOrder = group != null ? group.sortingOrder : actor.sortingOrder;
                    int layerOffset = behind ? -1 : 1;
                    if (effectSpace != null) layerOffset += order;
                    renderer.sortingOrder = Mathf.Clamp(actorOrder + layerOffset, short.MinValue, short.MaxValue);
                    if (sortedParent != null && effectSpace == null && !(actor is SpriteRenderer))
                    {
                        var bounds = actor.bounds;
                        if (bounds.size.z > 0.001f)
                            point.z = behind ? bounds.max.z + 0.01f : bounds.min.z - 0.01f;
                    }
                }
                if (effectSpace != null)
                {
                    renderer.transform.SetLocalPositionAndRotation(point, rotation);
                    return;
                }
                renderer.transform.SetPositionAndRotation(point, rotation);
            }

            public void Dispose()
            {
                foreach (var slot in primary) Release(slot);
                foreach (var slot in rear) Release(slot);
            }

            private static void Release((SpriteRenderer Renderer, PixelFxSurface Surface) slot)
            {
                slot.Surface?.Dispose();
                if (slot.Renderer == null) return;
                slot.Renderer.enabled = false;
                if (Application.isPlaying) Destroy(slot.Renderer.gameObject);
                else DestroyImmediate(slot.Renderer.gameObject);
            }
        }
    }
}
