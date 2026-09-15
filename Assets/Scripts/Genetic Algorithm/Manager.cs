using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.IO;

public class Manager : MonoBehaviour 
{
    [SerializeField] private SetText setText;

    [Header("Environment Settings")]
    [SerializeField][Range(0.1f, 20f)] public float GameSpeed = 1f;
    [SerializeField] public GameObject prefab;// Holds agent prefab
    private int randomSeedNo = 0; // Seed for grid
    [SerializeField] private int randomiseGridNumber = 10; // Randomise grid every x times
    private int randomGridCounter = 0;
    [SerializeField] public int populationSize;//creates population size
    [SerializeField] public float timeframe; // How long each generation lasts in seconds
    [SerializeField] public float spawnRate = 0.5f; // How quickly agents spawn on new generation
    [SerializeField] private GameObject cells;
    public bool goalReached = false;
    public int forceResetCounter = 0;

    [Header("Network Settings")] 
    [SerializeField] public int[] layers = new int[3] { 5, 3, 2 };//initializing network to the right size
    [SerializeField][Range(0.0001f, 1f)] public float MutationChance = 0.01f;
    [SerializeField][Range(0f, 1f)] public float MutationStrength = 0.5f;
    [SerializeField][Range(0f, 1f)] private float PercentageOfMutatedAgents = 0.5f;
    [SerializeField] private bool UseRandomAgents = false;
    [SerializeField][Range(0f, 1f)] private float PercentageOfRandomAgents = 0.5f;
    [SerializeField] private bool hyperMutation = false;
    [SerializeField] private int CheckMutation = 10;
    [SerializeField] private int CheckMutationCounter = 0;
    [SerializeField] private bool eliteBased = false;
    [SerializeField, Tooltip("Breed each new individual from two parents. Without this the " +
        "algorithm only copies and mutates one parent, which is a hill climb rather than a " +
        "genetic algorithm. Turn off to reproduce the original behaviour.")]
    private bool useCrossover = true;

    private int currentGeneration = 0;
    private float previousAverageFitness = 0f;
    private float previousMutationChance = 0f;
    private float previousMutationStrength = 0f;
    private List<float> totalAverageFitness = new List<float>();
    private List<float> runningAverageFitness = new List<float>();
    public float timeSinceStart = 0f;
    public int maxStepCount = 3000000;
    public int currentStepCount = 0; 
    public int resetStepCounter = 0;
    public int resetStepCount = 25000;

    [Header("Save Values")]
    public float totalDistanceCovered = 0f;
    public int numberOfCollisions = 0; // Number of collisions overall
    public int numberOfGoals = 0; // Number of goals reached
    public float maximumFitness = 0f;
    [SerializeField] private bool breadcrumbs = true;
    private float startingMutationChance;
    private float startingMutationStrength;
    [SerializeField] private string fitnessFunction;
    [SerializeField] private bool resetOnGridCompletion = false;
    [SerializeField] private CellManager cellManager;
    [SerializeField, Tooltip("Log per-generation fitness spread. Used to judge whether the " +
        "efficiency term in the goal fitness discriminates usefully between successful agents.")]
    private bool logGenerationStats = true;

    [Header("Replay")]
    [SerializeField, Tooltip("A network saved from a training run. Required for replay mode.")]
    private TextAsset trainedNetwork;
    [SerializeField, Tooltip("Run the trained network instead of evolving a new one. " +
        "No selection or mutation happens in this mode.")]
    private bool replayTrainedNetwork = false;

    [SerializeField, Tooltip("Seconds before agents are respawned while replaying. " +
        "Short generations suit watching evolution, but in replay there are no generations " +
        "to turn over and a network trained over long episodes needs time to cross the city.")]
    private float replayTimeframe = 60f;

    /// <summary>Episode length for the mode currently running.</summary>
    private float EffectiveTimeframe => replayTrainedNetwork ? replayTimeframe : timeframe;

    public bool IsReplaying => replayTrainedNetwork;
    public bool HasTrainedNetwork => trainedNetwork != null;

