# Orbital Relay: отчёт о production-миграции

Дата: 2026-10-06. Ветка: `codex/orbital-relay-production`. Исходный commit: `317e69e0`.
Основание: утверждённые [спецификация](superpowers/specs/2026-10-06-orbital-relay-production-design.md) и [план](superpowers/plans/2026-10-06-orbital-relay-production.md).

## Восемь задач

1. **State/config/result.** Добавлены чистая state machine, immutable validated settings и отдельный результат. Счётчики Stabilization и Bonus разделены. Combo сбрасывается при входе в Bonus; штрафов к Gold нет. Node не повторяется подряд.
2. **Release contact.** `OrbitalModuleRuntime.HasBodyContact` использует runtime-safe `OrbitalBodyContactDetector`: преобразованный четырёхугольник реального sprite body. Учитываются rotation/scale; halo, preview, drag и detached/hidden modules исключены. `TouchesCircle` удалён, runtime API не зависит от Editor/development define.
3. **Enemy pressure.** Добавлены scoped leases в существующем WorldEventSpawner и небольшой WorldEventPressureModifier. Relay имеет normal pressure=1, Bonus=1.6 по умолчанию; изменяется частота существующего EnemySpawner, не лимит. Lease снимается при completion, fail, gameplay cancel, owner reset, disable/destroy/unload. Старый lease не сбрасывает pressure нового event. WorldRule multiplier остаётся независимым; другие Events сохраняют standard pressure.
4. **Rewards/sites.** Nullable custom completion payload проходит через WorldEventSpawner. Успешная стабилизация квалифицирует одну Upgrade selection; Gold рассчитывается только из Bonus activations и выдаётся через `CurrencyManager.AddGoldExact`. Одна selection использует существующие три предлагаемые карточки, UpgradeManager и reward queue. Payload обрабатывается один раз до completion subscribers. Для custom reward normal site не создаёт дополнительный контейнер/Upgrade; special-site Ring сохранён.
5. **Authored prefab.** Созданы production OrbitalRelayEvent prefab, config, Node/core/contact/progress/pulse/activation, transition shockwave/flash/banner, combo и urgent-timer анимации. Runtime не строит визуалы процедурно и не зависит от Dev assets. Невалидная конфигурация отклоняется до spawn bookkeeping.
6. **Production/tutorial/bot.** Каталоги MVP, D1 и D2 переведены на новый prefab с сохранением остальных записей/весов. Tutorial после ранних combat/XP/placement шагов ведёт к Relay, стартует при входе, фокусирует активный Node и объясняет contact/RMB. Fail возвращает к SectorGoal и повторно создаёт тот же production prefab; завершение ожидает общую Upgrade queue до Exit. Bot двигает персонажа по реальным orbital body positions/velocity, не записывает progress/score. Длинный tutorial test адаптирован и скомпилирован.
7. **Lab/legacy cleanup.** WorldSystemsLab каталог использует тот же production prefab. Adapter только создаёт station/dev-run, authored shared notification/reward UI и scene-local существующий EnemySpawner. Preview по умолчанию не выдаёт rewards; опциональная общая queue проверяется в dev-run без сохранения Gold. Отдельной lab gameplay/state/HUD копии нет. Legacy CaptureZone и lab OrbitalHoldZone удалены после перевода потребителей.
8. **Acceptance.** Узкие Unity fixtures, полная компиляция Editor runtime/Editor scripts и Player runtime без debug defines; serialized-reference audit; bounded 30-секундная проверка реальных контактов, default Transition/Bonus, fail/reset/cancel/unload и reward queue. Golden Path, Batch Runner и полный tutorial route не запускались. Финальное независимое ревью завершено, его замечания обработаны ниже.

## Production flow и запуск

`Inactive → Stabilization → Transition → Bonus → Completed`; таймаут Stabilization ведёт в `Failed`.
В обычном секторе войти в арену и нажать **E** через существующий PlayerInteractor. В tutorial вход сам запускает событие на шаге SectorGoal. Start требует initialized station с mounted module и готовых production currency/reward owners.

Stabilization: 3 успешных непрерывных контакта по 0.6 сек за 20 сек. Transition: 0.75 сек без контактов, сильный feedback. Bonus: отдельные 15 сек непрерывного переключения Node, каждый контакт даёт activation и 10 Gold. После стабилизации qualification сохраняется: gameplay cancel выдаёт заработанный Bonus Gold и одну Upgrade selection. Administrative reset/unload не открывает rewards. При fail до стабилизации Gold=0, Upgrade не заработан.

