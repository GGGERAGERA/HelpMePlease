# Surface Map and production bunker

Approved scope, 2026-10-04. Implement inline in the current Unity checkout; no additional approval, broad regression suites, visual polish or football redesign.

## Audit
StartScreen and RunEndService already route to Bunker_OneRoom_Prototype, which is enabled in Build Settings. MainMenu is retained but disabled. The prototype already references production composition, character/weapon, selection UI, pause, exit/depth UI and football arena prefabs. It lacks BunkerContext, notification/event wiring and upgrade/anomaly stations. Composition initializes persistent services only when absent; reuse it rather than another bootstrap.

## Contract
SurfaceMapDefinition owns a stable map ID, bunker display position, sector references and starting IDs. SurfaceSectorDefinition owns a stable sector ID, adjacent IDs, map position/display data and RunConfig parameters. Edges are reciprocal. MVP is BUNKER-A1-A2, BUNKER-C1-C2, BUNKER-D1-D2. A1/C1/D1 start available. Completed nodes remain replayable; any available node can be chosen from bunker (frontier expansion, not an enforced current-position route).

RunConfig is a per-run snapshot of numeric modifiers and weighted prefab references, plus existing layout/spawn/rule/anomaly assets and optional BiomeId/ThemeId. RunStateManager owns it across internal stages and restart. Surface selection knows no gameplay controllers. Gameplay consumers read the snapshot and combine it with existing StageProfileData and WorldRuleData. The configured world rule applies to the initial location only. Later locations always offer the existing random world-rule choices, and their selected rule takes precedence. Legacy/dev starts use neutral defaults and no surface identity.

Progression state is plain serializable data; a service owns graph validation, selection, completion and storage via the project's PlayerPrefs mechanism. Save is namespaced by stable map ID, using sector IDs rather than positions. Unknown saved IDs are ignored when displaying the current graph. Only the existing confirmed production victory boundary may complete the selected node; abort, death and development runs never advance it. Reward handling remains at the existing end-run boundary.

## MVP
A: lower threat growth/pressure, XP bonus. C: higher initial threat/pressure, bomber prefab weight and gold bonus. D: event/site and anomaly weights, existing world rule, distinct existing layout configuration. No new enemies or art. UI uses authored reusable panel primitives, BUNKER, graph lines, state buttons, details and launch. Escape protocol access UI remains separate from map selection.

Bunker_OneRoom_Prototype is authored as the production scene with one context/panel/controller set, production station prefabs, notifications/events/summary UI, shared progression/services and existing football arena integration. MainMenu remains unchanged as reference and disabled build fallback. No runtime scene copying.

## Evolution and verification
BiomeId/ThemeId travel through RunConfig; later a resolver can select layout/enemy pools, visual theme and rules before startup without changing map progression or UI.
Targeted EditMode tests cover graph unlock/save/replay/invalid selection, config snapshots, non-victory guards, scene dependencies and routing. Targeted runtime smoke covers bunker startup and actual configured run entry; report any Unity execution limitations honestly.
