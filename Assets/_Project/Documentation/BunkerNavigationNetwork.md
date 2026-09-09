# Bunker: аудит и переход к постоянной навигационной сети

## Исходное состояние

Проверены сохранённые изменения предыдущей задачи (коммит `c6aea872`, `steps`), текущий MainMenu и фактические компоненты загруженной сцены.

Уже существовали:
- Один объект сцены `Bunker Floor Navigation` с `BunkerNavigationView`.
- Три mesh-asset: `Character.asset`, `Weapon.asset`, `RunGate.asset`; отдельного navigation prefab и графа pathfinding не было.
- `FloorLight.mat`, shader `BunkerFloorNavigation.shader`, кольцевые endpoint-маркеры, три TMP-подписи.
- `BunkerNavigationAuthoring`, `BunkerNavigationProbe`, `BunkerNavigationPlayModeQA`, `BunkerOnboardingTests` и старые снимки в `Documentation/BunkerNavigationQA`.
- Onboarding-биты `BunkerOnboarding.v1`, запись посещений из выбора персонажа/оружия и запуска run, debug-кнопки переключения шага.

Прежняя система выбирала один шаг в Update, зависела от расстояния игрока для текста и оставляла неактивные пути тусклыми. Старый Clearance.txt уже содержал пересечения маршрута с препятствиями; старые STEP-снимки не являются проверкой новой системы.

## Что сохранено, удалено и изменено

Переиспользованы класс runtime-компонента, имя и роль scene root, mesh-способ отрисовки, существующие GUID трёх mesh-assets, материал, shader, маленький напольный маркер и editor-команды. Планировка и prefab мини-игры не перестраивались.

Удалены три TMP-подписи, последовательные маршруты между станциями, ссылки на игрока/intro/panels в навигации, onboarding enum/API, вызовы записи шагов, debug-строки и ставшие ненужными onboarding-тесты. Старое значение PlayerPrefs больше не читается и не влияет на игру; чужие настройки сохранений не очищаются.

Вместо трёх последовательных путей сохранены один `MainCorridor` и восемь независимых веток. Character/Weapon/RunGate mesh-assets обновлены; добавлены MainCorridor, UpgradeStation, AnomalyStation, FutureStation, SecretRoom, MiniGame. Старый scene root заменён через существующий authoring-инструмент одним root с тем же именем и новыми дочерними ветками. Вторая navigation system не создавалась.

Ветки комнат действительно выключают MeshRenderer, включая endpoint. По уточнению пользователя маршрут Run Gate всегда видим при включённой сети, независимо от открытия ворот и доступности станции. Не используются Update, FixedUpdate, поиск объектов в runtime, pathfinding или позиция игрока. События `AvailabilityChanged` добавлены к существующим источникам состояния. При OnEnable/Start сеть читает актуальное состояние заново; OnDisable снимает подписки и скрывает геометрию. Пульс вычисляется shader-ом.

## Сцена и источники истины

Все элементы сети находятся в `MainMenu / Bunker Floor Navigation`. Центральная линия проходит от зоны появления по существующему проходу к центральному коридору на y=-12. Обход у двух ламп проверен по физическим коллайдерам; отрисовка находится над floor tilemap и под мебелью. У существующего Tilemap2 sortingOrder изменён с 0 на -1, у navigation — 0; геометрия tilemap не менялась. Короткий участок пола под проекцией высокой розовой лампы оставлен без светящейся полосы, чтобы её прозрачный sprite не пропускал линию поверх корпуса; travelling pulse сохраняет расстояние через этот зазор.

