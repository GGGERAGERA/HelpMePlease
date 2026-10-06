# Football presentation pass

## Подтверждение источника дублирования

Окончательная причина второго изображения поля — не gravity-зоны и не дополнительная
арена. `FootballMinigame.FrameCamera` сужал `Camera.rect`, а URP сохранял старое
содержимое render target вне viewport. В боковой полосе оставался предыдущий кадр
бункера с полем в другом масштабе.

Регрессия `ViewportBarsClearPreviousFrame` запускает production Bunker, заполняет
render target пурпурным цветом и рендерит матч. До исправления полосы сохранили
RGBA(1, 0, 1, 1); тест упал. После исправления полосы чёрные, тест проходит
на старте и повторном запуске. Полный target очищается в `beginCameraRendering`
только для football-камеры во время её сессии. При сбросе/выходе обработчик снимается
и исходный viewport восстанавливается. Геометрия комнаты не изменена.

Первоначальная проверка локальных прямоугольников gravity-зон ошибочно была принята
за полное объяснение проблемы. Их оформление улучшено отдельно, как описано ниже.

## Локальное оформление gravity-зон

Короткая Play Mode проверка загружает production `MainMenu` через текущий Bunker flow,
запускает матч через `FootballStartStation.Interact` и сохраняет кадры камеры до/после.
Для сравнения используется одинаковое кадрирование арены. Это проверка production-сцены
и её настоящих объектов, а не отдельная тестовая арена; ввод мышью не автоматизирован.

Новые прямоугольники принадлежат двум `MeshRenderer`:

- `FOOTBALL/Gameplay/MinigameArena_VS/Runtime/Anomalies/FootballGravity_1/Pixel Polarity`;
- `FOOTBALL/Gameplay/MinigameArena_VS/Runtime/Anomalies/FootballGravity_2/Pixel Polarity`.

При временном скрытии только этих renderer'ов прямоугольники исчезают, authored-пол
сохраняется. Прежний `FootballField.shader` рисовал заливку с минимальной alpha 0.2
и замкнутый контур всего quad размером 7×3.2. Он использован только
`FootballPolarity.mat`. Теперь shader рисует локальные кольца, центр и короткие
угловые маркеры; сплошной заливки и прямоугольной рамки нет. Силы, collider bounds,
движение, смена полярности и radial phase не изменены.

## Разделение ответственности

- Статическая арена: существующие `Pixel Field`, tilemaps, разметка и физические
  границы в `MinigameArena_VS`. Scene и prefab арены не изменялись.
- Runtime: существующие мячи, цели и gravity-зоны управляются `FootballMinigame`.
- Presenter: `FootballMinigameHUD` получает состояние через прежние методы,
  локализует подписи и football-подсказки, форматирует значения и управляет видимостью.
- View: `MinigameSidePanelView` обновляет ссылки на authored TMP-поля и переключает
  видимость панели/дополнительной секции. Он не зависит от football или localization.
- Prefab: `PF_MinigameSidePanel` содержит всю иерархию и оформление. Он вложен
  в существующий `PF_FootballHUD`; GUID, Canvas/root/presenter fileIDs внешнего HUD
  сохранены, поэтому ссылки арены остаются прежними.

Для другой мини-игры panel можно разместить под своим Canvas и передавать заголовок,
подписи, строковые значения, статус и управление через view. Дополнительную легенду
можно заполнить другими данными или отключить `SetExtraSection(..., false)`.
Layout не создаётся кодом в runtime. Используется существующий `Subject42 UI SDF`.
World-space Canvas football использует UI sorting layer, чтобы декор комнаты
не перекрывал данные; размеры и место панели сохранены.

## Короткая проверка

`FootballPresentationTests`: подключение nested prefab, независимое использование
view другой игрой, production Play Mode кадры и isolation gravity renderer'ов,
числовые значения счёта/рекорда/таймера, скрытие и повторный запуск.

`FootballMinigameTests`: authored geometry, старт станцией, счёт и голы, физика мяча,
отмена/сброс, дверь, камера, pause и завершение без изменения наград/рекорда игрока.
Проверка положения игрока в существующем smoke-тесте переведена в плоскость XY:
его render Z не является глубиной `Collider2D`.
Игрок выбирается через `PlayerRuntimeReference.CachedPlayer`, как в production,
чтобы authored preview-персонаж комнаты не попадал в проверку вместо игрока.

Кадры, список renderer'ов и XML результатов находятся в
`Artifacts/GeneratedQA/FootballPresentation/`; исходные кадры сохранены в `baseline/`.
Итог: 2 football-проверки и 4 presentation-проверки прошли.
Регрессия viewport сначала упала на старом поведении, затем прошла после
исправления очистки буфера. `game-view-viewport-fixed.png` — захват обычного
Game View в Play Mode: боковые полосы чёрные, старого фрагмента поля нет.
Golden Path, Batch Runner и standalone build не запускались.

## Файлы

Добавлены (и соответствующие `.meta`):

- `prefabs/Bunker/Minigames/PF_MinigameSidePanel.prefab`;
- `scripts/Bunker/Minigames/MinigameSidePanelView.cs`;
- `Dev/Tests/Core/Editor/FootballPresentationTests.cs`;
- `Documentation/FootballPresentation.md`.

Изменены:

- `prefabs/Bunker/Minigames/PF_FootballHUD.prefab`;
- `scripts/Bunker/Minigames/FootballMinigameHUD.cs`;
- `scripts/Bunker/Minigames/FootballMinigame.cs` (очистка letterbox);
- `art/Football/FootballField.shader`;
- `Data/Localization/LocalizationTable.asset`;
- `Dev/Tests/Core/Editor/FootballMinigameTests.cs`.

Все пути относительно `Assets/_Project/`. Файлы не удалялись; прежняя иерархия
HUD перенесена в общий prefab. Посторонние пользовательские изменения сохранены.
