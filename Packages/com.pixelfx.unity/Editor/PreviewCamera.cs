using System;
using UnityEngine;

namespace PixelFx.Editor
{
    [Serializable]
    internal sealed class PreviewCamera
    {
        [SerializeField] private Vector2 orbit;
        [SerializeField] private Vector2 pan;
        [SerializeField] private float zoom = 1;
        [SerializeField] internal bool Perspective;

        internal void Frame()
        {
            pan = Vector2.zero;
            zoom = 1;
        }

        internal void Front() => orbit = Vector2.zero;

        internal void Orbit(Vector2 delta)
        {
            orbit.x = Mathf.Repeat(orbit.x + delta.x * 0.5f, 360);
            orbit.y = Mathf.Clamp(orbit.y + delta.y * 0.5f, -89, 89);
        }

        internal void Pan(Vector2 delta, float height) => pan += delta / Mathf.Max(1, height);

        internal void Zoom(float delta, Vector2 cursor)
        {
            float next = Mathf.Clamp(zoom * Mathf.Exp(delta * 0.075f), 0.05f, 20);
            pan = cursor + (pan - cursor) * (zoom / next);
            zoom = next;
        }

        internal void Apply(Camera camera, Bounds bounds, float aspect)
        {
            var rotation = Quaternion.Euler(orbit.y, orbit.x, 0);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;
            float fit = Mathf.Max(Project(bounds.extents, up), Project(bounds.extents, right) / aspect);
            float depth = Project(bounds.extents, forward);
            float halfHeight = Mathf.Max(0.05f, fit) * 1.12f * zoom;
            // Keep the camera in front of its subject when zooming in.
            float distance = depth + (Perspective ? halfHeight / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) : 1);
            var center = bounds.center + (-pan.x * right + pan.y * up) * (2 * halfHeight);
            camera.orthographic = !Perspective;
            camera.orthographicSize = halfHeight;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = distance + depth + 20;
            camera.transform.SetPositionAndRotation(center - forward * distance, rotation);
        }

        private static float Project(Vector3 extents, Vector3 axis) =>
            extents.x * Mathf.Abs(axis.x) + extents.y * Mathf.Abs(axis.y) + extents.z * Mathf.Abs(axis.z);
    }
}
