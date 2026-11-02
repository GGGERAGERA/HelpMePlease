# Быстрая разработка Subject#42

Проверено на Unity **6000.3.13f1**, Windows, 02.11.2026. Карта кода: [PROJECT_MAP](../Assets/_Project/Documentation/PROJECT_MAP.md). Правила дизайна: [Core](../Assets/_Project/Documentation/Subject42_Game_Design_Bible_Core.md), затем конкретный раздел [Bible](../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md). Исторический план и наличие класса сами по себе не подтверждают production-подключение.

## Сцена под задачу

| Что меняется | Где проверять |
|---|---|
| Бункер, станции, ресурсы, миссии, Escape, реплики | `Assets/_Project/Scenes/MainBuild/MainMenu.unity`, Play, **F9** |
| Обычный запуск, сохранение, возврат | `StartScreen → MainMenu → MVP → MainMenu`; используйте короткий smoke ниже |
| Production-бой, враги, награды, аномалии | `Assets/_Project/Scenes/MainBuild/MVP.unity` через бункер; прямой development-запуск имеет другие условия допуска прогресса |
| Telekinetic Rig, footprint-конструктор, новый запас/наградные расширения | Тот же MVP через бункер; адресный `TelekineticProductionTests` в существующем TutorialVerificationRunner. План: [перенос 02.11](superpowers/plans/2026-11-02-telekinetic-production.md) |
| Segmented Orbit, оружие, щиты, Обходчик/Проломщик/пауки | `Assets/_Project/Dev/Labs/OrbitalLab/EnemyOrbitalLab.unity`; Tools → Subject42 → Dev → OrbitalLab → Play Segmented Orbit |
| Рисованная/настраиваемая орбита | `Dev/Labs/OrbitalLab/CustomOrbitLab.unity`, `OrbitalConstructorLab.unity` |
| Relay, Corridor, World Rules, независимые территории | `Assets/_Project/Dev/Labs/WorldSystemsLab/WorldSystemsLab.unity` |

Лаборатория проверяет конкретную механику. Production-награды, доступность контента и переходы проверяйте через production-сцену.

## Две короткие проверки

**Бункер:** Tools → Subject42 → QA → Fast smoke → **Bunker debug**. Проверяет изолированное хранение, F9-контроллер/панель, ресурсы/reset, ворота, прогрессию, реплику, обычный выход/возврат и восстановление настроек. Это ограниченный сценарий Editor, не полный ручной прогон сборки.

**MVP:** там же → **MVP event reward and return**. Переходит из StartScreen, запускает обычный забег, проходит authored Relay с реальными контактами, выбирает показанную награду и возвращается. Используется существующий `Phase7SmokeTests.ExerciseProductionRoute`; ожидание полного забега не требуется. Враги останавливаются и здоровье увеличивается только внутри теста.

Оба запуска требуют Edit Mode, завершённого импорта и сохранённых сцен. Они используют существующий `TutorialVerificationRunner`; результат — `Artifacts/GeneratedQA/Tutorial/results.xml`. Проверяйте **ненулевое число выполненных случаев**, `failed=0` и время результата. Нулевой фильтр не является успехом. Следующий запуск перезаписывает XML; важный результат сначала скопируйте.

Для оценки Play Mode времени берите `test-case.duration` или внешний таймер: корневой `test-run.duration` может сбрасываться при domain reload.

Для агентского запуска запишите `bunker-smoke` или `mvp-smoke` в `Artifacts/GeneratedQA/BunkerDebug/editor.request`. Пишите временный файл рядом, затем атомарно переименовывайте: Editor может читать запрос одновременно с записью. Не ставьте второй запрос до завершения первого. Не запускайте второй Unity над открытым проектом.

## Ручная проверка бункера без риска сохранению

Tools → Subject42 → Dev → Bunker → **Play isolated MainMenu** открывает Game View **1600×900**, создаёт тестовую копию и пропускает интро/tutorial только в ней. В обычном Play F9 открывает панель в режиме просмотра; включите копию кнопкой сверху.

- **Экономика:** точные Gold/Cores, шесть мета-веток, станции/вложения, модули, стабилизаторы, unlock-каталог. Default- и station-unlock определяются своими правилами.
- **Доступ:** семь `RoomDoors` и отдельный выход; Open/Close/Lock/AUTO вызывают `BunkerGateVisual`. AUTO возвращает последнее игровое состояние. В этой production-сцене комнаты определяются воротами, отдельный `BunkerRoomAccess` остаётся в лабораториях.
- **Прогресс:** миссии (вперёд по тестовым стадиям), карта, задания, финансирование Escape, обычный старт через `BunkerRunStarter`. Имитация прохождения карты не выдаёт победные награды и не завершает Escape.
- **Реплики:** существующий каталог Random/Room/Event, возвращение, текущий текст, блокировки/приоритет/кулдаун. Неуспешный запрос не обходит игровые правила. Кнопка завершения текущей реплики помогает проверить следующий вариант.
- **Сохранение:** reset теста, чистый тест и возврат к обычному профилю требуют подтверждения. Перед переключением закройте игровые окна и завершите мини-игру/интро.

