# Comparing Learning Techniques for Multi-Agent Navigation in Procedurally Generated Cities (Hybridisation of Genetic Algorithms and Neural Networks)

A dissertation project for fourth year Master's Computer Science at Newcastle University, by Oliver Fox.

A population of agents learns to drive to a goal through a procedurally generated
city. Each agent is a small feed-forward network (18 → 10 → 5) evolved by a genetic
algorithm rather than trained by gradient descent, and agents leave "breadcrumbs"
behind them — a trail inspired by ant pheromones — so the population can learn to
spread out instead of retracing one another's paths. It is the counterpart to
[reinforcement-navigation](https://github.com/0lliefox/reinforcement-navigation),
which solves the same problem with reinforcement learning.

## The 2026 revival

The dissertation was submitted in 2023 against Unity 2020.3. This branch brings it
up to **Unity 6 (6000.6.0f1)**, gets it building again, corrects a number of defects
found while doing so, and works towards a WebGL build.

**The code as submitted is preserved untouched.** The `master` branch is frozen, and
the tag **`dissertation-2023`** marks the exact state the work was submitted in. Nothing
described below has been applied to either.

### Defects corrected

These were found by reading the code during the revival. They are listed with their
effect on the original results, because several of them do affect how the numbers in
the dissertation should be read.

#### The distance term was always zero, so goal fitness was infinite

`CarController.FixedUpdate` assigned `lastPosition = transform.position` on the line
immediately before calling `UpdateFitness()`, which measures the distance from
`lastPosition`. The step was therefore always exactly zero and `totalDistanceTravelled`
never grew. Reaching the goal then computed

```
fitness = 20 + 100 / t²        where t = total distance travelled ≈ 0
```

This is visible in the original output. Every file in `Assets/Tests/` records
`Maximum fitness: Infinity`, and the run behind the dissertation's headline F7 figure
records `Maximum fitness: 4.398047E+14` — which solves to a total travel distance of
**4.8 × 10⁻⁷ units**.

Because `NeuralNetwork.CompareTo` returns `0` when comparing `Infinity` with `Infinity`,
sorting could not order goal-reaching agents against each other at all. **Selection
among successful agents was effectively arbitrary list order rather than fitness**, and
the efficiency term the formula was built around never influenced anything.

*Effect on the reported results:* the average and maximum fitness figures in §4.2.1 and
appendix B are artifacts of this division, not meaningful fitness values — the
`1.374 × 10¹¹` reported for F7 is reproduced exactly by
`Save-...-FFF7-RESETGOAL75timeframeRESETALL-100pop.txt`. The *ranking* of F7 above F6 is
separately supported by the qualitative argument in §4.2.1 (an agent with no collision
term learns to loiter near the goal without entering it), so the conclusion stands on
its own reasoning; the magnitudes do not.

After the fix, a goal-reaching agent scores a finite `20.0044` for roughly 151 units of
travel.

#### Goal visibility was partly wired to breadcrumbs

The `G` term of F6 and F7 — the `+10` distance penalty applied when the target is not
visible — tested `sensors[5]` through `sensors[8]`. The goal rays occupy indices **4–7**,
so the test missed goal ray 0 and instead included index 8, which is the **first
breadcrumb ray**. A goal off to one side was treated as invisible, and a breadcrumb
directly ahead was treated as the goal being visible.

This was left over from the nine-ray layout described in appendix A.5, before the agent
was discretised to four NSWE rays. The sensor banks now have named offsets that
`InputSensors` also uses, so the two cannot drift apart again.

#### Mutation chance was five times its stated value, and inert above 0.2

`Mutate` received `(int)(1 / MutationChance)` and tested `Random.Range(0f, chance) <= 5`,
making the real probability `5 × MutationChance`:

| Inspector value | Actual mutation rate |
| --- | --- |
| 0.01 | 5% |
| 0.1 (the configured value) | 50% |
| ≥ 0.2 | 100%, saturated |

Triggered hypermutation adjusts the chance in steps of 0.05 from a starting 0.1, so once
it reached 0.2 further increases had no effect whatsoever. `Mutate` now takes a
probability in `[0, 1]` and means it.

#### Elitism never worked

The elite branch assigned `networks[best]` into each elite slot **by reference**, so every
slot and the elite itself were one shared object: it was mutated `size` times over, and
every agent in that half wrote to the same `network.fitness`. Each slot now receives its
own copy. The final configuration has `eliteBased` off, so this did not affect the
submitted results.

#### Smaller corrections

- `totalDistanceCovered` accumulated the running total rather than the step, inflating the
  reported distance quadratically.
- "Exclude the best agent" from random-immigrant replacement excluded the best **two**,
  because `Random.Range(int, int)` has an exclusive upper bound.
- The scene labelled its output `F6` while the code had `F7` active. §4.2.1 selects F7, so
  the label was stale and now reads F7.
- The `Breadcrumb` prefab carried an orphaned `delay: 5`, left behind when the field was
  renamed to `removeDelay`, so it silently did nothing. The effective values were the C#
  defaults, which happen to be the values appendix A.4 selects by experiment — 10s
  evaporation and 0.5s penalty delay. The prefab now states both explicitly.
- Saved networks recorded no topology, only a bare list of floats. This is why the two
  weight files recoverable from git history cannot be loaded into anything: nothing records
  the shape that produced them. The format now carries a header and layer sizes, and
  rejects a mismatch instead of misreading it.
- Weights were written and parsed using the ambient culture, so any locale with a comma
  decimal separator would corrupt every value. Now invariant throughout, which matters for
  a browser build.

### Shared code with reinforcement-navigation

This project was forked from `reinforcement-navigation`, and the procedural generation,
cells, goals and camera scripts are near-identical between the two — several are
byte-for-byte the same file.

That shared code carries its own defects, which therefore exist in both repositories. The
one with any bearing on results is that roadblocks can never be placed on the last row or
column of the grid, because `Random.Range(0, height - 1)` combines an already-exclusive
upper bound with an unnecessary `- 1` — so one edge of every generated city was always
free of obstacles. It biased both projects identically, so it does not affect the
comparison between them. The rest are cosmetic: only two of the three building materials
are ever selected, and the cube mesh's front and back normals are inverted.

These are being corrected on this branch only. `reinforcement-navigation` is left as
submitted.

### Building

Requires Unity **6000.6.0f1** with the WebGL module. The scene is
`Assets/Scenes/Large City.unity`.

Editor-only maintenance and self-checks live in `Assets/Editor/` and can be run headlessly:

```
Unity -batchmode -quit -nographics -projectPath . \
      -executeMethod NetworkFormatTests.RunAll
```
