# Critical development tests

Core contains **16 scenarios in 7 test files**, plus one shared setup helper (CoreTestSupport.cs). Extended contains **0 scenarios / 0 files**.

Run only the NUnit **Core** category in Unity Test Runner. Most scenarios are short; the one production Golden Path uses the existing Bot with a fixed seed and a five-minute safety bound.

- Characters (3): Gera/Circle, Di-mag/FigureEight and facing binding, Vika/Custom.
- Orbital (4): ring/mount/module transaction; Link pair and Core; Vika custom paths on multiple rings; complete sector state restore.
- Rewards (5): Level Up displayed/granted; normal anomaly cards; chest reel stopped/granted; Casino Link grant; special anomaly ring grant.
- Lifecycle (2): death to bunker to clean second run; victory end boundary to bunker to clean second run and exactly-once gold commit. Actual boss victory is covered by Golden Path.
- Golden Path (1): production S1 → S2 → S3 → boss → bunker → second run.
- Localization (1): authored bunker RU → EN → RU.

Tests use the existing Editor assembly. There are no partial fixtures or dependencies on a hidden Extended suite. Setup restores modified progress and language preferences after integration checks.

GoldenPathLab/Bot, F1, OrbitalLab and WorldSystemsLab remain manual development tools. Add a targeted test only when a new critical contract warrants it.

Current navigation: [PROJECT_MAP](../../Documentation/PROJECT_MAP.md).

Orbital Relay migration adds narrowly filtered OrbitalRelayStateTests, OrbitalRelayContactTests, OrbitalRelayIntegrationTests, OrbitalRelayAuthoringTests and OrbitalRelayRuntimeTests. The runtime fixture runs a bounded production-prefab flow with real movement/contact in WorldSystemsLab. These fixtures run by exact class/name; do not select Core or Golden Path for relay acceptance. Subject42TutorialTests uses real Node tracking, Bonus and the shared Upgrade barrier; its long full route is compile-only during this migration.
