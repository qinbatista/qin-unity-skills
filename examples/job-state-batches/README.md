# Job State Batches

Five Unity 6000.6.7f1 scenes exercise the [completed Job batch contract](../../unity-code-skill/references/unity-game-code-structure-design.md#completed-job-batches). The Job writes each actor root through `TransformAccess`. The Manager owns the native slots and publishes one borrowed read-only state view after completion. The actor Controller reads its own slot and owns its Animator or its visual child's visibility. The scene Controller reads the authored references and starts the group.

## Five scenes

| Scene | Movement Job | Manager | Actor Controller | Play Mode behavior |
| --- | --- | --- | --- | --- |
| [Enemy chase](Assets/Scenes/GameScene/EnemyChase/EnemyChase.unity) | [EnemyChaseJob.cs](Assets/Scripts/EnemyChase/EnemyChaseJob.cs) | [EnemyChaseManager.cs](Assets/Scripts/EnemyChase/EnemyChaseManager.cs) | [EnemyChaseController.cs](Assets/Prefabs/EnemyChase/EnemyChaseController.cs) | Three enemies chase the player. The decoy overlap preserves membership. The player's collider retires the matching slot. |
| [Five-fireball volley](Assets/Scenes/GameScene/FireballVolley/FireballVolley.unity) | [FireballVolleyJob.cs](Assets/Scripts/FireballVolley/FireballVolleyJob.cs) | [FireballVolleyManager.cs](Assets/Scripts/FireballVolley/FireballVolleyManager.cs) | [FireballVolleyController.cs](Assets/Prefabs/FireballVolley/FireballVolleyController.cs) | Scene start launches one five-way volley. Each fireball reaches its range limit. A late subscriber reads the settled state and hides its visual. |
| [Coin magnet](Assets/Scenes/GameScene/CoinMagnet/CoinMagnet.unity) | [CoinMagnetJob.cs](Assets/Scripts/CoinMagnet/CoinMagnetJob.cs) | [CoinMagnetManager.cs](Assets/Scripts/CoinMagnet/CoinMagnetManager.cs) | [CoinMagnetController.cs](Assets/Prefabs/CoinMagnet/CoinMagnetController.cs) | Move the Collector into range to attract both coins. A collected coin keeps its terminal state and hides its visual on receipt. |
| [Platform patrol](Assets/Scenes/GameScene/PlatformPatrol/PlatformPatrol.unity) | [PlatformPatrolJob.cs](Assets/Scripts/PlatformPatrol/PlatformPatrolJob.cs) | [PlatformPatrolManager.cs](Assets/Scripts/PlatformPatrol/PlatformPatrolManager.cs) | [PlatformPatrolController.cs](Assets/Prefabs/PlatformPatrol/PlatformPatrolController.cs) | Each platform travels to its own endpoint, holds there, and reverses. Its Animator follows the hold state. |
| [Companion formation](Assets/Scenes/GameScene/CompanionFormation/CompanionFormation.unity) | [CompanionFormationJob.cs](Assets/Scripts/CompanionFormation/CompanionFormationJob.cs) | [CompanionFormationManager.cs](Assets/Scripts/CompanionFormation/CompanionFormationManager.cs) | [CompanionFormationController.cs](Assets/Prefabs/CompanionFormation/CompanionFormationController.cs) | The companions settle in their assigned slots. They keep those slots when the Leader translates and rotates. Distances use the unit-scale Leader root. Its Visual child owns display scale. |

```text
Assets/
├── Scenes/GameScene/<Case>/<Case>.unity
├── Scripts/<Case>/
│   ├── <Case>SceneController.cs
│   ├── <Case>Manager.cs
│   └── <Case>Job.cs
├── Prefabs/<Case>/
│   ├── <Case>Actor.prefab
│   └── <Case>Controller.cs
└── Editor/Tools/JobStateBatches/
    ├── BuildBatchScenes.cs
    └── Tests/CompletedBatchCases.cs
```

The animated actors also own their Animator Controller in the actor package. Each scene binds its prefab, target or route markers and movement settings on the scene Controller. Actor prefabs start inactive so the Manager can bind a stable slot before enabling the subscription.

## Run the five cases

[CompletedBatchCases.cs](Assets/Editor/Tools/JobStateBatches/Tests/CompletedBatchCases.cs) opens each saved scene and enters Play Mode. Each case detaches a Controller while its actor moves, checks the read-only batch size and rejects duplicate dispatch in a physics step. It also tests the feature-specific state and behavior after reattachment.

Run from the repository root using a copy of this project in the task-owned Cache and the [Unity CLI wrapper](../../unity-code-skill/references/unity-cli.md). Use `py -3` in Windows PowerShell.

```text
python3 -B unity-code-skill/scripts/unity_cli_daily.py test "<task-project>" --non-interactive --no-pager --mode EditMode --filter CompletedBatchCases --output "<task-cache>/batch-results.xml" -- -nographics -logFile "<task-cache>/batch-unity.log"
```

[BuildBatchScenes.cs](Assets/Editor/Tools/JobStateBatches/BuildBatchScenes.cs) creates the native scene and prefab bindings. Run it on the task-owned copy when changing the fixture setup.

```text
python3 -B unity-code-skill/scripts/unity_cli_daily.py run "<task-project>" --non-interactive --no-pager --log-file "<task-cache>/batch-authoring.log" -- -nographics -executeMethod Qin.UnitySamples.JobStateBatches.Editor.BuildBatchScenes.BuildAll
```
