using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build entry points, so producing a player is a repeatable command rather than
/// a sequence of clicks in the Build Settings window.
/// </summary>
public static class BuildPlayer
{
    private static string[] EnabledScenes()
    {
        return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
    }

    private static string ArgValue(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }
        return fallback;
    }

    [MenuItem("Tools/Revival/Build WebGL")]
    public static void WebGL()
    {
        string output = ArgValue("-outputPath", "Build/WebGL");

        // Gzip with the decompression fallback is what the portfolio's hosting
        // needs: firebase.json sets no Content-Encoding headers, so the loader has
        // to decompress in JavaScript. Dropping the fallback works under a local
        // dev server and then fails in production.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.threadsSupport = false;

        // Custom template: fills the viewport on any screen size and caps the
        // device pixel ratio, which the stock template does not.
        PlayerSettings.WebGL.template = "PROJECT:GeneticNavigation";

        // The browser build ships the demo scene only. Large City keeps the
        // dissertation configuration and is for running experiments in the editor.
        string scene = ArgValue("-scene", "Assets/Scenes/Web Demo.unity");
        string[] scenes = System.IO.File.Exists(scene) ? new[] { scene } : EnabledScenes();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[Build] WebGL {summary.result} in {summary.totalTime}, " +
                  $"{summary.totalSize / 1024 / 1024} MB, scenes: {string.Join(", ", options.scenes)}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
