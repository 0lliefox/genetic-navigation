using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only maintenance used during the 2026 revival of this project.
///
/// Scenes and ProjectSettings are marked binary by .gitattributes, so changes
/// to them produce no reviewable diff. Making those changes from code instead
/// of by hand keeps them reproducible and lets the commit explain itself.
/// </summary>
public static class ProjectMaintenance
{
    private const string ScenesFolder = "Assets/Scenes";

    private static IEnumerable<string> ProjectScenePaths()
    {
        return AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p);
    }

    /// <summary>
    /// Strips MonoBehaviour components whose script no longer resolves.
    ///
    /// The Manager object in "Large City" carries one such component, left by
    /// GridSearch.cs which was deleted in bfafe35. It is already disabled, and
    /// grid search is a batch-experiment tool with no role in the revival.
    /// </summary>
    [MenuItem("Tools/Revival/Remove Missing Scripts From All Scenes")]
    public static void RemoveMissingScripts()
    {
        var removedTotal = 0;

        foreach (var path in ProjectScenePaths())
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var removedHere = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    removedHere += GameObjectUtility
                        .RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                }
            }

            if (removedHere > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Revival] {path}: removed {removedHere} missing script(s)");
            }
            else
            {
                Debug.Log($"[Revival] {path}: clean");
            }

            removedTotal += removedHere;
        }

        Debug.Log($"[Revival] Removed {removedTotal} missing script component(s) in total.");
    }

    /// <summary>
    /// Rebuilds the scene list from what is actually on disk.
    ///
    /// The list was inherited when this project was forked from
    /// reinforcement-navigation: 12 entries, 11 naming scenes that never
    /// existed here, and the single enabled entry pointing at a phantom. A
    /// build in that state produces a player containing no scenes at all.
    /// </summary>
    [MenuItem("Tools/Revival/Rebuild Scene List")]
    public static void RebuildSceneList()
    {
        var scenes = ProjectScenePaths()
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError($"[Revival] No scenes found under {ScenesFolder}; refusing to " +
                           "write an empty scene list.");
            return;
        }

        EditorBuildSettings.scenes = scenes;
        AssetDatabase.SaveAssets();

        foreach (var scene in scenes)
        {
            Debug.Log($"[Revival] build scene: {scene.path} (enabled: {scene.enabled})");
        }
    }
}