Исправлена найденная пользователем невозможность production-запуска: collider арены перенесён на корень prefab, где находится Interactable. Ранее существующий PlayerInteractor не обнаруживал компонент на родителе дочернего collider; lab auto-start скрывал ошибку. Добавлены regression checks для authored collider и обычного `Interact()` entry path. Bunker/PlayerInteractor не изменялись.

## WorldSystemsLab

Открыть `Assets/_Project/Dev/Labs/WorldSystemsLab/WorldSystemsLab.unity`, Play, кнопка **УДЕРЖАНИЕ — ОРБИТАЛЬНОЕ РЕЛЕ** в EVENT. Lab запускает production prefab рядом с игроком. WASD — движение; RMB — существующая compression; Left Shift+WASD — lab управление центром орбит. `Relay enemy pressure` включает scene-local backend; `Relay shared Upgrade queue` включает существующую queue/UI в dev режиме. Результат показывает общий notification service.

Исправлены перекрытие Relay HUD старым Corridor UI (только на время Relay), lifetime center shift при unload и прозрачный родитель CanvasGroup итогового lab notification. Остальные Events не переписаны; их prefab references сохранены.

## Inspector

`Assets/_Project/Data/World/Events/OrbitalRelayConfig.asset`:

| Параметр | Default |
|---|---:|
| requiredActivations | 3 |
| stabilizationDuration | 20 |
| stabilizationContactTime | 0.6 |
| transitionDuration | 0.75 |
| bonusDuration | 15 |
| goldPerActivation | 10 |
| comboWindow | 2.5 |
| bonusEnemyPressureMultiplier | 1.6 |

Количество Node задаётся authored `nodes` collection prefab (минимум 2 distinct Nodes), их позиции/контактный радиус и presentation references редактируются в prefab. Guaranteed Upgrade — одна selection согласно production спецификации, отдельной reward system/tier нет.

## Удалённые legacy references/assets

- CaptureZoneEvent, CaptureZoneVisual и его completion helper: scripts и `.meta`.
- CaptureZoneEvent prefab, exclusive M_CaptureZone material и CaptureZone shader, их `.meta`.
- Lab OrbitalHoldZoneEvent script и OrbitalHoldZone prefab, их `.meta`.
- Старые typed references tutorial/sector/bot/tests/authoring/catalogs; obsolete lab serialized gameplay parameters/HUD/formulas.
- Exclusive localization `event.capture`, `event.capture.name`, `event.capture.description`, `event.enter`; standing-in-zone tutorial заменён relay инструкцией.
- `TouchesCircle` заменён runtime body API без compatibility layer.

Общие EventRewardContainer, WorldEventMarker, site config, другие Event assets и special-site Ring сохранены. Исторический VerticalSlice_Audit не переписан: упоминание CaptureZone там относится к прежнему состоянию проекта.

## Проверки и ограничения

- Unity: State 9/9, Contact 2/2, Integration 10/10, Authoring 8/8, Runtime 1/1 — **30 passed, 0 failed**.
- Runtime: реальные module contact guards, normal Interactor entry, Stabilization/Transition/Bonus, визуально видимый result, pressure 1→1.6→1, reset mid-Bonus, qualified cancel, administrative unload; exact Gold payout при meta multiplier и duplicate notification; одна shared selection.
- Site coverage: реальный Relay completion → production dispatcher → normal ProductionAnomalySite subscriber → общая queue; site завершается после queue idle, дополнительной selection/container нет. Для special site два terminal payload проходят тот же dispatcher/subscriber/queue: после первого Ring нет, после второго выдаётся ровно один отдельный NewRing. Payload fixtures изолируют reward contract, не подменяют gameplay smoke.
- Bot regression: реальный зарегистрированный nearby EnemyHealth и нулевой Transition tracking intent; safety direction раньше был 0 (RED), теперь сохраняет evasive movement (GREEN). При отсутствии safety override аналоговая скорость tracking сохраняется.
- Player runtime compile: 0 errors, 18 warnings; DefineConstants без UNITY_EDITOR/DEVELOPMENT_BUILD. Editor runtime: 0 errors, 18 warnings; Editor scripts: 0 errors, 3 warnings. Warnings не выдаются за ноль warnings.
- `git diff --check` для Assets/docs чист; нет live CaptureZone/TouchesCircle/OrbitalHoldZone usages, старых serialized GUID references и MissingScript/external GUID в проверенных migrated assets; production prefab не имеет Dev dependencies.
- Тесты плана консолидированы в пять узких fixtures: часть названных отдельных cases покрыта объединёнными contract/runtime checks. Полный Core/tutorial/Golden Path не выполнялись по ограничению пользователя.
- Проверена компиляция Player C#, полноценный packaged Player build и длинный production playthrough не выполнялись.
- Известный baseline WorldSystemsLab TacticalMapHUD сообщает missing authored map-marker shell references (то же в исходном commit); relay-only smoke ожидает ровно этот diagnostic. Map не изменён вне scope.

