using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PixelFx.Editor
{
    internal sealed class EffectPreviewScene : IDisposable
    {
        private readonly PreviewRenderUtility view;
        private readonly Transform root;
        private readonly Transform effectSpace;
        private readonly Transform targetAnchor;
        private GameObject actor;
        private Renderer actorRenderer;
        private Bounds actorBounds;
        private Bounds frameBounds;
        private Bounds effectBounds;
        private PixelFxPlayer.Playback playback;
        private PixelFxPlayer.Playback bodyPlayback;
        private PixelFxTimeline timeline;

        internal float Duration => timeline.Duration;
        internal float PlaybackSpeed => timeline.Settings.playbackSpeed;
        internal bool HasActorRenderer => actorRenderer != null;

        internal EffectPreviewScene()
        {
            view = new PreviewRenderUtility();
            var camera = view.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(24, 29, 42, 255);
            camera.fieldOfView = 35;
            camera.allowHDR = false;
            view.ambientColor = new Color(0.42f, 0.42f, 0.42f);
            view.lights[0].intensity = 1.4f;
            view.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
            view.lights[1].intensity = 0.55f;
            view.lights[1].transform.rotation = Quaternion.Euler(325, 155, 0);
            var container = new GameObject("PixelFX Preview") { hideFlags = HideFlags.HideAndDontSave };
            view.AddSingleGO(container);
            root = container.transform;
            effectSpace = CreateChild("Effect Placement");
            targetAnchor = CreateChild("Target Anchor");
        }

        private Transform CreateChild(string name)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(child, root.gameObject.scene);
            child.transform.SetParent(root, false);
            return child.transform;
        }

        internal void SetActor(GameObject source)
        {
            ClearPlayback();
            if (actor != null) Object.DestroyImmediate(actor);
            actor = null;
            actorRenderer = null;
            actorBounds = default;
            if (source == null) return;

            // Clone under an inactive parent so scene-driving components can be disabled first.
            var staging = CreateChild("Actor Preview").gameObject;
            staging.SetActive(false);
            try
            {
                actor = Object.Instantiate(source, staging.transform, false);
                actor.hideFlags = HideFlags.HideAndDontSave;
                actor.transform.SetLocalPositionAndRotation(Vector3.zero, source.transform.rotation);
                actor.transform.localScale = EditorUtility.IsPersistent(source) ? source.transform.localScale : source.transform.lossyScale;
                actor.SetActive(true);
                foreach (var component in actor.GetComponentsInChildren<Behaviour>(true))
                {
                    if (component == null) continue;
                    if (component is MonoBehaviour && IsSpine(component)) continue;
                    if (component is Animator || component is Animation || component is Camera
                        || component is Light || component is AudioSource || component is MonoBehaviour)
                        component.enabled = false;
                }
                staging.SetActive(true);
                InitializeSpine(actor);
                actor.transform.SetParent(root, true);
                RefreshActorBounds();
            }
            finally { Object.DestroyImmediate(staging); }
        }

        private void RefreshActorBounds()
        {
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !(renderer is SpriteRenderer || renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    continue;
                if (actorRenderer == null) actorBounds = renderer.bounds;
                else actorBounds.Encapsulate(renderer.bounds);
                if (actorRenderer == null || (!(actorRenderer is SpriteRenderer) && renderer is SpriteRenderer))
                    actorRenderer = renderer;
            }
            if (actorRenderer == null) return;
            // Keep a multipart 2D actor together between the front and back effect passes.
            if (actorRenderer is SpriteRenderer || IsSpineObject(actor))
            {
                var group = actor.GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (group == null)
                {
                    group = actor.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    group.sortingLayerID = actorRenderer.sortingLayerID;
                    group.sortingOrder = actorRenderer.sortingOrder;
                }
            }
        }

        internal void SetEffect(string id, PixelFxSettings settings, float distance, bool splash, PixelFxQuality quality,
            bool actorIsTarget)
        {
            ClearPlayback();
            var landing = Vector3.right * distance;
            var secondary = landing + new Vector3(2.15f, 0.75f);
            var context = new PixelFxContext { LandingPoint = landing, Quality = quality };
            if (splash) context.Hits = new[] { new PixelFxHit(landing), new PixelFxHit(secondary, 0.5f) };
            bool staged = id == PixelFxLibrary.FireballThrow;
            bool hitFlash = id == PixelFxLibrary.HitFlash;
            if (actor != null)
            {
                var destination = staged && actorIsTarget ? landing : Vector3.zero;
                var delta = destination - actor.transform.position;
                actor.transform.position = destination;
                actorBounds.center += delta;
                if (actorRenderer != null)
                {
                    var middle = new PixelFxActorBinding(actorRenderer, PixelFxLayer.Middle, root, follow: false);
                    var upper = new PixelFxActorBinding(actorRenderer, PixelFxLayer.Upper, root, follow: false);
                    if (!staged)
                        context.Binding = hitFlash ? upper : middle;
                    else if (actorIsTarget)
                    {
                        targetAnchor.position = landing;
                        upper = new PixelFxActorBinding(actorRenderer, PixelFxLayer.Upper, targetAnchor, follow: false);
                        context.Hits = splash
                            ? new[] { new PixelFxHit(landing, 1, upper), new PixelFxHit(secondary, 0.5f) }
                            : new[] { new PixelFxHit(landing, 1, upper) };
                    }
                    else
                        context.CastBinding = middle;
                }
            }
            bool separateBody = hitFlash && actorRenderer is SpriteRenderer;
            timeline = new PixelFxTimeline(id, settings, context, previewActor: actorRenderer == null,
                hitFlashPart: hitFlash && actorRenderer != null ? HitFlashPart.Accents : HitFlashPart.Combined);
            if (timeline.SurfaceCount > 0)
            {
                playback = new PixelFxPlayer.Playback(timeline, null, root, "Default", separateBody ? 1 : 0,
                    sortedParent: root, effectSpace: effectSpace);
                if (separateBody)
                {
                    var body = new PixelFxTimeline(id, settings, new PixelFxContext
                    {
                        Binding = new PixelFxActorBinding(actorRenderer, PixelFxLayer.Upper),
                        Quality = quality
                    }, hitFlashPart: HitFlashPart.Body);
                    bodyPlayback = new PixelFxPlayer.Playback(body, null, root, "Default", 0, sortedParent: root);
                }
            }

            var (width, height, x, y) = timeline.Definition.Canvas;
            float scale = timeline.Settings.scale / 32;
            var canvas = new Bounds(new Vector3(width / 2f - x, y - height / 2f) * scale,
                new Vector3(width, height) * scale);
            effectBounds = canvas;
            if (staged)
                foreach (var hit in timeline.Hits)
                    effectBounds.Encapsulate(new Bounds(canvas.center + hit.Position, canvas.size));
            FramePlacement();
        }

        internal void SetPlacement(Vector3 position, Vector3 rotation, float scale)
        {
            effectSpace.SetLocalPositionAndRotation(position, Quaternion.Euler(rotation));
            effectSpace.localScale = Vector3.one * scale;
        }

        internal void FramePlacement()
        {
            var center = effectSpace.TransformPoint(effectBounds.center);
            var x = effectSpace.TransformVector(Vector3.right * effectBounds.extents.x);
            var y = effectSpace.TransformVector(Vector3.up * effectBounds.extents.y);
            var size = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z)) * 2;
            frameBounds = new Bounds(center, size);
            if (actorRenderer != null) frameBounds.Encapsulate(actorBounds);
        }

        internal void Sample(float time)
        {
            if (playback == null) return;
            if (bodyPlayback != null)
            {
                bodyPlayback.Time = time;
                bodyPlayback.Draw();
            }
            playback.Time = time;
            playback.Draw();
        }

        internal void Draw(Rect rect, float time, PreviewCamera camera)
        {
            if (rect.width < 2 || rect.height < 2) return;
            Sample(time);
            camera.Apply(view.camera, frameBounds, rect.width / rect.height);
            view.BeginPreview(rect, GUIStyle.none);
            Texture image;
            try { view.Render(allowScriptableRenderPipeline: true, updatefov: false); }
            finally { image = view.EndPreview(); }
            if (image != null) GUI.DrawTexture(rect, image, ScaleMode.StretchToFill, false);
        }

        private static bool IsSpine(Behaviour component)
        {
            for (var type = component.GetType(); type != null; type = type.BaseType)
                if (type.FullName == "Spine.Unity.SkeletonRenderer")
                    return true;
            return false;
        }

        private static bool IsSpineObject(GameObject model)
        {
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>())
                if (behaviour != null && IsSpine(behaviour)) return true;
            return false;
        }

        private static void InitializeSpine(GameObject model)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>())
            {
                if (behaviour == null || !behaviour.enabled || !IsSpine(behaviour)) continue;
                try
                {
                    var type = behaviour.GetType();
                    var initialize = type.GetMethod("Initialize", flags, null, new[] { typeof(bool), typeof(bool) }, null);
                    if (initialize != null) initialize.Invoke(behaviour, new object[] { false, true });
                    else type.GetMethod("Initialize", flags, null, new[] { typeof(bool) }, null)
                        ?.Invoke(behaviour, new object[] { false });
                    type.GetMethod("LateUpdate", flags, null, Type.EmptyTypes, null)?.Invoke(behaviour, null);
                }
                catch (System.Reflection.TargetInvocationException error)
                {
                    Debug.LogWarning("PixelFX could not initialize the Spine preview: " + error.InnerException?.Message);
                }
            }
        }

        private void ClearPlayback()
        {
            playback?.Dispose();
            playback = null;
            bodyPlayback?.Dispose();
            bodyPlayback = null;
        }

        public void Dispose()
        {
            ClearPlayback();
            view.Cleanup();
        }
    }
}
