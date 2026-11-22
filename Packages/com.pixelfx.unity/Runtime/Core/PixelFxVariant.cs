using System;
using UnityEngine;

namespace PixelFx
{
    public enum PixelFxQuality { Full, Simplified, Off }

    [Serializable]
    public sealed class PixelFxSettings
    {
        public int seed;
        public Color[] palette;
        [Range(0.15f, 4)] public float scale = 1;
        [Range(0.1f, 4)] public float playbackSpeed = 1;
        [Range(0, 2)] public float glow = 1;
        [Range(0.4f, 1.7f)] public float bladeWidth = 1;
        [Range(0, 2)] public float sparks = 1;
        [Range(0, 4)] public float castDuration = 0.95f;
        [Range(0.25f, 40)] public float flightSpeed = 10.625f;
        [Range(0.3f, 2)] public float blastSize = 1;
        [Range(0, 16)] public float spin = 3.5f;
        [Range(0, 180)] public int trailRate = 68;

        public PixelFxSettings Copy()
        {
            var copy = (PixelFxSettings)MemberwiseClone();
            copy.palette = palette == null ? null : (Color[])palette.Clone();
            return copy;
        }
    }

    [Serializable]
    internal struct PixelFxPlacement
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;

        internal PixelFxPlacement(Vector3 position, Vector3 rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        internal void ApplyTo(Transform target)
        {
            target.SetLocalPositionAndRotation(position, Quaternion.Euler(rotation));
            target.localScale = scale;
        }
    }

    public sealed class PixelFxVariant : ScriptableObject
    {
        [SerializeField] private string effectId;
        [SerializeField] private PixelFxSettings settings;
        [SerializeField, HideInInspector] private bool useLibraryDefaults;
        [SerializeField] private bool includeTransform;
        [SerializeField] private PixelFxPlacement placement = new PixelFxPlacement(Vector3.zero, Vector3.zero, Vector3.one);

        public string EffectId => effectId;
        public PixelFxSettings Settings => useLibraryDefaults
            ? PixelFxLibrary.Get(effectId).Defaults()
            : settings?.Copy();
        internal PixelFxPlacement? Placement => !useLibraryDefaults && includeTransform ? placement : null;

        public void Configure(string id, PixelFxSettings values)
        {
            var definition = PixelFxLibrary.Get(id);
            effectId = definition.Id;
            settings = (values ?? definition.Defaults()).Copy();
            useLibraryDefaults = false;
        }

        internal void Configure(string id, PixelFxSettings values, PixelFxPlacement? transform)
        {
            Configure(id, values);
            includeTransform = transform.HasValue;
            placement = transform ?? new PixelFxPlacement(Vector3.zero, Vector3.zero, Vector3.one);
        }
    }
}
