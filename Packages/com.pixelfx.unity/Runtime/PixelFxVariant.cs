using System;
using System.Collections.Generic;
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

        public string EffectId => effectId;
        public PixelFxSettings Settings => settings?.Copy();

        public void Configure(string id, PixelFxSettings values)
        {
            var definition = PixelFxLibrary.Get(id);
            effectId = definition.Id;
            settings = (values ?? definition.Defaults()).Copy();
        }
    }

    public sealed class PixelFxDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        internal float ThumbnailTime { get; }
        private readonly int seed;
        private readonly uint[] palette;

        internal PixelFxDefinition(string id, string name, string category, int seed, float thumbnailTime, params uint[] palette)
        {
            Id = id;
            Name = name;
            Category = category;
            this.seed = seed;
            ThumbnailTime = thumbnailTime;
            this.palette = palette;
        }

        public PixelFxSettings Defaults()
        {
            var result = new PixelFxSettings { seed = seed, palette = new Color[palette.Length] };
            for (int i = 0; i < palette.Length; i++)
                result.palette[i] = new Color32((byte)(palette[i] >> 16), (byte)(palette[i] >> 8), (byte)palette[i], 255);
            return result;
        }
    }

    public static class PixelFxLibrary
    {
        public const string HeavyCleave = "melee/heavy-cleave";
        public const string FireballThrow = "moves/fireball-throw";
        public const string Shuriken = "original/shuriken";

        public static IReadOnlyList<PixelFxDefinition> Effects { get; } = Array.AsReadOnly(new[]
        {
            new PixelFxDefinition(HeavyCleave, "Heavy Cleave", "Melee", 5, 0.37f,
                0x2a0c10, 0x5c1414, 0xa3241a, 0xe0481c, 0xff8a2a, 0xffc23d, 0xfff0a0, 0xffffff),
            new PixelFxDefinition(FireballThrow, "Fireball Throw", "Moves", 1, 1.57f,
                0x4a1712, 0xc9281f, 0xf2561d, 0xff8a2a, 0xffc23d, 0xffe26b, 0xfff3a6, 0xffffff),
            new PixelFxDefinition(Shuriken, "Shuriken", "Projectile", 17, 0.43f,
                0x163341, 0x28788a, 0x46c7d4, 0xbdfcff),
        });

        public static PixelFxDefinition Get(string id)
        {
            foreach (var definition in Effects)
                if (definition.Id == id) return definition;
            throw new ArgumentException("Unknown PixelFX effect: " + id, nameof(id));
        }

        internal static uint Pack(Color32 c) => (uint)(c.r << 16 | c.g << 8 | c.b);
    }
}
