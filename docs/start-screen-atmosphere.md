# Start screen atmosphere

Open `Assets/_Project/Scenes/MainBuild/StartScreen.unity` and enter Play mode.

The original supplied image is imported at `Assets/_Project/art/UI/StartScreen/Subject42CapsuleLab.png`. It uses Point filtering, no mipmaps and no texture compression. The image is fitted without changing its aspect ratio and has enough overscan for mouse movement.

On `StartScreen/Canvas/Capsule laboratory background`, `StartScreenAtmosphere` controls:

- Mouse Travel: maximum displacement in canvas units (18 horizontal, 10 vertical).
- Follow Time: smooth movement response (0.35 seconds).
- Clear Interval: pause between condensation pulses (10–18 seconds).
- Condensation Duration: fade in and fade out over 6 seconds.
- Condensation Opacity: maximum tint opacity (0.18).

The condensation mask is generated once, mostly at the glass edges. Only opacity changes during play. The image and mist do not receive UI raycasts. Menu controls remain stationary and keep their existing navigation/localization, settings panel and transition controller. Animation uses unscaled time and returns toward the center when the mouse leaves the window or the application loses focus.

Run `Tools > Subject42 > Verify Start Screen` in Unity to check the authored sprite and control references, viewport coverage at three aspect ratios, settings open/close, mist while paused, and Start transitioning to the bunker with the loading overlay dismissed. Results are written to `Artifacts/StartScreen/results.xml`.

The integration also repairs one stale parent reference on the orphan `Reel 1` object in `MainMenu`. The object is retained as a scene root (the same effective parent it had when Unity failed to resolve the old reference).
