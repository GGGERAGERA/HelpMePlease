# Структура C#-скриптов

Структура применена внутри `Assets/_Project/scripts`. Перемещены только 151 `.cs` и их 151 `.meta`; GUID сохранены, namespaces не добавлялись, сцены, prefab и ScriptableObject не перемещались.

```text
scripts/
├── Bootstrap/
├── Core/
├── Run/
│   ├── Flow/
│   ├── State/
│   ├── Results/
│   ├── Rewards/
│   └── Stats/
├── Bunker/
│   ├── Interaction/
│   │   └── Events/
│   ├── UI/
│   └── MetaProgression/
├── Selection/
│   ├── Characters/
│   ├── Weapons/
│   └── Levels/
├── Combat/
│   ├── Player/
│   ├── Enemies/
│   ├── Weapons/
│   ├── Damage/
│   └── Effects/
├── Progression/
│   ├── Experience/
│   ├── RunUpgrades/
│   ├── Unlocks/
│   └── MetaUpgrades/
├── World/
│   ├── Weather/
│   ├── Events/
│   └── Spawning/
├── UI/
│   ├── Common/
│   ├── HUD/
│   ├── Pause/
│   └── Notifications/
├── Audio/
├── Data/
├── Debug/
└── Legacy/
```

## Назначение

- `Bootstrap` — входные persistent-компоненты, создающие контекст до запуска MVP. Сейчас здесь `RunSelectionManager`; `RunStateManager` оставлен в `Run/State`, поскольку это состояние, а не scene bootstrap.
- `Core` — место для действительно общих механизмов без принадлежности к gameplay-модулю. На текущем этапе пусто намеренно: классы получили более узкую ответственность.
- `Run/Flow` — запуск/завершение этапов забега, boss flow, таймер, game over и очистка сцены.
- `Run/State` — persistent runtime-состояние забега и номер endless-уровня.
- `Run/Results` — DTO итогов и причина завершения.
- `Run/Rewards` — чистый расчёт награды.
- `Run/Stats` — убийства и время одной сцены, которые коммитятся в persistent state.
- `Bunker/Interaction` — bunker context, станции, курсор, collider/hover, общие interactable и контент комнаты.
- `Bunker/Interaction/Events` — события/цели внутри бункера.
- `Bunker/UI` — панели, уведомления, summary и bunker view-компоненты.
- `Bunker/MetaProgression` — bunker shop/content data и переход из бункера в run. Название означает bunker-side orchestration; общие meta-сервисы находятся в `Progression/MetaUpgrades`.
- `Selection/Characters` — выбор и карточка персонажа.
- `Selection/Weapons` — выбор и карточка оружия.
- `Selection/Levels` — выбор следующей endless-карты и её карточки.
- `Combat/Player` — spawn игрока, здоровье, движение, pickup radius и runtime combat modifiers.
- `Combat/Enemies` — здоровье, identity, движение, projectile/collision и hit FX врагов.
- `Combat/Weapons` — оружие, projectiles, shot patterns, fire contexts и weapon FX.
- `Combat/Damage` — зарезервировано для общей модели damage, когда появится реальная общая ответственность. Новая абстракция не создавалась.
- `Combat/Effects` — explosion/burst/nuke/shockwave runtime-эффекты.
- `Progression/Experience` — XP manager и pickup.
- `Progression/RunUpgrades` — roll, выбор и применение upgrade текущего забега.
- `Progression/Unlocks` — registry, conditions, persistent unlock service.
- `Progression/MetaUpgrades` — валюта, уровни meta-upgrade, применение к новому игроку и UI summary.
- `World/Weather` — применение выбранной погоды и endless scaling, weather enum.
- `World/Events` — runtime world events и spawner событий.
- `World/Spawning` — основной enemy spawner.
- `UI/Common` — переиспользуемые view/animation/menu-компоненты без доменной мутации состояния.
- `UI/HUD` — HUD, camera feedback, health bars, popup и crosshair.
- `UI/Pause` — пауза и выход через центральный run-end flow.
- `UI/Notifications` — run message data/service/view и общая notification view.
- `Audio` — music и UI sound helpers.
- `Data` — все основные ScriptableObject-типы и level node data; сами `.asset` остаются на прежних местах.
- `Debug` — gold/unlock debug actions. В release build доступность должна быть проверена.
- `Legacy` — только подтверждённые остатки старого menu/portal presentation-кода. Они не удалены; старые portal prefab требуют отдельного решения.

## Правила зависимостей

1. UI вызывает controller/service и не начисляет награду напрямую.
2. `Run/Flow` координирует модули, но данные забега меняет через `RunStateManager`.
3. `Run/State` не хранит scene objects; только data assets и числовые snapshots.
4. `Combat` сообщает факты (death/kill/XP), а `Progression` решает unlock/upgrade.
5. `Data` не зависит от MonoBehaviour.
6. `Legacy` нельзя возвращать в active flow без проверки missing scripts и обхода `RunEndService`.

## Примечание по Unity

Unity может оставить старые пустые каталоги и их folder `.meta` до refresh/import. В них нет C#-файлов; это не дубликаты. Не удалять старые каталоги из Project window, пока Unity не завершит import и Console не подтвердит отсутствие ошибок.
