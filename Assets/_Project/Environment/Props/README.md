# Production sector props

Nine visual-only prefab variants reuse the existing `BunkerCase1`, `BunkerElements1`
and `BunkerElements3` textures through serialized Sprite assets. No texture copies,
colliders, behaviours, loot, lights or minimap markers are added to the prefabs.

Prefabs: `Resources/SectorProps`. Sprite assets and placement helper live here.
Regenerate assets with **Tools > Subject42 > Environment > Author sector props**.
Source crop rectangles, scale and palette are in `Editor/ProductionPropsAuthoring.cs`.

`ProductionExplorationSectorController.Initialize` calls `ProductionSectorProps.Place`
after placing sites, exit and breakables. This connects the props to every production
MVP sector, including the final sector. They are destroyed with the sector.

- Target: one prop per 42 square world units, currently 238 per 100 x 100 playable area.
- About 10–20 props per typical view, depending on ORBITAL framing, zoom and position.
- Rejection-sampled groups of 1–3 distinct variants; anchors at least 4.5 units apart.
- Each prop has at least 1.15 units of center spacing; no grid or repeating group sequence.
- Keep 2 units from map edges/breakables, 3 from the initial player, 3.5 from site centers,
  and exit radius + 3 from the exit center. Placement may underfill if safe space runs out.
- Local System.Random seed derived from the exit position; no gameplay RNG consumption.
- Small scale variation, horizontal flips, quarter turns for flat debris and slight tilts otherwise.
- ColdAshLit material, muted tint, Background / -105 (floor -110, micro marks -109).
  Player, enemies, XP, ORBITAL, events and interactables render above this entire pass.

Reuse the existing `ColdAshProductionTests.ProductionMovementCrowdsEventsSectorsAndBoss`
smoke test for production screenshots, movement through props, moving crowds, ORBITAL
hits, breakables, events, sector transitions and the final boss. This is a controlled
QA run with player invulnerability and injected crowds, not a balance/playthrough test.
