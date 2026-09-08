# Settings presentation verification

Both `MainMenu` (Bunker) and `MVP` reference the same
`Assets/_Project/prefabs/UI/SettingsPanel/SettingsPanel.prefab` and
`AudioSettingsPanel` controller. The shared prefab was restyled in place;
obsolete scene overrides of its child anchors were removed. The Bunker
Settings entry button was also restyled locally. Global legacy button assets
used by unrelated menus were not changed.

The production Pause prefab supplies the dark panel palette, cyan accent,
TMP font/material, and selectable color states. Settings contains no neon
sprite references or legacy animation components. `SettingsPanelLayout`
only fits the authored window inside the parent canvas; settings services,
listeners, persistence keys, and navigation controllers are unchanged.

The reachable settings remain master/music/sound volume, language, and
automatic fire. The standalone legacy `GraphicPanel.prefab` has no scene or
prefab references and was not modified.

## Screenshots

Captured by Unity from the running production scenes at 1920 × 1080:

1. `1-bunker-settings.png` — Bunker Settings.
2. `2-gameplay-pause.png` — Gameplay Pause.
3. `3-gameplay-settings.png` — Settings opened from Gameplay Pause.
4. `4-language-dropdown.png` — expanded TMP language dropdown.
5. `5-settings-english.png` — English localization selected through the dropdown.

## Verification

Run `SettingsPresentationTests`, `PauseOverviewTests`, and
`Subject42AuthoredUiTests.ProductionScenesHaveRequiredUiReferencesAndOneEventSystem`
in Unity's EditMode Test Runner. The route test enters Play mode and exercises
the real scene controls through EventSystem pointer events and raycast checks.

- Bunker → Settings → Back → reopen.
- MVP → Pause → Settings → Back → Pause → Resume.
- Escape from Settings returns to Pause while time remains stopped.
- Audio/language/automatic fire values persist across reopening and scene transition.
- TMP font, material and horizontal proportions match production Pause.
- No legacy sprites or animations in the two menu prefabs.
- Existing Pause reference, aspect ratio, and production scene validation tests.

`results.xml` contains the final Unity test result; `routes.txt` records the
completed route assertions.
