# Runtime and artifact evidence

## Capture the active display

On multi-display hardware, enumerate displays immediately before capture, select the active panel explicitly and compare the image dimensions and orientation with the running game window. A default screenshot can come from the inactive primary display. Open the image and confirm the expected game scene is visible. Capture the same active flow on both sides of the transfer and after return. For a bottom toolbar, record the backing bounds, each slot’s top and bottom clearance and each icon’s painted height after fitting. Pick every slot in the running game and confirm its own tool view or action. Review the unobstructed Player frame and read diagnostic errors from its device log.

For physical XCTest interactions, wait for the game window to finish activation. An app-switcher thumbnail can contain readable game controls while XCTest already reports the app in the foreground. Compare the window bounds and display scale with the reviewed screen, then locate the visible control and anchor the touch to that window. Verify the expected page, modal or gameplay state after the touch. A sent event alone supplies no evidence that the player action reached its target.

## Verify the actual inputs and runner

Record the source revision, Editor and package versions, scene and formal entry, fixture, platform and ABI, effective profile and observer. Bind a compiled observer or driver to the harness source that actually built it. Reopen serialized bindings or the real exported artifact before expensive replay. Reuse a warm environment only while its inputs still match. Sync changed allowed files, preserve unchanged identities and separate startup, import, test, rendering and evidence times.

Exercise the actual consumer path through normal startup, readiness, affected interaction and the relevant failure, retirement or reentry. Read the state produced by the action and actual visual output when in scope. Identify direct method calls, fixtures, scripted or virtual input, simulated geometry, and actual device touch with their corresponding evidence. For scene switching record each readiness milestone, including deferred content or subscenes, and verify teardown and another entry. Keep the task output, native harness and user data under their actual owners.

## Isolated test packaging

Use this workflow when the requested test needs a new Player package. For a requested or project-declared YoFaPublisher build, resolve `yofapublisher` through its installed `publisher-location.json` and follow the canonical checkout's named-game packaging and resource-cleanup references. Use the registered YoFaAssets source when applicable. Publisher determines its source/configuration staging, native build paths and provider handoff. Select that owner before creating a workspace so one request results in one build route.

