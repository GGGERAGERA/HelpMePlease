Red-haired girl — single idle sprite, 2026-10-31

Request: adapt the attached red-haired girl to the main character's size and style.
Style reference: Assets/_Project/art/Sprites/Aseprite/newCharacter4/Gera4.png,
first 50x50 cell, currently referenced by the active Gera renderer in
Assets/_Project/prefabs/Characters/Gera.prefab.

Deliverable: RedHairGirl_idle_50.png — 50x50 RGBA, binary alpha,
15 visible palette colors maximum, 39-pixel-tall figure, feet on row 49.
Gera_and_RedHairGirl_8x.png shows both sprites at the same 8x integer scale.
Gera_reference_50.png is the original first cell; Gera_reference_12x.png
is its nearest-neighbor enlargement supplied to the image generator.

Generation: built-in image_gen.imagegen with the Gera reference and the
user's attached girl. Exact prompt is in generation-prompt.txt.
RedHairGirl_generated_source.png preserves the enlarged generated output.
Export: nearest-neighbor sampling to 27x39, centered in a 50x50 cell,
alpha threshold 128, nearest solid palette color without dithering.

Art asset only. No prefab, animation, gameplay mapping or Unity scene changed.
Visual comparison and PNG dimensions/alpha/palette checked; no in-game QA.
