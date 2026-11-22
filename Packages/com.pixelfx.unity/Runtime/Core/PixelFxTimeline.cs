using System;
using UnityEngine;

namespace PixelFx
{
    internal enum PixelFxStage { Single, Cast, Flight, Impact }

    internal sealed class PixelFxTimeline
    {
        internal readonly PixelFxSettings Settings;
        internal readonly PixelFxQuality Quality;
        internal readonly float Duration;
        internal readonly PixelFxDefinition Definition;
        private readonly Vector3 anchor;
        private readonly PixelFxActorBinding binding, castBinding;
        private readonly float angle;
        private readonly FireballThrowTimeline fireball;

        internal string Id => Definition.Id;
        internal PixelFxHit[] Hits => fireball?.Hits ?? Array.Empty<PixelFxHit>();
        internal float CastTime => fireball?.CastTime ?? 0;
        internal float FlightTime => fireball?.FlightTime ?? 0;

        internal PixelFxTimeline(string id, PixelFxSettings settings, PixelFxContext context)
        {
            var definition = PixelFxLibrary.Get(id);
            Definition = definition;
            Settings = settings?.Copy() ?? definition.Defaults();
            var defaults = definition.Defaults();
            if (Settings.palette == null || Settings.palette.Length != defaults.palette.Length)
                Settings.palette = defaults.palette;
            Settings.scale = Limit(Settings.scale, 0.15f, 4, 1);
            Settings.playbackSpeed = Limit(Settings.playbackSpeed, 0.1f, 4, 1);
            Settings.glow = Limit(Settings.glow, 0, 2, 1);
            Settings.bladeWidth = Limit(Settings.bladeWidth, 0.4f, 1.7f, 1);
            Settings.sparks = Limit(Settings.sparks, 0, 2, 1);
            Settings.flightSpeed = Limit(Settings.flightSpeed, 0.25f, 40, 10.625f);
            Settings.blastSize = Limit(Settings.blastSize, 0.3f, 2, 1);
            Settings.spin = Limit(Settings.spin, 0, 16, 3.5f);
            Settings.trailRate = Mathf.Clamp(Settings.trailRate, 0, 180);
            context ??= new PixelFxContext();
            Quality = context.Quality ?? PixelFxPlayer.DefaultQuality;
            binding = context.Binding;
            castBinding = context.CastBinding;
            anchor = binding.PositionOr(context.Position, definition.DefaultActorAnchor);
            Vector2 direction = context.Direction.sqrMagnitude > 0 ? context.Direction.normalized : Vector2.right;
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (id == PixelFxLibrary.FireballThrow)
            {
                fireball = new FireballThrowTimeline(context, Settings, direction, angle);
                Duration = (CastTime + FlightTime + definition.Duration) / Settings.playbackSpeed;
            }
            else
            {
                Duration = definition.Duration / Settings.playbackSpeed;
            }
        }

        internal int SurfaceCount => Quality == PixelFxQuality.Off ? 0 :
            fireball == null ? 1 : Mathf.Max(1, fireball.Hits.Length);

        internal bool Sample(float seconds, int index, out PixelFxStage stage, out float localTime,
            out Vector3 position, out float angle, out float strength)
        {
            localTime = seconds * Settings.playbackSpeed;
            position = binding.Follow ? binding.PositionOr(anchor, Definition.DefaultActorAnchor) : anchor;
            angle = this.angle;
            strength = 1;
            stage = PixelFxStage.Single;
            if (index >= SurfaceCount || seconds < 0 || seconds >= Duration) return false;
            return fireball == null || fireball.Sample(index, out stage, ref localTime, ref position, out angle, out strength);
        }

        internal PixelFxActorBinding BindingFor(PixelFxStage stage, int index)
        {
            switch (stage)
            {
                case PixelFxStage.Single: return binding;
                case PixelFxStage.Cast: return castBinding;
                case PixelFxStage.Impact: return Hits[index].Binding;
                default: return default;
            }
        }

        internal static float Limit(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