| Ветка | Реальный объект/вход | Источник доступности |
|---|---|---|
| Character | Bunker/BunkerStation/CharacterRoom, (37.5,-8) | BunkerRoomAccess.Unlocked + isActiveAndEnabled |
| Weapon | Bunker/BunkerStation/WeaponRoom, (46.5,-8) | BunkerRoomAccess.Unlocked + isActiveAndEnabled |
| UpgradeStation | Bunker/BunkerStation/UpgradeRoom, (55.5,-8) | BunkerRoomAccess.Unlocked + isActiveAndEnabled |
| AnomalyStation | Bunker/BunkerStation/AnomalyRoom, (37.5,-17) | BunkerRoomAccess.Unlocked + isActiveAndEnabled |
| FutureStation | Bunker/BunkerStation/FutureRoom, (46.5,-17) | BunkerRoomAccess.Unlocked + isActiveAndEnabled |
| SecretRoom | Bunker/BunkerStation/SecretRoom, (55.5,-17) | BunkerRoomAccess.Unlocked + isActiveAndEnabled |
| RunGate | Bunker/BunkerStation/p_BunkerGates1, (64,2) | Постоянно видимая ветка выхода, независимо от состояния ворот/станции |
| MiniGame | MinigameArena_VS, вход между EntryLeft/EntryRight; StartZone (93.93,-0.52) | FootballMinigame.isActiveAndEnabled, включая activeInHierarchy родителя |

Идентификация шести помещений сделана по BunkerRoomId и связанным дверям/туману, а не только по имени. Станции Character/Weapon/Upgrade и Anomaly подтверждены компонентами BunkerStation. Future/Shop и Secret/Other — существующие помещения с room state, а не новые реализованные игровые функции. Отключённые старые варианты BunkerRoom2_* и старые коридоры в сеть не включены.

Для mini-game отдельного room unlock state сейчас нет. Используется существующая активность FootballMinigame; Running/Success/Idle не закрывают доступную комнату. Новые progression-флаги не добавлялись. Маршрут заканчивается перед входом в арену, не пересекает её игровое поле.

Run Gate по текущему startOpen=false закрыт при загрузке, но его навигационная ветка всегда видна. Open()/Close() и отключение взаимодействия станции не меняют подсветку выхода. Gameplay-логика запуска run не изменена.

## Проверка

Unity 6000.3.13f1: компиляция runtime/editor; автоматические сценарии в настоящем MainMenu Play Mode. Лог: `Artifacts/BunkerNetwork/PlayMode.txt`.

Проверены A–E с немедленными утверждениями после SetUnlocked/Open/Close/disabled, без ручного Refresh сети. Проверены одновременно открытые восемь веток, отключение/включение room root, arena root, gate station, navigation component, восстановление актуального состояния и неизменность mesh-assets при перемещении игрока.

Clearance.txt проверяет полосы маршрута с шагом 0.2 и радиусом 0.12 относительно статических solid 2D colliders. Zoom 7/20 и обзор 31 сняты одной мировой геометрией; световое ядро примерно 0.05–0.1 world unit, полная полоса с halo 0.22. Optional branches имеют яркость 0.7, основной коридор 0.75, открытый Run Gate 1.0.

Снимки — реальные Camera.Render из Play Mode; для статичных кадров временно выключен motion blur от телепортации камеры, затем восстановлен. Состояния теста не сохраняются в MainMenu; intro preference восстанавливается при выходе из Play Mode.

- `00-default.png`: реальные начальные состояния.
- `01-few-open.png`: Character, Weapon, MiniGame, RunGate открыты; остальные ветки отсутствуют.
- `02-mini-game-open.png`: вход и маршрут к футбольной мини-игре.
- `03-upgrade-open.png`: Upgrade открыта без reload.
- `04-closed-rooms-and-gate.png`: закрытые комнаты, отключённая мини-игра и закрытый Gate с постоянным маршрутом выхода; к закрытым комнатам и отключённой мини-игре веток нет.
- `05-full-network.png`: все существующие ветки открыты в QA.
- `06-zoom-in.png`, `07-zoom-out.png`: проверка читаемости.

Повторить: открыть сохранённую MainMenu в Edit Mode → Tools/Subject42/Bunker/Rebuild Authored Navigation → Run Navigation Play Mode QA. QA меняет только временные runtime-состояния и сохраняет результаты в Artifacts/BunkerNetwork.
