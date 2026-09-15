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
    /// Readout for recorded playback, which has no live fitness or elapsed time.
    /// </summary>
    public void SetPlaybackText(int generation, int goalsReached, float bestFitness, bool progression)
    {
        if (label == null || Time.unscaledTime < nextRefreshTime)
        {
            return;
        }
        nextRefreshTime = Time.unscaledTime + refreshInterval;

        // Goals here are the ones reached during this generation, not the running
        // total for the whole run. The total only climbs, so showing it made the
        // readout claim successes the viewer never sees.
        string text = progression
            ? $"<b><size=130%>Learning to navigate</size></b>\r\n\r\n" +
              $"<b>Generation:</b> {generation}\r\n" +
              $"<b>Goals this generation:</b> {goalsReached}\r\n"
            : $"<b><size=130%>Best agent</size></b>\r\n\r\n" +
              $"<b>From generation:</b> {generation}\r\n" +
              $"<b>Goals reached:</b> {goalsReached}\r\n" +
              $"<b>Playing at real time</b>\r\n";

        if (text == lastText)
        {
            return;
        }

        lastText = text;
        label.text = text;
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
    public void SetInfoText(string generation, string fitness, double timeElapsed, int numberOfGoals,
                            bool replaying = false)
    {
        if (label == null || Time.unscaledTime < nextRefreshTime)
        {
            return;
        }
        nextRefreshTime = Time.unscaledTime + refreshInterval;

        // https://stackoverflow.com/questions/463642/how-can-i-convert-seconds-into-hourminutessecondsmilliseconds-time
        TimeSpan time = TimeSpan.FromSeconds(timeElapsed);
        string str = time.ToString(@"hh\:mm\:ss\:fff");

        // Generation and fitness only mean something while evolving. In replay the
        // network is fixed, so reporting them would just show zeros.
        string text = replaying
            ? $"<b><size=130%>Trained agents</size></b>\r\n\r\n" +
              $"<b>Goals reached:</b> {numberOfGoals}\r\n" +
              $"<b>Running for:</b> {str}\r\n"
            : $"<b><size=130%>Evolving from scratch</size></b>\r\n\r\n" +
              $"<b>Generation:</b> {generation}\r\n" +
              $"<b>Best fitness:</b> {fitness}\r\n" +
              $"<b>Goals reached:</b> {numberOfGoals}\r\n" +
              $"<b>Running for:</b> {str}\r\n";

        if (text == lastText)
        {
            return;
        }

        lastText = text;
        label.text = text;
    }
}
