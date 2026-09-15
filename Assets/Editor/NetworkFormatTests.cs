using System;
using System.Globalization;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Self-checks for the network save format, runnable headlessly with
/// -executeMethod NetworkFormatTests.RunAll.
///
/// The format was rewritten during the 2026 revival to carry a topology header.
/// The original was a bare list of floats recording nothing about the network
/// that produced it, which is precisely why the two weight files recoverable
/// from git history cannot be loaded into anything.
/// </summary>
public static class NetworkFormatTests
{
    private static int failures;

    private static void Check(bool condition, string what)
    {
        if (condition)
        {
            Debug.Log($"[FormatTest] PASS  {what}");
        }
        else
        {
            failures++;
            Debug.LogError($"[FormatTest] FAIL  {what}");
        }
    }

    [MenuItem("Tools/Revival/Run Network Format Tests")]
    public static void RunAll()
    {
        failures = 0;

        RoundTripPreservesEveryParameter();
        HeaderlessTextIsRejected();
        MismatchedTopologyIsRejected();
        TruncatedFileIsRejected();
        GarbageIsRejected();
        ParsingIsCultureIndependent();

        if (failures == 0)
        {
            Debug.Log("[FormatTest] all checks passed");
        }
        else
        {
            Debug.LogError($"[FormatTest] {failures} check(s) failed");
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
    }

    private static void RoundTripPreservesEveryParameter()
    {
        int[] layers = { 18, 10, 5 };
        NeuralNetwork source = new NeuralNetwork(layers, 7);
        source.fitness = 1.2345f;

        NeuralNetwork target = new NeuralNetwork(layers, 7);
        bool loaded = target.LoadFrom(source.Serialise(3), "round-trip");
        Check(loaded, "a serialised network loads back");

        bool identical = true;
        for (int i = 0; i < source.biases.Length; i++)
        {
            for (int j = 0; j < source.biases[i].Length; j++)
            {
                identical &= source.biases[i][j] == target.biases[i][j];
            }
        }
        for (int i = 0; i < source.weights.Length; i++)
        {
            for (int j = 0; j < source.weights[i].Length; j++)
            {
                for (int k = 0; k < source.weights[i][j].Length; k++)
                {
                    identical &= source.weights[i][j][k] == target.weights[i][j][k];
                }
            }
        }
        Check(identical, $"all {source.TotalParameterCount()} parameters survive the round trip exactly");

        float[] probe = new float[layers[0]];
        for (int i = 0; i < probe.Length; i++)
        {
            probe[i] = (i % 7) * 0.31f - 1f;
        }
        float[] a = source.FeedForward(probe);
        float[] b = target.FeedForward(probe);
        bool sameOutput = a.Length == b.Length;
        for (int i = 0; sameOutput && i < a.Length; i++)
        {
            sameOutput &= a[i] == b[i];
        }
        Check(sameOutput, "the reloaded network produces identical outputs");
    }

    private static void HeaderlessTextIsRejected()
    {
        // This is the shape of Assets/Save.txt and Save-MC-0.0001MS0.25.txt,
        // recoverable from git history but unusable: a bare float list.
        NeuralNetwork net = new NeuralNetwork(new[] { 18, 10, 5 }, 0);
        string bare = "-0.6632014\n0.7101535\n0.4000691\n";
        Check(!net.LoadFrom(bare, "headerless"), "a headerless float list is refused");
    }

    private static void MismatchedTopologyIsRejected()
    {
        NeuralNetwork small = new NeuralNetwork(new[] { 4, 3, 2 }, 0);
        NeuralNetwork big = new NeuralNetwork(new[] { 18, 10, 5 }, 0);
        Check(!big.LoadFrom(small.Serialise(0), "wrong-topology"),
              "weights from a different topology are refused rather than silently misread");
    }

    private static void TruncatedFileIsRejected()
    {
        NeuralNetwork net = new NeuralNetwork(new[] { 18, 10, 5 }, 0);
        string text = net.Serialise(0);
        string truncated = text.Substring(0, text.Length / 2);
        NeuralNetwork target = new NeuralNetwork(new[] { 18, 10, 5 }, 0);
        Check(!target.LoadFrom(truncated, "truncated"), "a truncated file is refused");
    }

    private static void GarbageIsRejected()
    {
        NeuralNetwork net = new NeuralNetwork(new[] { 18, 10, 5 }, 0);
        Check(!net.LoadFrom("", "empty"), "empty input is refused");
        Check(!net.LoadFrom("genetic-navigation-network v1\nnot,a,topology\n", "bad header"),
              "an unreadable layer header is refused");
    }

    private static void ParsingIsCultureIndependent()
    {
        // A locale using comma as the decimal separator would corrupt every value
        // if the format relied on the current culture. The browser build can run
        // under any locale, so this matters more than it did on the author's machine.
        CultureInfo original = Thread.CurrentThread.CurrentCulture;
        try
        {
            int[] layers = { 18, 10, 5 };
            NeuralNetwork source = new NeuralNetwork(layers, 0);
            string text = source.Serialise(0);

            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            NeuralNetwork target = new NeuralNetwork(layers, 0);
            bool loaded = target.LoadFrom(text, "de-DE");

            bool identical = loaded;
            for (int i = 0; identical && i < source.biases.Length; i++)
            {
                for (int j = 0; identical && j < source.biases[i].Length; j++)
                {
                    identical &= source.biases[i][j] == target.biases[i][j];
                }
            }
            Check(identical, "values survive loading under a comma-decimal locale (de-DE)");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
