# Production UI fonts

- **Subject42 UI SDF**: Liberation Sans Regular, 86 pt sampling, SDF padding 9,
  one 2048×2048 Static atlas. Default in TMP Settings and assigned to authored
  production text and material presets. Contains printable Basic Latin, Latin-1,
  all Cyrillic U+0400–04FF, the original UI punctuation and the localized arrow →.
- **Subject42 Intro SDF**: the existing PressStart2P pixel appearance for the bunker
  introduction, Static 512×512 atlas. Its only fallback is Subject42 UI SDF.

Neither font depends on TMP's standard LiberationSans fallback. That unused
standard placeholder is frozen as Static with build clearing disabled, so a
previously loaded Editor resource cannot repopulate and persist its atlas. Do not
enable Dynamic population to add characters during Play Mode. Regenerate the
static atlas in the TMP Font Asset Creator, using the saved character sequence,
sampling size and padding; preserve the asset GUID and material/texture subassets.
The main font's source and OFL license are included here. The intro source remains
the existing `Assets/TextMesh Pro/Fonts/PressStart2P-vaV7.ttf` (Editor authoring only).
If the atlas dimensions change, update material presets that reference its texture,
including DeathText. Retain Static population when saving.

Run **Tools → Subject42 → Verify Production Fonts** from Edit Mode. The tests check
the entire localization table, the Cyrillic block, production Settings in RU/EN/RU,
and byte hashes of font resources after leaving Play Mode and saving assets.
Captures and results go to the existing ignored `Artifacts/GeneratedQA/ProductionFonts`
directory. An explicit `run-tests.request` file in that directory can also launch
the check after the next Editor script reload.
