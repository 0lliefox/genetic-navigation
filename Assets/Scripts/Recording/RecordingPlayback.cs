using UnityEngine;

/// <summary>
/// Plays back a recorded training run.
///
/// The browser build shows this rather than running the algorithm. Live training
/// needs a few hundred generations before anything visibly changes, which is far
/// longer than anyone will watch, and replaying a trained network depends on where
/// the goal happens to land. A recording of a real run shows the algorithm
/// actually improving, and costs nothing but moving transforms.
/// </summary>
public class RecordingPlayback : MonoBehaviour
{
    public enum Mode
    {
        /// Step through every recorded generation in order, earliest first.
        Progression,
        /// Loop one generation, by default the last and best recorded.
        SingleGeneration,
    }

    [SerializeField] private TextAsset recordingAsset;
    [SerializeField] private Mode mode = Mode.Progression;
    [SerializeField, Tooltip("Which generation to loop in SingleGeneration mode. " +
        "Negative counts back from the end, so -1 is the last recorded.")]
    private int generationIndex = -1;

    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private Transform goalMarker;
    [SerializeField] private SetText hud;

    [SerializeField, Tooltip("Recorded frames replayed per second. The recorder samples at " +
        "10Hz, so 10 is real time. A generation is 750 frames, which is 75 seconds of " +
        "recording, so this is set higher to show a whole generation in a watchable span.")]
    private float playbackFps = 75f;
    [SerializeField] private float agentHeight = 2.5f;

    private TrainingRecording.Recording recording;
    private Transform[] agents;
    private int currentGeneration;
    private float frameCursor;

    public int GenerationCount => recording?.Generations.Count ?? 0;
    public int CurrentGenerationNumber =>
        recording != null && currentGeneration < recording.Generations.Count
            ? recording.Generations[currentGeneration].Number
            : 0;

    private void Start()
    {
        if (recordingAsset == null)
        {
            Debug.LogError("RecordingPlayback has no recording assigned.");
            enabled = false;
            return;
        }

        recording = TrainingRecording.Deserialise(recordingAsset.bytes);
        if (recording == null || recording.Generations.Count == 0)
        {
            Debug.LogError("Recording is empty or could not be read.");
            enabled = false;
            return;
        }

        CreateAgents();

        if (mode == Mode.SingleGeneration)
        {
            ShowBestGeneration();
        }
    }

    private int ResolveGenerationIndex()
    {
        int count = recording.Generations.Count;
        int index = generationIndex < 0 ? count + generationIndex : generationIndex;
        return Mathf.Clamp(index, 0, count - 1);
    }

    /// <summary>
    /// The recorded generation that did best: most goals, and among equals the
    /// highest fitness. Falls back to the last recorded generation, which is the
    /// most evolved, when nothing ever reached a goal.
    /// </summary>
    private int BestGenerationIndex()
    {
        int best = recording.Generations.Count - 1;

        for (int i = 0; i < recording.Generations.Count; i++)
        {
            TrainingRecording.Generation g = recording.Generations[i];
            TrainingRecording.Generation b = recording.Generations[best];

            if (g.GoalsReached > b.GoalsReached ||
                (g.GoalsReached == b.GoalsReached && g.BestFitness > b.BestFitness))
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>Step through the whole run, showing the population improve.</summary>
    public void ShowProgression()
    {
        if (recording == null) return;
        mode = Mode.Progression;
        currentGeneration = 0;
        frameCursor = 0f;
    }

    /// <summary>Loop the generation that did best, so it can be watched properly.</summary>
    public void ShowBestGeneration()
    {
        if (recording == null) return;
        mode = Mode.SingleGeneration;
        currentGeneration = BestGenerationIndex();
        frameCursor = 0f;
    }

    private void CreateAgents()
    {
        // Every generation in a recording has the same agent count.
        int agentCount = recording.Generations[0].Frames[0].Positions.Length;
        agents = new Transform[agentCount];

        for (int i = 0; i < agentCount; i++)
        {
            GameObject go = agentPrefab != null
                ? Instantiate(agentPrefab, transform)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            go.name = $"PlaybackAgent{i}";
            go.transform.SetParent(transform, false);

            // Nothing here simulates: strip anything that would move or collide.
            foreach (var body in go.GetComponentsInChildren<Rigidbody>()) Destroy(body);
            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var car in go.GetComponentsInChildren<CarController>()) Destroy(car);

            agents[i] = go.transform;
        }
    }

    private void Update()
    {
        if (recording == null)
        {
            return;
        }

        TrainingRecording.Generation generation = recording.Generations[currentGeneration];
        if (generation.Frames.Count == 0)
        {
            return;
        }

        frameCursor += Time.deltaTime * playbackFps;

        // Play each generation right through. There used to be a wall clock timer
        // that moved on after a few seconds, which only ever showed the opening of
        // a generation: that is the stretch where agents are still spawning, so
        // most of the population had not appeared yet and the city looked empty.
        if (frameCursor >= generation.Frames.Count - 1)
        {
            frameCursor = 0f;

            if (mode == Mode.Progression)
            {
                currentGeneration = (currentGeneration + 1) % recording.Generations.Count;
                generation = recording.Generations[currentGeneration];
            }
        }

        ApplyFrame(generation);
        UpdateHud(generation);
    }

    private void ApplyFrame(TrainingRecording.Generation generation)
    {
        int a = Mathf.Clamp(Mathf.FloorToInt(frameCursor), 0, generation.Frames.Count - 1);
        int b = Mathf.Min(a + 1, generation.Frames.Count - 1);
        float t = frameCursor - a;

        TrainingRecording.Frame from = generation.Frames[a];
        TrainingRecording.Frame to = generation.Frames[b];

        if (goalMarker != null)
        {
            Vector2 goal = Vector2.Lerp(from.Goal, to.Goal, t);
            goalMarker.position = new Vector3(goal.x, goalMarker.position.y, goal.y);
        }

        for (int i = 0; i < agents.Length && i < from.Positions.Length; i++)
        {
            // Draw anything present, crashed included. A crashed agent sits where
            // it stopped in the real simulation, and hiding them left the city
            // looking empty when most of an early generation has already failed.
            bool visible = from.States[i] != TrainingRecording.AgentState.Absent;
            if (agents[i].gameObject.activeSelf != visible)
            {
                agents[i].gameObject.SetActive(visible);
            }
            if (!visible)
            {
                continue;
            }

            // Interpolated so 10Hz capture plays back smoothly at any frame rate.
            Vector2 p = Vector2.Lerp(from.Positions[i], to.Positions[i], t);
            agents[i].position = new Vector3(p.x, agentHeight, p.y);
            agents[i].rotation = Quaternion.Euler(0f,
                Mathf.LerpAngle(from.Headings[i], to.Headings[i], t), 0f);
        }
    }

    private void UpdateHud(TrainingRecording.Generation generation)
    {
        if (hud == null)
        {
            return;
        }

        hud.SetPlaybackText(generation.Number, generation.GoalsReached, generation.BestFitness,
                            mode == Mode.Progression);
    }
}
