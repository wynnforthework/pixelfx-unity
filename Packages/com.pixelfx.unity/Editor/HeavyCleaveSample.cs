using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace PixelFx.Editor
{
    public static class HeavyCleaveSample
    {
        [MenuItem("GameObject/PixelFX/Heavy Cleave", false, 10)]
        private static void AddEffect(MenuCommand command)
        {
            var obj = new GameObject("Heavy Cleave", typeof(HeavyCleavePlayer));
            GameObjectUtility.SetParentAndAlign(obj, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(obj, "Add Heavy Cleave");
            Selection.activeGameObject = obj;
        }

        [MenuItem("Tools/PixelFX/Open Heavy Cleave Demo")]
        public static void OpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0.9f, 0.85f, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2.1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(24, 29, 42, 255);
            new GameObject("Heavy Cleave", typeof(HeavyCleavePlayer));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Tools/PixelFX/Measure Heavy Cleave (1, 10, 30)")]
        public static void Measure()
        {
            string output = Environment.GetEnvironmentVariable("PIXELFX_VALIDATION_OUTPUT");
            foreach (int count in new[] { 1, 10, 30 })
            {
                var players = new HeavyCleavePlayer[count];
                try
                {
                    for (int i = 0; i < count; i++)
                        players[i] = new GameObject("Heavy Cleave Benchmark", typeof(HeavyCleavePlayer))
                            .GetComponent<HeavyCleavePlayer>();
                    for (int run = 0; run < 2; run++)
                    {
                        for (int i = 0; i < count; i++) players[i].BeginManualPlayback(5 + i);
                        long bytes = GC.GetAllocatedBytesForCurrentThread();
                        long ticks = Stopwatch.GetTimestamp();
                        for (int frame = 1; frame < 58; frame++)
                            for (int i = 0; i < count; i++) players[i].RenderAt(frame / 30.0);
                        double millis = (Stopwatch.GetTimestamp() - ticks) * 1000.0 / Stopwatch.Frequency / 57;
                        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                        if (run == 1)
                            Debug.Log($"PixelFX x{count}: {millis:F3} ms/sample, {allocated / 57.0:F0} managed bytes/sample");
                    }
                    if (count == 1 && !string.IsNullOrEmpty(output))
                    {
                        Directory.CreateDirectory(output);
                        var player = players[0];
                        player.BeginManualPlayback(5);
                        var texture = player.GetComponent<SpriteRenderer>().sprite.texture;
                        for (int frame = 0; frame < 58; frame++)
                        {
                            player.RenderAt(frame / 30.0);
                            File.WriteAllBytes(Path.Combine(output, $"{frame:D2}.png"), texture.EncodeToPNG());
                        }
                    }
                }
                finally
                {
                    foreach (var player in players)
                        if (player != null) UnityEngine.Object.DestroyImmediate(player.gameObject);
                }
            }
        }
    }
}
