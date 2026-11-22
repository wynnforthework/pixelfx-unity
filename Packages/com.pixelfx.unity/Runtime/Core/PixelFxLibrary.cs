using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelFx
{
    public sealed class PixelFxDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        internal float ThumbnailTime { get; }
        internal (int Width, int Height, int OriginX, int OriginY) Canvas { get; }
        internal Func<PixelFxTimeline, PixelFxFrameRenderer> CreateRenderer { get; }
        private readonly int seed;
        private readonly uint[] palette;

        internal PixelFxDefinition(string id, string name, string category, int seed, float thumbnailTime,
            (int Width, int Height, int OriginX, int OriginY) canvas,
            Func<PixelFxTimeline, PixelFxFrameRenderer> createRenderer, params uint[] palette)
        {
            Id = id;
            Name = name;
            Category = category;
            this.seed = seed;
            ThumbnailTime = thumbnailTime;
            Canvas = canvas;
            CreateRenderer = createRenderer;
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
                (HeavyCleaveEffect.Width, HeavyCleaveEffect.Height, HeavyCleaveEffect.OriginX, HeavyCleaveEffect.OriginY),
                HeavyCleaveEffect.CreateRenderer,
                0x2a0c10, 0x5c1414, 0xa3241a, 0xe0481c, 0xff8a2a, 0xffc23d, 0xfff0a0, 0xffffff),
            new PixelFxDefinition(FireballThrow, "Fireball Throw", "Moves", 1, 1.57f,
                (FireballThrowEffect.Width, FireballThrowEffect.Height, FireballThrowEffect.OriginX, FireballThrowEffect.OriginY),
                FireballThrowEffect.CreateRenderer,
                0x4a1712, 0xc9281f, 0xf2561d, 0xff8a2a, 0xffc23d, 0xffe26b, 0xfff3a6, 0xffffff),
            new PixelFxDefinition(Shuriken, "Shuriken", "Projectile", 17, 0.43f,
                (ShurikenEffect.Width, ShurikenEffect.Height, ShurikenEffect.OriginX, ShurikenEffect.OriginY),
                ShurikenEffect.CreateRenderer,
                0x163341, 0x28788a, 0x46c7d4, 0xbdfcff),
        });

        private static readonly Dictionary<string, PixelFxDefinition> ById = BuildIndex();

        private static Dictionary<string, PixelFxDefinition> BuildIndex()
        {
            var index = new Dictionary<string, PixelFxDefinition>(Effects.Count, StringComparer.Ordinal);
            foreach (var definition in Effects)
                index.Add(definition.Id, definition);
            return index;
        }

        internal static bool Contains(string id) => id != null && ById.ContainsKey(id);

        public static PixelFxDefinition Get(string id)
        {
            if (id != null && ById.TryGetValue(id, out var definition)) return definition;
            throw new ArgumentException("Unknown PixelFX effect: " + id, nameof(id));
        }

        internal static uint Pack(Color32 c) => (uint)(c.r << 16 | c.g << 8 | c.b);
    }
}
