using UnityEngine;
using UnityEngine.Rendering;

namespace PixelFx
{
    public enum PixelFxLayer { Lower, Middle, Upper }

    /// <summary>Places an effect relative to an actor. An explicit anchor overrides the sprite bounds.</summary>
    public readonly struct PixelFxActorBinding
    {
        public SpriteRenderer Actor { get; }
        internal Renderer SortingRenderer { get; }
        public Transform Anchor { get; }
        public PixelFxLayer Layer { get; }
        public bool Follow { get; }

        public PixelFxActorBinding(SpriteRenderer actor, PixelFxLayer layer, Transform anchor = null, bool follow = true)
            : this((Renderer)actor, layer, anchor, follow) { }

        internal PixelFxActorBinding(Renderer actor, PixelFxLayer layer, Transform anchor = null, bool follow = true)
        {
            Actor = actor as SpriteRenderer;
            SortingRenderer = actor;
            Anchor = anchor;
            Layer = layer;
            Follow = follow;
        }

        internal Vector3 PositionOr(Vector3 fallback, PixelFxLayer? defaultAnchor = null)
        {
            if (Anchor != null) return Anchor.position;
            if (SortingRenderer == null) return fallback;
            var bounds = SortingRenderer.bounds;
            var position = bounds.center;
            var anchorLayer = defaultAnchor ?? Layer;
            if (anchorLayer == PixelFxLayer.Lower) position.y = bounds.min.y;
            if (anchorLayer == PixelFxLayer.Upper) position.y = bounds.max.y;
            return position;
        }

        internal static SortingGroup SortingGroupFor(Transform transform)
        {
            SortingGroup outermost = null;
            for (var node = transform; node != null; node = node.parent)
            {
                if (!node.TryGetComponent<SortingGroup>(out var group) || !group.isActiveAndEnabled) continue;
                outermost = group;
                if (group.sortAtRoot) break;
            }
            return outermost;
        }
    }
}
