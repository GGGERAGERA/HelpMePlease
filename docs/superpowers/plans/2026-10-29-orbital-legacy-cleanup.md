# Production ORBITAL cleanup and module presentation

**Spec:** user's targeted cleanup request; [GDD §4.3](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#43-контроль-пространства), [§4.5](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#45-рост-силы).

**Before → after:** RingPower can still enter Segmented rewards; old central decoration and dead UI remain in Station.prefab; identical green module squares and broken green arc dashes → reject legacy ring rewards at selection/application, physically remove confirmed unused visuals/UI/authoring, use four shared coloured pixel glyphs and continuous cyan arcs clipped against sprites. Basis: explicit user request. Gameplay rules, Q/E, RMB, 0°/15°/0°, module effects and compatible reward weights stay unchanged.

**Reference boundaries:** ring power/speed, non-segmented aiming/rotation, LinkPair/LinkMatrix and custom paths are called by Orbital/Enemy/WorldSystems labs and shared tests. Keep these common implementations. CorePulse audio is used by Corridor/FalseSignal; AnomalyCoreConstruct supports Gravity/Rift; Escape currency is unrelated. Do not delete these by name alone. Serialized retired IDs stay reserved.

- [x] Verify and remove old centre prefab/embedded renderers/FX, unused constructor instruction/status/stow objects, dead Core FX authoring and unused CoreRay presentation category.
- [x] Block old ring reward selection and application for Segmented while retaining lab implementations.
- [x] Share DMG/FR/2X/RIC glyphs and colours across existing UI/combat; replace world arc dash strips with continuous fixed-step cyan paths and sprite clearance.
- [x] Update GDD/Core. Compile Editor and run one short MVP scenario, with visual inspection and real projectile emission. Remove temporary runner; leave MVP open. No player builds, suite, commits, staging, branch changes or F1 redesign.

**Acceptance:** old ring choices unavailable; centre renderers absent from prefab; weapons emit projectiles; four module types visible; base circles blue/dim, active arcs cyan/unbroken and clear of sprites. Programmatic setup/inputs are distinct from physical input or balance observation.

Verification: Editor compiled and Station.prefab loaded natively with valid required references, no missing scripts and no deleted core in recursive dependencies. Final bounded MVP scenario passed: actual normal pool and direct handlers reject seven legacy kinds, ring mutation gates reject old upgrades, real weapons emitted 7 shots, four shared type glyphs and six continuous cyan arcs were visually inspected in constructor/combat captures. Setup was scripted; no physical input or balance validation. The diagnostic arc colour comparison uses tolerance for Unity Gradient's 8-bit quantization. Temporary runner removed; MVP left open in Editor. No builds or test suite runs.

Physically removed: p_miniWeaponCore1.prefab and meta; Subject42CoreAuthoring.cs and meta; 51 embedded YAML blocks for the nested centre, halo/sparks, old instructions/status/stow UI; FlashCore calls/API, CoreRay category and unused shader Core overlay uniforms; old prefab registry row. Four lab callers now use the same Player sorting layer and preserved orders (19/18/117) plus the shared CircleSprite for the optional debug centre marker. F1 files unchanged.

Retained with real consumers: ring progression, aiming/rotation, LinkPair/LinkMatrix/custom geometry in Orbital/Enemy/WorldSystems labs; Pixel/Circle textures and old ring icons referenced by current prefabs/events/labs; AnomalyCoreConstruct for Gravity/Rift; CorePulse sound for Corridor/FalseSignal; Escape currency and serialized reserved IDs. Core I-III implementation/cards were already absent; their retired ID is not reused.
