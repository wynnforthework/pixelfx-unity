using System;
using UnityEngine;

namespace PixelFx
{
    internal enum PixelFxStage { Cleave, Shuriken, Cast, Flight, Impact }

    internal sealed class PixelFxTimeline
    {
        internal const float ImpactDuration = 5.4f;
        internal readonly PixelFxSettings Settings;
        internal readonly PixelFxQuality Quality;
        internal readonly Vector3 Anchor, Launch, Landing;
        internal readonly PixelFxHit[] Hits;
        internal readonly float Angle, FlightAngle, CastTime, FlightTime, Duration;
        internal readonly string Id;

        internal PixelFxTimeline(string id, PixelFxSettings settings, PixelFxContext context)
        {
            var definition = PixelFxLibrary.Get(id);
            Id = id;
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
            Anchor = context.Position;
            Vector2 direction = context.Direction.sqrMagnitude > 0 ? context.Direction.normalized : Vector2.right;
            Angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Launch = context.LaunchPoint ?? Anchor;
            Landing = context.LandingPoint ?? (Launch + (Vector3)(direction * (156.4f / 32)));
            Vector2 travel = Landing - Launch;
            FlightAngle = travel.sqrMagnitude > 0 ? Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg : Angle;
            if (id == PixelFxLibrary.FireballThrow)
            {
                Hits = new PixelFxHit[context.Hits?.Count ?? 1];
                if (context.Hits == null) Hits[0] = new PixelFxHit(Landing);
                else
                    for (int i = 0; i < Hits.Length; i++) Hits[i] = context.Hits[i];
                CastTime = Limit(Settings.castDuration, 0, 4, 0.95f);
                FlightTime = Vector3.Distance(Launch, Landing) / Settings.flightSpeed;
                Duration = (CastTime + FlightTime + ImpactDuration) / Settings.playbackSpeed;
            }
            else
            {
                Hits = Array.Empty<PixelFxHit>();
                Duration = (id == PixelFxLibrary.HeavyCleave ? (float)HeavyCleaveEffect.Duration : 1.2f) / Settings.playbackSpeed;
            }
        }

        internal int SurfaceCount => Quality == PixelFxQuality.Off ? 0 :
            Id == PixelFxLibrary.FireballThrow ? Mathf.Max(1, Hits.Length) : 1;

        internal bool Sample(float seconds, int index, out PixelFxStage stage, out float localTime,
            out Vector3 position, out float angle, out float strength)
        {
            localTime = seconds * Settings.playbackSpeed;
            position = Anchor;
            angle = Angle;
            strength = 1;
            stage = Id == PixelFxLibrary.HeavyCleave ? PixelFxStage.Cleave : PixelFxStage.Shuriken;
            if (index >= SurfaceCount || seconds < 0 || seconds >= Duration) return false;
            if (Id != PixelFxLibrary.FireballThrow) return true;
            if (localTime < CastTime)
            {
                stage = PixelFxStage.Cast;
                position = Launch;
                return index == 0;
            }
            localTime -= CastTime;
            if (localTime < FlightTime)
            {
                stage = PixelFxStage.Flight;
                position = Vector3.LerpUnclamped(Launch, Landing, localTime / FlightTime);
                angle = FlightAngle;
                return index == 0;
            }
            localTime -= FlightTime;
            stage = PixelFxStage.Impact;
            if (index >= Hits.Length) return false;
            position = Hits[index].Position;
            strength = Limit(Hits[index].Strength, 0, 1, 0);
            angle = 0;
            return strength > 0;
        }

        private static float Limit(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
