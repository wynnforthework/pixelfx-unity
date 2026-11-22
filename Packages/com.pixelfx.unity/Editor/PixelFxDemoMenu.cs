using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelFx.Editor
{
    internal static class PixelFxDemoMenu
    {
        private const string DemoScript = "PixelFxDemo.cs";

        [MenuItem("Tools/PixelFX/Open Demo")]
        private static void Open()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PixelFxPlayer).Assembly);
            var sample = Sample.FindByPackage(package.name, package.version)
                .Single(entry => entry.displayName == "PixelFX Demo");
            var type = System.Type.GetType("PixelFx.Samples.PixelFxDemo, Assembly-CSharp");
            string importedScript = Path.Combine(sample.importPath, DemoScript);
            if (!File.Exists(importedScript) && type == null)
            {
                if (!sample.Import(Sample.ImportOptions.None))
                    throw new System.InvalidOperationException("Could not import the PixelFX Demo sample.");
                ShowCompilationNotice("Sample imported.");
                return;
            }

            string packageScript = Path.Combine(sample.resolvedPath, DemoScript);
            if (File.Exists(importedScript) && DemoScriptDiffers(packageScript, importedScript))
            {
                int choice = EditorUtility.DisplayDialogComplex("PixelFX Demo",
                    "The imported demo script differs from the package. Updating replaces PixelFxDemo.cs, including any local edits to it.",
                    "Update Demo Script", "Cancel", "Open Existing Demo");
                if (choice == 1) return;
                if (choice == 0)
                {
                    UpdateDemoScript(packageScript, importedScript);
                    ShowCompilationNotice("Demo script updated.");
                    return;
                }
            }

            if (type == null)
            {
                ShowCompilationNotice("Waiting for the demo component.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(3.3f, 1.3f, -10);
            camera.orthographic = true;
            camera.orthographicSize = 4.9f;
            camera.backgroundColor = new Color32(24, 29, 42, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
            new GameObject("PixelFX Demo", type);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        internal static bool DemoScriptDiffers(string packageScript, string importedScript) =>
            !File.ReadLines(packageScript).SequenceEqual(File.ReadLines(importedScript));

        internal static void UpdateDemoScript(string packageScript, string importedScript)
        {
            File.Copy(packageScript, importedScript, overwrite: true);
            AssetDatabase.Refresh();
        }

        private static void ShowCompilationNotice(string message) =>
            EditorUtility.DisplayDialog("PixelFX Demo", message + " Open this menu again after Unity compiles it.", "OK");
    }
}
