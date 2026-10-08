# Phase 6 — результаты

Phase 6 завершена. Phase 7 не начата. Изменения оставлены в рабочем дереве, без commit/push.

## Тесты

Фактический baseline: **44 файла / 147 cases**, подтверждён source inventory и discovery Unity. После: **46 файлов / 138 cases**. Удалены 15 избыточных cases, добавлены 6 проверок границы/входных контрактов в двух отдельных fixtures. Ни одного пустого fixture.

Группировка по основной задаче файла; смешанные fixtures учитываются целиком:

| Группа | Файлов | Cases |
| --- | ---: | ---: |
| Core contracts | 21 | 66 |
| Regressions | 17 | 47 |
| Authoring validation | 4 | 10 |
| Dev/Lab checks | 4 | 15 |

Удалены все 9 перечисленных пользователем cases: `ProductionStateContractExists`, `ReleaseContactApiExists`, `TutorialAndBotConsumeProductionRelay`, `ScopedPressureContractExists`, `CompletionRewardContractExists`, `ProductionPrefabExists`, `SurfaceContractExists`, `RunLifecycleCarriesConfig`, `SharedRocketImplementationExists`.

Также удалены `LabCompositionHasRequiredAuthoredServices`, `InactivePrefabShowsStartZoneAndHidesNodes`, `LabCatalogUsesSameProductionPrefab`, `MigratedAssetsHaveNoMissingScriptOrExternalGuid`, `HudUsesAuthoredGraphicsOnly`, `ReusableViewsAreSavedPrefabs`. Их содержательная замена — реальные relay transitions/contact/start/respawn, scoped pressure leases, reentrant reward deduplication, tutorial/reward barriers, production config lifecycle, HUD composition и rocket cancellation/pooling. Corridor migration/structural fixtures сохранены ради lifecycle/ownership/collision/deterministic regressions; исторические GUID/source snapshots убраны. Authoring проверяет конфигурацию, реальные refs, discoverable colliders и missing nodes без требований к прежним именам/hierarchy.

Сохранены reward deduplication, lifecycle, save/load, atomic placement, session tokens, cleanup, deterministic route/state и regressions реальных багов. Новые cases защищают seed replay/раздельные потоки/Unity fallback, необязательное tutorial suppression, feedback neutral defaults, единственный F1 input owner и production asset boundary.

## Production / Dev

Production использует `GameplayRandom`, `RunDevelopmentOverrides`, `EnemyDebugDamagePolicy`, `ICombatFeelSettings` и `PhysicalCombatFeedbackTuningSource`. `BotRunSeed`, Bot session, special-power debug selector, combat dummy и tuning UI теперь адаптируют эти контракты. `PhysicalCombatFeedbackRuntime` и `CombatFeelVisualOverrides` перенесены в production с исходными meta/GUID; порядок enum, 146 neutral defaults и алгоритмы сохранены.

`GameplayHUD` больше не содержит `Subject42DebugMenu`. Dev bootstrap добавляет меню к HUD/Bunker только в Editor/development; один active owner обслуживает F1 между additive scenes, а scene unload позволяет повторное связывание. Release stub удалён. Все Dev runtime files получили полный Editor/development guard; release DLL не содержит project Dev types.

Asmdef **не введены**. Dev adapter ещё вызывает internal `PlayerLoadoutFactory.ApplyDebugMoveSpeed`, authoring фильтрует компоненты по assembly identity. Корректный assembly split требует отдельного решения по API visibility/filter semantics и Editor/test routing. Сейчас граница доказана компиляцией production без Dev; для её будущего постоянного закрепления сохранён `Tools/QA/Test-ProductionScriptBoundary.ps1`.

Две битые optional material refs лазерного prefab заменены на явный null, сохраняя прежнее эффективное значение. Неиспользуемый `crystal.prefab` с missing script оставлен для отдельного asset cleanup. Missing-ref integrity test ограничен referenced prefab MonoBehaviours; Dev dependency scan охватывает все production prefabs/configs и enabled build scenes.

## QA и проверки

Generated screenshots/XML/JSON/CSV/dumps/requests теперь пишутся в ignored **`Artifacts/GeneratedQA/<owner>`**. Девять targeted callbacks сохраняют только собственный XML, исключают конкурирующий запуск и явно сообщают о пустом filter. `BotRunSession` владеет standalone report, `BotBatchRunner` — batch/history, Golden Path — summary/captures. Подробности: `docs/phase6-test-tooling.md`. Нового mega-runner нет.

Свежие проверки 2026-10-08:

- Editor compile/domain reload — PASS.
- Non-development и development Player script compilation — PASS.
- Все 418 runtime sources с исключением 37 Dev sources: Editor/release/development — exit 0.
- Selected high-value tests + authoring — **56/56 PASS**, skipped 0.
- Production prefab/config/enabled scene dependency graph — без Dev refs.
- Actual release DLL — zero qualified project Dev type definitions.
- Source docs SHA-256 не изменились во время тестов; XML владельцев не перезаписываются чужими callbacks. Первоначальные изменения старых tracked XML восстановлены.
- `git diff --check`, GUID сохранённых перемещений и ignored output paths — PASS.

Evidence: `Artifacts/GeneratedQA/Phase6/selected-tests.xml`, `Compilation/results.txt`, `ProductionBoundary/results.txt`, `release-dev-types.txt`, inventory/category JSON и `menu-red.xml` (2 owners до fix).

Golden Path, Batch и полный gameplay matrix не запускались. На Phase 7 остаются отдельный asset cleanup (включая unused crystal), решение по assembly split/API visibility и при необходимости расширение missing-ref validation на scene/config/renderer data. Gameplay, visual polish и scene composition этой фазой не переработаны.