    [Header("Recording")]
    [SerializeField, Tooltip("Capture selected generations to persistentDataPath, for playback " +
        "in the browser. Also enabled by -record.")]
    private bool recordTraining = false;
    [SerializeField, Tooltip("Physics steps between captured frames. 5 gives 10Hz at the " +
        "default fixed timestep, which is plenty for playback.")]
    private int recordEveryNSteps = 5;

    private TrainingRecording.Recording recording;
    private TrainingRecording.Generation recordingGeneration;
    private int recordStepCounter;
    private readonly List<int> generationsToRecord = new List<int>();

    [Header("Training Output")]
    [SerializeField, Tooltip("Write the best network to persistentDataPath as training runs. " +
        "Also enabled by passing -train on the command line.")]
    private bool saveTrainedNetworks = false;
    private float bestFitnessSeen = float.NegativeInfinity;

    // Set with -seed. The grid seeds the global RNG from its own parameters at
    // Start, and the starting weights are drawn from that same stream, so without
    // this every run of a given build produces byte-identical results. Outcomes
    // vary enormously between seeds, so running several is worth more than running
    // one for longer.
    private int runSeed = int.MinValue;

    public List<NeuralNetwork> networks;
    private List<CarController> cars; 

    void Start()// Start is called before the first frame update
    {
        ApplyCommandLineOverrides();
        startingMutationChance = MutationChance;
        startingMutationStrength = MutationStrength;
        RunAlgorithm();
    }

