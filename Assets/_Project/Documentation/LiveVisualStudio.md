# Live Visual Studio — implementation and operation

## Intent and constraints

Build a live, unpaused visual workbench for the production MVP using existing uGUI/TMP. Preserve the current CombatHierarchy/Astra work and existing labs/debug APIs. No gameplay tuning, art direction, GDD edits, commits, staging or branch changes.

Original is an explicit immutable Git revision, Current is the captured working tree at session initialization, Custom is sparse user overrides. Original never means “whatever the prefab currently contains”. Code/geometry changes which cannot be represented by a visual adapter must be reported, not simulated by a misleading slider.

## Architecture decision

Extend the existing scene composition and visual preset, with a runtime profile/registry shared by release presentation and the development workbench. Component adapters expose only visual properties. Pooled creation and enemy registration supply stable type identities; scene targets are enumerated on scene load, not every frame. Runtime material clones/property blocks and cloned config protect authored assets. Group → Type → Instance precedence; instance edits never enter production JSON.

Keep the existing lab implementations behind new navigation. A separate uGUI side panel owns pointer gestures, remains independent of F1 and never owns game time. Existing input owners must consult its UI gate.

## Implementation plan

- [x] Capture starting changes and resolve the pre-CombatHierarchy Git revision using evidence/history. Generate baseline values and provenance without checking out that revision.
- [x] Implement sparse profile model, validation, precedence, comparison modes and bounded undo/redo. Add tests for per-type independence, resets, serialization and non-finite input rejection.
- [x] Implement target registration, component adapters and runtime application. Add checks for pooling and material ownership; runtime validation remains a separate acceptance step.
- [x] Add explicit ORBITAL/config/FlashLink family adapters. Preserve current visuals by default; add sibling-family/gameplay isolation tests.
- [x] Replace the F1 entry UI with a compact Russian command hub, retaining labs/APIs, search and favorites.
- [x] Implement a separate live side panel with picking, scopes, searchable property groups, direct numeric/color input, reset/history, profile import/export and explicit production save.
- [ ] Compile, test profile behavior, exercise the production scenario in the user's Editor and inspect actual Game View PNGs. Report unsupported coverage and unverified input honestly.

## Review focus

Current values survive Original/Custom switching; newborn pooled instances inherit type overrides; renderer changes do not touch colliders/shared materials; animation cannot silently overwrite tuned presentation; UI capture does not leak RMB or a drag into gameplay; imported profiles cannot create NaN/unbounded allocations; release applies approved profile without debug UI.

## Verification record

Код внесён. Проверка runtime и фактического изображения продолжается; наличие кода или компиляции не означает принятие визуального результата. Временные доказательства находятся только в существующем ignored `Artifacts/GeneratedQA`.

## Управление

- **F1** — новый Debug Hub: «Визуал», «Забег», «Бой и события», «Диагностика». Поиск ищет команды всех разделов; звезда сохраняет избранное разработчика.
- **Визуал → Live Visual Studio** — независимая панель. **F8** скрывает/возвращает уже открытую панель. Заголовок перемещает её, нижний правый угол меняет ширину, «—» минимизирует. Положение, ширина и прозрачность сохраняются локально.
- **Пипетка** — следующий клик по изображению выбирает зарегистрированный Renderer; при перекрытии появляется список. Picking использует игровую MainCamera и bounds, поэтому прозрачные пиксели внутри sprite bounds тоже могут попасть в список. Рамка не доказывает отсутствие перекрытия: инспектор отдельно показывает active/enabled/нахождение в кадре.
- **Type** изменяет один компонент определённого prefab-типа и его новые экземпляры. **Group** изменяет соответствующий параметр всей категории. **Instance** имеет приоритет и действует только на выбранный runtime-компонент. Instance не экспортируется в переносимые/production-профили.
- У врагов раздельные Group для обычных, подрывников, стрелков, элиты, турелей, глаз и боссов. Явное изменение параметра в Group снимает его Type/Instance overrides внутри этой категории, чтобы ползунок сразу влиял на всю выбранную группу; изменение Type снимает Instance overrides этого параметра данного типа. Undo восстанавливает заменённые настройки. При загрузке готового профиля сохраняется приоритет Instance → Type → Group.
- Число справа от slider можно вводить напрямую через точку; **±** даёт точный шаг, **Shift** уменьшает шаг в десять раз. Для цвета доступны RGB/HEX SDR/HSV; независимая насыщенность пикселей находится в «Коррекция пикселей · Lit 2D», отдельно от Tint.
- **Original** — поддерживаемые значения Git `19b6c01af57f91a3cb0248e5767079e50a1f2859`; **Current** — захваченное состояние текущей рабочей копии при регистрации объекта; **Custom** — Current плюс пользовательские изменения. Отсутствующее Git-значение явно обозначается «нет → Current». Переключение не стирает Custom.
- Undo/Redo и сбросы изменяют временный профиль. После изменения параметров не требуется подтверждение. Геометрия опасностей и боевые таймеры не редактируются.
- **Save JSON / Load** работают с именованными различиями в `Assets/_Project/Data/World/Presentation/Profiles` (Development player: `persistentDataPath/VisualProfiles`). Clipboard export/import тоже использует JSON.
- Только явная кнопка **Применить к production** записывает утверждённый snapshot `Data/World/Presentation/LiveVisualProduction.json` и назначает его существующему `VisualTuningSavedValues.asset`. Именованные JSON в `Profiles` остаются черновиками: последующий Save JSON не изменяет уже утверждённый production snapshot. Обычная release-сборка применяет утверждённый профиль через штатную production composition; debug UI в ней отсутствует. Ползунки не сохраняют prefab/material/config assets.

