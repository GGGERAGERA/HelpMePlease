# Upgrade icons — v1

Generated with the built-in image_gen tool. Integrated into Unity as `Assets/_Project/art/UI/UpgradeIcons/OrbitalUpgradeIcons.png`, with eight named sprite regions saved in its importer metadata. Original pixels and alpha are preserved. Point filtering, no mipmaps, no compression, clamp wrap, no NPOT resizing.

Six sprite references are assigned on OrbitalPresentationConfig; ARC and LINK are assigned on their OrbitalModuleView prefabs. IconUsesOwnColors prevents recoloring those two multicolor sprites while retaining module colors for titles and gameplay effects. Both upgrade cards and bunker module previews use ImageTint.

Validation: all 13 production rewards resolve non-null icons; ARC/LINK retain white image tint and distinct module colors; existing production card and pause rendering checks passed under invariant numeric culture. Screenshots: `Artifacts/GeneratedQA/UpgradeIconIntegration/cards-with-icons.png` and `pause-with-icons.png`. Temporary import/validation helper removed after completion.

Order, left to right:
- Top: ARC, LINK, mount, ring capacity.
- Bottom: ring damage, ring speed, CORE, new orbit.

Prompt specification: one cohesive 4 × 2 pixel-art atlas for a sci-fi orbital survival game; chunky 48–64 pixel-style sprites, near-black outlines, restrained shading, readable silhouettes; white/grey mechanisms with cyan accents; violet electrical ARC emitter, green linked nodes, mount socket with plus, orbital ring with expansion arrows, ring with orange impact burst, ring with cyan rotation arrows, cyan/violet reactor core, concentric new orbit with plus. No text, labels, card frames or watermarks. Requested flat #3f6080 backdrop; generated output differs from that backdrop request. Preserve original output.
