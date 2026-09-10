# Enemy Gallery

Открыть `Assets/_Project/Scenes/EnemyGallery.unity`, нажать Play.
WASD / стрелки — движение, колесо — zoom, F1 — подписи обеих зон.

- Зона A: 7 исходных production prefabs и 4 подготовленных Prefab Variants.
- Зона B справа (X >= 36): 48 статических визуальных кандидатов.
- Источник, тип, описание и масштаб предпросмотра — в EnemyGalleryCandidateMarker.

## Обновление

`Tools > Subject42 > Refresh Enemy Gallery` обновляет production references
и варианты из `prefabs/Enemies/PreparedVariants`. Сохраняет позиции и зону B.

`Tools > Subject42 > Refresh Enemy Visual Candidates` обновляет displays из
проверенного списка `EnemyGalleryVisualAuthoring.Selections`, сохраняет позиции
и удаляет display только при исчезновении source asset. Новый арт нужно сначала
визуально проверить и добавить в список; кадры и части атласов не распознаются
автоматически как новые персонажи. Refresh не генерирует отчёты или скриншоты.

## Подготовленные варианты

| Base prefab | Alternate Graphic | Анимация |
|---|---|---|
| p_Enemy_Bomber | Graphic/Enemy1Bomber | animWalk1 через общий idle override |
| p_Enemy_Shooter | Graphic/Enemy1Shooter | исходный animIdle1 |
| p_Enemy_classic | Graphic/Enemy1_0 | animWalk1 через общий idle override |
| p_Enemy_default | Graphic/Enemy1Elite1 | animWalk1 через общий idle override |

Файлы `*_Alt.prefab` находятся в `prefabs/Enemies/PreparedVariants`.
Health, collider, AI и attack наследуются. Исходные prefabs и spawn tables не менялись.
Общий override нужен потому, что исходный idle не анимирует три старые Graphic-ветки.

В галерее боевые компоненты удалены только у scene instances. Production-турель
плавно поворачивает свой ствол без стрельбы. Player и camera остались исходными.
Кандидаты статические, без gameplay scripts и colliders. Sprite selections и
материалы предпросмотра моделей хранятся вложенными объектами в одном
`art/EnemyGalleryVisuals.asset`; отдельные файлы для них не создаются;
оригинальные текстуры, import settings и материалы не меняются.

## Проверка

`Tools > Subject42 > Validate Enemy Gallery (3 minute Play Mode)` запускает
проверку анимаций, scales, неподвижности, отсутствия боя и подписей обеих зон.
Результаты сохраняются только в игнорируемый Git каталог
`Artifacts/GeneratedQA/EnemyGallery` и не попадают в рабочие изменения.

Проверено: 180 секунд, 0 errors / warnings, все 10 Animator-экспонатов меняли кадры,
ствол турели двигался. Повторные Refresh сохранили identities и позиции обеих зон.
Кандидаты остаются арт-предложениями; модельные previews используют unlit-материалы.