## Подключённые контракты

SpriteRenderer: tint/alpha, sorting/flip, независимые hue/saturation/brightness/contrast/gamma для существующего Subject42 Lit shader и URP Sprite-Lit-Default (временный совместимый material clone). Поддержанные shader float/range/color/vector читаются из настоящего shader. MaterialPropertyBlock сохраняет свойства других владельцев; combat-flash временно сохраняет владение material.

Light2D/Light: RGB/intensity, поддерживаемые радиусы/falloff/shadow intensity локальных Light2D. Global Light не показывает неработающие радиусы/alpha/shadows. Camera: orthographic size. Существующие Volume: Bloom и ColorAdjustments; редактируется runtime profile, включая корректное восстановление overrideState.

Particles: size/speed/lifetime, rate over time при включённом модуле, ограниченный maxParticles, отдельный Tint/Alpha через colorOverLifetime. Это позволяет окрашивать и частицы с C# EmitParams, сохраняя исходный startColor. Размер/lifetime действуют на новые частицы; Tint/Alpha — на уже существующие. Настройки извлекаемых из SimplePrefabPool объектов применяются до активации/синхронного Emit. TrailRenderer/LineRenderer: width, цвета/alpha; TrailRenderer.time. Линии станции принадлежат специализированному ORBITAL-адаптеру.

ORBITAL: RingSurface, tier palette/emission/glow, ядро/halo/визуальные интервалы, цвет и следы трёх семейств снарядов. FlashLink имеет независимые семейства Other, MuzzlePistol/Shotgun/Sniper, ImpactPistol/Shotgun/Sniper, Impact, Arc, CoreRay, Relocation. У каждого есть tint/примесь цвета/HDR/alpha/множитель duration/width. Структурные настройки, размер swept hit shape, урон, cadence, радиусы поражения и progression исключены.

## Известные границы покрытия

- Original содержит 8 004 конечных значения, 453 идентичности материалов и происхождение из 156 Git blobs. Это Git-снимок, а не ранний QA-кадр «before», уже содержавший незакоммиченные изменения. Старые mine-map/bunker/lab сцены и два неразрешённых старых turret-location пути не входят в snapshot; подробности находятся в Notes JSON.
- Изменения C#-структуры, генерации геометрии, состава prefab и поведения AI не восстанавливаются numeric slider. Для точного сравнения всего старого поведения нужен отдельный checkout/build указанного commit с изолированным сохранением. Текущий Editor и рабочее дерево автоматически не откатываются.
- Смена texture, изменение shape/bursts/sub-emitters и редактирование Animator не подключены. Специализированные custom shaders с другим lighting/alpha-контрактом показывают доступные shader-поля и причину отсутствия независимой коррекции пикселей.
- Безопасный изолированный replay VFX не подключён: перезапуск gameplay-объекта может нанести урон/запустить callbacks. Соответствующей фиктивной кнопки нет.
- Масштаб доступен только отделённым visual leaf-компонентам без физики/скриптов; ORBITAL-модули исключены, поскольку sprite geometry участвует в попаданиях. Радиусы предупреждений, точки линий и root transforms не регулируются.
- Реестр обновляется при композиции сцены, спавне врагов, pool creation/reuse, создании предупреждений/взрывов/декора и ORBITAL-компонентов. Для прочих сторонних/procedural объектов есть явное «Обновить»; полного поиска сцены каждый кадр нет.
- Полное покрытие всех визуальных систем не заявляется. В частности, legacy FEEL/Visual Lab остаются отдельными инструментами; изменение одного и того же presentation-параметра одновременно из двух лабораторий не является поддерживаемым workflow.
