using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelFx.Editor
{
    internal static class PixelFxDemoMenu
    {
        [MenuItem("Tools/PixelFX/Open Demo")]
        private static void Open()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PixelFxPlayer).Assembly);
            var type = System.Type.GetType("PixelFx.Samples.PixelFxDemo, Assembly-CSharp");
            if (type == null)
            {
                foreach (var sample in Sample.FindByPackage(package.name, package.version))
                {
                    if (!sample.Import(Sample.ImportOptions.None))
                        throw new System.InvalidOperationException("Could not import PixelFX samples.");
                }
                EditorUtility.DisplayDialog("PixelFX Demo", "Sample imported. Open this menu again after Unity compiles it.", "OK");
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
    }
}
