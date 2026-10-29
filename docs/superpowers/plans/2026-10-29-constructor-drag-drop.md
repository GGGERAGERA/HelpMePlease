# MVP constructor Drag & Drop implementation plan

> Execute inline with superpowers:executing-plans. User specification is authoritative; no commits, staging, branch changes, builds, broad tests or additional screens.

**Goal:** manual constructor access, a stock notification on the combat HUD, and direct dragging with atomic swaps.

**Architecture:** keep the existing authored constructor and pixel assets. Bind pointer drag events to its existing controls; validate complete transfers in OrbitalRunState before committing, then synchronize the existing runtime objects.

**Tech stack:** Unity 6000.3, C#, uGUI/TMP.

**Spec / GDD:** user request of 29.10.2026; [Bible §4.5](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#45-рост-силы), Core §4 «Рост силы». Current production uses click selection and automatic reward entry. Replace these with drag/drop and optional Tab/HUD entry so the player chooses when to install stock. Basis: explicit user request. Acceptance: Editor compilation and one short MVP run covering weapon drag, occupied-slot swap and module drag, retaining item counts and weapon upgrades.

## Constraints and review focus

- Preserve O/M, unlocked levels, weapon limit, module adjacency, individual weapon state and combat timing.
- Invalid drop, click, Escape, lost focus or constructor close must not consume an item.
- Stock replacement at the weapon cap must conserve both instances; swaps validate both destinations before mutation.
- Keep reserve rows with positive counts visible; hide notification under every modal using existing input gates.
- User's limited verification takes precedence over the skill's full-suite/TDD/build workflow.

## Tasks

- [x] Add atomic weapon transfers and module swaps in OrbitalRunState.Segmented.cs and synchronize them in OrbitalStationRuntime.Segmented.cs.
- [x] Replace obsolete click selection in OrbitalInteractionPresentation.Constructor.cs with drag events, ghost icon, valid-target highlighting and reserve drop. Clean authored prefab labels/buttons; reuse launcher as pulsing HUD notification.
- [x] Complete rewards immediately after their existing grant; update Bible §4.5 and Core to supersede automatic entry and two-click placement.
- [x] Compile in the open Editor, run MVP once, verify weapon drag/swap/module drag, and leave MVP open. Review diff without staging or committing.

Verification: Editor compiled successfully; bounded MVP scenario passed through actual drag handlers after waiting for the first UI render. Stock counts, weapon identity/level, module effects, invalid drop, swap and reserve replacement checked. Physical mouse/keyboard dragging was not verified. Temporary Editor runner removed; MVP remains the active scene. No commits, staging, branch changes, player builds or broad test runs.