## Локальные решения реализации

- UpgradeManager.CanAcceptWorldEventReward — минимальный readiness query перед запуском/dispatch; риск при неверной readiness логике: event недоступен либо payout не завершится.
- RunMessageService допускает HUD-less authored notification support; onboarding hints без HUD не запускаются. Риск: lab notifications потребуют HUD, если сервис впоследствии начнёт безусловно обращаться к нему.
- Lab station/dev-run/support переиспользуются до destroy adapter; Clear отключает support и чистит только owned enemies. Это предотвращает deferred-destroy singleton race при immediate restart. Риск: reusable dev context дольше живёт до unload; rewards по умолчанию suppressed.
- Test cases объединены в узкие fixtures вместо длинных/широких runners; риск: полный tutorial маршрут не проверен runtime.
- Collider арены authored на event root для совместимости с существующим Interactor; ArenaBounds anchor сохранён. Runtime geometry/progress остаются общими, Bunker не затронут.
- Baseline TacticalMapHUD диагностируется, но не исправляется: изменение map assets нарушило бы scope; риск — map preview остаётся с прежней ошибкой.
- Durable reward compensation после death/unload не добавляется согласно спецификации: administrative disposal не открывает UI; риск — нет persistence незавершённой reward delivery.
- Полный tutorial route не выполняется по ограничению пользователя, он и существующие длинные site tests адаптированы и compile-only; риск — длинные маршруты требуют отдельной ручной проверки.
- Reviewer не запускал Unity/build/визуальную проверку повторно: review был read-only, execution evidence получен реализатором; риск — визуальная оценка не имеет независимого второго runtime-прогона.

## Финальное ревью

Одно независимое read-only ревью всей ветки `317e69e0..2fc35835`: Critical — нет. Два Important обработаны одним fix pass:

1. Bot safety больше не умножается безусловно на tracking magnitude: рядом с enemy/projectile/boundary/exit сохраняется полный safety intent. Regression test RED→GREEN; Integration 10/10.
2. Добавлено отсутствовавшее bounded site coverage нормальной и special награды через реальные production owners. Runtime 1/1 (~30 сек). Gameplay smoke использует реальные contacts; site reward contract изолируется terminal payload fixtures. Существующие ProductionAnomalyRewardFlowTests также переведены с ручного NotifyEventCompleted на реальные Relay contacts; длинный fixture не запускался.

Повторный reviewer не вызывался; изменения проверены тестами и компиляцией. Deferred Minor: при отсутствии station/modules/reward owners обычный production interaction недоступен без localized explanation; diagnostics при прямом Start есть, normal interaction reason требует отдельного UI уточнения. Это не мешает штатному запуску с валидными production owners.

Declined-to-judge пункты reviewer рассмотрены явно: baseline map diagnostic оставлен вне scope; durable reward compensation исключён спецификацией; полный tutorial run запрещён пользователем; execution/visual evidence проверен реализатором, reviewer занимался code/asset inspection. Риски указаны выше.

## Файлы

Полный manifest без Unity `.meta` (они добавлены/удалены вместе с соответствующими assets) приведён ниже.

