# Escape Protocol

The existing `PF_EscapeProtocolStation` in the HUB at `(6.47, 15.34, 0)` opens the core-funded project. All six authored bunker regions and their stations/minigames remain in place. MainMenu already references the edited `PF_RunDepthSelection` UI prefab; no scene or room was added.

`Assets/_Project/Data/Meta/EscapeProtocol.asset` defines three stable stage IDs, localized titles/objectives, costs **5 / 10 / 15**, optional future unlock IDs, and the final sector reference. The first two stages have no unlock rewards. All unlock ID arrays are empty. The terminal shows total invested cores, stage state, current investment, wallet balance, and the exact next contribution. One explicit click spends up to the current stage's remaining cost; a second click cannot charge the completed stage again.

`MetaProgressionManager.EscapeProtocol.cs` extends the existing manager. `META_ESCAPE_PROTOCOL_PROJECT_V1` stores partial investments by stage ID, stage completion, final victory, and completion-message acknowledgement. Currency debit and project changes are staged before one `PlayerPrefs.Save`, then UI notifications run. Gold and expedition task payouts retain their existing behavior.

On the first migration only, existing `EscapeAccess` credits the corresponding already-earned stages without taking cores. The legacy Guardian keys remain intact. Later Guardian victories cannot replace paid project progress. The bunker HUD uses the current project objective, then the final objective, then permanent completion.

Funding all stages opens independent **F1** on the existing surface map. Ordinary six-sector branches keep their unlock rules. F1 uses the same arena, three-location route, existing profiles, and existing Guardian. It guarantees the real **Orbital Relay** through the production event resolver. Completing that Relay during this expedition is required before the final exit/Guardian phase; timer expiry alone cannot bypass it. Enemy health remains `1x`; its distinguishing rule is the required event plus existing threat/spawn pressure. F1 excludes random Rocket Storm arenas because their footprint cannot fit every ordinary site; other sectors retain their existing pools. Missing, unavailable, disallowed, or invalid mandatory content disables launch.

Only a confirmed production F1 victory with the required event satisfied completes the project. Death, returning early, ordinary victories, and development runs do not. Completion is saved separately and shown through the existing bunker result notification. Ordinary expeditions remain playable afterward.

Primary changes:

- Data: `Data/Meta/EscapeProtocol.asset`, `Data/SurfaceMap/Sector_F1.asset`, existing map/localization assets and MetaProgression/UI prefab bindings.
- Progression: `EscapeProtocolDefinition`, `MetaProgressionManager.EscapeProtocol`, `CurrencyManager`.
- Presentation: `EscapeProtocolView`, `BunkerHudView`, `SurfaceMapView`, `BunkerRunSummaryPresenter`.
- Run rules: `RunConfig`, `RunContentResolver`, `RunStateManager`, `RunFlowController`, `RunEndService`, `BunkerRunStarter`, `HUDManager`.
- Authoring/verification: `EscapeProtocolAuthoring`, `EscapeProtocolTests`, `EscapeProtocolFlowTests`, guarded preference backup in `CoreTestSupport`.

The EditMode tests cover partial funding/reload, stale/repeated requests, insufficient balance, overpayment caps, migration, independent F1 access, rejected completion paths, saved completion, and ordinary access. The Play Mode fixture uses the real station/UI, event contacts, both location-choice transitions, final exit condition, Guardian damage/death, bunker return and a new session. It seeds temporary test cores, skips travel/timer waits, and restores the user's preferences. It is not a manual standalone-build playthrough.

Terminal screenshot: `D:\fork\HelpMePlease\Artifacts\GeneratedQA\EscapeProtocol\escape-protocol-terminal.png`.

Verified on 2026-10-17:

- All five project EditMode tests and the full new Play Mode scenario passed, including real Relay contacts, both location transitions, Guardian victory, completion after a new session, and launching an ordinary A1 expedition afterward.
- Targeted regression batch: **31/32 passed** (`Artifacts/GeneratedQA/EscapeProtocol/regression-initial.xml`). The existing `ExpeditionCoreFlowTests` hit an intermittent ordinary-sector Rocket Storm placement rejection. Its isolated rerun passed **1/1** (`regression-retry.xml`). This is an existing placement limitation outside F1, not a clean first-pass suite; no ordinary-sector pool was changed.
- Release and Development player script compilation passed. Both also compiled with all project Dev/Lab sources excluded: 441 runtime sources, 63 excluded Dev sources per configuration. Reports are `player-compilation.txt` and `production-boundary.txt` in the same QA directory.
- Production asset dependency/missing-reference check passed. `git diff --check` passed.
- Registry comparison against the pre-task backup found **zero changes** to the user's cores, gold, Escape progress, core receipts, last expedition reward record, and surface-map preferences after test cleanup. All files from the pre-task workspace snapshot remain present.
- Terminal and completion screenshots were visually inspected. No manual standalone-build walkthrough was performed.
