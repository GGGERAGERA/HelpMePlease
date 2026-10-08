# Source and asset layout

| Folder under `Assets/_Project` | Current responsibility |
| --- | --- |
| `scripts/Bootstrap` | Typed production scene composition |
| `scripts/Run` | State, flow, snapshots, rewards/results and surface route |
| `scripts/Bunker` | Interaction, UI, progression, gallery and minigames |
| `scripts/Combat` | Player/enemies, ORBITAL, weapons, anomaly powers, feedback/effects |
| `scripts/World` | Events, rules, anomalies, hazards, props, spawning and presentation |
| `scripts/UI` | Shared windows/input, HUD, pause, settings and transitions |
| `scripts/Progression`, `Selection`, `Missions` | Rewards/unlocks, current selection and mission contracts |
| `scripts/Data`, `Audio`, `Localization`, `Pickups` | Asset definitions and shared runtime services |
| `Data` | Authored production configs/catalogs |
| `prefabs` | Authoritative characters, Bootstrap services, Bunker, UI, world and combat assets |
| `Dev/Debug` | Guarded Editor/development runtime tools and adapters |
| `Dev/Editor` | Editor-only authoring/diagnostics |
| `Dev/Tests/Core/Editor` | Core contracts, regression, authoring and Dev/Lab tests |
| `Dev/Labs` | Manual development scenes/prefabs |
| `Documentation` | Current project/system navigation |

Production Corridor's entire feature bundle is under `scripts/World/Events/Corridor`, `Data/WorldEvents/Corridor` and `prefabs/Environment/WorldEvents/Corridor`. ORBITAL miniWeapon sources remain in `prefabs/miniWeapons`.

Moves preserve `.meta` GUIDs. No mass namespace or folder reorganization accompanies cleanup. Runtime sources use the predefined Assembly-CSharp; Editor tools/tests use Assembly-CSharp-Editor. `Artifacts/GeneratedQA` at repository root contains ignored reproducible output. Historical reports are evidence of their recorded run, not current architecture.
