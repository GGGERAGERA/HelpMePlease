# Development tests

Run the NUnit **Core** category in Unity Test Runner for bounded validation. Core contains **32 scenarios / 34 expanded cases / 12 files**. Extended contains **207 methods / 290 cases / 56 files** and is not part of the Core run.

Core protects character identity/facing (Gera Circle, Di-mag FigureEight, Vika Custom), ORBITAL commands and state, rewards and eligibility, lifecycle/cleanup, a separate GoldenPath route for each character, and RU/EN production screens.

Both groups stay beneath an Editor directory in the existing Editor assembly. Partial fixtures share setup/helpers across groups; filter by category, not by fixture name or folder.

Extended retains distinct malformed-state matrices, renderer/math invariants, pooling/audio limits, physical scene transactions, authoring/lab contracts and visual layout assertions. Historical screenshot/report writers, duplicate GoldenPath batch wrappers and request-file runners were removed. Input matrices consolidated into Core still execute every original variant.

Batch sizes, seeds and failure replay remain available in GoldenPathLab. Tests do not require a pre-existing local history file.

Current project navigation: [PROJECT_MAP](../../Documentation/PROJECT_MAP.md).
