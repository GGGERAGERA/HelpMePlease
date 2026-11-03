# F1 полностью демонтирована

Удалены существующий F1 bootstrap/input, все страницы, navigation/history/search/favorites, local UI state/builders и Live Visual Studio UI. Нового меню/инструмента вместо них нет. В production MVP больше нет обработчика F1, открывающего это меню.

Физически удалены **9 файлов и их `.meta`** в этой работе:

- `Assets/_Project/Dev/Debug/F1/Subject42DebugMenu.cs`
- `Assets/_Project/Dev/Debug/F1/Subject42DebugMenu.Bunker.cs`
- `Assets/_Project/Dev/Debug/F1/Subject42DebugMenu.Capture.cs`
- `Assets/_Project/Dev/Debug/F1/Subject42DebugMenu.Hub.cs`
- `Assets/_Project/Dev/Debug/F1/Subject42DebugMenu.Mvp.cs`
- `Assets/_Project/Dev/Debug/F1/Subject42DebugMenu.Sandbox.cs`
- `Assets/_Project/Dev/Debug/F1/BotLabDebugUI.cs`
- `Assets/_Project/Dev/Debug/F1/LiveVisualStudio.cs` (включая `LiveVisualGesture`)
- `Assets/_Project/Dev/Tests/Core/Editor/F1SandboxTests.cs`

В двух существующих `Dev/Labs/BunkerSimple/{OLD,SIMPLE}/MainMenu.unity` удалены исключительно leaf-объекты `Development Debug Menu`, их RectTransform/CanvasRenderer/menu component и parent child references. Поиск GUID/fileID не выявил других consumers. Удалены F1-only проверки/handlers в тестах, UI Verify launcher Studio, F1-наблюдатель в TrailerStandaloneDiagnostics, пустые input predicates и неиспользуемые event/blocker hooks. Независимые combat/profile/pooling проверки адаптированы к существующим runtime API, не удалены вместе с интерфейсом.

Сохранены:

- Production `OrbitalStationRuntime`, `OrbitalRunState`, конструктор Tab и shared pause/reward/Bullet Time APIs; оружие/модули/награды/враги/события/бункер и сохранения. Ни баланс, ни геометрия, ни прогрессия не изменялись.
- Игровые debug APIs `EnemySpawner`, `UpgradeManager`, `WorldEventSpawner`, `ProductionSectorDebugController`, `TrailerCaptureSession`, weapon/enemy experiment setters; независимые лаборатории и их существующие собственные hotkeys/UI. Папка с названием F1 содержит также эти отдельно используемые инструменты, поэтому целиком не удалялась.
- Golden Path, боты и отчётность. Только прежняя F1-привязка существующей bot session перенесена в текущую production scene composition после readiness; новая сессия автоматически не создаётся. Dead F1 guards/restore helper удалены, обычные pause/reward/transition checks сохранены.
- VFX, `LiveVisualRuntime`, `OrbitalVisualAdapter`, scene adapters, `OrbitalPresentationConfig`, применение production profiles и historical ring values. Existing catalog Prepare/baseline authoring остались как независимые consumers данных. Все шесть пользовательских visual assets совпадают с исходными SHA-256 ([проверка](visual-assets-after.txt)); Artifacts/Subject42Character не затронуты.

Проверка: [Editor compilation](editor-compilation.txt) без ошибок C#, затем ровно **один** `F1RemovalSmokeTests.ProductionMovesFiresAndUsesNativeConstructorWithoutDebugMenu`: PASS, 1/1, 9,387 с ([XML](results.xml), [пункты](result.txt)). Вход MainMenu → MVP, реальное движение Rigidbody2D на 3,246 м через существующий MovementIntent/production physics, обычная стрельба Segmented, открытие/закрытие штатного constructor handler Tab с восстановлением времени и ввода. Удалённые UI types отсутствуют в assemblies, production visual runtime существует. Физический ввод клавиатуры не синтезировался.

Кадры: [MVP/движение/стрельба](mvp-movement-and-fire.png), [штатный Tab](native-tab-constructor.png). Независимый read-only review конкретных регрессий не нашёл. Массовых тестов, builds, git add/commit/смены ветки не было. MVP оставлена открытой в Unity Editor.

Аудиты consumers: [тесты](test-consumers.txt), [visual UI](visual-ui-removal.txt), [боты](bot-dependencies.txt).