    /// <summary>
    /// Lets a headless training run be configured without editing the scene.
    ///
    /// Generations advance on simulated time, so wall clock training speed is set
    /// by Time.timeScale, not by how fast the machine is. Time.maximumDeltaTime
    /// caps how much simulated time a single frame may advance, and its default of
    /// 0.333s is exactly 20x at 60fps, so raising GameSpeed past 20 does nothing on
    /// its own. It is raised here to match.
    /// </summary>
    private void ApplyCommandLineOverrides()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-gameSpeed" && float.TryParse(args[i + 1], out float speed))
            {
                GameSpeed = speed;
            }
            else if (args[i] == "-population" && int.TryParse(args[i + 1], out int size))
            {
                populationSize = size;
            }
            else if (args[i] == "-maxSteps" && int.TryParse(args[i + 1], out int steps))
            {
                maxStepCount = steps;
            }
            else if (args[i] == "-mutationChance" && float.TryParse(args[i + 1], out float chance))
            {
                MutationChance = chance;
            }
            else if (args[i] == "-mutationStrength" && float.TryParse(args[i + 1], out float strength))
            {
                MutationStrength = strength;
            }
            else if (args[i] == "-seed" && int.TryParse(args[i + 1], out int seed))
            {
                runSeed = seed;
            }
            else if (args[i] == "-crossover")
            {
                useCrossover = args[i + 1] != "off";
            }
            else if (args[i] == "-record")
            {
                recordTraining = true;
                // Comma separated generation numbers, e.g. -record 1,10,50,150,326
                foreach (string part in args[i + 1].Split(','))
                {
                    if (int.TryParse(part.Trim(), out int g))
                    {
                        generationsToRecord.Add(g);
                    }
                }
            }
        }

        // Headroom for one frame to advance the whole of a fast generation.
        Time.maximumDeltaTime = Mathf.Max(Time.maximumDeltaTime, GameSpeed / 30f);
    }

    private void Update()
    {
        timeSinceStart = Time.realtimeSinceStartup;
    }

    private void FixedUpdate()
    {
        if (forceResetCounter >= populationSize)
        {
            resetStepCounter = 0;
            forceResetCounter = 0;
            CreateAgents();
        }

        currentStepCount++;
        CaptureRecordingFrame();

        if (resetStepCount > 0)
        {
            resetStepCounter++;

            if (resetStepCounter > resetStepCount)
            {
                resetStepCounter = 0;
                CreateAgents();
            }
        }

        if (!resetOnGridCompletion && currentStepCount >= maxStepCount) 
        {
            SaveRun($"Assets/Tests/Save-PopSize{populationSize}-MC{startingMutationChance}-MS{startingMutationStrength}" +
                $"-HM{hyperMutation}-RND{UseRandomAgents}{PercentageOfRandomAgents}-RndGrid{randomiseGridNumber}-" +
                $"Layers{layers[0]}-Breadcrumbs({breadcrumbs})-FF{fitnessFunction}.txt");
        }
        else if (resetOnGridCompletion && cellManager.numberOfCellsVisited >= 100)
        {
            SaveRun($"Assets/Tests/Save-PopSize{populationSize}-MC{startingMutationChance}-MS{startingMutationStrength}" +
                $"-HM{hyperMutation}-RND{UseRandomAgents}{PercentageOfRandomAgents}-RndGrid{randomiseGridNumber}-" +
                $"Layers{layers[0]}-Breadcrumbs({breadcrumbs})-FF{fitnessFunction}.txt"); 
        }

        setText.SetInfoText(
            currentGeneration.ToString(),
            (runningAverageFitness.Count > 0) ? runningAverageFitness[runningAverageFitness.Count - 1].ToString() : runningAverageFitness.Count.ToString(),
            timeSinceStart,
            numberOfGoals,
            replayTrainedNetwork);
    }

    public void SaveRun(string path)//this is used for saving the biases and weights within the network to a file.
    {
        int numberOfCellsVisitedMultipleTimes = 0;
        int maxNumberOfTimesCellWasVisited = 0;
        int numberOfCellsVisited = 0;
        foreach (Cell cell in cells.GetComponentsInChildren<Cell>())
        {
            if (cell.hasVisited && cell.timesVisited > 1)
            {
                numberOfCellsVisitedMultipleTimes++;
            }

            if (cell.timesVisited > maxNumberOfTimesCellWasVisited)
            {
                maxNumberOfTimesCellWasVisited = cell.timesVisited;
            }

            if (cell.hasVisited)
            {
                numberOfCellsVisited++;
            }
        }

        // Editor-only: `path` is a project-relative Assets/ path, and no Assets
        // folder exists in a player build. Runtime builds log the summary instead.
#if UNITY_EDITOR
        File.Create(path).Close();
        StreamWriter writer = new StreamWriter(path, true);
        writer.WriteLine($"Distance covered: {totalDistanceCovered}");
        writer.WriteLine($"No. of collisions: {numberOfCollisions}");
        writer.WriteLine($"Cells visited: {numberOfCellsVisited}");
        writer.WriteLine($"Cells visited multiple times: {numberOfCellsVisitedMultipleTimes}");
        writer.WriteLine($"Max times one cell was visited: {maxNumberOfTimesCellWasVisited}");
        writer.WriteLine($"No. of goals reached in total: {numberOfGoals}");
        writer.WriteLine($"Average fitness: {totalAverageFitness.Average()}");
        writer.WriteLine($"Maximum fitness: {maximumFitness}");
        writer.WriteLine($"No. of generations: {currentGeneration}");
        writer.WriteLine($"Time elapsed: {timeSinceStart}");
        writer.Close();

        UnityEditor.EditorApplication.isPlaying = false;
#else
        Debug.Log(
            $"Run complete. Distance covered: {totalDistanceCovered}, " +
            $"collisions: {numberOfCollisions}, cells visited: {numberOfCellsVisited}, " +
            $"goals: {numberOfGoals}, generations: {currentGeneration}, " +
            $"max fitness: {maximumFitness}, elapsed: {timeSinceStart}");
#endif
        Application.Quit();
    }

    public void RunAlgorithm()
    {
        if (populationSize % 2 != 0)
            populationSize = 50;//if population size is not even, sets it to fifty

        InitNetworks();

        if (resetStepCount > 0)
        {
            CreateAgents();
        }
        else
        {
            InvokeRepeating("CreateAgents", 0.1f, EffectiveTimeframe); //repeating function
        }
    }

    public void InitNetworks()
    {
        numberOfGoals = 0;
        networks = new List<NeuralNetwork>();

        // Replay mode gives every agent the same trained network. They still
        // diverge, because each spawns at a slightly different moment and the goal
        // moves as it is reached, so the result is a group of competent drivers
        // rather than a single one repeated.
        string trained = null;
        if (replayTrainedNetwork)
        {
            if (trainedNetwork == null)
            {
                Debug.LogWarning("Replay mode is on but no trained network is assigned; " +
                                 "falling back to training from scratch.");
                replayTrainedNetwork = false;
            }
            else
            {
                trained = trainedNetwork.text;
            }
        }

        // Re-seed immediately before drawing the weights, so this does not depend on
        // whether Manager.Start or GridWithParams.Start happened to run first.
        if (runSeed != int.MinValue)
        {
            Random.InitState(runSeed);
        }

        for (int i = 0; i < populationSize; i++)
        {
            layers[0] = CarController.LAYERS;
            NeuralNetwork net = new NeuralNetwork(layers, i);

            if (trained != null && !net.LoadFrom(trained, trainedNetwork.name))
            {
                // LoadFrom has already said why. Random weights are a poor demo, so
                // make the failure obvious rather than quietly showing noise.
                Debug.LogError("Trained network could not be loaded; showing untrained agents.");
                replayTrainedNetwork = false;
                trained = null;
            }

            //net.Load("Assets/Save/Save-MC-0.5000001MS0.635-Layers3-PopSize100-MC0.1-MS0.5-HMTrue-RNDFalse0.25-RndGrid100-Layers18-Breadcrumbs(True)-FFF7-RESETGOAL75timeframeRESETALL-100pop.txt");//on start load the network save
            networks.Add(net); 
        }
    }

    public void CreateAgents()
    {
        Time.timeScale = GameSpeed;//sets gamespeed, which will increase to speed up training
        currentGeneration++;
        if (cars != null)
        {
            cars[0].RemoveAllBreadcrumbs();

            if (cars[0].isRandomGrid && randomGridCounter >= randomiseGridNumber)
            {
                //if (cars[0].randomiseGoalPosition)
                //{
                //    cars[0].RandomiseTargetPos();
                //}
                if (randomSeedNo > 99)
                {
                    randomSeedNo = 0;
                }
                else
                {
                    randomSeedNo++;
                }
                cars[0].BuildRandomGrid(randomSeedNo);
                randomGridCounter = 0;
            }

            for (int i = 0; i < cars.Count; i++)
            {
                GameObject.Destroy(cars[i].gameObject);//if there are Prefabs in the scene this will get rid of them
            }

            SortNetworks();//this sorts networks and mutates them
        }

        FinishRecordingGeneration();
        BeginRecordingGeneration();

        randomGridCounter++;
        StartCoroutine(DelayedCreation(spawnRate / Time.timeScale, populationSize)); // Spawning lots at one location led to agents being shoved off the map
    }

    private IEnumerator DelayedCreation(float delay, int populationSize)
    {
        cars = new List<CarController>();
        for (int i = 0; i < populationSize; i++)
        {
            CarController car = Instantiate(prefab, new Vector3(-20f, 2.5f, 0), Quaternion.Euler(0, 90f, 0), transform.parent).GetComponent<CarController>();
            car.agentNo = i;
            car.network = networks[i]; // Deploys network to each learner
            cars.Add(car);
            yield return new WaitForSeconds(delay);
        }
    }

    public void SortNetworks() 
    {   
        // Replay mode is a demonstration, not a search: keep every agent on the
        // trained weights rather than selecting and mutating away from them.
        if (replayTrainedNetwork)
        {
            return;
        }

        networks.Sort();

        LogGenerationStats();
        RecordTrainedNetwork();

        if (runningAverageFitness.Count > 0 && runningAverageFitness.Count >= CheckMutation)
        {
            runningAverageFitness.RemoveAt(0);
        }
        runningAverageFitness.Add(networks[populationSize - 1].fitness); // Get best fitness from generation
        totalAverageFitness.Add(networks[populationSize - 1].fitness);

        Hypermutation();

        networks[populationSize - 1].Save($"Assets/Save/Save-MC-{MutationChance}MS{MutationStrength}-Layers{layers.Length}-PopSize{populationSize}-MC{startingMutationChance}-MS{startingMutationStrength}" +
                $"-HM{hyperMutation}-RND{UseRandomAgents}{PercentageOfRandomAgents}-RndGrid{randomiseGridNumber}-" +
                $"Layers{layers[0]}-Breadcrumbs({breadcrumbs})-FF{fitnessFunction}.txt", numberOfGoals); // Saves networks weights and biases to file, to preserve network performance

        int size = Mathf.RoundToInt(populationSize * PercentageOfMutatedAgents);

        if (eliteBased)
        {
            // Each slot needs its own copy of the elite. This previously assigned
            // the elite *by reference*, so every elite slot and the elite itself
            // were one shared object: it was mutated `size` times over, and every
            // car in that half wrote to the same network.fitness. Elitism never
            // actually worked.
            NeuralNetwork elite = networks[populationSize - 1];
            for (int i = 0; i < size; i++)
            {
                networks[i] = elite.copy(new NeuralNetwork(layers, elite.agentNo));
                networks[i].Mutate(MutationChance, MutationStrength);
            }
        } 
        else if (useCrossover)
        {
            // Breed the replaced half from two parents drawn out of the surviving
            // half. Only indices below `size` are written and only those at or
            // above it are read, so parents are never overwritten mid-loop.
            for (int i = 0; i < size; i++)
            {
                NeuralNetwork parentA = SelectParent(size);
                NeuralNetwork parentB = SelectParent(size);

                networks[i] = NeuralNetwork.Crossover(parentA, parentB, new NeuralNetwork(layers, i));
                networks[i].Mutate(MutationChance, MutationStrength);
            }
        }
        else
        {
            for (int i = 0; i < size; i++)
            {
                networks[i] = networks[i + size].copy(new NeuralNetwork(layers, networks[i + size].agentNo));
                networks[i].Mutate(MutationChance, MutationStrength);
            }
        }

        // Replace random individuals with new random network
        if (UseRandomAgents)
        {
            int numOfRandomAgents = Mathf.RoundToInt(populationSize * PercentageOfRandomAgents);
            for (int i = 0; i < numOfRandomAgents; i++)
            {
                // Random.Range(int, int) has an exclusive upper bound, so the old
                // `populationSize - 2` excluded the best *two* agents, not one.
                int randomValue = Random.Range(0, populationSize - 1); // exclude best agent from random
                networks[randomValue] = new NeuralNetwork(layers, networks[randomValue].agentNo); 
            }
        }
    }

    private bool ShouldRecordThisGeneration()
    {
        return recordTraining
            && (generationsToRecord.Count == 0 || generationsToRecord.Contains(currentGeneration));
    }

    private void BeginRecordingGeneration()
    {
        if (!ShouldRecordThisGeneration())
        {
            return;
        }

        recording ??= new TrainingRecording.Recording();
        recordingGeneration = new TrainingRecording.Generation { Number = currentGeneration };
        recordStepCounter = 0;
    }

    private void CaptureRecordingFrame()
    {
        if (recordingGeneration == null || cars == null || cars.Count == 0)
        {
            return;
        }

        if (++recordStepCounter < recordEveryNSteps)
        {
            return;
        }
        recordStepCounter = 0;

        // All agents share one goal, so ask whichever one is alive.
        Vector3 goal = Vector3.zero;
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i] != null)
            {
                goal = cars[i].TargetPosition;
                break;
            }
        }

        // Always the full population. Agents spawn over the first few frames, so
        // sizing to cars.Count would make early frames narrower than later ones,
        // and the header records one agent count for the whole generation.
        var frame = new TrainingRecording.Frame
        {
            Goal = new Vector2(goal.x, goal.z),
            Positions = new Vector2[populationSize],
            Headings = new float[populationSize],
            States = new TrainingRecording.AgentState[populationSize],
        };

        for (int i = 0; i < populationSize && i < cars.Count; i++)
        {
            CarController car = cars[i];
            if (car == null)
            {
                continue; // Not spawned yet, or already destroyed: stays inactive.
            }

            frame.Positions[i] = new Vector2(car.transform.position.x, car.transform.position.z);
            frame.Headings[i] = car.transform.eulerAngles.y;
            frame.States[i] = car.active
                ? TrainingRecording.AgentState.Driving
                : TrainingRecording.AgentState.Crashed;
        }

        recordingGeneration.Frames.Add(frame);
    }

    private void FinishRecordingGeneration()
    {
        if (recordingGeneration == null)
        {
            return;
        }

        recordingGeneration.GoalsReached = numberOfGoals;
        recordingGeneration.BestFitness = networks != null && networks.Count > 0
            ? networks[networks.Count - 1].fitness
            : 0f;

        if (recordingGeneration.Frames.Count > 0)
        {
            recording.Generations.Add(recordingGeneration);
            SaveRecording();
            Debug.Log($"[Record] generation {recordingGeneration.Number}: " +
                      $"{recordingGeneration.Frames.Count} frames, " +
                      $"{recording.Generations.Count} generations captured so far");
        }

        recordingGeneration = null;
    }

    private void SaveRecording()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (recording == null || recording.Generations.Count == 0)
        {
            return;
        }

        string tag = runSeed == int.MinValue ? "" : $"-seed{runSeed}";
        string path = Path.Combine(Application.persistentDataPath, $"training-recording{tag}.bytes");
        File.WriteAllBytes(path, TrainingRecording.Serialise(recording));
