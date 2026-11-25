# Five particle graphs

## Create and compare the five effects

Each creation starts in a blank URP project. The [author](../scripts/particle-graph-trials/QinParticleGraphAuthor.cs) builds editable VFX Graph nodes from the five briefs below. Each trial makes small, deliberate changes to rates or forces. Compare the physical gesture across the three creations using a fixed camera and random seed.

| Brief | What to look for |
| --- | --- |
| Rain hitting the ground | Drops accelerate downward. Ground contact creates a short fan of finer droplets in a second system in the same graph. Moving the contact plane moves the splash. |
| Rising embers | Warm sparks rise, drift under drag, and fade. Moving the emitter changes where new sparks appear. |
| Fountain | A narrow jet spreads as it rises. Gravity slows the ascent and turns the same droplets downward. |
| Windblown leaves | Leaves travel downwind, descend and turn as they age. The short emission replay makes the drift of one group visible. |
| Attracted motes | A cloud contracts around a target. Moving the target during emission pulls particles toward its new position. |

Use the installed Unity 6000.6.7f1 Editor, Unity CLI and a graphics device with compute support. The fixture selects URP, Shader Graph and VFX Graph 17.6.0 before it authors the assets. Its [launcher](../scripts/replay_particle_graphs.py) requires a fresh child directory for each receiver. Run these commands from the installed `unity-effects-skill/scripts` directory using `python3` on macOS or Linux, or `py -3` on Windows. Replace `<editor>` with the Editor path (the `Unity.app` bundle on macOS) and `<run>` with the project's allocated task Cache directory.

```text
python3 -B replay_particle_graphs.py --work "<run>/one" --editor "<editor>" --trial 1
python3 -B replay_particle_graphs.py --work "<run>/two" --editor "<editor>" --trial 2
python3 -B replay_particle_graphs.py --work "<run>/three" --editor "<editor>" --trial 3
python3 -B particle_graph_checks.py "<run>/one" "<run>/two" "<run>/three" --output "<run>/comparison.json"
```

Open a generated graph under `Project/Assets/GraphTrials` to inspect its emission, simulation and rendering groups. The receiver renders each result through one `VisualEffect`. Inspect the samples in each receiver's `out` directory for progression, shifted inputs, retirement and replay. The [comparison](../scripts/particle_graph_checks.py) evaluates each brief across the three receivers. Device frame-time budgets require a matched capture in the receiving game.
