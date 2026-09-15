using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates Assets/Scenes/Web Demo Live.unity: the trained network driving in
/// real time, with the city controls the reinforcement learning demo has.
///
/// The recorded playback scene cannot do this. A recording has its environment
/// baked in, so roadblocks cannot be changed and the agent cannot react to
/// anything. Running the network live is what lets a visitor rebuild the city or
/// clear the roadblocks and watch the agent deal with it.
///
/// This only became viable once training randomised the goal every generation.
/// Against a fixed goal a network scores well by memorising one route, and such a
/// network falls apart the moment anything moves. Having to reach a different
/// target each generation forces it to use its direction sensors, which is the
/// same thing that lets it cope with a rebuilt city.
/// </summary>
public static class BuildLiveDemoScene
{
    private const string SourceScene = "Assets/Scenes/Large City.unity";
    private const string OutputScene = "Assets/Scenes/Web Demo Live.unity";
    private const string NetworkAsset = "Assets/Resources/TrainedNetwork.txt";

    private static int IntArg(string name, int fallback)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out int v)) return v;
        }
        return fallback;
    }

    [MenuItem("Tools/Revival/Build Live Demo Scene")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

        var manager = Object.FindFirstObjectByType<Manager>();
        var grid = Object.FindFirstObjectByType<GridWithParams>();
        if (manager == null || grid == null)
        {
            Fail("Large City is missing its Manager or Grid.");
            return;
        }

        var network = AssetDatabase.LoadAssetAtPath<TextAsset>(NetworkAsset);
        if (network == null)
        {
            Fail($"No trained network at {NetworkAsset}. Run ImportTrainedNetwork first.");
            return;
        }

        var so = new SerializedObject(manager);
        so.FindProperty("trainedNetwork").objectReferenceValue = network;
        so.FindProperty("replayTrainedNetwork").boolValue = true;
        so.FindProperty("populationSize").intValue = IntArg("-population", 12);
        so.FindProperty("GameSpeed").floatValue = 1f;   // real time, so decisions are readable
        so.FindProperty("replayTimeframe").floatValue = 45f;
        so.FindProperty("logGenerationStats").boolValue = false;
        so.FindProperty("maxStepCount").intValue = int.MaxValue;
        so.FindProperty("recordTraining").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        ConfigureCamera();
        BuildControls(grid, manager);

        if (!EditorSceneManager.SaveScene(scene, OutputScene, true))
        {
            Fail($"Could not save {OutputScene}.");
            return;
        }

        AddToBuildSettings();
        Debug.Log($"[LiveDemo] wrote {OutputScene}");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        if (camera.GetComponent<FitCameraToCity>() == null)
        {
            var fit = camera.gameObject.AddComponent<FitCameraToCity>();
            var so = new SerializedObject(fit);
            so.FindProperty("grid").objectReferenceValue = Object.FindFirstObjectByType<GridWithParams>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (camera.GetComponent<ViewDrag>() == null) camera.gameObject.AddComponent<ViewDrag>();

        var resolution = camera.GetComponent<CameraScreenResolution>();
        if (resolution != null) resolution.enabled = false;
    }

    private static void BuildControls(GridWithParams grid, Manager manager)
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[LiveDemo] no Canvas; skipping controls.");
            return;
        }

        var label = Object.FindFirstObjectByType<SetText>();
        if (label != null)
        {
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(28f, -120f);
            rect.sizeDelta = new Vector2(420f, 220f);
        }

        var switcher = Object.FindFirstObjectByType<DemoSceneSwitch>()
                       ?? new GameObject("DemoSceneSwitch").AddComponent<DemoSceneSwitch>();

        Button(canvas.transform, "Rebuild city",     new Vector2(28f, 110f),  grid.BuildGrid);
        Button(canvas.transform, "Clear roadblocks", new Vector2(218f, 110f), grid.ClearRoadblocks);
        Button(canvas.transform, "Add roadblocks",   new Vector2(408f, 110f), grid.BuildRoadblocks);
        Button(canvas.transform, "Watch it learn",   new Vector2(28f, 40f),   switcher.ShowLearning);
    }

    private static void Button(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(180f, 44f);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var button = go.GetComponent<Button>();
        var colours = button.colors;
        colours.highlightedColor = new Color(1f, 1f, 1f, 0.35f);
        button.colors = colours;
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(button.onClick, action);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
    }

    private static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == OutputScene)) return;
        scenes.Add(new EditorBuildSettingsScene(OutputScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void Fail(string message)
    {
        Debug.LogError("[LiveDemo] " + message);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
