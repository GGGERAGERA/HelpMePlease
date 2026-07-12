# Changes After Audit

Дата: 2026-07-12. Итоговый статус: **Playable**. Код компилируется, но без полного ручного A–Z Play Mode прохода проект нельзя честно назвать Vertical Slice Candidate/Ready.

## Что изменено

### Документация

- `Documentation/VerticalSlice_Audit.md` — полный статический аудит и severity.
- `Documentation/Folder_Structure.md` — применённая структура и правила зависимостей.
- `Documentation/Architecture_Overview.md` — модули, данные, зависимости, lifetime, Inspector и Console.
- `Documentation/Scene_Setup_Checklist.md` — MainMenu/MVP и отдельные обязательные компоненты.
- `Documentation/VerticalSlice_TestPlan.md` — ручные тесты A–Z.
- Этот файл — итог изменений, ручных действий и остаточных рисков.

### Структура

- Все 151 C#-скрипт перемещены из смешанной старой раскладки в `scripts/Bootstrap`, `Run`, `Bunker`, `Selection`, `Combat`, `Progression`, `World`, `UI`, `Audio`, `Data`, `Debug`, `Legacy`.
- Вместе с каждым скриптом перемещён его `.meta`; итог: 151 `.cs`, 151 соседний `.meta`, 0 orphan meta, 0 duplicate filename, 0 duplicate GUID.
- Namespaces и имена классов не менялись. Сцены, prefab, `.asset` и графика не изменялись.
- Полный перечень назначения новых путей находится в `Folder_Structure.md`; git показывает перемещения как delete/add до staging, что нормально для rename detection.

### Безопасные code fixes

1. `scripts/Combat/Player/PlayerHealth.cs`
   - `Start` больше не перезаписывает положительное runtime HP.
   - Исправляет потерю health snapshot при reload MVP из-за Unity execution order.
   - Новый игрок с `currentHealth <= 0` по-прежнему получает max HP.

2. `scripts/Progression/MetaUpgrades/CurrencyManager.cs`
   - Meta gold multiplier теперь устанавливается идемпотентно, а не умножается повторно на каждом MVP reload.
   - Удалён полный `Environment.StackTrace` из штатного `AddGold` log; сам диагностический log сохранён.
   - Награда по-прежнему начисляется только из `RunStateManager.EndRun` (debug cheat не относится к normal flow).

3. `scripts/World/Spawning/EnemySpawner.cs`
   - Добавлен null-check base `enemyPrefabs` перед чтением `.Length`.
   - Поведение корректно настроенного spawner не изменено.

## Что намеренно не исправлялось

- Старые `PortalToNextLevel.prefab`/`PortalToMenu.prefab` с отсутствующими legacy scripts: active scenes на них не ссылаются; удаление требует ручного подтверждения.
- `LevelNodeData.hasExtraChest` и chest prefab: данные сохранены на будущее, gameplay системы сундука сейчас нет.
- Строгие enemy unlock ID: нельзя нормализовать без решения о канонических ID; нужны Inspector checks.
- Два start controller (`BunkerRunStarter`, `MainMenuController`) и Restart methods: их фактическое подключение определяется UnityEvent, массовое изменение могло поменять gameplay.
- `LEVEL` в Pause/RunResult означает character XP level, а не endless level: это UI/product decision.
- Performance cleanup (`GetComponent` в hit, Find при enemy death), mojibake comments/text и logging bunker hover: не блокируют компиляцию, требуют отдельного профилирования/локализации.
- `WorldEvent` и survival methods: возможный будущий/scene-driven код, не удалён.
- Новые interfaces/base classes/DI framework не создавались.

## Проверки

- C# inventory: 151/151 script-meta pairs, GUID уникальны.
- Active YAML: MainMenu и MVP содержат 0 записей `m_Script: {fileID: 0}`.
- Local script references: 25 в MainMenu, 29 в MVP разрешаются через сохранённые GUID; prefab-contained components учитываются отдельно.
- Compile: `Assembly-CSharp` собран с актуальным новым списком source paths — **0 errors**.
- Единственный build warning: deprecated style API во внешнем `Assets/JMO Assets/Welcome Screen/CFXR_WelcomeScreen.cs`; к `_Project` не относится.
- `git diff --check`: проблем whitespace нет для tracked diff; новые файлы до staging отображаются как untracked.
- Полный Play Mode не выполнялся автоматически; Unity scenes/UI/input требуют ручного A–Z теста.

## Что настроить/проверить вручную в Unity

1. Дождаться Asset Import/Domain Reload после перемещения. Выполнить `Assets -> Refresh`, при необходимости `Regenerate project files`.
2. Убедиться, что Console не содержит compile errors и Missing Script. Старые строки путей в уже существующем Editor.log не считать текущей компиляцией; очистить Console.
3. В Build Settings проверить enabled scenes с точными именами `MainMenu` и `MVP`; стартовать через MainMenu.
4. По `Scene_Setup_Checklist.md` проверить все Inspector refs, особенно prefab-contained character/weapon/level cards и button UnityEvents.
5. На enemy prefabs проверить `EnemyIdentity.enemyId`: `Bomber` и `Tupik` должны точно совпадать с unlock `targetId`; базовый enemy не должен оставаться с пустым ID, если его убийства участвуют в unlock.
6. На boss prefab проверить `EnemyHealth.isBoss`, tag `Enemy`, identity, loot и отсутствие legacy Victory/portal listeners.
7. Проверить, что активные buttons не вызывают старые map/scene selection, direct scene load или portal flow.
8. Выполнить `VerticalSlice_TestPlan.md` A–Z, обязательно L/V/W (HP, repeated upgrades, три уровня) и G/P/U (оба end paths, один reward).
9. После полного прохода приложить чистый Console screenshot и числовую сверку gold формулы.

## Остаточные риски

- Unity execution order и prefab overrides подтверждаются только Play Mode; code fix HP компилируется, но должен быть воспроизведён на фактическом player prefab.
- Summary хранит base `GoldEarned`, тогда как `CurrencyManager` применяет meta multiplier при начислении. Возможна UX-разница между показанным и фактическим delta; продуктово решить, что отображать.
- Direct MVP launch остаётся debug path и отличается от полного bootstrap.
- Empty старые folders/folder meta могут оставаться до Unity refresh; это не duplicate scripts.
- UnityEvent/Animator parameters нельзя полностью доказать чтением C#; controllers/listeners проверяются вручную.
- Debug objects присутствуют в MainMenu YAML; их доступность в release нужно ограничить сценой/build policy.

## Readiness

- **Not Ready:** нет — компиляционных blocker после изменений нет.
- **Playable:** **да**, подтверждено статическим flow и compilation.
- **Vertical Slice Candidate:** только после успешных A–Z и Inspector checklist.
- **Vertical Slice Ready:** только после candidate run на целевой сборке и чистой Console.
