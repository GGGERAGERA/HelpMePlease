# MVP Segmented Orbit geometry and visual plan

> Execute inline; the user's bounded visual/geometry request overrides broader test/build and commit workflows.

**Goal:** physical radii 1.75 / 3.00 / 4.25 m and pixel segments tracing only active module-to-neighbour arcs.

**Architecture:** retain existing ring/mount/runtime objects. Resolve fixed production radii without the old padding/inner multiplier, hide continuous combat ring lines, and reuse the existing pooled world-link renderers for flat, stepped arc dashes.

**Tech stack:** Unity 6000.3, C#, existing LineRenderer prefab/material and palette.

**Spec:** user's request; [GDD §4.3](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#43-контроль-пространства), [§4.5](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#45-рост-силы).

**Before → after / player reading:** actual radii approximately 1.7325 / 4.35 / 5.55 m and straight green chords → equal 1.25 m steps and short pixel arcs matching active O–M relationships. Basis: explicit user request. No rules, statistics, effect targeting, reward or constructor UI changes.

**Review focus:** wraparound 11↔0; two shooting neighbours produce both arcs; dormant modules and empty/locked levels produce no bright geometry; geometry follows existing phase and station centre; prior drag/drop changes are preserved.

- [x] Apply fixed Segmented radii in state bootstrap and runtime radius resolution, preserving 3×12 angles.
- [x] Replace only world-link drawing with flat pixel arc dashes from existing SegmentedModuleTargets; hide continuous rings in combat.
- [x] Record geometry/visual decision in GDD and Core.
- [x] Compile in the open Editor and inspect one short MVP Play Mode, including active/dormant links and Q/E/centre transforms. No suite, player build, staging, commit or branch change. Leave MVP open.

Verification: Editor compilation passed. One short production MVP Play Mode produced inner.png and three-levels.png in Temp/SegmentedGeometryQA; both images visually inspected. Three real radii, active/dormant link mapping, shared phase and centre parenting confirmed. Q/E handler was invoked programmatically and a temporary centre offset was injected; physical input and balance were not assessed. Temporary runner removed; MVP left open in Editor. No builds or test suite runs.
