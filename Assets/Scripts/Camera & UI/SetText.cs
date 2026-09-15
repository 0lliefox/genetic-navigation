using System;
using TMPro;
using UnityEngine;

public class SetText : MonoBehaviour
{
    [SerializeField, Tooltip("Seconds of real time between HUD refreshes.")]
    private float refreshInterval = 0.1f;

    private TextMeshProUGUI label;
    private float nextRefreshTime;
    private string lastText;

    private void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// Updates the HUD, at most once per refreshInterval.
    ///
    /// Manager calls this from FixedUpdate, so it used to run on every physics
    /// step: at a game speed of 20 that is roughly a thousand times a second, each
    /// one doing a GetComponent and forcing TextMeshPro to reparse and re-lay out
    /// the whole string. Unscaled time is used so the refresh rate stays the same
    /// however fast the simulation is running.
    /// </summary>
    public void SetInfoText(string generation, string fitness, double timeElapsed, int numberOfGoals)
    {
        if (label == null || Time.unscaledTime < nextRefreshTime)
        {
            return;
        }
        nextRefreshTime = Time.unscaledTime + refreshInterval;

        // https://stackoverflow.com/questions/463642/how-can-i-convert-seconds-into-hourminutessecondsmilliseconds-time
        TimeSpan time = TimeSpan.FromSeconds(timeElapsed);
        string str = time.ToString(@"hh\:mm\:ss\:fff");

        string text =
            $"<b><size=150%>Genetic Algorithm Demonstration</size></b>\r\n\r\n" +
            $"<b>Current Generation:</b> {generation}\r\n" +
            $"<b>Last Max Fitness:</b> {fitness}\r\n" +
            $"<b>Time Elapsed:</b> {str}\r\n" +
            $"<b>No. of Goals:</b> {numberOfGoals}\r\n";

        if (text == lastText)
        {
            return;
        }

        lastText = text;
        label.text = text;
    }
}
