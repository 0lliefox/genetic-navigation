using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves between the two halves of the demo.
///
/// They are separate scenes rather than one scene toggling objects because the
/// live half runs the genetic algorithm and the recorded half runs none of it.
/// Enabling and disabling a half-initialised Manager is far more fragile than
/// simply loading the other scene.
/// </summary>
public class DemoSceneSwitch : MonoBehaviour
{
    public const string PlaybackScene = "Web Demo";
    public const string LiveScene = "Web Demo Live";

    /// <summary>Recorded playback of a real training run, showing the population improve.</summary>
    public void ShowLearning()
    {
        Load(PlaybackScene);
    }

    /// <summary>The trained network driving live, where the city can be changed.</summary>
    public void ShowLiveAgent()
    {
        Load(LiveScene);
    }

    private void Load(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            // Time scale is set by whichever scene was running; reset it so the
            // next one does not inherit a fast-forward.
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"Scene '{sceneName}' is not in the build settings.");
        }
    }
}
