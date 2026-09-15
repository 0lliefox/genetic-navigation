using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Brings a training recording into the project as a TextAsset the build can load.
/// Recordings are written to persistentDataPath during training, which no build
/// can read.
/// </summary>
public static class ImportRecording
{
    public const string Destination = "Assets/Resources/TrainingRecording.bytes";

    private static string Arg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return fallback;
    }

    [MenuItem("Tools/Revival/Import Training Recording")]
    public static void Run()
    {
        string source = Arg("-recordingFile", "");
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            Fail($"No recording at '{source}'. Pass -recordingFile.");
            return;
        }

        byte[] data = File.ReadAllBytes(source);

        // Parse before importing. A recording that cannot be read would leave the
        // demo blank, and it is far easier to diagnose here than in a browser.
        var parsed = TrainingRecording.Deserialise(data);
        if (parsed == null || parsed.Generations.Count == 0)
        {
            Fail($"{source} did not parse as a recording.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Destination));
        File.WriteAllBytes(Destination, data);
        AssetDatabase.ImportAsset(Destination, ImportAssetOptions.ForceUpdate);

        int agents = parsed.Generations[0].Frames[0].Positions.Length;
        int frames = 0;
        foreach (var g in parsed.Generations) frames += g.Frames.Count;

        Debug.Log($"[Recording] {source} -> {Destination}");
        Debug.Log($"[Recording] {parsed.Generations.Count} generations, {agents} agents, " +
                  $"{frames} frames, {data.Length / 1024} KB");
        foreach (var g in parsed.Generations)
        {
            Debug.Log($"[Recording]   gen {g.Number}: {g.Frames.Count} frames, " +
                      $"goals {g.GoalsReached}, best fitness {g.BestFitness:G4}");
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static void Fail(string message)
    {
        Debug.LogError("[Recording] " + message);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }
}
