# ORBITAL COMBAT LAB

Изолированный gameplay-прототип телeкинетической орбитальной станции.

- Сцена: `Assets/_Project/Prototype/OrbitalCombatLab/OrbitalCombatLab.unity`
- F1: открыть/скрыть русскоязычное debug-меню.
- WASD: двигать игрока и весь центр орбитальной системы.
- ЛКМ: перетащить Gun/Blade/Pusher на зелёную подсвеченную орбиту.
- Esc или ПКМ: отменить drag.
- `RING EDIT MODE`: клик по линии выбирает кольцо, колесо меняет скорость,
  `Q/E` меняют постоянный `Ring Phase Offset`, `R` разворачивает направление,
  `Space` ставит кольцо на паузу. Короткий `Q/E` даёт шаг 15°, удержание после
  0.3 с — плавное движение; `Shift` включает точный шаг 3°/медленный hold,
  `Ctrl` — шаг 45°/быстрый hold. Те же операции доступны slider'ом, кнопками
  ±15°, reset и align в меню. По умолчанию выбранное кольцо временно перестаёт
  вращаться только на время редактирования.

## Mini Weapons iteration

По умолчанию Gun/Blade/Pusher используют production miniWeapons только как
дочерние визуалы через Lab-only wrappers в `Visuals/Resources/OrbitalCombatLab`:

- Gun → `p_miniWeaponPistol1`: +X forward, штатный `FirePoint1`, recoil Animator
  и muzzle particles запускаются событием Lab-выстрела;
- Blade → `p_miniWeaponLaserSward1`: локальная ось лезвия +Y, по умолчанию
  TANGENTIAL; production collider отключён на runtime-инстансе, contact damage
  остаётся в Lab;
- Pusher → `p_miniWeaponImpulseGun1`: локальные particles запускаются только
  при Lab pulse, а точный радиус всё ещё показывает pooled Lab shockwave;
- Link Node остаётся простым пульсирующим magenta core с исходными динамическими
  PAIRS / CHAIN / ALL NEARBY линиями.

`ВИЗУАЛ ОРУЖИЯ` переключает `PRIMITIVES` / `MINI WEAPONS` в runtime без смены
mount, cooldown или combat settings. Отдельные sliders управляют scale, rotation
offset, sorting и интенсивностью эффектов. Production colliders/Rigidbody и
небезопасные runtime behaviours отключаются только на созданных Lab-инстансах;
исходные prefab не изменяются. Спрайты инстансов используют локальный unlit
материал Lab. Персонаж и враги, напротив, сохраняют production Lit-материалы:
Lab теперь всегда создаёт собственный белый `Global Light 2D`, поэтому они не
становятся чёрными при запуске изолированной сцены.

## Actor visuals

- маркер игрока заменён на `p_Player1 Variant`, используемый только как
  анимированный дочерний визуал;
- красные шары заменены на один production-тип зомби `p_Enemy_default`;
- movement, damage, health, pooling и respawn по-прежнему принадлежат Lab;
  production MonoBehaviour, Collider2D, Rigidbody2D, audio и particles на
  runtime-инстансах отключены до их активации;
- каждый враг получает один простой Lab-owned `CircleCollider2D` и динамический
  `Rigidbody2D`, поэтому зомби физически body-block'ают друг друга;
- исходные player/enemy prefab не изменяются. Lab-only wrappers лежат рядом с
  wrappers оружия в `Visuals/Resources/OrbitalCombatLab`.

## Pattern Combat iteration

Общий toggle `PATTERN COMBAT` отключает все новые боевые реакции, оставляя
визуальные links и trails для прямого A/B-сравнения.

- `LINK NODE` создаёт PAIRS, CHAIN или ALL NEARBY связи. Проверка попаданий
  централизована и распределена по кадрам; используется общий cooldown цели.
- `ORBITAL ALIGNMENT RESONANCE` поддерживает RADIAL VOLLEY, BEAM, SHOCKWAVE
  и CYCLE, а также VISUAL ONLY.
- Movement presets: GEAR, FLOWER, WAVE, SYNC, CHAOS и обратимый FREEZE.
- Trails: OFF, SHORT, MEDIUM, HYPNOTIC.
- `TRAILS FOLLOW VISUAL PROFILE`: CLEAN/COMBAT → OFF, HYPNOTIC/MAXIMUM →
  HYPNOTIC. Основные боевые пресеты стартуют с OFF; PATTERN FLOWER использует
  короткий малопрозрачный след, обычный HYPNOSIS сохраняет длинный.
- Formations: DISTRIBUTE, CLUSTER, FRONT ARC, ALTERNATE и FREE MOUNT PHASE.
- Shapes: CIRCLE, ELLIPSE, BREATHING, WOBBLE.
- Ring fields: GHOST, SLOW, PULSE, CUT, CONDUCTOR.
- Comparison presets: PATTERN FLOWER, COMBAT WEB, ORBITAL FORTRESS,
  HYPNOSIS, DIRECTED FORTRESS, MINI WEAPONS START/FLOWER/FORTRESS и
  LINK HYPNOSIS.
- Меню Unity: `Tools > Prototype > Build Orbital Combat Lab` пересобирает сцену.
- Меню Unity: `Tools > Prototype > Build Orbital Mini Weapon Visuals`
  пересобирает только Lab wrappers из актуальных production prefab.
- Меню Unity: `Tools > Prototype > Build Orbital Actor Visuals`
  пересобирает Lab wrappers игрока и зомби.

Прототип не добавлен в основной run и не использует production progression,
инвентарь, экономику или каталоги контента. Вся орбитальная, target, projectile,
damage, push, drag, cooldown, pooling и stats логика остаётся собственной логикой
Lab; miniWeapons не получают вторую боевую ответственность.
