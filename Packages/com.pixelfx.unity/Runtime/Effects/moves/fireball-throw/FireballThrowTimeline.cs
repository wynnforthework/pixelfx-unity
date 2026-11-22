using UnityEngine;

namespace PixelFx
{
    internal sealed class FireballThrowTimeline
    {
        internal const float ImpactDuration = 5.4f;
        internal readonly PixelFxHit[] Hits;
        internal readonly float CastTime, FlightTime;
        private readonly Vector3 castOrigin, launch, landing;
        private readonly Vector3? explicitLaunch;
        private readonly PixelFxActorBinding castBinding;
        private readonly float angle;
        private Vector3 flightLaunch;
        private float flightAngle;
        private bool launched;

        internal FireballThrowTimeline(PixelFxContext context, PixelFxSettings settings, Vector2 direction, float angle)
        {
            this.angle = angle;
            castBinding = context.CastBinding;
            explicitLaunch = context.LaunchPoint;
            castOrigin = castBinding.PositionOr(explicitLaunch ?? context.Position);
            launch = explicitLaunch ?? castOrigin;
            landing = context.LandingPoint ?? (launch + (Vector3)(direction * (156.4f / 32)));
            Hits = new PixelFxHit[context.Hits?.Count ?? 1];
            if (context.Hits == null) Hits[0] = new PixelFxHit(landing);
            else
                for (int i = 0; i < Hits.Length; i++)
                {
                    var hit = context.Hits[i];
                    Hits[i] = new PixelFxHit(hit.Binding.PositionOr(hit.Position), hit.Strength, hit.Binding);
                }
            CastTime = PixelFxTimeline.Limit(settings.castDuration, 0, 4, 0.95f);
            FlightTime = Vector3.Distance(launch, landing) / settings.flightSpeed;
        }

        internal bool Sample(int index, out PixelFxStage stage, ref float localTime,
            ref Vector3 position, out float rotation, out float strength)
        {
            rotation = angle;
            strength = 1;
            if (localTime < CastTime)
            {
                stage = PixelFxStage.Cast;
                position = castBinding.Follow ? castBinding.PositionOr(castOrigin) : castOrigin;
                if (castBinding.Actor != null && castBinding.Layer == PixelFxLayer.Middle) rotation = 0;
                return index == 0;
            }
            localTime -= CastTime;
            if (!launched)
            {
                launched = true;
                flightLaunch = explicitLaunch ?? (castBinding.Follow ? castBinding.PositionOr(launch) : launch);
                Vector2 travel = landing - flightLaunch;
                flightAngle = travel.sqrMagnitude > 0 ? Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg : angle;
            }
            if (localTime < FlightTime)
            {
                stage = PixelFxStage.Flight;
                position = Vector3.LerpUnclamped(flightLaunch, landing, localTime / FlightTime);
                rotation = flightAngle;
                return index == 0;
            }
            localTime -= FlightTime;
            stage = PixelFxStage.Impact;
            if (index >= Hits.Length) return false;
            var hit = Hits[index];
            position = hit.Binding.Follow ? hit.Binding.PositionOr(hit.Position) : hit.Position;
            rotation = 0;
            strength = PixelFxTimeline.Limit(Hits[index].Strength, 0, 1, 0);
            return strength > 0;
        }
    }
}