#endif
    }

    /// <summary>
    /// Writes the best network of the run out during training.
    ///
    /// The editor path below saves into Assets/Save every generation, which is
    /// gitignored and does not exist in a build. This writes to
    /// persistentDataPath instead so a standalone player can be left training
    /// headless, which is how the shipped network is produced.
    ///
    /// Two files: the best fitness seen at any point in the run, and the most
    /// recent generation's best. Fitness here is the value at the moment an agent
    /// stopped, so a single lucky run can top the table; having both means the
    /// choice of which to ship can be made by watching them rather than by
    /// trusting the number.
    /// </summary>
    private void RecordTrainedNetwork()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        bool requested = saveTrainedNetworks
            || System.Environment.GetCommandLineArgs().Contains("-train");

        if (!requested || networks.Count == 0)
        {
            return;
        }

        // Index populationSize - 1 is the generation's best and is never mutated,
        // so this is the network as it performed, not a mutated descendant.
        NeuralNetwork best = networks[networks.Count - 1];
        string serialised = best.Serialise(numberOfGoals);

        string tag = runSeed == int.MinValue ? "" : $"-seed{runSeed}";
        File.WriteAllText(Path.Combine(Application.persistentDataPath, $"latest-network{tag}.txt"), serialised);

        if (best.fitness > bestFitnessSeen)
        {
            bestFitnessSeen = best.fitness;
            File.WriteAllText(Path.Combine(Application.persistentDataPath, $"best-network{tag}.txt"), serialised);
            Debug.Log($"[Train] seed {runSeed} gen {currentGeneration}: new best fitness {best.fitness:G6}, " +
                      $"goals so far {numberOfGoals}");
        }
