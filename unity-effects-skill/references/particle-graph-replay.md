# Five particle graphs

## Create one particle graph

For one of the five briefs below, copy the [graph author](../scripts/particle-graph-trials/QinParticleGraphAuthor.cs) and [assembly reference](../scripts/particle-graph-trials/VisualEffectGraph.Editor.asmref) together into an Editor folder in a compatible URP receiver. From an Editor script call `QinParticleGraphAuthor.Create("Rain")`, substituting `Embers`, `Fountain`, `Leaves` or `Motes` for the desired subject. A background Unity CLI run can set `QIN_VFX_SUBJECT` and execute `QinParticleGraphAuthor.CreateSelected`. The optional trial number selects a recipe variation from 1 through 3 and defaults to 2. The author saves one new graph and its particle mask under `Assets/GraphTrials`; an occupied graph path stops the operation. Assign the graph to a single `VisualEffect` and bind its local `Driver` input. Rain also exposes local `GroundHeight` in metres.

To bind a player, cache `Shader.PropertyToID("Driver")` in the owning script. When the player moves, write `effect.SetVector3(driverId, effect.transform.InverseTransformPoint(player.position))`. This converts a world position to the example's local simulation space once. For a rain emitter, this shifts future births. For attracted motes, it changes the force on particles already alive. Adapt the simulation space and bounds if the effect host itself has to move while released particles stay in the world.

## Create and compare the five effects

Each creation starts in a blank URP project. The [author](../scripts/particle-graph-trials/QinParticleGraphAuthor.cs) builds editable VFX Graph nodes from the five briefs below. Each trial makes small, deliberate changes to rates or forces. Compare the physical gesture across the three creations using a fixed camera and random seed.

| Brief | Birth rate and lifetime | Capacity | Motion to verify |
| --- | --- | --- | --- |
| Rain hitting the ground | 58–66 drops/s until contact, up to 0.82 s within the declared range. Each contact emits three droplets lasting 0.22–0.50 s. | 64 drops + 128 splash | Drops accelerate downward. Ground contact creates a short fan of finer droplets in a second system in the same graph. Moving the contact plane moves the splash. |
| Rising embers | 37–41/s for 1.35–2.15 s | 96 | Warm sparks rise, drift under drag, and fade. Moving the emitter changes where new sparks appear. |
| Fountain | 72–80/s for 1.10–1.50 s | 128 | A narrow jet spreads as it rises. Gravity slows the ascent and turns the same droplets downward. |
| Windblown leaves | 13–15/s for 1.60–2.55 s | 44 | Leaves travel downwind, descend and turn as they age. The short emission replay makes the drift of one group visible. |
| Attracted motes | 42–46/s for 1.25–2.05 s | 104 | A cloud contracts around a target. Moving the target during emission pulls particles toward its new position. |

Use the installed Unity 6000.6.7f1 Editor, Unity CLI and a graphics device with compute support. The fixture selects URP, Shader Graph and VFX Graph 17.6.0 before it authors the assets. Its [launcher](../scripts/replay_particle_graphs.py) requires a fresh child directory for each receiver. Run these commands from the installed `unity-effects-skill/scripts` directory using `python3` on macOS or Linux, or `py -3` on Windows. Replace `<editor>` with the Editor path (the `Unity.app` bundle on macOS) and `<run>` with the project's allocated task Cache directory.

```text
python3 -B replay_particle_graphs.py --work "<run>/one" --editor "<editor>" --trial 1
python3 -B replay_particle_graphs.py --work "<run>/two" --editor "<editor>" --trial 2
python3 -B replay_particle_graphs.py --work "<run>/three" --editor "<editor>" --trial 3
python3 -B particle_graph_checks.py "<run>/one" "<run>/two" "<run>/three" --output "<run>/comparison.json"
```

Open a generated graph under `Project/Assets/GraphTrials` to inspect its emission, simulation and rendering groups. The receiver renders each result through one `VisualEffect`. Inspect the samples in each receiver's `out` directory for progression, shifted inputs, retirement and replay. The [comparison](../scripts/particle_graph_checks.py) evaluates each brief across the three receivers. Device frame-time budgets require a matched capture in the receiving game.
