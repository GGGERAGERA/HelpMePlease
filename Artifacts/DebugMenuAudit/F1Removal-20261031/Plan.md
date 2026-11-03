# Полный демонтаж F1, 31.10.2026

Прямой запрос пользователя: физически удалить текущую F1 и интерфейс Live Visual Studio, без замены/переноса страниц. Это удаление debug UI, правила игры и принятые GDD-механики не меняются.

- [x] Проверить реальные source/serialized consumers. Удалить все Subject42DebugMenu partials, BotLabDebugUI и исключительно F1 UI tests/helpers вместе с .meta.
- [x] Удалить Live Visual Studio UI/gesture classes и её UI-only tests/verification; сохранить runtime, общие adapters/profile data и независимое существующее authoring.
- [x] Убрать F1/LVS input/pause references. Сохранить production reward/pause/constructor/Bullet Time API и реальные debug APIs.
- [x] Сохранить Golden Path/ботов: удалить только F1 guards; существующую привязку сцены для уже созданной bot session обеспечить текущей production-композицией. Никакого нового менеджера или UI.
- [x] Удалить сериализованные компоненты снятого F1 script из двух BunkerSimple lab scenes, не затрагивая остальные компоненты/геометрию.
- [x] Editor compile и ровно один короткий smoke MainMenu → MVP → физическое движение через существующий MovementIntent → обычная стрельба → штатный constructor toggle/return. Проверить отсутствие F1/LVS типов и зависшего ввода; оставить MVP открытой. Без builds/массовых тестов/коммитов.

Исходная рабочая папка содержит незакоммиченные изменения предыдущего упрощения F1, пользовательские visual assets и новые Artifacts/Subject42Character. Они не сбрасываются. Сохранённые visual profiles/runtime и ассеты персонажей не изменять. Полные проверки старых лабораторий не запускаются; потребители адаптируются адресно и проходят Editor compilation.

Результат: [Report.md](Report.md). Editor compile + один короткий smoke PASS; MVP открыта.
