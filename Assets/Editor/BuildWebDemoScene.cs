using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates Assets/Scenes/Web Demo.unity, the browser-facing scene.
///
/// Built from code rather than by hand because .gitattributes marks scenes as
/// binary, so a hand-made scene commits as an unreadable blob. This way the scene
/// is reproducible, the settings that differ from the dissertation are stated in
/// one place, and it can be regenerated after any change to Large City.
///
/// Large City keeps the exact configuration the dissertation ran with and is never
/// modified by this.
/// </summary>
public static class BuildWebDemoScene
{
    private const string SourceScene = "Assets/Scenes/Large City.unity";
    private const string OutputScene = "Assets/Scenes/Web Demo.unity";

    // Browser tuning. The dissertation ran 100 agents at 20x speed, which measured
    // 11 fps in WebGL. These are the starting point; see the README for measured
    // figures.
    private const int DefaultPopulationSize = 50;
    private const float DefaultGameSpeed = 2f;
    private const float DefaultTimeframe = 12f;

    private static int WebPopulationSize => IntArg("-population", DefaultPopulationSize);
    private static float WebGameSpeed => FloatArg("-gameSpeed", DefaultGameSpeed);
    private static float WebTimeframe => FloatArg("-timeframe", DefaultTimeframe);

    private static string RawArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }

    private static int IntArg(string name, int fallback)
    {
        string v = RawArg(name);
        return v != null && int.TryParse(v, out int parsed) ? parsed : fallback;
    }

    private static float FloatArg(string name, float fallback)
    {
        string v = RawArg(name);
        return v != null && float.TryParse(v, out float parsed) ? parsed : fallback;
    }

    [MenuItem("Tools/Revival/Build Web Demo Scene")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

        var manager = Object.FindFirstObjectByType<Manager>();
        if (manager == null)
        {
            Fail($"No Manager in {SourceScene}; cannot build the web demo.");
            return;
        }

        ApplyBrowserTuning(manager);
        ConfigureCameraForSmallScreens();

        bool saved = EditorSceneManager.SaveScene(scene, OutputScene, true);
        if (!saved)
        {
            Fail($"Failed to save {OutputScene}.");
            return;
        }

        AddToBuildSettings();
        Debug.Log($"[WebDemo] wrote {OutputScene}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static void ApplyBrowserTuning(Manager manager)
    {
        var so = new SerializedObject(manager);
        Set(so, "populationSize", WebPopulationSize);
        Set(so, "GameSpeed", WebGameSpeed);
        Set(so, "timeframe", WebTimeframe);
        // Editor experiment plumbing that has no place in a public demo.
        Set(so, "maxStepCount", int.MaxValue);
        Set(so, "logGenerationStats", false);
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[WebDemo] population {WebPopulationSize}, game speed {WebGameSpeed}, " +
                  $"generation {WebTimeframe}s");
    }

    /// <summary>
    /// Adds the camera handling recovered from the original web build, which keeps
    /// the city framed on narrow screens by compensating orthographic size against
    /// aspect ratio.
    /// </summary>
    private static void ConfigureCameraForSmallScreens()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Fail("No MainCamera in the scene; cannot set up the web demo camera.");
            return;
        }

        if (!camera.orthographic)
        {
            Debug.LogWarning("[WebDemo] main camera is perspective; " +
                             "CameraScreenResolution only compensates orthographic size.");
        }

        var resolution = camera.GetComponent<CameraScreenResolution>();
        if (resolution == null)
        {
            resolution = camera.gameObject.AddComponent<CameraScreenResolution>();
        }

        var so = new SerializedObject(resolution);
        so.FindProperty("mainCamera").objectReferenceValue = camera;
        Set(so, "maintainWidth", true);
        Set(so, "adaptPosition", 0);
        so.ApplyModifiedPropertiesWithoutUndo();

        if (camera.GetComponent<ViewDrag>() == null)
        {
            camera.gameObject.AddComponent<ViewDrag>();
        }

        // CameraScreenResolution leaves narrow screens alone, so the city ran off
        // the edge on a phone. This measures the city and fits it to the viewport.
        var fit = camera.GetComponent<FitCameraToCity>();
        if (fit == null)
        {
            fit = camera.gameObject.AddComponent<FitCameraToCity>();
        }

        var fitSo = new SerializedObject(fit);
        fitSo.FindProperty("grid").objectReferenceValue = Object.FindFirstObjectByType<GridWithParams>();
        fitSo.ApplyModifiedPropertiesWithoutUndo();

        // FitCameraToCity supersedes it; leaving both on fights over the size.
        if (resolution != null)
        {
            resolution.enabled = false;
        }
    }

    private static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == OutputScene))
        {
            return;
        }
        scenes.Add(new EditorBuildSettingsScene(OutputScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void Set(SerializedObject so, string name, int value)
    {
        var p = Require(so, name);
        if (p != null) p.intValue = value;
    }

    private static void Set(SerializedObject so, string name, float value)
    {
        var p = Require(so, name);
        if (p != null) p.floatValue = value;
    }

    private static void Set(SerializedObject so, string name, bool value)
    {
        var p = Require(so, name);
        if (p != null) p.boolValue = value;
    }

    private static SerializedProperty Require(SerializedObject so, string name)
    {
        var p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogError($"[WebDemo] field '{name}' no longer exists on " +
                           $"{so.targetObject.GetType().Name}; the scene builder is out of date.");
        }
        return p;
    }

    private static void Fail(string message)
    {
        Debug.LogError("[WebDemo] " + message);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }
}
