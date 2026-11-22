using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelFx.Editor
{
    internal sealed class EffectLibraryWindow : EditorWindow
    {
        [SerializeField] private string selectedId = PixelFxLibrary.HeavyCleave;
        [SerializeField] private PixelFxVariant selectedVariant;
        [SerializeField] private string search = "";
        [SerializeField] private bool loop = true;
        [SerializeField] private PixelFxQuality quality;
        [SerializeField] private float targetDistance = 156.4f / 32;
        [SerializeField] private bool splash = true;
        private readonly Dictionary<string, Texture2D> thumbnails = new Dictionary<string, Texture2D>();
        private readonly List<PixelFxVariant> variants = new List<PixelFxVariant>();
        private PixelFxSettings draft;
        private Preview preview;
        private Vector2 gridScroll, settingsScroll;
        private float time;
        private bool playing;
        private double previous;

        [MenuItem("Tools/PixelFX/Effect Library")]
        private static void Open() => GetWindow<EffectLibraryWindow>("PixelFX Library");

        private void OnEnable()
        {
            minSize = new Vector2(700, 420);
            RefreshVariants();
            Select(selectedId, selectedVariant);
            EditorApplication.update += Tick;
            EditorApplication.projectChanged += RefreshVariants;
            Undo.undoRedoPerformed += Reload;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.projectChanged -= RefreshVariants;
            Undo.undoRedoPerformed -= Reload;
            preview?.Dispose();
            preview = null;
            ClearThumbnails();
        }

        private void ClearThumbnails()
        {
            foreach (var texture in thumbnails.Values) DestroyImmediate(texture);
            thumbnails.Clear();
        }

        private void RefreshVariants()
        {
            variants.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:PixelFxVariant"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<PixelFxVariant>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && PixelFxLibrary.Contains(asset.EffectId)) variants.Add(asset);
            }
            variants.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            ClearThumbnails();
            Repaint();
        }

        private void Reload()
        {
            RefreshVariants();
            Select(selectedId, selectedVariant);
        }

        private void Select(string id, PixelFxVariant variant)
        {
            selectedId = PixelFxLibrary.Contains(id) ? id : PixelFxLibrary.HeavyCleave;
            selectedVariant = variant;
            draft = variant != null ? variant.Settings : PixelFxLibrary.Get(selectedId).Defaults();
            draft ??= PixelFxLibrary.Get(selectedId).Defaults();
            time = 0;
            playing = false;
            Rebuild();
        }

        private void Rebuild()
        {
            preview?.Dispose();
            preview = new Preview(selectedId, draft, targetDistance, splash, quality);
            time = Mathf.Clamp(time, 0, preview.Duration);
            Repaint();
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - previous);
            previous = now;
            if (!playing || preview == null) return;
            time += Mathf.Min(dt, 0.1f);
            if (time >= preview.Duration)
            {
                if (loop) time %= preview.Duration;
                else { time = preview.Duration; playing = false; }
            }
            Repaint();
        }

        private void OnGUI()
        {
            if (preview == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawGrid();
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawPreview();
                    DrawParameters();
                }
            }
        }

        private void DrawGrid()
        {
            float width = Mathf.Clamp(position.width * 0.37f, 245, 400);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                var matches = new List<(PixelFxDefinition Definition, PixelFxVariant Variant)>();
                foreach (var effect in PixelFxLibrary.Effects)
                    if (Matches(effect, effect.Name)) matches.Add((effect, null));
                foreach (var variant in variants)
                {
                    if (variant == null) continue;
                    var effect = PixelFxLibrary.Get(variant.EffectId);
                    if (Matches(effect, variant.name)) matches.Add((effect, variant));
                }
                gridScroll = EditorGUILayout.BeginScrollView(gridScroll);
                int columns = Mathf.Max(2, Mathf.FloorToInt((width - 18) / 123));
                float tileWidth = (width - 18) / columns - 5;
                for (int i = 0; i < matches.Count; i += columns)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int j = i; j < Mathf.Min(i + columns, matches.Count); j++)
                        {
                            var (effect, variant) = matches[j];
                            Rect tile = GUILayoutUtility.GetRect(tileWidth, 95, GUILayout.Width(tileWidth));
                            bool selected = selectedId == effect.Id && selectedVariant == variant;
                            string name = variant == null ? effect.Name : variant.name;
                            if (GUI.Button(tile, new GUIContent("", name + " / " + effect.Category)))
                                Select(effect.Id, variant);
                            if (Event.current.type != EventType.Repaint) continue;
                            Rect image = new Rect(tile.x + 3, tile.y + 3, tile.width - 6, 67);
                            GUI.DrawTexture(image, Thumbnail(effect, variant), ScaleMode.ScaleToFit);
                            var label = new Rect(tile.x + 6, tile.y + 72, tile.width - 12, 18);
                            GUI.Label(label, name, EditorStyles.miniLabel);
                            if (selected) EditorGUI.DrawRect(new Rect(tile.x + 3, tile.yMax - 3, tile.width - 6, 2), new Color(0.35f, 0.68f, 0.92f));
                        }
                    }
                }
                if (matches.Count == 0) GUILayout.Label("No matching effects", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
            }
        }

        private bool Matches(PixelFxDefinition effect, string name) =>
            (effect.Name + " " + effect.Id + " " + effect.Category + " " + name)
                .IndexOf(search ?? "", StringComparison.OrdinalIgnoreCase) >= 0;

        private Texture2D Thumbnail(PixelFxDefinition effect, PixelFxVariant variant)
        {
            string key = variant == null ? effect.Id : AssetDatabase.GetAssetPath(variant);
            if (thumbnails.TryGetValue(key, out var cached)) return cached;
            using var source = new Preview(effect.Id, variant == null ? null : variant.Settings, 156.4f / 32, false, PixelFxQuality.Full);
            var texture = source.CaptureThumbnail(effect.ThumbnailTime);
            thumbnails[key] = texture;
            return texture;
        }

        private void DrawPreview()
        {
            Rect rect = GUILayoutUtility.GetRect(300, Mathf.Clamp(position.height * 0.42f, 170, 340), GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) preview.Draw(rect, time);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent(playing ? "Ⅱ" : "▶", "Play / Pause"), EditorStyles.toolbarButton, GUILayout.Width(32)))
                {
                    if (time >= preview.Duration) time = 0;
                    playing = !playing;
                }
                if (GUILayout.Button(new GUIContent("◀", "Previous frame"), EditorStyles.toolbarButton, GUILayout.Width(28))) Step(-1);
                if (GUILayout.Button(new GUIContent("▶|", "Next frame"), EditorStyles.toolbarButton, GUILayout.Width(28))) Step(1);
                loop = GUILayout.Toggle(loop, "Loop", EditorStyles.toolbarButton, GUILayout.Width(48));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{time:F2} / {preview.Duration:F2} s", EditorStyles.miniLabel);
            }
            EditorGUI.BeginChangeCheck();
            float next = EditorGUILayout.Slider(time, 0, preview.Duration);
            if (EditorGUI.EndChangeCheck()) { playing = false; time = next; }
        }

        private void Step(int direction)
        {
            playing = false;
            time = Mathf.Clamp(time + direction / (30 * preview.PlaybackSpeed), 0, preview.Duration);
        }

        private void DrawParameters()
        {
            var effect = PixelFxLibrary.Get(selectedId);
            var defaults = effect.Defaults();
            if (draft.palette == null || draft.palette.Length != defaults.palette.Length) draft.palette = defaults.palette;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(selectedVariant == null ? effect.Name : selectedVariant.name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Defaults", GUILayout.Width(66))) { draft = defaults; Rebuild(); }
                using (new EditorGUI.DisabledScope(selectedVariant == null || !AssetDatabase.GetAssetPath(selectedVariant).StartsWith("Assets/", StringComparison.Ordinal)))
                    if (GUILayout.Button("Update Variant", GUILayout.Width(108))) SaveVariant(true);
                if (GUILayout.Button("Save As Variant", GUILayout.Width(110))) SaveVariant(false);
            }
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 125;
            EditorGUI.BeginChangeCheck();
            draft.seed = EditorGUILayout.IntField("Seed", draft.seed);
            draft.scale = EditorGUILayout.Slider("Scale", draft.scale, 0.15f, 4);
            draft.playbackSpeed = EditorGUILayout.Slider("Playback Speed", draft.playbackSpeed, 0.1f, 4);
            draft.glow = EditorGUILayout.Slider("Glow", draft.glow, 0, 2);
            quality = (PixelFxQuality)EditorGUILayout.EnumPopup("Preview Quality", quality);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Palette", GUILayout.Width(120));
                for (int i = 0; i < draft.palette.Length; i++)
                    draft.palette[i] = EditorGUILayout.ColorField(GUIContent.none, draft.palette[i], false, false, false, GUILayout.MaxWidth(42));
            }
            switch (selectedId)
            {
                case PixelFxLibrary.HeavyCleave:
                    draft.bladeWidth = EditorGUILayout.Slider("Blade Width", draft.bladeWidth, 0.4f, 1.7f);
                    draft.sparks = EditorGUILayout.Slider("Spark Density", draft.sparks, 0, 2);
                    break;
                case PixelFxLibrary.FireballThrow:
                    draft.castDuration = EditorGUILayout.Slider("Cast Duration", draft.castDuration, 0, 4);
                    draft.flightSpeed = EditorGUILayout.Slider("Flight Speed", draft.flightSpeed, 0.25f, 40);
                    draft.blastSize = EditorGUILayout.Slider("Blast Size", draft.blastSize, 0.3f, 2);
                    targetDistance = EditorGUILayout.Slider("Target Distance", targetDistance, 0, 12);
                    splash = EditorGUILayout.Toggle("Secondary Target", splash);
                    break;
                case PixelFxLibrary.Shuriken:
                    draft.spin = EditorGUILayout.Slider("Spins / Second", draft.spin, 0, 16);
                    draft.trailRate = EditorGUILayout.IntSlider("Trail Density", draft.trailRate, 0, 180);
                    break;
            }
            if (EditorGUI.EndChangeCheck()) Rebuild();
            EditorGUIUtility.labelWidth = originalLabelWidth;
            EditorGUILayout.EndScrollView();
        }

        private void SaveVariant(bool update)
        {
            if (update)
            {
                Undo.RecordObject(selectedVariant, "Update PixelFX Variant");
                selectedVariant.Configure(selectedId, draft);
                EditorUtility.SetDirty(selectedVariant);
            }
            else
            {
                string path = EditorUtility.SaveFilePanelInProject("Save PixelFX Variant",
                    PixelFxLibrary.Get(selectedId).Name + " Variant", "asset", "");
                if (string.IsNullOrEmpty(path)) return;
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                var variant = CreateInstance<PixelFxVariant>();
                variant.Configure(selectedId, draft);
                AssetDatabase.CreateAsset(variant, path);
                selectedVariant = variant;
            }
            AssetDatabase.SaveAssets();
            RefreshVariants();
        }
    }
}