F9 закрывает окно и возвращает ввод. Время не останавливается, поэтому реплики можно наблюдать. Тестовый профиль сохраняется при переходах в MVP/обратно. В Editor его журнал хранится в SessionState для перезагрузки скриптов; Stop Play Mode отбрасывает его. В development player он живёт до закрытия приложения. Записи в основной профиль из теста не предусмотрены. Release не содержит панель/bootstrap/sandbox API.

На 02.11.2026 `SIGNAL TRACE` зависит от отключённого `FalseSignalEvent`: панель показывает недоступность и не принимает миссию в обход API. Выплата/стадии MissionService проверяются на временной доступной копии данных; это не подтверждает доступность production-миссии.

Хранилище проекта — `scripts/Bootstrap/PlayerPrefs.cs`. Все игровые потребители проходят через него, включая динамические receipt-ключи. **Не используйте `UnityEngine.PlayerPrefs` напрямую в новом gameplay-коде**: это обойдёт тестовую изоляцию. Прямые native-вызовы допустимы внутри этой границы и в проверках неизменности основного профиля.

## Достаточная проверка после локальной правки

| Изменение | Минимальная проверка |
|---|---|
| Текст/раскладка debug-окна | Компиляция и затронутая вкладка в 1600×900 |
| Ворота/доступ | Одна затронутая дверь: визуал + solid collider + AUTO; при изменении самого gate — адресный `BunkerDoorDebugTests` |
| Хранение/мета/миссии/переход профиля | Bunker debug smoke; при наградах — соответствующая адресная проверка выплаты |
| Только боевая формула/модуль | Один соответствующий метод из `OrbitalProductionTransferTests` / `OrbitalCombatV2*Tests`, затем нужная лаборатория |
| Компонент production-сцены/сериализованные ссылки | Адресный integrity-тест затронутого prefab/сцены; при изменении путей дополнительно production smoke |
| Release-guards/ссылки production → Dev | Tools → Subject42 → QA → Compile release and development scripts; при необходимости no-Dev boundary ниже |

Не запускайте категорию `Core`, GoldenPathLab, Bot Batch, длительные tactical-сравнения или полный набор тестов после изменения подписи/одного debug-контрола. Они нужны для своих рисков и по отдельному решению. Не повторяйте уже зелёную проверку без нового изменения, сбоя или неразрешённого сомнения.

## Как не тратить время на Editor

Оставляйте один Editor открытым. Объединяйте связанные изменения кода, затем один Refresh (`Ctrl+R`) и адресная проверка. Дождитесь окончания импорта/компиляции прежде, чем ставить запрос тестов. Не удаляйте `Library`, не делайте Reimport All и не меняйте настройки domain reload ради обычной локальной задачи. Для штатной итерации остановите Play перед изменением кода; после редкой перекомпиляции в Play тестовое сохранение можно безопасно пересоздать на вкладке «Сохранение».

Основная повторяемая цена в текущем Editor — компиляция плюс domain reload и вход/выход Play; повторное чтение всего проекта и длинный фильтр добавляют ненужное время. Замеры и границы выводов: [отчёт смены](night-shift-2026-11-02.md). Ускорение runtime/импорта не заявляется без отдельного сравнения одинакового сценария.

## Где искать владельца поведения

Все следующие пути относительно `Assets/_Project`:

| Система | Точка входа |
|---|---|
| Сборка сцены/зависимости | `scripts/Bootstrap/ProductionSceneComposition.cs`, `prefabs/Bootstrap` |
| Забег, завершение, награда | `scripts/Run/State/RunStateManager.cs`, `scripts/Run`, `RunEndService`, `RunFlowController` |
| Бункер/debug | `scripts/Bunker`, `scripts/Selection`, `Dev/Debug/Bunker`, `Dev/Editor/BunkerDebugTools.cs` |
| Мета, ресурсы, Escape | `scripts/Progression/MetaUpgrades`, `scripts/Bunker/MetaProgression`, `Data/Meta` |
| Карта/миссии/задания | `scripts/Run/SurfaceMap`, `scripts/Missions`, `scripts/Run/Expeditions`, соответствующие `Data` |
| Орбита и оружие | `scripts/Combat/OrbitalStation`, `prefabs/Orbital`, `prefabs/miniWeapons`; lab-контроллер — `Dev/Debug/OrbitalLab/EnemyOrbitalLabController*` |
| Враги/составы | `scripts/Combat/Enemies`, `Data/Stages/EnemySpawnProfiles`; сцены и prefab являются источником параметров |
| Мир/события | `scripts/World`, `Data/World`, `Data/WorldEvents`, `prefabs/Environment/WorldEvents` |
| Проверки | `Dev/Tests/Core/Editor`, [README тестов](../Assets/_Project/Dev/Tests/README.md) |

Для поиска сначала `rg` по конкретному типу/ключу/GUID. Сериализованное использование проверяйте по GUID, создание из кода — по типу и строковым путям, отдельно учитывайте Resources/Editor/Labs. Отсутствие GUID-ссылок на обычный C# helper не доказывает, что он не используется.

No-Dev compilation без запуска gameplay: `Tools/QA/Test-ProductionScriptBoundary.ps1`, передав реальные `Assembly-CSharp.rsp` из результатов `PlayerScriptCompilationCheck`. Не создавайте новые asmdef только ради одной debug-панели.
