Bunker residents — ten single-frame pixel-art characters, 2026-10-31

All sprites: 50x50 RGBA canvas, 27x39 visible silhouette including outline,
centered x12..38 / y11..49, dark charcoal pixel outline, alpha only 0 or 255,
maximum 16 visible solid colors per character. No dithering or antialiasing.
Sprites are still idle art candidates; no animation or gameplay integration.

01 Rassada: elderly gardener, violet scarf and green seedling.
02 Iskra: female welder, raised mask and orange coveralls.
03 Borsch: round cook, chef cap and ladle.
04 Aquarium: yellow suit, water helmet and goldfish.
05 Plesen: elderly doctor with a green mushroom growing on his head.
06 Klyuch: caretaker with navy cap, gray mustache and brass key.
07 Folga: conspiracy theorist, silver foil hat and burgundy sweater.
08 Krot: tunnel worker with helmet lamp and dark goggles.
09 Televizorych: CRT-headed robot in a vest and slippers.
10 Kukolnik: worn rabbit mascot costume with bent ear and human boots.

Source design context: Game Design Bible section 9, bunker as a familiar home.
Art references: existing Gera sprite and the previously generated female robot.
Generation mode: built-in image_gen.imagegen, one call per character.
Exact prompts and palettes are preserved in manifest.json and prompts/.
Unmodified generated images are preserved in sources/ in the workspace.

Export: nearest-neighbor sampling of the source alpha bounds, fixed palette
quantization, binary alpha threshold 128, silhouette edge recolored charcoal.
If point sampling loses an extreme row/column, normalize the sampled bounds
to the requested 27x39 before outlining. No smooth interpolation is used.
The cook's tiny dark eye retains source coverage where dark ink occupies
at least 12 percent of a skin cell's source block in the eye region.
validation.json records pixel-level checks. Contact sheet labels/background
are presentation only; individual sprite PNGs and the atlas are transparent.

The ZIP contains all ten final sprites, previews, atlas, prompts, manifest,
README and validation report. Large generated sources stay in the workspace.
