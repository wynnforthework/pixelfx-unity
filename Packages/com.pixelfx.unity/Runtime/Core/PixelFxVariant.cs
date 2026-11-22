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

    public sealed class PixelFxVariant : ScriptableObject
    {
        [SerializeField] private string effectId;
        [SerializeField] private PixelFxSettings settings;
        [SerializeField, HideInInspector] private bool useLibraryDefaults;

        public string EffectId => effectId;
        public PixelFxSettings Settings => useLibraryDefaults
            ? PixelFxLibrary.Get(effectId).Defaults()
            : settings?.Copy();

        public void Configure(string id, PixelFxSettings values)
        {
            var definition = PixelFxLibrary.Get(id);
            effectId = definition.Id;
            settings = (values ?? definition.Defaults()).Copy();
            useLibraryDefaults = false;
        }
    }
}
