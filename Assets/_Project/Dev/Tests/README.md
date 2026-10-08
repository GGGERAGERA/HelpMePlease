# Development tests

Tests live in `Core/Editor` and the predefined Editor assembly. Folder/category names are navigation, not a request to run every scenario.

| Purpose | Examples |
| --- | --- |
| Core contracts | ORBITAL state/transactions/rewards, run identity/selection, world event state |
| Regression/integration | Ownership, sector snapshots, cleanup, contacts, surface progression |
| Authoring/integrity | Scene/prefab/config dependency boundary, references, routes and authored assets |
| Dev/Lab | Guarded input/menu/settings contracts and shared runtime adapters |

Use exact fixture/method filters for a limited change. `Core` includes the long Golden Path and must not be used for limited cleanup acceptance. GoldenPathLab and Batch Runner are opt-in development tools.

`TutorialVerificationRunner` accepts semicolon-separated fully qualified fixture/method names in `Artifacts/GeneratedQA/Tutorial/run-tests.request`. Its callbacks save only its own run, reject empty execution and preserve failure output. Other runners own separate generated folders. Refresh/import and compilation must finish before queuing a run.

`ProductionBoundaryAuthoringTests` validates saved production scenes, prefabs/configs and their dependency closure. It distinguishes explicit optional nulls from missing GUID/local fileID/subasset references, includes renderers and MonoBehaviours, and preserves dirty open scenes. Fixture cases verify null acceptance, broken references and saved-scene inspection.

`Phase7SmokeTests` has two explicit bounded scenarios: StartScreen → Bunker → Run → completed authored Relay → displayed/clicked reward → Bunker, and WorldSystemsLab shared Relay startup/owner cleanup. These are independent of bot batches. Other longer integration fixtures remain available by explicit selection.

Shared startup/wait/preference restoration lives in `CoreTestSupport`. Tests must assert behavior or authoring contracts; private-field names, source text and fixed hierarchy counts do not constitute gameplay contracts. Add checks only for meaningful regressions.

Script-only release/development validation is available through `PlayerScriptCompilationCheck`. `Tools/QA/Test-ProductionScriptBoundary.ps1` at repository root uses actual Unity response files and removes every project Dev source before recompiling. No custom asmdef routing is required.

See [PROJECT_MAP](../../Documentation/PROJECT_MAP.md) and repository `docs/phase7-cleanup.md` for current acceptance results and the final assembly decision.
