using UnityEngine;
using UnityEngine.Rendering;

namespace PixelFx
{
    public enum PixelFxLayer { Lower, Middle, Upper }

    /// <summary>Places an effect relative to an actor. An explicit anchor overrides the sprite bounds.</summary>
    public readonly struct PixelFxActorBinding
    {
        public SpriteRenderer Actor { get; }
        public Transform Anchor { get; }
        public PixelFxLayer Layer { get; }
        public bool Follow { get; }

        public PixelFxActorBinding(SpriteRenderer actor, PixelFxLayer layer, Transform anchor = null, bool follow = true)
        {
            Actor = actor;
            Anchor = anchor;
            Layer = layer;
            Follow = follow;
        }

        internal Vector3 PositionOr(Vector3 fallback)
        {
            if (Anchor != null) return Anchor.position;
            if (Actor == null) return fallback;
            var bounds = Actor.bounds;
            var position = bounds.center;
            if (Layer == PixelFxLayer.Lower) position.y = bounds.min.y;
            if (Layer == PixelFxLayer.Upper) position.y = bounds.max.y;
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
