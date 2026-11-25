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
        internal float Duration { get; }
        internal (int Width, int Height, int OriginX, int OriginY) Canvas { get; }
        internal Func<PixelFxTimeline, PixelFxFrameRenderer> CreateRenderer { get; }
        internal Func<PixelFxTimeline, PixelFxLayerRenderer> CreateLayerRenderer { get; set; }
        internal PixelFxStage? LayeredStage { get; set; }
        internal PixelFxLayer? DefaultActorAnchor { get; set; }
        internal bool UsesProjectedActorMask { get; set; }
        internal bool KeepUpright { get; set; }
        private readonly int seed;
        private readonly uint[] palette;

        internal PixelFxDefinition(string id, string name, string category, int seed, float thumbnailTime, float duration,
            (int Width, int Height, int OriginX, int OriginY) canvas,
            Func<PixelFxTimeline, PixelFxFrameRenderer> createRenderer, params uint[] palette)
        {
            Id = id;
            Name = name;
            Category = category;
            this.seed = seed;
            ThumbnailTime = thumbnailTime;
            Duration = duration;
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
        public const string Fireball = "fire/fireball";
        public const string FireRing = "fire/fire-ring";
        public const string DivineShield = "holy/divine-shield";
        public const string Blessing = "holy/blessing";
        public const string Halo = "holy/halo";
        public const string Heal = "holy/heal";
        public const string HealingRing = "holy/heal-aoe";
        public const string HolyPillar = "holy/holy-pillar";
        public const string Smite = "holy/smite";
        public const string Sunburst = "holy/sunburst";
        public const string Shuriken = "original/shuriken";
        public const string HitSpark = "impacts/hit-spark";
        public const string HitFlash = "impacts/hit-flash";
        public const string Parry = "impacts/parry";
        public const string BluntHit = "impacts/blunt-hit";
        public const string CriticalHit = "impacts/crit-hit";
        public const string GroundSlam = "impacts/ground-slam";
        public const string SpellCharge = "arcane/spell-charge";
        public const string MagicCircleCast = "arcane/magic-circle-cast";
        public const string SmallExplosion = "explosions/small-explosion";

        public static IReadOnlyList<PixelFxDefinition> Effects { get; } = Array.AsReadOnly(new[]
        {
            new PixelFxDefinition(Sunburst, "Sunburst", "Holy", 3, 0.6f, SunburstEffect.Duration,
                (SunburstEffect.Width, SunburstEffect.Height, SunburstEffect.OriginX, SunburstEffect.OriginY),
                SunburstEffect.CreateRenderer,
                0x3b1d2a, 0x7a3a2c, 0xc4652e, 0xf0a040, 0xffd25e, 0xfff0a0, 0xfffbe0, 0xffffff)
            {
                DefaultActorAnchor = PixelFxLayer.Middle,
                KeepUpright = true,
            },
            new PixelFxDefinition(Smite, "Smite", "Holy", 9, 0.6f, SmiteEffect.Duration,
                (SmiteEffect.Width, SmiteEffect.Height, SmiteEffect.OriginX, SmiteEffect.OriginY),
                SmiteEffect.CreateRenderer,
                0x3b1d2a, 0x7a3a2c, 0xc4652e, 0xf0a040, 0xffd25e, 0xfff0a0, 0xfffbe0, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = SmiteEffect.CreateLayerRenderer,
                KeepUpright = true,
            },
            new PixelFxDefinition(HolyPillar, "Holy Pillar", "Holy", 3, 0.8f, HolyPillarEffect.Duration,
                (HolyPillarEffect.Width, HolyPillarEffect.Height, HolyPillarEffect.OriginX, HolyPillarEffect.OriginY),
                HolyPillarEffect.CreateRenderer,
                0x3b1d2a, 0x7a3a2c, 0xc4652e, 0xf0a040, 0xffd25e, 0xfff0a0, 0xfffbe0, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = HolyPillarEffect.CreateLayerRenderer,
                KeepUpright = true,
            },
            new PixelFxDefinition(HealingRing, "Healing Ring", "Holy", 6, 0.6f, HealingRingEffect.Duration,
                (HealingRingEffect.Width, HealingRingEffect.Height, HealingRingEffect.OriginX, HealingRingEffect.OriginY),
                HealingRingEffect.CreateRenderer,
                0x0e2a24, 0x14472f, 0x1d6e3a, 0x2f9a45, 0x5cc654, 0xa0e46c, 0xdcffb4, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = HealingRingEffect.CreateLayerRenderer,
                KeepUpright = true,
            },
            new PixelFxDefinition(Heal, "Heal", "Holy", 2, 0.6f, HealEffect.Duration,
                (HealEffect.Width, HealEffect.Height, HealEffect.OriginX, HealEffect.OriginY),
                HealEffect.CreateRenderer,
                0x3d2027, 0x7d3f35, 0xc97a4a, 0xf2b46a, 0xffd994, 0xfff0cf, 0xfffaf2, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = HealEffect.CreateLayerRenderer,
                UsesProjectedActorMask = true,
                KeepUpright = true,
            },
            new PixelFxDefinition(Halo, "Halo", "Holy", 5, 8f / 30, HaloEffect.Duration,
                (HaloEffect.Width, HaloEffect.Height, HaloEffect.OriginX, HaloEffect.OriginY),
                HaloEffect.CreateRenderer,
                0x3b1d2a, 0x7a3a2c, 0xc4652e, 0xf0a040, 0xffd25e, 0xfff0a0, 0xfffbe0, 0xffffff)
            {
                DefaultActorAnchor = PixelFxLayer.Lower,
                UsesProjectedActorMask = true,
                KeepUpright = true,
            },
            new PixelFxDefinition(Blessing, "Blessing", "Holy", 7, 18f / 30, BlessingEffect.Duration,
                (BlessingEffect.Width, BlessingEffect.Height, BlessingEffect.OriginX, BlessingEffect.OriginY),
                BlessingEffect.CreateRenderer,
                0x3b1d2a, 0x7a3a2c, 0xc4652e, 0xf0a040, 0xffd25e, 0xfff0a0, 0xfffbe0, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = BlessingEffect.CreateLayerRenderer,
                UsesProjectedActorMask = true,
            },
            new PixelFxDefinition(DivineShield, "Divine Shield", "Holy", 8, 18f / 30, DivineShieldEffect.Duration,
                (DivineShieldEffect.Width, DivineShieldEffect.Height, DivineShieldEffect.OriginX, DivineShieldEffect.OriginY),
                DivineShieldEffect.CreateRenderer,
                0x3b1d2a, 0x7a3a2c, 0xc4652e, 0xf0a040, 0xffd25e, 0xfff0a0, 0xfffbe0, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = DivineShieldEffect.CreateLayerRenderer,
                UsesProjectedActorMask = true,
            },
            new PixelFxDefinition(FireRing, "Fire Ring", "Fire", 6, 20f / 30, FireRingEffect.Duration,
                (FireRingEffect.Width, FireRingEffect.Height, FireRingEffect.OriginX, FireRingEffect.OriginY),
                FireRingEffect.CreateRenderer,
                0x4a1712, 0x8a1f17, 0xc9281f, 0xf2561d, 0xff8a2a, 0xffc23d, 0xffe26b, 0xfff3a6, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = FireRingEffect.CreateLayerRenderer,
            },
            new PixelFxDefinition(MagicCircleCast, "Magic Circle Cast", "Arcane", 6, 51f / 30, MagicCircleCastEffect.Duration,
                (MagicCircleCastEffect.Width, MagicCircleCastEffect.Height, MagicCircleCastEffect.OriginX, MagicCircleCastEffect.OriginY),
                MagicCircleCastEffect.CreateRenderer,
                0x140a26, 0x2b1252, 0x481c88, 0x6b2cc0, 0x9a48ec, 0xc684ff, 0xecc8ff, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = MagicCircleCastEffect.CreateLayerRenderer,
            },
            new PixelFxDefinition(SmallExplosion, "Small Explosion", "Explosions", 7, 6f / 30, SmallExplosionEffect.Duration,
                (SmallExplosionEffect.Width, SmallExplosionEffect.Height, SmallExplosionEffect.OriginX, SmallExplosionEffect.OriginY),
                SmallExplosionEffect.CreateRenderer,
                0x4a1712, 0xc9281f, 0xf2561d, 0xff8a2a, 0xffc23d, 0xffe26b, 0xfff3a6, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = SmallExplosionEffect.CreateLayerRenderer,
            },
            new PixelFxDefinition(SpellCharge, "Spell Charge", "Arcane", 5, 16f / 30, SpellChargeEffect.Duration,
                (SpellChargeEffect.Width, SpellChargeEffect.Height, SpellChargeEffect.OriginX, SpellChargeEffect.OriginY),
                SpellChargeEffect.CreateRenderer,
                0x140a26, 0x2b1252, 0x481c88, 0x6b2cc0, 0x9a48ec, 0xc684ff, 0xecc8ff, 0xffffff)
            {
                DefaultActorAnchor = PixelFxLayer.Lower,
            },
            new PixelFxDefinition(HeavyCleave, "Heavy Cleave", "Melee", 5, 0.37f, (float)HeavyCleaveEffect.Duration,
                (HeavyCleaveEffect.Width, HeavyCleaveEffect.Height, HeavyCleaveEffect.OriginX, HeavyCleaveEffect.OriginY),
                HeavyCleaveEffect.CreateRenderer,
                0x2a0c10, 0x5c1414, 0xa3241a, 0xe0481c, 0xff8a2a, 0xffc23d, 0xfff0a0, 0xffffff),
            new PixelFxDefinition(Fireball, "Fireball", "Fire", 5, 12f / 30, FireballEffect.Duration,
                (FireballEffect.Width, FireballEffect.Height, FireballEffect.OriginX, FireballEffect.OriginY),
                FireballEffect.CreateRenderer,
                0x4a1712, 0x8a1f17, 0xc9281f, 0xf2561d, 0xff8a2a, 0xffc23d, 0xffe26b, 0xfff3a6, 0xffffff),
            new PixelFxDefinition(FireballThrow, "Fireball Throw", "Moves", 1, 1.57f, SmallExplosionEffect.Duration,
                (FireballThrowEffect.Width, FireballThrowEffect.Height, FireballThrowEffect.OriginX, FireballThrowEffect.OriginY),
                FireballThrowEffect.CreateRenderer,
                0x4a1712, 0xc9281f, 0xf2561d, 0xff8a2a, 0xffc23d, 0xffe26b, 0xfff3a6, 0xffffff)
            {
                LayeredStage = PixelFxStage.Cast,
                CreateLayerRenderer = FireballThrowEffect.CreateLayerRenderer,
            },
            new PixelFxDefinition(Shuriken, "Shuriken", "Projectile", 17, 0.43f, (float)ShurikenEffect.Duration,
                (ShurikenEffect.Width, ShurikenEffect.Height, ShurikenEffect.OriginX, ShurikenEffect.OriginY),
                ShurikenEffect.CreateRenderer,
                0x163341, 0x28788a, 0x46c7d4, 0xbdfcff),
            new PixelFxDefinition(HitSpark, "Hit Spark", "Impacts", 4, 1f / 30, HitSparkEffect.Duration,
                (HitSparkEffect.Width, HitSparkEffect.Height, HitSparkEffect.OriginX, HitSparkEffect.OriginY),
                HitSparkEffect.CreateRenderer,
                0x4a1712, 0xc9281f, 0xf2561d, 0xff8a2a, 0xffe26b, 0xfff3a6, 0xffffff),
            new PixelFxDefinition(HitFlash, "Hit Flash", "Impacts", 17, 2f / 30, HitFlashEffect.Duration,
                (HitFlashEffect.Width, HitFlashEffect.Height, HitFlashEffect.OriginX, HitFlashEffect.OriginY),
                HitFlashEffect.CreateRenderer,
                0x14040a, 0x3a0a14, 0x6a101c, 0xa01824, 0xd42a30, 0xff5a50, 0xff9a8a),
            new PixelFxDefinition(Parry, "Parry", "Impacts", 11, 2f / 30, ParryEffect.Duration,
                (ParryEffect.Width, ParryEffect.Height, ParryEffect.OriginX, ParryEffect.OriginY),
                ParryEffect.CreateRenderer,
                0x2a1a08, 0x6a4410, 0xb07a1a, 0xe8b030, 0xffe066, 0xfff6c4, 0xffffff),
            new PixelFxDefinition(BluntHit, "Blunt Hit", "Impacts", 9, 1f / 30, BluntHitEffect.Duration,
                (BluntHitEffect.Width, BluntHitEffect.Height, BluntHitEffect.OriginX, BluntHitEffect.OriginY),
                BluntHitEffect.CreateRenderer,
                0x1d1620, 0x4a3a3c, 0x7d6a66, 0xb09c90, 0xdccab4, 0xf6ead2, 0xffffff),
            new PixelFxDefinition(CriticalHit, "Critical Hit", "Impacts", 3, 2f / 30, CriticalHitEffect.Duration,
                (CriticalHitEffect.Width, CriticalHitEffect.Height, CriticalHitEffect.OriginX, CriticalHitEffect.OriginY),
                CriticalHitEffect.CreateRenderer,
                0x2a0510, 0x5c0a1c, 0xa3142a, 0xe0302c, 0xff7a2a, 0xffc23d, 0xfff09a, 0xffffff),
            new PixelFxDefinition(GroundSlam, "Ground Slam", "Impacts", 5, 0.2f, GroundSlamEffect.Duration,
                (GroundSlamEffect.Width, GroundSlamEffect.Height, GroundSlamEffect.OriginX, GroundSlamEffect.OriginY),
                GroundSlamEffect.CreateRenderer,
                0x2a1712, 0x5e2c1c, 0xa4502a, 0xe8893a, 0xffc764, 0xfff0b4, 0xffffff)
            {
                LayeredStage = PixelFxStage.Single,
                DefaultActorAnchor = PixelFxLayer.Lower,
                CreateLayerRenderer = GroundSlamEffect.CreateLayerRenderer,
            },
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
