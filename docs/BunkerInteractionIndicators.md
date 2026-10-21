# Production bunker interaction indicators

`MainMenu.unity` owns one `BunkerInteractionIndicators` on the existing interaction system. The ORIGINAL `PF_InteractionArrow` instances and provider bindings are saved in the scene, including nested prefabs. No replacement visual prefab is used. The editor synchronizer maintains them on hierarchy changes, with visible Edit Mode previews. At runtime the manager adopts those instances, discovers remaining `IBunkerInteractable` providers once, and avoids duplicates. Existing `BunkerInteractableCollider.Start` also registers newly spawned providers. Initially disabled providers retain their binding and reveal the arrow when enabled. Cat and the temporarily unavailable football entrance are excluded.

The binding adapter preserves the original Animator, Visual hierarchy, sprite, scale, material, sorting and Light2D. It uses the existing interaction layer, and exposes the existing provider through `BunkerInteractableCollider`. `BunkerCursorInteractor` handles hover and left click through its normal input path. Both visibility and click acceptance check current proximity, provider eligibility, pause, transitions and modal panels. No station business logic is copied. Discovery, arrays and prefab instances are not created every frame.

Production visibility distance is 5.5 world units from the closest point on the existing interaction collider. This includes the normal approach through the character room doorway. The original cyan sprite bobs through `InteractionArrow_Idle.anim` (0 → 0.16 → 0 over 1.8 seconds) with its original light pulse. Hover delegates the existing object outline without changing the original color. The trigger follows the animated Visual bounds. Only station placement may gain clearance for tall cabinets. The physical object also retains its original hover and mouse interaction, subject to the same proximity and UI policy; arrow hits take priority over body hits. Nearby arrows have separate trigger hitboxes. Non-bunker scenes retain legacy cursor and prompt presentation through serialized policy flags.

## Production inventory

| Object | Existing action |
|---|---|
| Character station | Character selection |
| Orbital weapon station | Orbital module selection |
| Upgrade table | Meta upgrades |
| Anomaly table | Anomaly stabilizers |
| Run exit / lift | Depth / expedition selection |
| Escape Protocol terminal | Escape Protocol window |
| Mission operator | Existing expedition mission window |
| Orbital Slot | Existing slot window |
| Enemy gallery target | Advance the room's existing enemy display |
| Pinball cabinet | Existing pinball window |
| Roulette table | Existing roulette window |

Dice Poker remains unavailable as authored and has no visible arrow. The cat keeps its original E interaction and localized prompt. Keyboard E compatibility remains for other providers without world hints. World Canvas station labels, arcade headings and generated station badges are suppressed in MainMenu; window headings and gallery display content remain.

## Temporary football closure

The production scene disables the existing football controller, start station, HUD, goals, feedback and runtime gameplay roots. The room, floor, field decoration, scripts, prefab references and assets remain. Its existing wall art closes the playing field with a solid maintenance barrier. The southern transit strip and `Door_Football` proximity opening remain available: this is the only existing route to the arcade. A physical pixel plate uses the existing localization service for `В РАЗРАБОТКЕ` / `COMING SOON`. Disabled football is omitted from its F1 debug section.

To restore football deliberately, re-enable its scene gameplay roots and controller, remove the plate and maintenance barrier, then remove the explicit football exclusion in the shared adapter after the feature is ready. Other games, expedition logic, save services and Deploy are unchanged.

## Verification artifacts

`Artifacts/GeneratedQA/BunkerInteraction/` contains production Play Mode screenshots at 1600×900, test results, a per-provider interaction trace and compilation results. `BunkerInteractionPresentationTests` verifies all active providers, same-frame range rejection, initially disabled providers, modal blocking, separated nearby hitboxes, cat prompts/petting, physical field closure, the shared arcade transit and RU/EN signage. The legacy arcade hover and pause regressions use the shared cursor routing API. `OriginalCyanArrowSurvivesInitializationAndAnimates` captures five real Game View frames over 1.8 seconds and checks the original controller, scale and untinted sprite. All window interactions check that the original arrow returns after closing the window.

Screenshot automation calls the same world-position cursor handlers as physical input. Native Windows capture timed out on both attempts; actual hardware mouse and E events have not been verified.

## Original implementation and regression

- `b4ebcb61` introduced `PF_InteractionArrow.prefab`, `PF_InteractionArrow.controller`, `InteractionArrow_Idle.anim` and `InteractionArrow_SciFi.png`. The Animator animates the child `Visual`, not the root.
- `af4d43d6` added the cyan Light2D and its pulse; `832c9287` switched the sprite material to Sprite-Unlit-Default. Those original assets remain byte-for-byte unchanged in this restoration.
- `385a7d0f` (parent of `94bb94b4`) is the last serialized production setup before the original arrow deactivations. This is Git evidence of settings, not a claim that an old build was executed today.
- `94bb94b4` added `m_IsActive: 0` overrides to the character/weapon arrows and other original indicators; the mission provider/arrow was disabled too. Scene overrides carried those deactivations into Play Mode.
- The subsequent failed migration explicitly disabled every original instance and substituted a static `PF_BunkerInteractionArrow` with scale 0.45 and amber tint. It discarded the Animator and Light2D. Its runtime renderer gate depended on live player proximity and UI state, unlike its Edit Mode preview.
- A fresh production Play Mode diagnostic (`original-before-restoration.txt`) confirmed all seven original animators inactive and the bound character replacement missing its Animator. The current restoration removes that replacement and the code generating/disabling originals, reactivates/rebinds originals, and uses the same original prefab for missing providers. Visibility is now gated on the original renderer/light/collider, leaving the Animator root active. There is no synthetic bob implementation.
