# Runtime architecture

## Scene composition and lifecycle

The production route is StartScreen → MainMenu (Bunker) → MVP (run) → MainMenu. `ProductionSceneComposition` is the authored composition root: typed references initialize shared localization/audio/progression/configuration services and bind local character, enemy, camera, HUD and reward consumers. Gameplay readiness requires the player, ORBITAL station, modifiers, HUD and camera; Bunker readiness requires its loadout and camera. `SceneTransitionOverlay` reveals ready scenes.

`RunStateManager` owns cross-scene state, snapshots and run identity. Callbacks use the current run identity; stale end/restart requests cannot act on another run. Scene owners register cleanup and release their own roots/leases. `RunEndService` closes a run and `BunkerRunSummaryPresenter` consumes the summary. `RunFlowController` owns sector/exit/final-boss phases and waits for rewards/input before transition. The route contains three exploration sectors, with the boss phase in the final sector.

## Characters, Bunker and ORBITAL

`RunSelectionManager` selects CharacterData. `PlayerLoadoutFactory` creates the authoritative production character and ORBITAL loadout; character visuals are embedded in Gera, DiMag and Vika prefabs. Bunker character selection uses its current panel/window and selection-source flow; the old CharacterSelectionUI/CharacterStationEmbeddedView bundle is removed.

`OrbitalRunState` stores rings, mounts, modules and links. `OrbitalStationRuntime` projects state into authored presentation, combat and interaction. The reward provider/flow handles valid targets and placement; `UpgradeManager` owns queued cards and commits the selected reward. Presentation refreshes do not advance gameplay combat. Input ownership prevents reward/drag and transition conflicts. Existing WeaponData/Pistol/Laser consumers remain; this cleanup does not replace that live path.

Bunker room/station progression, selection, gallery and minigames have their own owners. Context objects supply local interaction dependencies. Shared panels/windows manage their own visibility/input subscriptions. Investments and escape progression remain production behavior.

## World systems

`GameplayAreaService` supplies bounds. `WorldRuleController` applies a world rule; `LevelAnomalyController` tracks local effects/presentation. `ProductionExplorationSectorController` owns sector sites, portals and props. Each `ProductionAnomalySite` owns its environment and objective roots. Event admission/reward registration in `WorldEventSpawner` does not transfer site environment ownership.

Corridor shares production route definitions, kit, config, hazards and `CorridorEvent` between run and Lab. Orbital Relay shares authored nodes, state, body-contact detection, scoped enemy pressure and completion reward delivery. Event cleanup releases its pressure/root without retiring independent sites. Claims and terminal callbacks consume their owner registration before reward/meta callbacks can re-enter.

## UI and development boundary

Production UI uses authored prefabs and scene references: StartScreen/settings, Bunker panels, HUD/tactical map, reward cards/placement, pause and results. The tactical map requires its marker components; optional references remain null only where the consumer allows them. `PhysicalCombatFeedbackRuntime`, settings contracts and visual presentation live in production. F1 menus and tuning controls live in Dev and attach only for Editor/development runs.

WorldSystemsLab and OrbitalLab reuse production services/assets through scene adapters; they do not own alternate gameplay implementations. GoldenPathLab and bots are opt-in Dev tools. Tests remain in the predefined Editor assembly and run by narrow fixture/method filters. Saved-asset validation and production compilation with Dev sources removed enforce the boundary.

No new service locator, registry, broad namespace rewrite or assembly split is introduced. ORBITAL uses `Subject42.Combat.OrbitalStation`; some other domains have namespaces and some retain global types. See [PROJECT_MAP](PROJECT_MAP.md) for paths and `docs/phase7-cleanup.md` for final verification, asmdef decision and remaining limits.
