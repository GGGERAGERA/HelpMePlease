# Football: diff и классификация перед исправлением

Источник до pass: commit `7fc47f78`, `Assets/_Project/Scenes/MainMenu.unity`; эти файлы мини-игры были чистыми перед прошлым pass. Существующие посторонние изменения не откатываются. Полный patch предыдущего pass и снимок redesign сохранены отдельно перед исправлением.

| Элемент | До pass | После pass | Класс / решение |
|---|---|---|---|
| Композиция | 24×28; три зоны с долями 20/40/40 | Новое поле 24×16 | visual/content: вернуть из исходной сцены |
| Игровые мячи | 4, исходные четыре spawn points | 1, три новых точки | gameplay-essential: вернуть |
| Аномалии | 2 движущихся gravity fields на исходных lanes | Удалены | gameplay-essential: вернуть динамическое поведение |
| Цели | 3 движущиеся цели; зелёная/жёлтая/красная, очки 2/5/10 | Удалены | gameplay-essential: вернуть |
| Ворота | 2 исходных prefab с mesh/net, попадание +20 | Одни новые прямоугольные ворота, завершение раунда | gameplay + visual/content: вернуть исходные объекты и scoring |
| Раунд | 60 секунд, счёт, рекорд | Один гол завершает игру | gameplay-essential: вернуть |
| Граница игрока | Стрелять из нижней ball zone; верхний player-only barrier | Игрок ходит по всему полю | gameplay-essential: вернуть authored collider и локальные collision exceptions |
| Крупные статические блоки | В исходном корне таких блоков нет; препятствия — динамические поля/цели/ворота | Добавлены два новых bank-препятствия | Удалить привнесённый контент |
| HUD | Исходный компактный HUD времени/счёта/рекорда | Большая нижняя полоса | visual/content: вернуть исходную подачу; оставить компактный reset |
| Layout generator | Автопересчёт зон, anchors, ворот; Awake/OnValidate/Update | Удалён | Ненужная procedural infrastructure: не возвращать |
| Runtime gate/boundary builders, legacy fallbacks | Создавали/чинили геометрию и ссылки в runtime | Удалены | Ненужная procedural infrastructure: не возвращать |
| Физика/reset/camera | Неполный периметр, bounce 0.5, reset/recovery, общее кадрирование | Collider walls, bounce 0.98/friction 0/CCD, R/reset, authored CameraBounds | Infrastructure: сохранить, привязав к исходной композиции |

Движение целей и аномалий, выбор вида цели и её respawn — исходная динамическая механика, а не генерация основной геометрии. Возвращаются только её нужные компоненты. Координаты зон, lanes, спавнов и внешний вид ворот берутся из исходной сцены; генератор в итоговый prefab не включается.


[Полный diff первого pass](C:/Users/User/.codex/visualizations/2026/09/07/01a07b50-6dbb-70a2-a471-cd3107a971e0/football-before-correction/vertical-slice-pass.patch).

Итог исправления: gameplay-essential и исходный visual/content восстановлены; procedural infrastructure из таблицы не возвращена. Удалены привнесённые поле 24×16, два bank-блока, новые ворота и нижняя HUD-плашка. Геометрический audit: 90 исходных transforms совпадают по локальным position/rotation/scale; missing scripts в prefab — 0. Итоговый контейнер сохраняет GUID предыдущего MinigameArena_VS.prefab.
