# ORBITAL INTEGRATION SANDBOX — report

## Scene basis and isolation

- Source: `Assets/_Project/Scenes/MVP.unity`.
- Sandbox copy: `Assets/_Project/Prototype/OrbitalCombatLab/Integration/OrbitalIntegrationSandbox.unity`.
- No production scene, prefab, progression, save, level-up, world-rule or anomaly file is modified.
- Golden Path progression and the original Lab debug UI are disabled in the sandbox.

## Runtime adapters

- Player: late-binds the runtime-created real `CharacterMovement2D`; no fallback player is spawned.
- Enemies: mirrors `EnemyHealth.ActiveInstances` as non-owning targeting proxies. Damage is sent through
  `EnemyHealth.TakeDamage`; despawned targets are dropped without changing production lifecycle.
- World: keeps production sector generation, world rules, anomalies, special sites, events,
  breakables, exit, HUD and lighting intact.
- Camera: uses the existing production camera. Production mode never writes it. Combat/Full Station
  are temporary sandbox overrides and F6 restores production control.
- UI: sandbox-only IMGUI provides START/MID/FINAL, eight compatibility palettes, presentation toggle,
  brightness/saturation controls, camera modes, connection diagnostics and named F8 screenshots.

## Presets tested

- START: 1 ring, 1 weapon, core level 1.
- MID: 6 rings, 12 objects, link nodes, arc emitter, core level 2.
- FINAL: 12 rings, 24 objects, chain links, arc emitters, core level 3; trails and mines disabled.

Manual runtime evidence reached 129 orbital shots and 17 arc hits against real enemies in one FINAL
run. Production player, HUD, generated sector, GravityOrb site, four breakables and sector exit remained active.
The required F9 pass produced 15 screenshots using the real None, Darkness, Rain, Snow and Golden rule
assets plus the three anomaly presentation profiles, then restored the original runtime rule.

## Conflicts found

1. Production camera framing (`orthographicSize` about 8.1) clips MID/FINAL. The measured FINAL radius
   is 13.0; Full Station needs 14.3 and works through the isolated temporary override.
2. The right-side integration panel overlaps the tactical-map legend. F1 hides it; a production UI must
   never reuse this panel.
3. Twelve simultaneously bright rings compete with enemies and HUD even when they technically fit.
   The outer half should progressively fade or collapse to contour-only presentation.
4. Cyan loses separation in Rain/Cold; magenta links compete with Arc/Beam anomaly language. The supplied
   profiles compensate locally, but production should derive contrast from the active environment.
5. The first integration run exposed a real despawn race between `EnemyHealth` and orbital targeting.
   The proxy now validates the target transform at use time; subsequent manual runs produced no new
   OrbitalCombatLab exceptions.
6. Stopping the copied MVP scene can emit an existing `ProductionAnomalySite.OnDestroy` teardown
   `MissingReferenceException`. It also occurs with the orbital presentation disabled and is outside the
   permitted Lab-only edit scope. Active-play integration passes are clean from Lab exceptions.

## Size and palette recommendation

- Technical Lab ceilings remain 64 rings / 768 mounted objects.
- Verified integration ceiling in this sandbox is 12 rings / 24 objects, radius 13.0.
- Recommended production combat envelope: 6–8 readable rings, with 12 reserved for temporary
  full-station spectacle or a finale. Beyond 8, fade line alpha and mounts by depth and avoid all-nearby links.
- Keep cyan for ring topology and magenta for persistent link topology. Reserve white-violet for short arc
  discharges, red for hostile beam/hazard, gold for rewards/Golden world state. Apply environment-aware
  luminance compensation without changing those semantic hues.
- Arc is the strongest compatibility conflict because both the production anomaly and station discharge use
  short violet electrical language. Link Nodes remain readable, but FINAL link density should be capped.
- Events, exit, pickups and breakables remain visible in START/MID. In FINAL they remain functionally present,
  but navigation readability requires Full Station or progressive fading of outer rings.

## Migration recommendation

Do not migrate the prototype wholesale yet. The isolated adapter proves that real player tracking,
enemy damage and world coexistence are viable. A production pass should first extract a data-driven
station model plus three narrow adapters (player anchor, target query/damage, camera request), cap normal
combat at 8 rings, and replace IMGUI with existing HUD conventions. Then ship behind a feature flag and
repeat the full unchecked compatibility matrix before touching progression or saves.
