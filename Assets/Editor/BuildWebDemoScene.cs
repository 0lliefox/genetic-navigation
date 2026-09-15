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

        ConfigureCameraForSmallScreens();
        PositionHud();
        ConvertToPlayback(manager);

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

    /// <summary>
    /// Spreads the coverage cells over the whole city.
    ///
    /// The scene holds a hundred cells laid out for a smaller city, so against the
    /// 13x13 grid the green "visited" overlay only covered a corner and read as a
    /// bug. Large City is left alone; this only reshapes the web demo's copy.
    /// </summary>
    private static void LayOutCoverageCells()
    {
        var grid = Object.FindFirstObjectByType<GridWithParams>();
        var cells = Object.FindObjectsByType<Cell>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (grid == null || grid.parameters == null || cells.Length == 0)
        {
            Debug.LogWarning("[WebDemo] no grid or cells found; leaving coverage overlay alone.");
            return;
        }

        int perSide = Mathf.RoundToInt(Mathf.Sqrt(cells.Length));
        if (perSide * perSide != cells.Length)
        {
            Debug.LogWarning($"[WebDemo] {cells.Length} cells is not a square number; " +
                             "leaving the coverage overlay alone.");
            return;
        }

        // Buildings sit every shapeWidth * margin units, spanning (count - 1) of
        // those. Add one spacing so the cells cover the outer edges too.
        float spacing = grid.parameters.shapeWidth * grid.parameters.marginBetweenShapes.x;
        float span = (grid.parameters.width - 1) * spacing + spacing;
        float cellSize = span / perSide;

        Vector3 origin = grid.transform.position - new Vector3(spacing * 0.5f, 0f, spacing * 0.5f);

        for (int i = 0; i < cells.Length; i++)
        {
            int row = i / perSide;
            int col = i % perSide;
            Transform t = cells[i].transform;

            t.position = new Vector3(
                origin.x + (col + 0.5f) * cellSize,
                t.position.y,
                origin.z + (row + 0.5f) * cellSize);

            // Cell art is a unit cube, so scale maps directly to world size.
            t.localScale = new Vector3(cellSize, t.localScale.y, cellSize);
        }

        Debug.Log($"[WebDemo] coverage cells: {perSide}x{perSide} of {cellSize:F0} units " +
                  $"across {span:F0} units of city");
    }

    /// <summary>
    /// Moves the stats readout into the top left instead of printing it across the
    /// middle of the city, which was unreadable on a narrow screen.
    /// </summary>
    private static void PositionHud()
    {
        var label = Object.FindFirstObjectByType<SetText>();
        if (label == null)
        {
            Debug.LogWarning("[WebDemo] no SetText in the scene; HUD left as it is.");
            return;
        }

        var rect = label.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(420f, 220f);

        var text = label.GetComponent<TMPro.TextMeshProUGUI>();
        if (text != null)
        {
            text.alignment = TMPro.TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            // The city behind is mid grey and varies, so give the text its own
            // contrast rather than relying on whatever it happens to sit over.
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 200);
        }

        Debug.Log("[WebDemo] HUD anchored to the top left");
    }

    /// <summary>
    /// Strips the live simulation and replaces it with recorded playback.
    ///
    /// The browser shows a recording rather than running the algorithm. Live
    /// training needs hundreds of generations before anything visibly changes, and
    /// replaying a trained network depends on where the goal happens to land, so
    /// neither reliably shows what the project does. A recording of a real run
    /// does, and costs nothing but moving transforms: no physics, no raycasts and
    /// no network evaluation in the browser at all.
    /// </summary>
    private static void ConvertToPlayback(Manager manager)
    {
        var recordingAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ImportRecording.Destination);
        if (recordingAsset == null)
        {
            Fail($"No recording at {ImportRecording.Destination}. " +
                 "Run ImportRecording first, then build this scene.");
            return;
        }

        // The goal keeps its look but stops placing itself; playback drives it.
        var placement = Object.FindFirstObjectByType<CheckPosition>();
        Transform goalMarker = placement != null ? placement.transform : null;
        if (placement != null)
        {
            Object.DestroyImmediate(placement);
        }

        // Coverage cells only turn green when a live agent triggers them, so they
        // would sit inert and misleading during playback.
        var cellManager = Object.FindFirstObjectByType<CellManager>();
        if (cellManager != null)
        {
            Object.DestroyImmediate(cellManager.gameObject);
        }

        var hud = Object.FindFirstObjectByType<SetText>();
        var agentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Agent.prefab");

        var playbackObject = new GameObject("Playback");
        var playback = playbackObject.AddComponent<RecordingPlayback>();

        var so = new SerializedObject(playback);
        so.FindProperty("recordingAsset").objectReferenceValue = recordingAsset;
        so.FindProperty("agentPrefab").objectReferenceValue = agentPrefab;
        so.FindProperty("goalMarker").objectReferenceValue = goalMarker;
        so.FindProperty("hud").objectReferenceValue = hud;
        so.FindProperty("mode").enumValueIndex = 0; // Progression
        so.ApplyModifiedPropertiesWithoutUndo();

        // Remove the genetic algorithm itself last, so the lookups above still work.
        if (manager != null)
        {
            Object.DestroyImmediate(manager.gameObject);
        }

        Debug.Log("[WebDemo] converted to recorded playback; live simulation removed");
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
