# Visual Effects Lab

## Scope

Two Editor-only scenes, excluded from production Build Settings:

- `EffectsLab.unity`: searchable catalog of FX assets from the whole `Assets` tree; an isolated effect and a separate source-object preview.
- `ShaderLab.unity`: twenty-six sprite, field and environment samples on four pages, using project sprites and a floor Tile.

The user selected the full searchable catalog rather than a spatial exhibition of over 1,100 prefab candidates. Catalog discovery happens in Editor. Preview content is loaded by its recorded asset path only when selected; the scene never searches the runtime world for gameplay owners.

## Evidence and preview semantics

- Include standalone FX folders, native particle/trail/line subtrees in other prefabs, inherited/nested variants, and explicitly identified procedural presentation examples.
- Record serialized caller fields from actual prefab data. Mark production dependency reachability separately from library/dev assets; a reference does not prove an event currently fires in a run.
- Caller models are visual-only copies. Their gameplay, storage, spawners, colliders and audio do not run. A paired raw-asset preview is labelled as such when the production owner applies additional tuning.
- Use existing public presentation APIs for supported procedural examples. Do not duplicate combat, weather, damage or progression logic merely to demonstrate it.
- Configure imported FX lifecycle on inactive preview copies before activation. Preserve visual support scripts; suppress demo controllers, global camera shake, audio and self-destruction where needed for replay.
- The catalog stores paths and subtree identities, not strong references to every imported effect. Only the selected preview is loaded.
- Shader samples are visual experiments, not approved gameplay changes. Production scenes, saves, balance and Build Settings remain unchanged.

## Implementation plan

- [x] Author catalog data and discovery with caller evidence and embedded subtree identities.
- [x] Implement isolated preview playback, source switching, search/filter and replay controls.
- [x] Implement sprite/field shader experiments and local animation controls.
- [x] Author both scenes, cameras, presentation anchors, labels and Editor menu entries.
- [x] Verify catalog coverage, lifecycle isolation, replay and representative real Game View frames.

Runtime preview responsibilities live in `Dev/Debug/VisualEffectsLab`; scene/catalog authoring lives in `Dev/Editor/VisualEffectsLab`. No production manager or alternate combat runtime is introduced.

## Use

Open **Tools > Subject42 > Dev > Visual Effects > Play Effects Lab** or **Play Shader Lab**. Alternatively open either saved scene and enter Play Mode. Save modified scenes before switching labs.

Effects Lab has text search by name, asset path, library and caller; type and production-dependency filters; replay/stop/autoreplay; preview scale, fit and 2D/3D views. Select **Другой источник** to cycle actual serialized references and **Код** to locate the caller script. **Выделить ассет** selects the source asset in Project. Only the selected effect and its source visual are instantiated.

The right stand shows a visual source copy with gameplay disabled. `OWNER VISUAL` identifies the containing prefab of an embedded effect without a serialized caller; it does not invent call evidence. Unreferenced standalone assets show an empty source stand. ScriptableObject callers are identified by code and data instead of an invented actor.

Eight procedural examples call local presentation APIs: portal, enemy/player damage flash, relay feedback, corridor gate pulse, false-signal scan, cat heart and bomber shockwave. Their actor controllers do not run. Other context-dependent systems (weather, camera overlays, combat-generated orbital geometry) are not reproduced as a second gameplay runtime. A raw asset preview cannot prove their production timing, size or geometry.

Shader Lab includes the original sprite, outline, dissolve, hologram, frost, overheat, glitch and ghost, plus shield pulse, shockwave, vortex and electric grid. Pause, reset, speed and intensity affect only local MaterialPropertyBlocks. Buttons in both labs switch between scenes.

Shader Lab opens on **Существа / броня (6)**. **Зоны / окружение (6)** contains the new spatial/weather studies; **Разлом / импульс (2)** keeps the two larger panels; **Спрайты / поля (12)** keeps the original gallery. Set **Сила** to zero for the baseline, or use **Пауза** and **Сначала** to inspect a phase. **Rebuild Shader Lab only** updates these pages without rebuilding the effects catalog or EffectsLab scene. Page titles, roots and sample membership come from the same authored Page entries; sample enumeration is available without waiting for OnEnable.

After adding or changing FX prefabs/references, use **Rebuild catalog and scenes**. Discovery includes inherited/nested prefab contents; ordinary browsing never rebuilds the catalog. Rebuilding replaces the two generated lab scenes and lab materials, so keep custom experiments in separate assets.

## Verification

Use `VisualEffectsLabTests` through the existing `TutorialVerificationRunner`. Diagnostic PNG/XML/log files go to ignored `Artifacts/GeneratedQA/VisualEffectsLab`, not this folder. Verify representative library, embedded, source-paired and procedural entries, then compare at least two real Game View frames for shader animation. Catalog coverage is automated; it does not claim manual visual approval of every imported effect.

## Environment experiments — 06.11.2026

Requested preview: a spatial rift distorting its surroundings and a floor responding to a travelling impulse, in the project's pixel style. Keep the existing twelve samples on their own page and add two larger environment panels on a second page. Use the existing floor Tile/sprite and enemy sprite; animate through the existing scene controller and local MaterialPropertyBlocks.

The rift warps the panel's sampled floor and sprite silhouettes, with a jagged cyan/violet opening. This local material demonstration needs no camera texture or renderer-feature changes; it does not claim screen-space refraction of arbitrary production objects. The impulse moves out from an emitter, lights tile seams and briefly displaces the floor pattern. Intensity zero shows the undistorted source for comparison. Neither experiment changes combat rules or production materials.

Plan: (1) add the two modes in one environment-preview shader; (2) author their materials and page, extending existing page/time/intensity controls; (3) verify page switching, material isolation, compilation and real Game View frames at two times plus zero intensity. Use the shader-only authoring command to preserve the effects catalog and scene.

## Variety expansion — visual hypotheses

The user requested diverse, game-relevant shader examples. Add six creature studies (adaptive shell, diagnostic scan, infection, nano assembly, phase fold, charged armour) and six environment studies (stasis pocket, gravity well, relay links, suppression, biological film, wet floor). These are material/presentation experiments, not new approved statuses, mechanics, timings or production visual replacements.

Design basis: [Bible §2.1, readable danger](../../../Documentation/Subject42_Game_Design_Bible.md#21-законы-игрока), [§4.3, spatial control](../../../Documentation/Subject42_Game_Design_Bible.md#43-контроль-пространства), [§6.2, World Rule contract](../../../Documentation/Subject42_Game_Design_Bible.md#62-контракт-world-rule). The first two guide legible silhouettes and spatial signals; §6.2 guides distinct local fields. Infection, nano assembly and biological film remain hypotheses; their existence here does not claim gameplay availability. No player decision or game rule changes in this work.

Implementation: extend the existing two shader families and author two extra six-sample pages. Replace parallel page/sample/title arrays with one authored page description per group in ShaderLabController; derive the flattened sample list and toolbar from it. Preserve the first fourteen samples and their materials. Keep shared time/pause/reset/intensity controls and shader-only authoring. Acceptance: all four pages switch correctly, twenty-six samples compile and render, zero intensity gives their baseline, shared materials remain unchanged, and two real Game View frames show animation on each new page. Reject or revise any sample that hides the actor entirely for most of its cycle or makes its title/neighbours unreadable.
