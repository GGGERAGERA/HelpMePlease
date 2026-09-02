# ORBITAL INTEGRATION SANDBOX — QA matrix

Scene basis: `Assets/_Project/Scenes/MVP.unity` copied to `OrbitalIntegrationSandbox.unity`.
The source scene must remain byte-identical. Golden progression is disabled in the copy.

For every row, verify START, MID and FINAL presets; player movement; real enemy targeting/damage;
HUD/camera/world-event continuity; ring, link, core and arc readability; no second player/camera/light.

| Environment/profile | START | MID | FINAL | Required observation |
|---|---:|---:|---:|---|
| Base World | ✅ | ✅ | ✅ | START is clear. FINAL is readable only with Full Station framing; production framing clips it. |
| Darkness | ✅ | ✅ | ✅ | Compensation keeps thin rings readable without washing out darkness. |
| Rain | — | ✅ | ✅ | Rings remain distinct from rain streaks; links do not merge with wet highlights. |
| Cold / Snow | — | ✅ | ✅ | Boosted cyan remains visible, although it is closest to the world hue. |
| Golden | — | ✅ | ✅ | Magenta/cyan identity remains distinct from gold enemies and rewards. |
| Anomaly Arc | — | — | ✅ | Persistent links remain readable, but Arc is the strongest semantic/color conflict. |
| Anomaly Gravity | — | — | ✅ | FINAL profile stays legible over the production GravityOrb sector. |
| Anomaly Beam | — | — | ✅ | Red hostile beam remains semantically distinct from cyan rings/magenta links. |

Controls: F1 toggles integration UI, F8 captures a named QA screenshot, hold Tab for temporary
full-station framing. `PRODUCTION` restores the copied scene camera immediately.

Representative manual evidence:

- `QA/20260902_135628_Start_BaseWorld.png` — real production player/HUD/world, START.
- `QA/20260902_135737_Mid_Darkness.png` — MID, 6 rings / 12 objects.
- `QA/20260902_135852_Final_AnomalyGravity.png` — FINAL over a GravityOrb production sector.
- `QA/20260902_140942_Final_BaseWorld.png` — FINAL full-station framing, radius 13 / ortho 14.3.

F9 generated 15 `MATRIX_*.png` captures using the real None/Darkness/Rain/Snow/Golden assets and
restored the original runtime rule afterward. Dashes are combinations the requested matrix marked optional
or did not request. A separate adapter-off capture confirms the production level-up screen continues alone.

Known console item: stopping the copied MVP scene can emit an existing `ProductionAnomalySite.OnDestroy`
teardown `MissingReferenceException`. Final active-play passes had no new OrbitalCombatLab exceptions.