```text
M	Assets/_Project/Data/Localization/LocalizationTable.asset
M	Assets/_Project/Data/SurfaceMap/Sector_D1.asset
M	Assets/_Project/Data/SurfaceMap/Sector_D2.asset
A	Assets/_Project/Data/World/Events/OrbitalRelayConfig.asset
M	Assets/_Project/Dev/Debug/GoldenPathLab/BotController.cs
A	Assets/_Project/Dev/Debug/GoldenPathLab/OrbitalRelayBotSteering.cs
D	Assets/_Project/Dev/Debug/WorldSystemsLab/OrbitalHoldZoneEvent.cs
M	Assets/_Project/Dev/Debug/WorldSystemsLab/WorldSystemsLabController.cs
A	Assets/_Project/Dev/Debug/WorldSystemsLab/WorldSystemsLabRelayAdapter.cs
A	Assets/_Project/Dev/Editor/WorldSystemsLab/OrbitalRelayAuthoring.cs
M	Assets/_Project/Dev/Editor/WorldSystemsLab/WorldSystemsLabAuthoring.cs
A	Assets/_Project/Dev/Editor/WorldSystemsLab/WorldSystemsLabRelaySupportAuthoring.cs
D	Assets/_Project/Dev/Labs/WorldSystemsLab/OrbitalHoldZone.prefab
A	Assets/_Project/Dev/Labs/WorldSystemsLab/RelaySupport.prefab
M	Assets/_Project/Dev/Labs/WorldSystemsLab/WorldSystemsLab.unity
A	Assets/_Project/Dev/Tests/Core/Editor/OrbitalRelayAuthoringTests.cs
A	Assets/_Project/Dev/Tests/Core/Editor/OrbitalRelayContactTests.cs
A	Assets/_Project/Dev/Tests/Core/Editor/OrbitalRelayIntegrationTests.cs
A	Assets/_Project/Dev/Tests/Core/Editor/OrbitalRelayRuntimeTests.cs
A	Assets/_Project/Dev/Tests/Core/Editor/OrbitalRelayStateTests.cs
M	Assets/_Project/Dev/Tests/Core/Editor/Subject42TutorialTests.cs
M	Assets/_Project/Dev/Tests/README.md
M	Assets/_Project/Documentation/PROJECT_MAP.md
M	Assets/_Project/Scenes/MainBuild/MVP.unity
D	Assets/_Project/Shaders/CaptureZone.shader
A	Assets/_Project/art/FX/OrbitalRelay/ComboPunch.anim
A	Assets/_Project/art/FX/OrbitalRelay/ComboPunch.controller
A	Assets/_Project/art/FX/OrbitalRelay/NodeActivation.anim
A	Assets/_Project/art/FX/OrbitalRelay/NodeActivation.controller
A	Assets/_Project/art/FX/OrbitalRelay/NodePulse.anim
A	Assets/_Project/art/FX/OrbitalRelay/NodePulse.controller
A	Assets/_Project/art/FX/OrbitalRelay/RelayGlow.mat
A	Assets/_Project/art/FX/OrbitalRelay/StabilizedTransition.anim
A	Assets/_Project/art/FX/OrbitalRelay/StabilizedTransition.controller
A	Assets/_Project/art/FX/OrbitalRelay/UrgentTimer.anim
A	Assets/_Project/art/FX/OrbitalRelay/UrgentTimer.controller
D	Assets/_Project/art/mat/WorldRules/M_CaptureZone.mat
D	Assets/_Project/prefabs/Environment/WorldEvents/CaptureZoneEvent.prefab
A	Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab
A	Assets/_Project/scripts/Combat/OrbitalStation/OrbitalBodyContactDetector.cs
M	Assets/_Project/scripts/Combat/OrbitalStation/OrbitalModuleRuntime.cs
M	Assets/_Project/scripts/Combat/OrbitalStation/OrbitalModuleVisual.cs
M	Assets/_Project/scripts/Progression/RunUpgrades/UpgradeManager.cs
M	Assets/_Project/scripts/Run/Exploration/ProductionAnomalySite.cs
M	Assets/_Project/scripts/Run/Exploration/ProductionExplorationSectorController.cs
M	Assets/_Project/scripts/Run/Tutorial/TutorialController.cs
M	Assets/_Project/scripts/Run/Tutorial/TutorialOverlay.cs
M	Assets/_Project/scripts/UI/Notifications/RunMessageService.cs
D	Assets/_Project/scripts/World/Events/CaptureZoneEvent.cs
D	Assets/_Project/scripts/World/Events/CaptureZoneVisual.cs
A	Assets/_Project/scripts/World/Events/OrbitalRelayConfig.cs
A	Assets/_Project/scripts/World/Events/OrbitalRelayEvent.cs
A	Assets/_Project/scripts/World/Events/OrbitalRelayNode.cs
A	Assets/_Project/scripts/World/Events/OrbitalRelayPresentation.cs
A	Assets/_Project/scripts/World/Events/OrbitalRelayResult.cs
A	Assets/_Project/scripts/World/Events/OrbitalRelayState.cs
M	Assets/_Project/scripts/World/Events/WorldEvent.cs
A	Assets/_Project/scripts/World/Events/WorldEventPressureModifier.cs
M	Assets/_Project/scripts/World/Events/WorldEventSpawner.cs
```

Дополнительные изменения финального fix pass: `Assets/_Project/Dev/Tests/Core/Editor/ProductionAnomalyRewardFlowTests.cs`, этот отчёт и execution record в `docs/superpowers/plans/2026-10-06-orbital-relay-production.md`. Остальные исправления финального pass находятся в уже перечисленных BotController / OrbitalRelayIntegrationTests / OrbitalRelayRuntimeTests.
