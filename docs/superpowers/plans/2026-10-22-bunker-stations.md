# Bunker stations continuation

**Goal:** finish the supplied Subject#42 request using the approved character window, minimal copy and cyan/amber/red feedback.

**Architecture:** one production window (the existing character prefab, GUID preserved), one card renderer and one progression renderer. The character detail is preserved; an item detail component composes orbital, upgrade and anomaly presentation. Existing source adapters and progression services retain all purchasing, unlock and save behavior.

**Constraints:** no Deploy, minigame, expedition or save-format changes. No invented weapon silhouettes. Resolve module art from production OrbitalModuleView. Existing scene/component references remain valid. Work in the open Unity checkout to verify the actual scene.

- [x] Audit and regressions: initial tests reproduced independent routing, missing modules and decorative lamp; component IDs audited.
- [x] Migrate presentation: approved prefab retained, lamp removed, shared item detail and hold renderer composed, production art catalog populated.
- [x] Play Mode: 7/7 targeted checks passed, including station/item purchases, confirms, locks, zero gold, maximum, RU/EN, reopening, shell propagation and character-art isolation. Eight real 1600×900 screenshots captured.
- [x] Independent read-only review found no actionable issues. Release and Development script compilation passed. See docs/BunkerStationInterfaces.md and Artifacts/GeneratedQA/BunkerStations/results.xml.

**Review focus:** aliasing old/new serialized window fields must never close the active window; changing cards cancels hold against the old target; locked characters expose no art or identity; source events detach on close; previews use authored image tint and do not double-tint colored sprites.

**Ruling:** user explicitly asked to finish the interrupted implementation from the supplied design; continue in this checkout without another design approval cycle. Native screen capture currently times out, so physical mouse verification may be unavailable; never equate EventSystem checks with manual testing.
