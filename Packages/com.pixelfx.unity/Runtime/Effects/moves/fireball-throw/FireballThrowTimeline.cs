using UnityEngine;

namespace PixelFx
{
    internal sealed class FireballThrowTimeline
    {
        internal const float ImpactDuration = 5.4f;
        internal readonly PixelFxHit[] Hits;
        internal readonly float CastTime, FlightTime;
        private readonly Vector3 launch, landing;
        private readonly float angle, flightAngle;

        internal FireballThrowTimeline(PixelFxContext context, PixelFxSettings settings, Vector2 direction, float angle)
        {
            this.angle = angle;
            launch = context.LaunchPoint ?? context.Position;
            landing = context.LandingPoint ?? (launch + (Vector3)(direction * (156.4f / 32)));
            Vector2 travel = landing - launch;
            flightAngle = travel.sqrMagnitude > 0 ? Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg : angle;
            Hits = new PixelFxHit[context.Hits?.Count ?? 1];
            if (context.Hits == null) Hits[0] = new PixelFxHit(landing);
            else
                for (int i = 0; i < Hits.Length; i++) Hits[i] = context.Hits[i];
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
                position = launch;
                return index == 0;
            }
            localTime -= CastTime;
            if (localTime < FlightTime)
            {
                stage = PixelFxStage.Flight;
                position = Vector3.LerpUnclamped(launch, landing, localTime / FlightTime);
                rotation = flightAngle;
                return index == 0;
            }
            localTime -= FlightTime;
            stage = PixelFxStage.Impact;
            if (index >= Hits.Length) return false;
            position = Hits[index].Position;
            rotation = 0;
            strength = PixelFxTimeline.Limit(Hits[index].Strength, 0, 1, 0);
            return strength > 0;
        }
    }
}
