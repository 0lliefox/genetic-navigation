# Comparing Learning Techniques for Multi-Agent Navigation in Procedurally Generated Cities (Hybridisation of Genetic Algorithms and Neural Networks)

A dissertation project for fourth year Master's Computer Science at Newcastle University, by Oliver Fox.

A population of agents learns to drive to a goal through a procedurally generated city.
Each agent is a small feed-forward network (18 inputs, 10 hidden, 5 outputs) evolved by a
genetic algorithm instead of trained by gradient descent. Agents drop "breadcrumbs" behind
them, based on ant pheromone trails, so the population learns to spread out rather than
follow the same route. It is the counterpart to
[reinforcement-navigation](https://github.com/0lliefox/reinforcement-navigation), which
solves the same problem with reinforcement learning.

## The 2026 revival

The dissertation was submitted in 2023 using Unity 2020.3. This branch updates it to Unity
6 (6000.6.0f1), gets it building again, fixes a number of bugs, and adds a WebGL build.

The submitted code is preserved. The `master` branch is frozen and the tag
`dissertation-2023` marks the exact state the work was handed in. None of the changes below
have been applied to either.

## Changelog

### Fixed: distance travelled was always zero

`UpdateFitness()` measured how far an agent had moved since `lastPosition`, but
`FixedUpdate` set `lastPosition` to the current position on the line just before calling it.
The distance was therefore always zero, and `totalDistanceTravelled` never grew.

An agent that reached the goal scored `20 + 100 / t²`, where `t` is that distance. Dividing
by zero gave infinity. Every file in `Assets/Tests/` records `Maximum fitness: Infinity`.

This also broke sorting. `CompareTo` returns 0 when comparing infinity with infinity, so
agents that reached the goal could not be ranked against each other. Selection between
successful agents came down to list order rather than fitness.

An agent reaching the goal now scores around 20.004 for a real journey of about 150 units.

**This affects the numbers in the dissertation.** The average and maximum fitness figures in
section 4.2.1 and appendix B come from this calculation, so they are not meaningful fitness
values. The `1.374e11` reported for F7 is reproduced exactly by the file
`Save-...-FFF7-RESETGOAL75timeframeRESETALL-100pop.txt`, whose maximum of `4.398e14` works
out to a total travel distance of 0.00000048 units.

The finding that F7 beats F6 is argued separately in section 4.2.1, on the grounds that an
agent with no collision term learns to sit near the goal without entering it. That reasoning
does not depend on the numbers, so the conclusion still holds. The magnitudes do not.

### Fixed: goal visibility was partly reading breadcrumbs

F6 and F7 add a penalty of 10 to the distance when the target is not visible. The check read
`sensors[5]` to `sensors[8]`, but the goal rays are at indices 4 to 7. So it ignored one goal
ray and instead read index 8, which is the first breadcrumb ray.

In practice, a goal off to one side counted as not visible, and a breadcrumb straight ahead
counted as the goal being visible. This was left over from the nine-ray setup described in
appendix A.5, before the agent was cut down to four rays.

The sensor banks now have named offsets that `InputSensors` uses as well, so the two cannot
get out of step again.

### Fixed: mutation chance was five times what the inspector said

`Mutate` was called with `(int)(1 / MutationChance)` and tested
`Random.Range(0f, chance) <= 5`, which works out to a real probability of `5 × MutationChance`.

| Inspector value | Actual rate |
| --- | --- |
| 0.01 | 5% |
| 0.1 (the value used) | 50% |
| 0.2 and above | 100% |

Triggered hypermutation moves the value in steps of 0.05 starting from 0.1, so once it
reached 0.2 any further increase did nothing at all. `Mutate` now takes a probability
between 0 and 1 and uses it directly.

### Fixed: elitism never worked

The elite branch copied `networks[best]` into each elite slot by reference, so every slot
and the elite itself were the same object. It got mutated once per slot, and every agent in
that half wrote to the same fitness value. Each slot now gets its own copy.

`eliteBased` is off in the final scene, so this did not affect the submitted results.

### Changed: how the algorithm works

Two changes to the method, not bug fixes. The dissertation's results were measured
with the original behaviour.

**Fitness now scores the whole episode.** It used to be assigned every physics step, so
an agent was judged only on where it happened to be when it last updated. One that
navigated well then drifted scored badly, and one that parked beside the goal scored
well. That is what taught agents to hover near the goal rather than enter it. Fitness is
now the best the agent managed at any point, plus credit per goal reached.

**Crossover was added.** Every new individual used to be a copy of a single parent with
mutations applied, which is a hill climb rather than a genetic algorithm: good partial
solutions from different individuals could never be combined. Individuals are now bred
from two parents with uniform crossover, with parents picked by a two-way tournament.
Crossover was sketched out in the first commit but left commented out, and that file was
deleted before submission, so it never ran. Set `useCrossover` to false for the original
behaviour.

### Fixed: smaller things

- `totalDistanceCovered` added the running total each step instead of the step itself, so
  the reported distance grew quadratically.
- Random immigrant replacement was meant to exclude the best agent but excluded the best
  two, because `Random.Range(int, int)` already excludes its upper bound.
- The scene labelled its output F6 while the code ran F7. Section 4.2.1 picks F7, so the
  label was out of date and now reads F7.
- The Breadcrumb prefab still had a `delay: 5` field from before it was renamed to
  `removeDelay`, so it did nothing. The values actually in use were the code defaults, which
  are the ones appendix A.4 settles on (10 second evaporation, 0.5 second penalty delay).
  The prefab now sets both explicitly.
- Saved networks stored only a list of numbers with nothing about the network shape. That is
  why the two weight files in the git history cannot be loaded into anything. The format now
  records the layer sizes and refuses a file that does not match.
- Weights were written and read using the machine's locale, so anywhere that uses a comma
  for decimals would have corrupted every value. This matters for a browser build, so it is
  now locale independent.

### Changed: Unity 6

- Upgraded from Unity 2020.3.23f1 to 6000.6.0f1.
- Removed ML-Agents, AI Navigation, Timeline and Multiplayer Centre. Nothing in the project
  used them. They came along when this project was forked from `reinforcement-navigation`.
- Removed the vendored TextMesh Pro sample scripts, which were the only thing failing to
  compile, and updated TextMesh Pro to the Unity 6 version.
- `Manager.SaveRun` called `UnityEditor.EditorApplication`, which cannot compile into a
  build. Saving now only happens in the editor, and builds log the summary instead.
- The build scene list held 12 entries, 11 of them naming scenes from the reinforcement
  learning project that have never existed here. The only enabled one did not exist, so a
  build produced a player with no scenes in it.

## Shared code with reinforcement-navigation

This project was forked from `reinforcement-navigation`. The procedural generation, cells,
goals and camera scripts are nearly the same in both, and several files are identical.

The shared code has its own bugs, so they exist in both repositories. The only one that
affects results is that roadblocks can never be placed on the last row or column of the
grid, so one edge of every city is always clear. It applied equally to both projects, so it
does not affect the comparison between them. The rest are cosmetic, such as only two of the
three building materials ever being used.

These are fixed on this branch only. `reinforcement-navigation` is left as submitted.

## The web build

`Assets/Scenes/Web Demo.unity` plays back a recording of a real training run. It does not
run the algorithm in the browser.

Running it live does not work as a demo. The algorithm needs a few hundred generations
before anything visibly changes, which is far longer than anyone watches a web page, and
replaying a single trained network depends on where the goal happens to land, so it can
just as easily show agents failing. A recording shows the population actually improving,
and shows the same thing every time.

It is also much cheaper. Playback moves transforms and nothing else: no physics, no
raycasts and no network evaluation at runtime, which is what makes it comfortable on a
phone.

Two modes:

- **Watch it learn** steps through the recorded generations in order, about ten seconds
  each, so the whole run takes roughly two minutes.
- **Best generation** loops whichever generation did best, chosen by goals reached.

The recording shipped with the demo is the best of six seeds: 454 goals over the full 800
generation budget, with a clear breakthrough between generations 300 and 400.

Recordings hold a handful of generations sampled across a run. Positions and headings are
quantised into signed shorts, centimetres and tenths of a degree, which is well inside
what playback needs. That is about 500 KB per generation before compression, and position
data compresses very well.

The scene is generated by `Assets/Editor/BuildWebDemoScene.cs` rather than built by hand.
Scenes are marked binary in `.gitattributes`, so a hand-made one commits as an unreadable
blob. `Assets/Scenes/Large City.unity` is left exactly as the dissertation had it.

Phones are handled two ways. `FitCameraToCity` measures the city and fits it to whatever
shape the viewport is, and the page template caps the device pixel ratio at 1 on small
screens. On a 375x812 screen that drops the render target from 2.30 megapixels to 0.30.
These numbers come from a desktop browser; performance on a real phone has not been
measured yet.

## Producing a recording

```
Unity -batchmode -nographics -projectPath . \
      -executeMethod BuildPlayer.MacTraining -outputPath Build/Training.app

Build/Training.app/Contents/MacOS/'Genetic Navigation' \
      -batchmode -nographics -train -gameSpeed 60 -maxSteps 3000000 \
      -seed 1 -record 1,5,15,30,60,100,150,220,300,400,500,650,800
```

Outcomes vary enormously between seeds, so it is worth running several and keeping the
best. Across six seeds at the same budget the goal counts were 14, 28, 34, 98, 370 and 454.

`-maxSteps 3000000` is the same budget the reinforcement learning project trained with,
which works out at 800 generations.

Game speed needs care. Agents spawn from a coroutine over several frames, so a generation
has to last enough frames for the whole population to appear. At a game speed of 200 a
generation is only a handful of frames and the run quietly continues with a fraction of
its agents. A warning fires if that happens. 60 is about the useful limit, and is also
faster in practice than 200, which is past the point the machine keeps up:

| Game speed | Generations per 75s | Agents spawned |
| --- | --- | --- |
| 20 | 9 | 100 of 100 |
| 60 | 24 | 100 of 100 |
| 200 | 17 | 1 of 100 |

Then bring the recording into the project and rebuild the scene:

```
Unity -batchmode -nographics -projectPath . \
      -executeMethod ImportRecording.Run -recordingFile <path to .bytes>
Unity -batchmode -nographics -projectPath . \
      -executeMethod BuildWebDemoScene.Run
```


## Building

Needs Unity 6000.6.0f1 with the WebGL module.

```
Unity -batchmode -nographics -projectPath . \
      -executeMethod BuildPlayer.WebGL -outputPath Build/WebGL
```

The build uses gzip with the decompression fallback turned on. The portfolio site serves
these files from Firebase Hosting with no `Content-Encoding` headers, so the loader has to
decompress in JavaScript. Turning the fallback off produces a build that works under a
local server and fails in production.

Editor tools and self-checks are in `Assets/Editor/` and run headlessly:

```
Unity -batchmode -quit -nographics -projectPath . \
      -executeMethod NetworkFormatTests.RunAll
```
