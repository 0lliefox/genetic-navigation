using System.Collections.Generic;
using System;
using System.Globalization;
using System.IO;
using System.Text;

public class NeuralNetwork : IComparable<NeuralNetwork>
{
    public int agentNo;
    public int[] layers;//layers
    public float[][] neurons;//neurons
    public float[][] biases;//biasses
    public float[][][] weights;//weights
    private int[] activations;//layers

    public float fitness = 0;//fitness

    //backprop
    public float learningRate = 0.01f;//learning rate
    public float cost = 0;

    public NeuralNetwork(int[] layers, int agentNo)
    {
        this.layers = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            this.layers[i] = layers[i];
        }
        InitNeurons();
        InitBiases();
        InitWeights();
        this.agentNo = agentNo;
    }

    private void InitNeurons()//create empty storage array for the neurons in the network.
    {
        List<float[]> neuronsList = new List<float[]>();
        for (int i = 0; i < layers.Length; i++)
        {
            neuronsList.Add(new float[layers[i]]);
        }
        neurons = neuronsList.ToArray();
    }

    private void InitBiases()//initializes and populates array for the biases being held within the network.
    {
        List<float[]> biasList = new List<float[]>();
        for (int i = 0; i < layers.Length; i++)
        {
            float[] bias = new float[layers[i]];
            for (int j = 0; j < layers[i]; j++)
            {
                bias[j] = UnityEngine.Random.Range(-0.5f, 0.5f);
            }
            biasList.Add(bias);
        }
        biases = biasList.ToArray();
    }

    private void InitWeights()//initializes random array for the weights being held in the network.
    {
        List<float[][]> weightsList = new List<float[][]>();
        for (int i = 1; i < layers.Length; i++)
        {
            List<float[]> layerWeightsList = new List<float[]>();
            int neuronsInPreviousLayer = layers[i - 1];
            for (int j = 0; j < neurons[i].Length; j++)
            {
                float[] neuronWeights = new float[neuronsInPreviousLayer];
                for (int k = 0; k < neuronsInPreviousLayer; k++)
                {
                    //float sd = 1f / ((neurons[i].Length + neuronsInPreviousLayer) / 2f);
                    neuronWeights[k] = UnityEngine.Random.Range(-0.5f, 0.5f);
                }
                layerWeightsList.Add(neuronWeights);
            }
            weightsList.Add(layerWeightsList.ToArray());
        }
        weights = weightsList.ToArray();
    }

    public float[] FeedForward(float[] inputs)//feed forward, inputs >==> outputs.
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            neurons[0][i] = inputs[i];
        }
        for (int i = 1; i < layers.Length; i++)
        {
            int layer = i - 1;
            for (int j = 0; j < neurons[i].Length; j++)
            {
                float value = 0f;
                for (int k = 0; k < neurons[i - 1].Length; k++)
                {
                    value += weights[i - 1][j][k] * neurons[i - 1][k];
                }
                neurons[i][j] = activate(value + biases[i][j]);
            }
        }
        return neurons[neurons.Length - 1];
    }

    //public float sigmoid(float x)
    //{
    //    float k = (float)Math.Exp(x);
    //    return k / (1.0f + k);
    //}

    public float activate(float value)
    {
        return (float)Math.Tanh(value);
    }

    /// <summary>
    /// Perturbs each weight and bias with probability <paramref name="chance"/>,
    /// by a uniform amount in [-strength, +strength].
    ///
    /// This used to take an integer derived as (int)(1 / MutationChance) and test
    /// Random.Range(0f, chance) &lt;= 5, which made the real mutation probability
    /// 5 x MutationChance - and saturated it at 100% for any MutationChance of 0.2
    /// or above, so the inspector value above 0.2 had no effect at all. It now
    /// takes the probability directly and means what it says.
    /// </summary>
    public void Mutate(float chance, float strength)
    {
        for (int i = 0; i < biases.Length; i++)
        {
            for (int j = 0; j < biases[i].Length; j++)
            {
                if (UnityEngine.Random.value < chance)
                {
                    biases[i][j] += UnityEngine.Random.Range(-strength, strength);
                }
            }
        }

        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    if (UnityEngine.Random.value < chance)
                    {
                        weights[i][j][k] += UnityEngine.Random.Range(-strength, strength);
                    }
                }
            }
        }
    }

    public int CompareTo(NeuralNetwork other) //Comparing For NeuralNetworks performance.
    {
        if (other == null) return 1;

        if (fitness > other.fitness)
            return 1;
        else if (fitness < other.fitness)
            return -1;
        else
            return 0;
    }

    public NeuralNetwork copy(NeuralNetwork nn) //For creatinga deep copy, to ensure arrays are serialzed.
    {
        for (int i = 0; i < biases.Length; i++)
        {
            for (int j = 0; j < biases[i].Length; j++)
            {
                nn.biases[i][j] = biases[i][j];
            }
        }
        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    nn.weights[i][j][k] = weights[i][j][k];
                }
            }
        }
        return nn;
    }

    // Saved networks are a header line, the layer sizes, then every bias and
    // weight in declaration order, then fitness and goal count. The header exists
    // because the original format was a bare list of floats with no shape
    // information - which is why the two weight files recovered from git history
    // are unusable: nothing records which topology produced them.
    private const string SaveFormatHeader = "genetic-navigation-network v1";

    /// <summary>Serialises this network to the text form described above.</summary>
    public string Serialise(int numberOfGoals)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine(SaveFormatHeader);
        text.AppendLine(string.Join(",", Array.ConvertAll(layers, l => l.ToString(CultureInfo.InvariantCulture))));

        for (int i = 0; i < biases.Length; i++)
        {
            for (int j = 0; j < biases[i].Length; j++)
            {
                text.AppendLine(biases[i][j].ToString("R", CultureInfo.InvariantCulture));
            }
        }

        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    text.AppendLine(weights[i][j][k].ToString("R", CultureInfo.InvariantCulture));
                }
            }
        }

        text.AppendLine(fitness.ToString("R", CultureInfo.InvariantCulture));
        text.AppendLine(numberOfGoals.ToString(CultureInfo.InvariantCulture));
        return text.ToString();
    }

    /// <summary>
    /// Loads weights and biases from serialised text, rejecting anything whose
    /// topology does not match this network. Returns false and logs why on
    /// failure, leaving the network untouched.
    ///
    /// Works at runtime, so a trained network can ship as a TextAsset - there is
    /// no Assets folder in a player build for the file-based path below.
    /// </summary>
    public bool LoadFrom(string contents, string sourceName = "<text>")
    {
        if (string.IsNullOrWhiteSpace(contents))
        {
            UnityEngine.Debug.LogError($"Network load failed: {sourceName} is empty.");
            return false;
        }

        string[] lines = contents.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        if (lines.Length < 2 || lines[0].Trim() != SaveFormatHeader)
        {
            UnityEngine.Debug.LogError(
                $"Network load failed: {sourceName} is not a '{SaveFormatHeader}' file. " +
                "Weight files saved before the format gained a header cannot be loaded, " +
                "because the topology that produced them was never recorded.");
            return false;
        }

        int[] fileLayers;
        try
        {
            fileLayers = Array.ConvertAll(lines[1].Split(','),
                l => int.Parse(l.Trim(), CultureInfo.InvariantCulture));
        }
        catch (FormatException)
        {
            UnityEngine.Debug.LogError($"Network load failed: {sourceName} has an unreadable layer header '{lines[1]}'.");
            return false;
        }

        if (fileLayers.Length != layers.Length)
        {
            UnityEngine.Debug.LogError(
                $"Network load failed: {sourceName} has {fileLayers.Length} layers, this network has {layers.Length}.");
            return false;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            if (fileLayers[i] != layers[i])
            {
                UnityEngine.Debug.LogError(
                    $"Network load failed: {sourceName} topology [{string.Join(",", fileLayers)}] " +
                    $"does not match this network [{string.Join(",", layers)}].");
                return false;
            }
        }

        int expected = 2 + TotalParameterCount();
        if (lines.Length < expected)
        {
            UnityEngine.Debug.LogError(
                $"Network load failed: {sourceName} holds {lines.Length - 2} values, expected {TotalParameterCount()}.");
            return false;
        }

        int index = 2;
        try
        {
            for (int i = 0; i < biases.Length; i++)
            {
                for (int j = 0; j < biases[i].Length; j++)
                {
                    biases[i][j] = float.Parse(lines[index++], CultureInfo.InvariantCulture);
                }
            }

            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < weights[i].Length; j++)
                {
                    for (int k = 0; k < weights[i][j].Length; k++)
                    {
                        weights[i][j][k] = float.Parse(lines[index++], CultureInfo.InvariantCulture);
                    }
                }
            }
        }
        catch (FormatException)
        {
            UnityEngine.Debug.LogError($"Network load failed: {sourceName} has an unreadable value on line {index + 1}.");
            return false;
        }

        return true;
    }

    /// <summary>Number of biases plus weights this topology holds.</summary>
    public int TotalParameterCount()
    {
        int total = 0;
        for (int i = 0; i < biases.Length; i++)
        {
            total += biases[i].Length;
        }
        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                total += weights[i][j].Length;
            }
        }
        return total;
    }

    public void Load(string path)//this loads the biases and weights from within a file into the neural network.
    {
#if UNITY_EDITOR
        LoadFrom(File.ReadAllText(path), path);
#endif
    }

    public void Save(string path, int numberOfGoals)//this is used for saving the biases and weights within the network to a file.
    {
        // Editor-only: `path` points into Assets/, which does not exist in a
        // player build. Runtime weight loading goes through LoadFrom(string).
#if UNITY_EDITOR
        File.WriteAllText(path, Serialise(numberOfGoals));
#endif
    }
}
