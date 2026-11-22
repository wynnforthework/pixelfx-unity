using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PixelFx.Editor
{
    internal static class PixelFxValidation
    {
        private const string TestAsset = "Assets/__PixelFxValidation.asset";
        private static readonly Vector3 Target = new Vector3(4.8875f, 0, 0);

        public static void Run()
        {
            Check(PixelFxLibrary.Effects.Count == 3, "Catalog");
            ValidateVariants();
            ValidateStages();
            ValidatePlayback();
            ImportSample();
            Debug.Log("PixelFX validation passed");
        }

        private static void ValidateVariants()
        {
            Check(AssetDatabase.LoadMainAssetAtPath(TestAsset) == null, "Validation asset path is available");
            var variant = ScriptableObject.CreateInstance<PixelFxVariant>();
            try
            {
                var settings = PixelFxLibrary.Get(PixelFxLibrary.HeavyCleave).Defaults();
                settings.bladeWidth = 1.5f;
                variant.Configure(PixelFxLibrary.HeavyCleave, settings);
                AssetDatabase.CreateAsset(variant, TestAsset);
                AssetDatabase.SaveAssets();
                Resources.UnloadAsset(variant);
                variant = AssetDatabase.LoadAssetAtPath<PixelFxVariant>(TestAsset);
                Check(variant.EffectId == PixelFxLibrary.HeavyCleave && variant.Settings.bladeWidth == 1.5f, "Variant persisted");
                Undo.RecordObject(variant, "PixelFX validation");
                settings.bladeWidth = 0.5f;
                variant.Configure(PixelFxLibrary.HeavyCleave, settings);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(variant.Settings.bladeWidth == 1.5f, "Variant undo");

                var output = Environment.GetEnvironmentVariable("PIXELFX_VALIDATION_OUTPUT");
                foreach (var effect in PixelFxLibrary.Effects)
                {
                    using var preview = new Preview(effect.Id, null, Target.x, true, PixelFxQuality.Full);
                    var thumbnail = preview.CaptureThumbnail(effect.ThumbnailTime);
                    Check(thumbnail.width > 0, "Thumbnail");
                    Object.DestroyImmediate(thumbnail);
                    var timeline = new PixelFxTimeline(effect.Id, null, new PixelFxContext { LandingPoint = Target });
                    using var surface = new PixelFxSurface(timeline, false);
                    float t = effect.ThumbnailTime;
                    Check(timeline.Sample(t, 0, out var stage, out float local, out _, out _, out float strength), "Can sample");
                    surface.Draw(stage, local, strength);
                    var expected = surface.Texture.GetRawTextureData();
                    surface.Draw(stage, local + 0.3f, strength);
                    surface.Draw(stage, local, strength);
                    Check(System.Linq.Enumerable.SequenceEqual(expected, surface.Texture.GetRawTextureData()), "Deterministic backwards seek");
                    if (string.IsNullOrEmpty(output)) continue;
                    Directory.CreateDirectory(output);
                    string name = effect.Id.Substring(effect.Id.IndexOf('/') + 1);
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), surface.Texture.EncodeToPNG());
                }
            }
            finally { AssetDatabase.DeleteAsset(TestAsset); }
        }

        private static void ValidateStages()
        {
            var hits = new[] { new PixelFxHit(Target), new PixelFxHit(Target + Vector3.up * 2, 0.5f) };
            var context = new PixelFxContext { LandingPoint = Target, Hits = hits };
            var timeline = new PixelFxTimeline(PixelFxLibrary.FireballThrow, null, context);
            Check(Mathf.Abs(timeline.FlightTime - 0.46f) < 0.001f, "Reference flight time");
            Check(timeline.Sample(0.5f, 0, out var stage, out _, out _, out _, out _) && stage == PixelFxStage.Cast, "Cast");
            Check(timeline.Sample(1.18f, 0, out stage, out _, out var mid, out _, out _) && stage == PixelFxStage.Flight
                && Mathf.Abs(mid.x - Target.x / 2) < 0.01f, "Flight midpoint");
            hits[1] = new PixelFxHit(Vector3.zero);
            Check(timeline.Sample(1.5f, 1, out stage, out _, out var hit, out _, out float strength)
                && stage == PixelFxStage.Impact && strength == 0.5f && hit.y == 2, "Impact snapshot");
            using var primary = new PixelFxSurface(timeline, false);
            using var secondary = new PixelFxSurface(timeline, false);
            primary.Draw(PixelFxStage.Impact, 0.12f, 1);
            secondary.Draw(PixelFxStage.Impact, 0.12f, 0.5f);
            Check(AlphaSum(primary.Texture) > AlphaSum(secondary.Texture), "Primary impact is visually stronger");
            context.LandingPoint = Target * 2;
            var far = new PixelFxTimeline(PixelFxLibrary.FireballThrow, null, context);
            Check(Mathf.Abs(far.FlightTime - timeline.FlightTime * 2) < 0.001f, "Distance scales time");
            context.LandingPoint = Vector3.zero;
            var zero = new PixelFxTimeline(PixelFxLibrary.FireballThrow, null, context);
            Check(zero.FlightTime == 0 && zero.Sample(zero.CastTime, 0, out stage, out _, out _, out _, out _)
                && stage == PixelFxStage.Impact, "Zero-distance transition");
        }

        private static long AlphaSum(Texture2D texture)
        {
            var bytes = texture.GetRawTextureData<byte>();
            long sum = 0;
            for (int i = 3; i < bytes.Length; i += 4) sum += bytes[i];
            return sum;
        }

        private static void ValidatePlayback()
        {
            var obj = new GameObject("PixelFX Validation", typeof(PixelFxPlayer));
            try
            {
                var player = obj.GetComponent<PixelFxPlayer>();
                foreach (var effect in PixelFxLibrary.Effects)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<PixelFxVariant>(EffectLibraryWindow.DefaultAssetPath(effect));
                    Check(asset != null && asset.EffectId == effect.Id, "Built-in asset: " + effect.Id);
                    Check(asset.Settings.seed == effect.Defaults().seed, "Built-in settings: " + effect.Id);
                    var context = new PixelFxContext { LandingPoint = Target, Quality = PixelFxQuality.Full };
                    var byId = player.Play(effect.Id, context: context);
                    var byAsset = player.Play(asset, context);
                    Check(byId.Duration == byAsset.Duration, "Built-in playback timing: " + effect.Id);
                    player.Tick(effect.ThumbnailTime);
                    var idTexture = obj.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite.texture;
                    var assetTexture = obj.transform.GetChild(1).GetComponent<SpriteRenderer>().sprite.texture;
                    Check(System.Linq.Enumerable.SequenceEqual(idTexture.GetRawTextureData(), assetTexture.GetRawTextureData()),
                        "Built-in playback frame: " + effect.Id);
                    byId.Stop();
                    byAsset.Stop();
                }
                foreach (PixelFxQuality quality in Enum.GetValues(typeof(PixelFxQuality)))
                {
                    var handle = player.Play(PixelFxLibrary.FireballThrow, context: new PixelFxContext { Quality = quality });
                    Check(obj.transform.childCount == (quality == PixelFxQuality.Off ? 0 : 1), "Surface count");
                    int completions = 0;
                    handle.Completed += _ => completions++;
                    player.Tick(handle.Duration);
                    Check(handle.IsDone && !handle.WasStopped && completions == 1 && obj.transform.childCount == 0, "Natural completion");
                    handle = player.Play(PixelFxLibrary.Shuriken, context: new PixelFxContext { Quality = quality });
                    handle.Stop();
                    Check(handle.IsDone && handle.WasStopped && obj.transform.childCount == 0, "Stop");
                }
            }
            finally { Object.DestroyImmediate(obj); }
        }

        private static void ImportSample()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PixelFxPlayer).Assembly);
            foreach (var sample in Sample.FindByPackage(package.name, package.version))
            {
                Check(sample.isImported || sample.Import(Sample.ImportOptions.None), "Samples import");
                Check(File.Exists(Path.Combine(sample.importPath, "PixelFxDemo.cs")), "Sample script imported");
                return;
            }
            throw new InvalidOperationException("No PixelFX sample registered.");
        }

        [MenuItem("Tools/PixelFX/Measure Quality (1, 10, 30)")]
        public static void Benchmark()
        {
            foreach (var quality in new[] { PixelFxQuality.Full, PixelFxQuality.Simplified })
            foreach (int count in new[] { 1, 10, 30 })
            {
                var surfaces = new PixelFxSurface[count];
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        var settings = PixelFxLibrary.Get(PixelFxLibrary.HeavyCleave).Defaults();
                        settings.seed += i;
                        var timeline = new PixelFxTimeline(PixelFxLibrary.HeavyCleave, settings, new PixelFxContext { Quality = quality });
                        surfaces[i] = new PixelFxSurface(timeline, false);
                        surfaces[i].Draw(PixelFxStage.Cleave, 0.5f, 1);
                    }
                    long raster = 0, upload = 0, allocated = 0;
                    for (int frame = 0; frame < 57; frame++)
                    {
                        foreach (var surface in surfaces)
                        {
                            long startBytes = GC.GetAllocatedBytesForCurrentThread();
                            long start = Stopwatch.GetTimestamp();
                            bool changed = surface.Render(PixelFxStage.Cleave, frame / 30f, 1);
                            long middle = Stopwatch.GetTimestamp();
                            if (changed) surface.Submit();
                            long end = Stopwatch.GetTimestamp();
                            if (frame == 0) continue;
                            raster += middle - start;
                            upload += end - middle;
                            allocated += GC.GetAllocatedBytesForCurrentThread() - startBytes;
                        }
                    }
                    double factor = 1000.0 / Stopwatch.Frequency / 56;
                    Debug.Log($"PixelFX {quality} x{count} | raster {raster * factor:F3} ms | submit {upload * factor:F3} ms | managed {allocated / 56.0:F0} B per sample");
                }
                finally { foreach (var surface in surfaces) surface?.Dispose(); }
            }
        }

        private static void Check(bool value, string test)
        {
            if (!value) throw new InvalidOperationException("PixelFX validation failed: " + test);
        }
    }
}
