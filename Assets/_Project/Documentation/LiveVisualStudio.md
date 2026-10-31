# Visual profiles — runtime and authoring

## Current scope — 31.10.2026

The F1 interface and the development-only Live Visual Studio side panel have been removed at the user's request. The panel's picking, window settings, input ownership, cursor lifecycle, clipboard actions and named-file Save/Load no longer exist. Its dedicated Editor verification launcher has also been removed. No replacement interface is introduced.

The production visual profile system remains in place. Removing the development interface does not change the selected production snapshot, saved profiles, authored visual values or independent laboratory tools.

## Runtime ownership

[`ProductionSceneComposition`](../scripts/Bootstrap/ProductionSceneComposition.cs) attaches [`LiveVisualRuntime`](../scripts/World/Presentation/LiveVisual/LiveVisualRuntime.cs) with the existing `VisualTuningPreset`. In a release build, `Attach` returns without creating a runtime when `LiveProductionProfile` is unassigned. An assigned profile is validated, copied without Instance overrides and applied through the existing scene presentation path.

The runtime registry and component adapters are production dependencies. Enemy factories, pooled effects, scene props, warning/explosion effects, enemy health presentation and ORBITAL creation register actual scene components. Pool reuse captures the owner's configured presentation and applies overrides before activation and synchronous emission. These hooks remain intact.

[`VisualComponentAdapters`](../scripts/World/Presentation/LiveVisual/VisualComponentAdapters.cs) apply supported renderer, material, light, camera, volume, particle and trail properties. Runtime material clones, property blocks and cloned ORBITAL configuration protect authored assets. Temporary combat flash material ownership remains separate from persistent body correction.

[`OrbitalVisualAdapter`](../scripts/World/Presentation/LiveVisual/OrbitalVisualAdapter.cs) remains responsible for existing weapon/projectile/FlashLink presentation. Compatibility application of saved RingSurface, Tier I–IV, line and ring highlight/dimming property IDs remains in `BindSavedRingValues`. Removing their former controls must not drop values already present in production or user profiles. Component targets and compatibility application are unchanged by the interface removal.

## Profile contract

[`VisualProfile`](../scripts/World/Presentation/LiveVisual/VisualProfile.cs) stores sparse overrides. Imported profiles resolve Instance → Type → Group. Production serialization excludes Instance entries without modifying the live profile. Copies own their Values, Notes and material provenance entries independently.

The existing runtime API retains Original, Current and Custom comparison, load/export, reset and undo/redo. Original is the explicit Git baseline recorded in `LiveVisualOriginal.json`; Current is captured when a component is registered, including the owner's configured pooled values; Custom layers profile overrides over Current. Switching comparison modes preserves Custom. A missing Original value uses the captured Current value rather than claiming an unavailable historical default.

Profile validation rejects unsupported versions, scopes, duplicate property keys, non-finite values and invalid parameter counts/keys before `LiveVisualRuntime.Load` changes the session. The deleted panel's decoded-text length limit and bounded file reader belonged to that UI's file import/export path; they are not new limits on production profile loading.

## Existing authoring tools

`LiveVisualStudioAuthoring.Prepare` remains at its existing Editor menu item, `Tools/Subject42/Visual Studio/Prepare production catalog`. It binds the explicit original baseline, supported shader, prefab source roots and historical material references to the existing preset. This is independent catalog authoring needed by the runtime's stable source identities; it does not select or replace a production profile.

`LiveVisualBaselineAuthoring` remains the independent historical-baseline exporter. It reads explicit Git objects without checking out the revision or importing historical assets into the current project. The F1 removal does not run either authoring action.

The existing `VisualTuningSavedValues.asset`, `LiveVisualProduction.json` and `Profiles` contents are user-owned data. This change leaves them untouched. Independent Visual/FEEL/laboratory implementations remain subject to their existing entry points and ownership.

## Verification boundaries

`LiveVisualProfileTests` retains catalog/prefab/material references, temporary combat-flash material ownership, precedence, invalid profile validation and independent production serialization. The former named-file UI scenario is adapted to direct runtime export/load: Type/Group persistence, Instance exclusion without changing the live session, visible alpha restoration, rejection of an invalid profile before changing session state, unchanged production references and authored asset byte equality. It creates no profile files and does not replace the global runtime. The cursor and bounded-reader scenarios were specific to the deleted interface and are removed with it.

`LiveVisualProductionTests.ProductionTypesNewSpawnsParticlesLightsAndProfileRoundTrip` now operates directly on `LiveVisualRuntime`. The production startup, two enemy types, shader/material isolation, new spawn/reuse, Global Light 2D, particles, actual pool identity, owner configuration, first synchronous Emit, JSON round-trip, comparison restoration and authored asset byte checks remain. Hub/panel opening and the hub screenshot are removed. Future scenario artifacts use `Artifacts/GeneratedQA/LiveVisualRuntime`; its PNGs still require visual observation.

`OrbitalVisualAdapterTests` and `PooledVisualConfigurationTests` remain unchanged, including saved ring-profile compatibility and reuse configuration checks.

For the interface removal, verification is static only: references and scoped diffs are inspected. Unity, compilation, tests and builds are not run, and no new PASS or visual acceptance is claimed. Older XML and screenshots describe earlier code and do not validate this removal.

## Historical evidence retained as context

The earlier `live-visual-profile-material-source-copy-isolation` work established independent material provenance copies. Its managed-code evidence is recorded in `Artifacts/GeneratedQA/live-visual-profile-material-source-copy-isolation-20261028.txt`. The older cycle-70 Unity result failed after inline `JsonUtility` normalized a null material-source entry; the current serialization test separates direct managed null copying from JSON checks using real sources. This history is not a fresh Unity result.

The earlier production pool-source diagnostic distinguishes managed-null from Unity-null and checks the selected catalog prefab against the authored asset by identity, persistence and path. It preserves the original suitability criteria: a root ParticleSystem, `stopAction=None`, constant one-dimensional size and no root MonoBehaviours. Those diagnostics remain in the runtime production scenario; no synthetic fallback or runtime repair is introduced.

Historical successes for the removed panel's named-file Save/Load and size checks are no longer current feature contracts. The unfinished cursor restoration investigation is retired with its owning interface. Runtime checks and manual visual acceptance remain separate levels of evidence.
