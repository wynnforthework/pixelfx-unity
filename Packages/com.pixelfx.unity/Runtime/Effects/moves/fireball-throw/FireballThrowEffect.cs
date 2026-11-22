using System;
using static System.Math;
using UnityEngine;

namespace PixelFx
{
    // Upstream chain at 5294518. The timeline supplies Unity's stage anchors and hit strengths.
    internal sealed class FireballThrowEffect
    {
        internal const int Width = 400, Height = 384, OriginX = 200, OriginY = 320;
        private const double ChargeStart = 0.08, ReferenceCast = 0.95;
        private static readonly uint[] ChargePalette =
            { 0x1f0b10, 0x4a1118, 0x8c1d1c, 0xcc3a1a, 0xf2701f, 0xffae35, 0xffe27a, 0xfff8d6 };

        private readonly PixelPainter painter, castPainter;
        private readonly SpellChargeEffect charge;
        private readonly FireballEffect projectile;
        private readonly PixelFxSettings blastSettings;
        private readonly bool full;
        private readonly double castStart;
        private double lastStrength;
        private SmallExplosionEffect blast;

        private FireballThrowEffect(PixelFxTimeline timeline)
        {
            var settings = timeline.Settings;
            full = timeline.Quality == PixelFxQuality.Full;
            painter = new PixelPainter(Width, Height, OriginX, OriginY) { LightStrength = settings.glow };
            castPainter = new PixelPainter(Width, Height, OriginX - SpellChargeEffect.HandX, OriginY - SpellChargeEffect.HandY)
                { LightStrength = settings.glow };
            // Start on the first 30 FPS sample at or after the cue, as upstream's chain does.
            castStart = Ceiling(ChargeStart * Min(1, timeline.CastTime / ReferenceCast) * 30) / 30;
            if (castStart >= timeline.CastTime) castStart = 0;

            var castSettings = settings.Copy();
            var flightSettings = PixelFxLibrary.Get(PixelFxLibrary.Fireball).Defaults();
            var original = PixelFxLibrary.Get(PixelFxLibrary.SmallExplosion).Defaults();
            bool customPalette = false;
            for (int i = 0; i < settings.palette.Length; i++)
                customPalette |= !settings.palette[i].Equals(original.palette[i]);
            if (customPalette)
            {
                flightSettings.palette[0] = settings.palette[0];
                flightSettings.palette[1] = Color.Lerp(settings.palette[0], settings.palette[1], 0.5f);
                Array.Copy(settings.palette, 1, flightSettings.palette, 2, settings.palette.Length - 1);
            }
            else
            {
                castSettings.palette = Array.ConvertAll(ChargePalette,
                    ink => (Color)new Color32((byte)(ink >> 16), (byte)(ink >> 8), (byte)ink, 255));
            }
            flightSettings.seed = unchecked(settings.seed + 1);
            blastSettings = settings.Copy();
            blastSettings.seed = unchecked(settings.seed + 2);

            charge = new SpellChargeEffect(castSettings);
            projectile = new FireballEffect(flightSettings, full, painter);
        }

        internal static PixelFxFrameRenderer CreateRenderer(PixelFxTimeline timeline)
        {
            var render = CreateLayerRenderer(timeline);
            return (pixels, stage, time, strength) => render(pixels, stage, time, strength, PixelFxRenderPass.Combined);
        }

        internal static PixelFxLayerRenderer CreateLayerRenderer(PixelFxTimeline timeline) =>
            new FireballThrowEffect(timeline).Render;

        private void Render(byte[] pixels, PixelFxStage stage, double time, double strength, PixelFxRenderPass pass)
        {
            var canvas = stage == PixelFxStage.Cast ? castPainter : painter;
            canvas.Clear();
            switch (stage)
            {
                case PixelFxStage.Cast:
                    if (time >= castStart) charge.Draw(canvas, time - castStart, full, pass);
                    break;
                case PixelFxStage.Flight:
                    projectile.Draw(time);
                    break;
                case PixelFxStage.Impact:
                    if (strength <= 0) break;
                    if (blast == null || strength != lastStrength)
                    {
                        blast = new SmallExplosionEffect(blastSettings, full, painter,
                            blastSettings.blastSize * Sqrt(strength), strength);
                        lastStrength = strength;
                    }
                    blast.Draw(time, PixelFxRenderPass.Combined);
                    break;
            }
            canvas.FadeLightEdges();
            canvas.Composite(pixels, true, false);
        }
    }
}
