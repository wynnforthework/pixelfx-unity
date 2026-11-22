using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelFx
{
    public readonly struct PixelFxHit
    {
        public Vector3 Position { get; }
        public float Strength { get; }
        public PixelFxActorBinding Binding { get; }

        public PixelFxHit(Vector3 position, float strength = 1)
            : this(position, strength, default) { }

        public PixelFxHit(Vector3 position, float strength, PixelFxActorBinding binding)
        {
            Position = position;
            Strength = strength;
            Binding = binding;
        }
    }

    public sealed class PixelFxContext
    {
        public Vector3 Position { get; set; }
        public Vector2 Direction { get; set; } = Vector2.right;
        public Vector3? LaunchPoint { get; set; }
        public Vector3? LandingPoint { get; set; }
        public PixelFxActorBinding Binding { get; set; }
        public PixelFxActorBinding CastBinding { get; set; }
        // Null uses the landing point. An empty list means no visible impact.
        public IReadOnlyList<PixelFxHit> Hits { get; set; }
        public PixelFxQuality? Quality { get; set; }
    }

    public sealed class PixelFxPlayback
    {
        public float Duration { get; }
        public bool IsDone { get; private set; }
        public bool WasStopped { get; private set; }
        public event Action<PixelFxPlayback> Completed;

        internal PixelFxPlayback(float duration) => Duration = duration;

        public void Stop() => Finish(true);

        internal void Finish(bool stopped)
        {
            if (IsDone) return;
            IsDone = true;
            WasStopped = stopped;
            var callback = Completed;
            Completed = null;
            callback?.Invoke(this);
        }
    }
}
