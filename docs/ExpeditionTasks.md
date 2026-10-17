# Ядра и дополнительные задания экспедиций

Рабочий цикл: карта поверхности → «В путь» → выбрать одно задание либо «Без задания» → «В ПУТЬ» → выполнение → немедленное сохранение ядер → результат и баланс в бункере.

| Задание | Условие | Ядра | Источник прогресса |
| --- | --- | --- | --- |
| Выживший | 3 минуты | 1 | `RunStatsManager.RunTime`, событие `ProgressChanged` |
| Охотник | 75 врагов | 2 | `EnemyHealth` → `KillManager` → `RunStatsManager.AddKill`, `ProgressChanged` |
| Путник | 2 локации | 2 | `RunStateManager.RegisterCompletedLevel`, после настоящего выхода; обучение не считается локацией |

Время идёт через существующий `Time.deltaTime`: пауза и выбор наград его не увеличивают. Проверка времени выполняется в тот же кадр, в котором пересечён порог, включая переходы между локациями. Счётчики забега сохраняются штатным `CommitCurrentSceneStats`; вне обучения трекер использует общую статистику забега. Не требуется появления случайного события или особого противника. Выходы не принудительные, поэтому задания достижимы на каждом из шести маршрутов.

## Настройка данных

- `Assets/_Project/Data/Expeditions/Task_*.asset`: уникальный ID, ключи названия/описания/прогресса, тип условия, цель, награда, необязательный список ID секторов. Описание может использовать `{0}` для целевого значения.
- `Assets/_Project/Data/SurfaceMap/Sector_*.asset`: `Victory Cores`, `First Clear Cores`, `Expedition Tasks`. Набор задаётся отдельно для каждого сектора. Для текущего компактного экрана подготовлены три задания.
- `Assets/_Project/Data/Localization/LocalizationTable.asset`: RU/EN формулировки.

| Сектор | Победа | Первое прохождение дополнительно |
| --- | --- | --- |
| A1 | 1 | 1 |
| C1, D1 | 2 | 1 |
| A2 | 3 | 2 |
| C2, D2 | 4 | 2 |

## Сохранение и однократная выдача

`CurrencyManager` хранит ядра отдельно в `TOTAL_CORES`, старые сохранения получают нулевой баланс. Золото и его множители работают по прежним правилам. Траты ядер нет.

Каждому запуску соответствует новый GUID. `TryGrantCoresOnce` записывает баланс, квитанцию и разбивку наград в одной границе `PlayerPrefs.Save()` до уведомления интерфейса. Задание оплачивается сразу при выполнении, поэтому смерть и выход из игры после выполнения не отменяют награду. Повторный callback защищён квитанцией; новый забег получает новый GUID и позволяет повторить задание. Награда сектора требует подтверждённой победы. Бонус первого прохождения проверяет существующий `SurfaceMapService` и имеет отдельную постоянную квитанцию сектора. Уже пройденные секторы старого сохранения не получают бонус задним числом.

Результаты используют копию `ExpeditionCoreRewards` из `RunSummary` и ничего не начисляют. `OnCoresUpdated` обновляет HUD бункера после сохранения; вложенные начисления завершаются до повторной публикации итогового баланса. Assisted/development runs не получают награды.

## Основные файлы интеграции

- Валюта: `scripts/Progression/MetaUpgrades/CurrencyManager.cs`.
- Задания: `scripts/Run/Expeditions/ExpeditionTaskDefinition.cs`, `ExpeditionTaskRun.cs`.
- Забег: `scripts/Run/State/RunStateManager.cs`, `RunConfig.cs`, `scripts/Run/Stats/RunStatsManager.cs`, `scripts/Run/Results/RunSummary.cs`.
- Карта и выбор: `scripts/Run/SurfaceMap/SurfaceSectorDefinition.cs`, `SurfaceMapService.cs`, `scripts/Bunker/UI/SurfaceMapView.cs`, `ExpeditionTaskPicker.cs`, `ExpeditionTaskChoice.cs`.
- Представление: `scripts/Bunker/UI/BunkerHudView.cs`, `BunkerRunSummaryPresenter.cs`, `scripts/UI/HUD/ExpeditionTaskHud.cs`, `HUDManager.cs`, `scripts/UI/Common/CoreCurrencyIcon.cs`, `ExpeditionCoreSummaryView.cs`, `DeathResultPresentation.cs`.
- Основные префабы: `prefabs/UI/Bunker/BunkerHUD.prefab`, `prefabs/UI/HUD/GameplayHUD.prefab`, `prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab`, `PF_BunkerRunSummary.prefab`.
- Авторинг: `Dev/Editor/Authoring/ExpeditionCoreAuthoring.cs`; тесты: `Dev/Tests/Core/Editor/ExpeditionCoreTests.cs`, `ExpeditionCoreFlowTests.cs`, обновлённые `BunkerHudTests.cs`, `CoreTestSupport.cs`.
- `Scenes/MainBuild/StartScreen.unity`: удалены три устаревшие записи удаления уже отсутствующих компонентов кнопок, обнаруженные проверкой ссылок; действующие компоненты и поведение не менялись.

Пути в списке относительно `Assets/_Project`. Авторинг доступен через `Tools > Subject42 > Expeditions`; он не меняет сцены, маршруты, открытие секторов или пользовательские сохранения. При повторном сохранении префабов сохраняются локальные ID вложенных объектов, на которые ссылаются production-сцены.

## Реальная проверка

17.10.2026: Unity Test Runner — 27/27 успешно. Покрыты все шесть запрошенных сценариев; Play Mode дополнительно проверяет настоящие кнопки выбора, событие убийства/статистики, смерть игрока, победу над существующим боссом, возврат в бункер и новый сеанс Play Mode с загрузкой сохранённого баланса и карты. Ожидание маршрута в тесте сокращается, сам босс и подтверждение победы используются штатные. Проверены старые прохождения, отсутствие задания, повторные callbacks и вложенные события валюты. Пройдены существующие проверки HUD, карты и сериализованных ссылок production-ассетов.

Release/Development скрипты скомпилированы, также проверена компиляция без всех Dev/Lab исходников. Пять снимков интерфейса из `Artifacts/GeneratedQA/ExpeditionCores/` просмотрены. Ручное прохождение отдельной сборки не проводилось. Итоговый XML: `Artifacts/GeneratedQA/Tutorial/results.xml`.