#endif
    }

    /// <summary>
    /// Reports the fitness spread across the generation just finished.
    ///
    /// The non-finite count is the direct check on the divide-by-zero that used to
    /// give every goal-reaching agent a fitness of Infinity - which left CompareTo
    /// unable to order them, so selection among successful agents was arbitrary.
    /// It should always be zero.
    /// </summary>
    private void LogGenerationStats()
    {
        if (!logGenerationStats || networks.Count == 0)
        {
            return;
        }

        int nonFinite = networks.Count(n => float.IsNaN(n.fitness) || float.IsInfinity(n.fitness));

        Debug.Log(
            $"[GA] gen {currentGeneration} " +
            $"best={networks[networks.Count - 1].fitness:G6} " +
            $"median={networks[networks.Count / 2].fitness:G6} " +
            $"worst={networks[0].fitness:G6} " +
            $"mean={networks.Average(n => n.fitness):G6} " +
            $"goals={numberOfGoals} distance={totalDistanceCovered:G6} " +
            $"non-finite={nonFinite}");
    }

    /// <summary>
    /// Picks a parent from the surviving half by a two-way tournament, so fitter
    /// individuals breed more often without the best one dominating outright.
    /// </summary>
    private NeuralNetwork SelectParent(int survivorsStart)
    {
        int a = Random.Range(survivorsStart, networks.Count);
        int b = Random.Range(survivorsStart, networks.Count);
        return networks[a].fitness >= networks[b].fitness ? networks[a] : networks[b];
    }

    private void Hypermutation()
    {
        CheckMutationCounter++;
        if (CheckMutationCounter >= CheckMutation && hyperMutation)
        {
            float avgFitness = runningAverageFitness.Average();
            if (previousAverageFitness != 0f)
            {
                if (avgFitness < previousAverageFitness)
                {
                    // Mutation Chance
                    if (previousMutationChance < MutationChance)
                    {
                        previousMutationChance = MutationChance;
                        if (MutationChance <= 0.2f)
                        {
                            MutationChance -= 0.0025f;
                        }
                        else
                        {
                            MutationChance -= 0.05f;
                        }
                    }
                    else if (previousMutationChance > MutationChance)
                    {
                        previousMutationChance = MutationChance;
                        if (MutationChance >= 0.8f)
                        {
                            MutationChance += 0.0025f;
                        }
                        else
                        {
                            MutationChance += 0.05f;
                        }
                    }

                    // Mutation Strength
                    if (previousMutationStrength < MutationStrength)
                    {
                        previousMutationStrength = MutationStrength;
                        if (MutationStrength <= 0.2f)
                        {
                            MutationStrength -= 0.005f;
                        }
                        else
                        {
                            MutationStrength -= 0.05f;
                        }
                    }
                    else if (previousMutationStrength > MutationStrength)
                    {
                        previousMutationStrength = MutationStrength;
                        if (MutationStrength >= 0.8f)
                        {
                            MutationStrength += 0.005f;
                        }
                        else
                        {
                            MutationStrength += 0.05f;
                        }
                    }
                }
                else if (avgFitness > previousAverageFitness)
                {
                    // Mutation Chance
                    if (previousMutationChance < MutationChance)
                    {
                        previousMutationChance = MutationChance;
                        if (MutationChance >= 0.8f)
                        {
                            MutationChance += 0.0025f;
                        }
                        else
                        {
                            MutationChance += 0.05f;
                        }
                    }
                    else if (previousMutationChance > MutationChance)
                    {
                        previousMutationChance = MutationChance;
                        if (MutationChance <= 0.2f)
                        {
                            MutationChance -= 0.0025f;
                        }
                        else
                        {
                            MutationChance -= 0.05f;
                        }
                    }

                    // Mutation Strength
                    if (previousMutationStrength < MutationStrength)
                    {
                        previousMutationStrength = MutationStrength;
                        if (MutationStrength >= 0.8f)
                        {
                            MutationStrength += 0.005f;
                        }
                        else
                        {
                            MutationStrength += 0.05f;
                        }
                    }
                    else if (previousMutationStrength > MutationStrength)
                    {
                        previousMutationStrength = MutationStrength;
                        if (MutationStrength <= 0.2f)
                        {
                            MutationStrength -= 0.005f;
                        }
                        else
                        {
                            MutationStrength -= 0.05f;
                        }
                    }
                }
            }
            else
            {
                previousMutationChance = MutationChance;
                previousMutationStrength = MutationStrength;
                MutationChance += 0.05f;
                MutationStrength += 0.1f;
            }

            if (MutationChance > 1)
            {
                MutationChance = 1;
            }
            else if (MutationChance < 0)
            {
                MutationChance = 0.0001f;
            }

            if (MutationStrength > 1)
            {
                MutationStrength = 1;
            }
            else if (MutationStrength < 0)
            {
                MutationStrength = 0;
            }

            previousAverageFitness = avgFitness;
            CheckMutationCounter = 0;
        }
    }
    /// <summary>Switches between watching the trained network and evolving a new one.</summary>
    public void SetReplayMode(bool replay)
    {
        if (replay && trainedNetwork == null)
        {
            Debug.LogWarning("No trained network assigned; staying in training mode.");
            return;
        }

        if (replay == replayTrainedNetwork)
        {
            return;
        }

        replayTrainedNetwork = replay;
        RestartRun();
    }

    /// <summary>Clears the current population and starts again in the current mode.</summary>
    public void RestartRun()
    {
        CancelInvoke();
        StopAllCoroutines();

        if (cars != null)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                if (cars[i] != null)
                {
                    Destroy(cars[i].gameObject);
                }
            }
            cars = null;
        }

        currentGeneration = 0;
        currentStepCount = 0;
        resetStepCounter = 0;
        bestFitnessSeen = float.NegativeInfinity;
        MutationChance = startingMutationChance;
        MutationStrength = startingMutationStrength;

        RunAlgorithm();
    }


}
