# Measuring a Unity optimization

## Compare the same workload

Write down the behavior and the budget before choosing an implementation. A 60 Hz presentation has about 16.7 ms between frames. Use the actual game's target and inspect the limiting thread or GPU pass. CPU and GPU work can overlap. Averages can hide a hitch on the first impact or a missed frame when many drops arrive together.

1. Reproduce the same scene, camera, seed or input sequence, actor count, spawn rate and peak concurrency. Record the device, build type, resolution, quality, graphics API, VSync or frame cap and capture duration. Distinguish cold loading, warm steady state and scene exit/reentry. Keep the profiler settings the same across runs. Use a development Player on the intended device for budget claims. Label measurements from the Editor with their actual scope. [Unity Profiler data collection](https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-profiling-applications.html)
2. Locate the first expensive frame or phase. Read the main, render and worker threads in the CPU Timeline. Determine what a wait is waiting for before changing code. For GPU work, capture the relevant pass on a supported backend. Use a narrow marker or allocation call stack to identify the owner. Deep Profiling can change timing, so repeat the comparison with normal instrumentation. [CPU Usage Profiler](https://docs.unity3d.com/6000.0/Documentation/Manual/ProfilerCPU.html)
3. Change one cause. Replay at normal load and the same peak, then repeat under comparable conditions. Report a distribution such as median and p95 or p99 frame time, sample count or duration, and the worst spike tied to the trigger. For a one-shot load, compare repeated load durations. Record the behavior and the resource cost the change trades against, such as retained pool memory or extra GPU work.

| Suspected cost | Measure | Preserve in the replay |
| --- | --- | --- |
| Actor scans or Jobs | Time per simulation tick including scheduling, completion waits and presentation, visited versus active actors | Same actor states, event order and response latency at idle and peak |
| Allocation or spawning | Managed bytes/frame after warm-up, collection spikes, creations per burst, active and inactive high-water marks | Capacity exhaustion policy, reset of a reused instance and final retirement |
| Loading or retention | Cold time to interactive, longest activation frame, active leases and retained assets across repeated reentry | Successful load, failure, late completion after exit and clean reentry |
| Physics | Physics step time, bodies, active contacts and candidate pairs in the densest region | Required hits, trigger delivery, resting contacts and fast crossings |
| Shaders and VFX | GPU pass time, covered pixels or overdraw, draw calls and live particles at peak camera coverage | Same visible cue, simultaneous hit positions, lifetime, sorting and culling |

Use the receiving game's [runtime evidence workflow](../../unity-testing-skill/references/runtime-and-artifact-evidence.md#observe-the-native-runtime) for the actual replay. For example, replay the same pickup wave twice, check that each item awards once, and compare the frame that attracts the whole wave. A lower physics sample with higher total frame time calls for another design. If the second capture is unavailable, report the verified behavior and leave the size of the improvement open.
