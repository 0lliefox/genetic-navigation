using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Format shared by the recorder and the player.
///
/// A recording holds a handful of generations sampled across a training run, so
/// the browser can show the algorithm improving without running it. Positions are
/// quantised to centimetres in a signed short, which is ample for a city about
/// 400 units across and halves the size against floats.
/// </summary>
public static class TrainingRecording
{
    public const string Magic = "GNREC1";
    public const float PositionScale = 100f;   // centimetre precision
    public const float HeadingScale = 10f;     // tenths of a degree

    public class Frame
    {
        public Vector2 Goal;
        public Vector2[] Positions;
        public float[] Headings;
        public bool[] Active;
    }

    public class Generation
    {
        public int Number;
        public int GoalsReached;
        public float BestFitness;
        public List<Frame> Frames = new List<Frame>();
    }

    public class Recording
    {
        public List<Generation> Generations = new List<Generation>();
    }

    public static byte[] Serialise(Recording recording)
    {
        using (var stream = new MemoryStream())
        using (var w = new BinaryWriter(stream))
        {
            w.Write(Magic.ToCharArray());
            w.Write(recording.Generations.Count);

            foreach (Generation g in recording.Generations)
            {
                w.Write(g.Number);
                w.Write(g.GoalsReached);
                w.Write(g.BestFitness);
                int agentCount = g.Frames.Count > 0 ? g.Frames[0].Positions.Length : 0;
                w.Write(g.Frames.Count);
                w.Write(agentCount);

                // Every frame must be the same width or the reader cannot know
                // where one ends and the next begins.
                foreach (Frame check in g.Frames)
                {
                    if (check.Positions.Length != agentCount)
                    {
                        throw new InvalidDataException(
                            $"Generation {g.Number} has frames of differing widths " +
                            $"({check.Positions.Length} vs {agentCount}); recording would be unreadable.");
                    }
                }

                foreach (Frame f in g.Frames)
                {
                    w.Write((short)Mathf.Clamp(f.Goal.x * PositionScale, short.MinValue, short.MaxValue));
                    w.Write((short)Mathf.Clamp(f.Goal.y * PositionScale, short.MinValue, short.MaxValue));

                    for (int i = 0; i < f.Positions.Length; i++)
                    {
                        w.Write((short)Mathf.Clamp(f.Positions[i].x * PositionScale, short.MinValue, short.MaxValue));
                        w.Write((short)Mathf.Clamp(f.Positions[i].y * PositionScale, short.MinValue, short.MaxValue));
                        w.Write((short)Mathf.Clamp(f.Headings[i] * HeadingScale, short.MinValue, short.MaxValue));
                        w.Write(f.Active[i]);
                    }
                }
            }

            w.Flush();
            return stream.ToArray();
        }
    }

    public static Recording Deserialise(byte[] data)
    {
        using (var stream = new MemoryStream(data))
        using (var r = new BinaryReader(stream))
        {
            string magic = new string(r.ReadChars(Magic.Length));
            if (magic != Magic)
            {
                Debug.LogError($"Not a training recording: header was '{magic}', expected '{Magic}'.");
                return null;
            }

            var recording = new Recording();
            int generations = r.ReadInt32();

            for (int gi = 0; gi < generations; gi++)
            {
                var g = new Generation
                {
                    Number = r.ReadInt32(),
                    GoalsReached = r.ReadInt32(),
                    BestFitness = r.ReadSingle(),
                };

                int frameCount = r.ReadInt32();
                int agentCount = r.ReadInt32();

                for (int fi = 0; fi < frameCount; fi++)
                {
                    var f = new Frame
                    {
                        Goal = new Vector2(r.ReadInt16() / PositionScale, r.ReadInt16() / PositionScale),
                        Positions = new Vector2[agentCount],
                        Headings = new float[agentCount],
                        Active = new bool[agentCount],
                    };

                    for (int i = 0; i < agentCount; i++)
                    {
                        f.Positions[i] = new Vector2(r.ReadInt16() / PositionScale, r.ReadInt16() / PositionScale);
                        f.Headings[i] = r.ReadInt16() / HeadingScale;
                        f.Active[i] = r.ReadBoolean();
                    }

                    g.Frames.Add(f);
                }

                recording.Generations.Add(g);
            }

            return recording;
        }
    }
}
