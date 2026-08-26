# Visual Tuning Lab — runtime mapping

SAVE создаёт `Assets/_Project/Resources/VisualTuningSavedValues.asset` и фиксирует все доступные anomaly targets, а не только текущий selector. При следующем Play Mode `Subject42DebugMenu.Start` загружает этот asset и применяет snapshot к production consumers до построения Visual Lab UI. RESET использует тот же загруженный snapshot. Значения ниже применяются live. `Captured` означает production-значение, захваченное существующим consumer при запуске, если сохранённого preset ещё нет.

## Persistence flow

`Visual UI → VisualTuningSnapshot → Resources/VisualTuningSavedValues.asset → Subject42DebugMenu.Start → existing runtime consumers`.

SAVE доступен только в Editor, перечитывает asset после `SaveAssets` и сравнивает сохранённый snapshot с runtime snapshot. В development build загрузка использует `Resources.Load`; зависимости от `UnityEditor` находятся только под `#if UNITY_EDITOR`.

| Section | Parameter | Debug MIN | Production | Debug MAX | Runtime consumer | Saved asset |
|---|---|---:|---:|---:|---|---|
| ENVIRONMENT | Readability preset | Original | Captured | Dark World | ProductionSectorDebugController.ApplyEnvironment | VisualTuningSavedValues |
| ENVIRONMENT | Decor Brightness | 0.25× | Captured | 1.50× | registered environment SpriteRenderer color | VisualTuningSavedValues |
| ENVIRONMENT | Environment Darken | 0% | 0% | 100% | sector ground/environment overlay | VisualTuningSavedValues |
| ENVIRONMENT | Anomaly Focus | Off | Captured | On | LevelAnomalyController focus overlay | VisualTuningSavedValues |
| ENVIRONMENT | Outside Darkness | 0% | Captured | 100% | anomaly focus darkness overlay | VisualTuningSavedValues |
| ENVIRONMENT | Outside Color | 0% | Captured | 100% | anomaly focus color blend | VisualTuningSavedValues |
| ENVIRONMENT | Focus Transition | 0.20 s | Captured | 0.35 s | LevelAnomalyController transition | VisualTuningSavedValues |
| ENVIRONMENT | Wind Dust Amount | 0× | 1× | 5× | WorldRuleVisual WindDustParticles emission | VisualTuningSavedValues |
| ENEMIES | Scope | All | Captured | Boss | ProductionSectorDebugController registry filter | VisualTuningSavedValues |
| ENEMIES | Brightness | 0.50× | Captured | 2.50× | enemy readability material | VisualTuningSavedValues |
| ENEMIES | Saturation | 0× | Captured | 3× | enemy readability material | VisualTuningSavedValues |
| ENEMIES | Tint Strength | 0% | Captured | 100% | enemy readability material | VisualTuningSavedValues |
| ENEMIES | Hue Shift | −180° | 0° | 180° | enemy readability material | VisualTuningSavedValues |
| ENEMIES | Recolor R/G/B | 0 | Captured | 1 | enemy readability material target color | VisualTuningSavedValues |
| ENEMIES | Recolor Strength | 0% | 0% | 100% | enemy readability material | VisualTuningSavedValues |
| ENEMIES | Outline Enabled | Off | Captured | On | enemy readability shader | VisualTuningSavedValues |
| ENEMIES | Outline Strength | 0× | Captured | 2× | enemy readability shader | VisualTuningSavedValues |
| ENEMIES | Outline Width | 0.50× | Captured | 4× | enemy readability shader | VisualTuningSavedValues |
| PLAYER | Visual Scale | 0.50× | 1× | 2× | player child SpriteRenderer.localScale | VisualTuningSavedValues |
| PLAYER | Visual Offset X | −2 | 0 | 2 | player child SpriteRenderer.localPosition | VisualTuningSavedValues |
| PLAYER | Visual Offset Y | −2 | 0 | 2 | player child SpriteRenderer.localPosition | VisualTuningSavedValues |
| PLAYER | Brightness | 0× | 1× | 4× | player SpriteRenderer.color | VisualTuningSavedValues |
| PLAYER | Saturation | 0× | 1× | 3× | player SpriteRenderer.color | VisualTuningSavedValues |
| PLAYER | Opacity | 0% | 100% | 100% | player SpriteRenderer.color.a | VisualTuningSavedValues |
| PLAYER | Tint Strength | 0% | 0% | 100% | player SpriteRenderer.color | VisualTuningSavedValues |
| PLAYER | Tint R/G/B | 0 | 1 | 1 | player SpriteRenderer.color | VisualTuningSavedValues |
| PLAYER | Glow Intensity | 0× | 1× | 5× | WorldRuleVisual player Light2D | VisualTuningSavedValues |
| PLAYER | Glow Radius | 0.10× | 1× | 5× | WorldRuleVisual player Light2D | VisualTuningSavedValues |
| WEAPON | Visual Scale | 0.50× | 1× | 2.50× | weapon child SpriteRenderer.localScale | VisualTuningSavedValues |
| WEAPON | Visual Offset X | −2 | 0 | 2 | weapon child SpriteRenderer.localPosition | VisualTuningSavedValues |
| WEAPON | Visual Offset Y | −2 | 0 | 2 | weapon child SpriteRenderer.localPosition | VisualTuningSavedValues |
| WEAPON | Brightness | 0× | 1× | 4× | weapon SpriteRenderer.color | VisualTuningSavedValues |
| WEAPON | Saturation | 0× | 1× | 3× | weapon SpriteRenderer.color | VisualTuningSavedValues |
| WEAPON | Opacity | 0% | 100% | 100% | weapon SpriteRenderer.color.a | VisualTuningSavedValues |
| WEAPON | Tint Strength | 0% | 0% | 100% | weapon SpriteRenderer.color | VisualTuningSavedValues |
| WEAPON | Tint R/G/B | 0 | 1 | 1 | weapon SpriteRenderer.color | VisualTuningSavedValues |
| PLAYER RING | Enabled | Off | On | On | PlayerWeaponOrbitVisual LineRenderer | VisualTuningSavedValues |
| PLAYER RING | Visual Radius | 0.25× | 1× | 3× | LineRenderer positions; weapon radius unchanged | VisualTuningSavedValues |
| PLAYER RING | Thickness | 0.005 | 0.035 | 0.15 | LineRenderer width | VisualTuningSavedValues |
| PLAYER RING | Brightness | 0× | 1.25× | 4× | ring material color | VisualTuningSavedValues |
| PLAYER RING | Opacity | 0% | 72% | 100% | ring material alpha | VisualTuningSavedValues |
| PLAYER RING | Pulse Amount | 0% | 6% | 75% | PlayerWeaponOrbitVisual.RefreshColor | VisualTuningSavedValues |
| PLAYER RING | Pulse Speed | 0 Hz | 0.22 Hz | 5 Hz | PlayerWeaponOrbitVisual.LateUpdate | VisualTuningSavedValues |
| PLAYER RING | Rotation Speed | −180°/s | 4°/s | 180°/s | ring geometry rotation | VisualTuningSavedValues |
| PLAYER RING | Offset X/Y | −3 | 0 | 3 | ring LineRenderer positions | VisualTuningSavedValues |
| PLAYER RING | Tint R/G/B | 0 | Cyan | 1 | ring material color | VisualTuningSavedValues |
| PROJECTILES | Projectile Visual Scale | 0.10× | 1× | 4× | projectile visual child transform | VisualTuningSavedValues |
| PROJECTILES | Trail Width | 0.10× | 1× | 5× | TrailRenderer.widthMultiplier | VisualTuningSavedValues |
| PROJECTILES | Trail Lifetime / Length | 0.10× | 1× | 6× | TrailRenderer.time | VisualTuningSavedValues |
| PROJECTILES | Trail Opacity | 0× | 1× | 3× | TrailRenderer gradient alpha | VisualTuningSavedValues |
| PROJECTILES | Trail Brightness | 0× | 1× | 6× | TrailRenderer gradient RGB | VisualTuningSavedValues |
| PROJECTILES | Laser Core Width | 0.10× | 1× | 8× | LaserBeamRenderer next beam | VisualTuningSavedValues |
| PROJECTILES | Laser Glow Width | 0.10× | 1× | 8× | LaserBeamRenderer next beam | VisualTuningSavedValues |
| PROJECTILES | Laser Brightness | 0× | 1× | 6× | LaserBeamRenderer core/glow colors | VisualTuningSavedValues |
| ANOMALIES | Global Accent | 1× | Captured | 1.75× | ProductionAnomalySite renderers | VisualTuningSavedValues |
| ANOMALIES | Monochrome | Off | Off | On | supported active anomaly sites | VisualTuningSavedValues |
| ANOMALIES | Primary/Secondary/Fill RGBA | 0 | Captured | 1 | selected Arc/Beam visual contract | VisualTuningSavedValues |
| ANOMALIES | Boundary Width | 0.01 | Captured | 3 | selected site LineRenderer; collider unchanged | VisualTuningSavedValues |
| ANOMALIES | Boundary Alpha | 0 | Captured | 1 | selected site boundary | VisualTuningSavedValues |
| ANOMALIES | Inner Line Width | 0.01 | Captured | 3 | selected site inner LineRenderer | VisualTuningSavedValues |
| ANOMALIES | Visual Scale | 0.25× | Captured | 3× | selected site presentation geometry | VisualTuningSavedValues |
| ANOMALIES | Edge Glow | 0.01× | Captured | 10× | selected site material | VisualTuningSavedValues |
| ANOMALIES | Pulse Speed | 0 | Captured | 10 | selected site visual animation | VisualTuningSavedValues |
| ANOMALIES | Pulse Strength | 0% | Captured | 100% | selected site visual animation | VisualTuningSavedValues |
| ANOMALIES | Pattern Speed | 0 | Captured | 10 | selected site visual material | VisualTuningSavedValues |
| ANOMALIES | Pattern Strength | 0% | Captured | 100% | selected site visual material | VisualTuningSavedValues |
| CAMERA / SCREEN FX | Vignette Intensity | 0% | Captured | 100% | URP global VolumeProfile Vignette | VisualTuningSavedValues |
| CAMERA / SCREEN FX | Orthographic Size | 2 | Captured | 16 | CameraFollow controlled Camera | VisualTuningSavedValues |

## NOT PERSISTABLE YET / не добавлено без изменения production-архитектуры

- Player outline: у player shader нет подтверждённого outline contract.
- Muzzle visual offset: существующий muzzle/fire transform может быть gameplay origin; разделять молча небезопасно.
- Muzzle glow size и отдельный muzzle visual scale: нет общего production consumer для всех оружий.
- Enemy overall scale и blob shadow: нет единого отдельного presentation root/shadow contract для всех архетипов.
- Gravity site tuning: Gravity не реализует `IAnomalyVisualTunable`; его radius связан с gameplay zone.
- Arc node/spark count и Gravity particle amount: значения создаются специализированными runtime-компонентами без общего безопасного setter.
- Beam segment length: это gameplay attack length; Visual Lab его не меняет.
- Blood/death particle count: нет общего production FX API. Существующие impact/death/popup presentation параметры остаются в FEEL LAB и не дублируются.
- Vignette smoothness, global saturation/brightness: активный VolumeProfile не предоставляет подтверждённые текущей системой setters.
