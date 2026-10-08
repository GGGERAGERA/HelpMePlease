# Phase 6 QA tooling

Generated screenshots, XML results, text dumps, JSON/CSV bot reports, request files,
and temporary player compilation output belong under `Artifacts/GeneratedQA/`.
The root ignore rule covers every owner directory below it. Handwritten docs and
authored production assets remain in their normal source folders. The reward-card
text dump is now `Artifacts/GeneratedQA/RewardStyle/reward-card-texts.ru.txt`.

## Targeted editor runners

These runners remain small entry points for their existing fixtures. They share
`Subject42.Verification.AnyRunActive` to defer requests while another test run is
active. Each runner sets its own session ownership flag immediately before
`Execute`; its globally registered callback writes `results.xml` only when that
flag is set. `RunStarted` observes global activity without claiming report
ownership. Finish, API errors, and synchronous execution errors release ownership.
Session state survives the domain reloads caused by tests entering Play mode.

| Owner folder under `Artifacts/GeneratedQA/` | Request | Selection |
| --- | --- | --- |
| `CompactHud` | `run.request` | Empty: HUD fixtures; otherwise one fixture/test name |
| `RewardColors` | `run.request` | Empty: reward presentation/progression/anomaly fixtures; otherwise one fixture/test name |
| `StartScreen` | `run-tests.request` | Start-screen presentation fixture |
| `SurfaceMap` | `run-tests.request` | Existing map/mission/marker/sector aliases; `refresh-only` imports assets and runs no tests |
| `Tutorial` | `run-tests.request` | Empty: tutorial fixture; `Core`: category; otherwise semicolon-separated fixture/test names |
| `WorldHazards` | `run-tests.request` | World-hazard logic and presentation fixtures |
| `ProductionFonts` | `run-tests.request` | Production-font fixture |
| `OrbitalRelay/Presentation` | `check.request` | Relay presentation fixture |
| `CorridorMigration` | `verify.request` | `all`: existing corridor regression selection; otherwise semicolon-separated fixture/test names |

An owned completed run preserves Unity's result XML. A selection with no passing
or failing cases also emits `run-error.txt` and a Console error, so a misspelled
filter cannot look like a successful zero-test run. Skipped and inconclusive
details remain in the XML. A later completed selection with executed cases removes
the stale error file. Test API setup/build errors write `run-error.txt` and release
the runner's ownership.

`CorridorMigration/compile.request` remains the older corridor compilation entry
point and writes `compile.result` plus `PlayerScripts` in its owner directory. It
waits until test runs finish and does not compile in the same poll that starts a
corridor test selection. It is retained for existing callers; the Phase 6 compile
check is a separate tool.

## Runtime Bot Lab ownership

`BotRunSession` owns standalone run reporting and writes
`BotRuns/latest_run.json`. `BotBatchRunner` owns sequential orchestration and
batch checkpoint/final JSON and CSV in `BotBatches`; Golden Path history and its
Markdown summary are also in that directory. `GoldenPathLab` reads that history
and optionally writes screenshots to `BotBatches/Screenshots/<batch>/`.

Golden Path and Batch are retained development tools with separate responsibilities.
The Phase 6 tooling audit does not execute them. Their runtime output paths already
used `Artifacts/GeneratedQA/` and are ignored. The older `Artifacts/BotRuns/` and
`Artifacts/BotBatches/` ignore rules remain compatible with old local captures.

## Authoring diagnostics

Mission, surface-map/marker, relay, relay-presentation, and relay-lab authoring
requests and diagnostic reports now use their corresponding QA owner folders.
Surface-map preview renders and character-selection prototype screenshots also
use those ignored folders. These path changes preserve intentional authoring of
prefabs, scenes, configuration, and production artwork under `Assets/`.

The targeted runner selections were checked against the retained fixture classes
and methods after Phase 6 cleanup. No Golden Path/Batch runner was removed and no
combined test runner was introduced.
