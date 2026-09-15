using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Brings a network produced by a training run into the project and wires it to
/// the web demo, so the browser build can replay it.
///
/// Training writes to persistentDataPath, because Assets/Save is gitignored and
/// no Assets folder exists in a player build. This copies the file into
/// Resources, where it becomes a TextAsset the build can load.
/// </summary>
public static class ImportTrainedNetwork
{
    private const string Destination = "Assets/Resources/TrainedNetwork.txt";
    private const string WebDemoScene = "Assets/Scenes/Web Demo.unity";

    private static string Arg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return fallback;
    }

    [MenuItem("Tools/Revival/Import Trained Network")]
    public static void Run()
    {
        string defaultSource = Path.Combine(
            Application.persistentDataPath.Replace("/Genetic Navigation", "/Genetic Navigation"),
            "best-network.txt");
        string source = Arg("-networkFile", defaultSource);

        if (!File.Exists(source))
        {
            Fail($"No network file at {source}. Run training first.");
            return;
        }

        string contents = File.ReadAllText(source);

        // Never ship a file without checking it loads. A network that fails to
        // parse would leave the demo showing untrained agents, which looks like
        // the algorithm does not work rather than like a broken file.
        int[] layers = { CarController.LAYERS, 10, 5 };
        var probe = new NeuralNetwork(layers, 0);
        if (!probe.LoadFrom(contents, source))
        {
            Fail($"{source} did not load into a {string.Join(",", layers)} network; not importing.");
            return;
        }

        File.WriteAllText(Destination, contents);
        AssetDatabase.ImportAsset(Destination, ImportAssetOptions.ForceUpdate);

        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Destination);
        if (asset == null)
        {
            Fail($"Imported {Destination} but Unity did not produce a TextAsset.");
            return;
        }

        AssignToWebDemo(asset);

        Debug.Log($"[Import] {source} -> {Destination}, " +
                  $"{probe.TotalParameterCount()} parameters, verified loadable.");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static void AssignToWebDemo(TextAsset asset)
    {
        if (!File.Exists(WebDemoScene))
        {
            Debug.LogWarning($"[Import] {WebDemoScene} does not exist yet; " +
                             "run BuildWebDemoScene, then import again.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(WebDemoScene, OpenSceneMode.Single);
        var manager = UnityEngine.Object.FindFirstObjectByType<Manager>();
        if (manager == null)
        {
            Debug.LogWarning($"[Import] no Manager in {WebDemoScene}.");
            return;
        }

        var so = new SerializedObject(manager);
        so.FindProperty("trainedNetwork").objectReferenceValue = asset;
        so.FindProperty("replayTrainedNetwork").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Import] web demo now starts in replay mode.");
    }

    private static void Fail(string message)
    {
        Debug.LogError("[Import] " + message);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }
}