For an ordinary local Unity test package, use the game's declared CLI builder and [Codex Workflow's project Cache lifecycle](https://github.com/qinbatista/qin-codex-skills/blob/master/workflow-skill/references/project-cache-artifact-policy.md#plan-before-producing-files).

1. Bind the platform, Editor, existing build profile and authorized saved source. Preflight the host tools, expected disk use and cleanup with Workflow. Allocate one fresh registered run in the game's ignored `Cache/temp-*`. Route temporary paths, Unity and native build outputs, vendor download and dependency caches, logs and runtime scratch to that run before launch. Use an existing compatible host by default. Provision a VM only when the user explicitly requests one.
2. Copy the required saved files into a new Unity project in that run. Include `Assets` and their `.meta` files, `Packages`, `ProjectSettings` and verified local dependency inputs. Include saved in-progress changes required by the case. Use independent file copies and keep writable dependencies inside the run. Skip `.git`, `Library`, `Cache`, `Temp`, `Logs`, previous builds and prior snapshots. Verify the selected input inventory and bytes against the copy. If selected files changed during copying, discard the inconsistent copy and freeze the inputs again without interrupting the original Editor or other writers.
3. Start the quiet build immediately from the verified copy when its platform prerequisites are ready. Reuse existing host tooling. Fetch only missing dependencies through their supported task-local paths. When a dependency requires a shared installation or writes outside the run, use a supported contained alternative or report that prerequisite as pending. Only the copied project receives build-time changes. Later source edits stay available to concurrent work and apply to a future run.
4. Read back and verify the exact produced package before installation or handoff. Run only the requested tests against that artifact. Record the artifact hash and observed result. Transfer only explicitly requested deliverables or minimum necessary recovery inputs to their existing owners, with fresh readback before releasing the run.
5. On success, failure, timeout or cancellation, stop and verify the exit of owned processes. Once consumers finish, use Workflow's delivery checkpoint to remove the exact run and verify both path and registration are gone. This includes the copied project, downloads, tool caches, generated native projects, test packages, logs and other temporary outputs. Release installation or runtime resources created solely for this run through their native owners. Keep existing tools, shared runtimes and unrelated work with their original owners. Report any exact path that still has an active or unconfirmed consumer.

## Preserve and verify package identity

Use the game's selected profile and approved defaults for packages and isolated copies. Keep the actual platform identifier, company and product names, localized app names, icons, scenes, version and build settings, orientation, graphics and quality, SDK/service settings and signing choices. Resolve effective host, CLI and native overrides. Apply user-authorized settings exceptions to their exact scope. Isolate test data through an existing supported path that preserves the app identity.

Before delivery, read back the exact APK, AAB, IPA or other target artifact and its effective manifest or `Info.plist` where applicable. Verify names and authored icon bindings, then read installed identity when the test uses an installation. Record evidence from the exact artifact. Keep device or simulator twins, native store outcomes, test providers and built-but-unrun artifacts individually attributable. Use the actual startup, an applicable navigation round trip and the requested playable path when the receiving release requires a smoke test.

## Observe the native runtime

- Watch startup and the changed flow for frame and process continuity. Inspect bounded logs and distinguish managed exceptions, native crashes, OS termination, hangs and expected shutdown. Keep the first actionable diagnostic and rerun the real trigger after repair.
- Keep routine loop and callback logging quiet at its producer, including repeated formatting. Enable narrow diagnostics for a bounded case. Measure idle and active output and check the actual exported profile or installed settings. Restore diagnostic state and preserve useful lifecycle summaries and failures.
- For visual or input evidence inspect fresh native frames, scene geometry and the actual changed interaction. For moving evidence, check real decoded playback, duration, frame timing, action and target identity. Review captures without opening a visible player when the platform supports internal capture and decoding.

## Playable scene review

When asked to assess a playable scene, capture the actual Play Mode or Player state at the intended camera scale. Use motion and input evidence for feedback, control response or stutter. Judge the scene against its genre, platform and brief.

- Check that the player, hazards, goals, interactables, pickups, projectiles and navigation cues stay readable. The camera should show upcoming action and keep important actors in frame.
- Read the HUD, meters, scores and prompts while moving. Buttons and overlays must leave the action visible. Check sprites, lighting, particles, environment and menus together for a consistent visual style.
- Trigger the action. Observe whether input, animation, hits, damage, transitions and audiovisual feedback communicate cause and effect. Check that effects preserve visibility and frame pacing during play.
- Exercise the menus, pause, loss, win and restart states covered by the brief. Confirm that input returns to the expected state after each transition.

## Frame pacing and memory

Resolve the project's real frame limit, active refresh rate, quality and runtime overrides in its declared Unity version and platform. Measure achieved timing separately from the requested setting. Inspect CPU/GPU time, stalls and loading boundaries with a recorded scene, workload, device, graphics settings and observation interval. Keep platform defaults and source-owned timing intact while diagnosing. Compare cold startup, first use, scene transitions and warm steady state separately.

For scene or memory cases, compare repeated entry and actual cleanup on the required Player. Read the relevant resource, lease or native object counts and ownership. Compare OS process, graphics, Unity allocated or reserved memory and managed allocation and collection on their own metrics. Verify that a snapshot completed on the intended target and account for profiling overhead. Attribute shared dependencies once and state whether the observed maximum covers the actual peak. Preserve the workload, visual quality and gameplay behavior during a before/after replay.

## macOS batch startup

Check Unity Bee's native IPC path budget. Use a shorter registered task path when it exceeds the socket limit. On APFS, `/.vol/<device>/<inode>` may address the same task temp directory. Verify the directory identity and bind a short Unix socket that appears inside task Cache before assigning the alias to the child's temporary environment. Keep the project and logs with their original owner.

macOS can write application preferences in a user domain during the build or headless Player startup. The temporary directory and log flags do not select that domain. Use the project's process-scoped preference isolation and verify the exact user preference domains after the Editor and Player exit. Preserve preexisting preferences and the original bundle identity. If a stopped run still has an open native handle, reconcile the exact consumer and repeat Workflow's cleanup check before allocating the next run.
