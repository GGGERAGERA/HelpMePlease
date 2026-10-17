# Escape Protocol implementation plan

**Goal:** Fund a three-stage persistent project (5/10/15 cores), open a separate final expedition, then permanently complete the global goal after its verified victory. Work directly in the current saved Unity project, preserving the earlier core/task changes.

**Architecture:** Extend the existing MetaProgressionManager (a partial file, not another manager), CurrencyManager and EscapeProtocolView. Reuse the dedicated PF_EscapeProtocolStation at (6.47, 15.34). The existing six rooms, stations and minigames remain in place. A single ScriptableObject defines stage identities/costs, optional future unlock IDs (initially empty) and final sector. Partial investments and currency debit commit in the same save boundary. Legacy guardian EscapeAccess remains intact and credits already achieved stages once, on first migration only.

**Final trial:** A separately gated F1 node on the same surface map/arena. Existing Orbital Relay must be completed during this run before the final Guardian fight can begin. It uses existing event placement/content, world-rule/pressure profiles and the existing Guardian; it is not a new boss, biome or story event. Required Relay is guaranteed by the existing sector event resolver; if its content becomes unavailable the final launch is disabled honestly. Ordinary branches keep their old unlock rules.

- [x] Inventory all six rooms and the existing dedicated terminal. Add focused failing contract tests.
- [x] Implement configurable stage funding, old-save migration and atomic core spending; test partial/repeated/invalid investments, reload and final access.
- [x] Integrate the existing terminal view and bunker HUD. Add final node/data and real Relay-before-Guardian victory condition. Save completion and show it in existing bunker results.
- [x] Author production data/UI/scene bindings, preserve nested prefab IDs, run targeted tests and a real Play Mode terminal/final loop. Capture and inspect terminal screenshot.
- [x] Review changes, compile Release/Development and verify production assets and earlier core/task regression tests. Report actual evidence and exact screenshot path.

**Constraints:** No ImageGen, new rooms, functional station overlays, cosmetic repairs or invented intermediate rewards. No gold changes or modifications to task payouts. Future real unlocks have data IDs and an event hook but no speculative implementations. All test preferences are backed up once across domain reloads, including project state, map strings, core receipts and legacy progress, and restored afterward.
