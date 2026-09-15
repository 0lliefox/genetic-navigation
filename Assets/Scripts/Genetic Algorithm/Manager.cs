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

    public List<NeuralNetwork> networks;
    private List<CarController> cars; 

    void Start()// Start is called before the first frame update
    {
        startingMutationChance = MutationChance;
        startingMutationStrength = MutationStrength;
        RunAlgorithm();
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
            numberOfGoals);
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
            InvokeRepeating("CreateAgents", 0.1f, timeframe); //repeating function
        }
    }

    public void InitNetworks()
    {
        numberOfGoals = 0;
        networks = new List<NeuralNetwork>();
        for (int i = 0; i < populationSize; i++)
        {
            layers[0] = CarController.LAYERS;
            NeuralNetwork net = new NeuralNetwork(layers, i);
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
        networks.Sort();

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
            for (int i = 0; i < size; i++)
            {
                networks[i] = networks[populationSize - 1];
                networks[i].Mutate((int)(1 / MutationChance), MutationStrength);
            }
        } 
        else
        {
            for (int i = 0; i < size; i++)
            {
                networks[i] = networks[i + size].copy(new NeuralNetwork(layers, networks[i + size].agentNo));
                networks[i].Mutate((int)(1 / MutationChance), MutationStrength);
            }
        }

        // Replace random individuals with new random network
        if (UseRandomAgents)
        {
            int numOfRandomAgents = Mathf.RoundToInt(populationSize * PercentageOfRandomAgents);
            for (int i = 0; i < numOfRandomAgents; i++)
            {
                int randomValue = Random.Range(0, populationSize - 2); // exclude best agent from random
                networks[randomValue] = new NeuralNetwork(layers, networks[randomValue].agentNo); 
            }
        }
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
}