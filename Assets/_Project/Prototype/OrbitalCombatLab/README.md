# ORBITAL COMBAT LAB

Изолированный gameplay-прототип телeкинетической орбитальной станции.

- Сцена: `Assets/_Project/Prototype/OrbitalCombatLab/OrbitalCombatLab.unity`
- F1: открыть/скрыть русскоязычное debug-меню.
- WASD: двигать игрока и весь центр орбитальной системы.
- ЛКМ: перетащить Gun/Blade/Pusher на зелёную подсвеченную орбиту.
- Esc или ПКМ: отменить drag.
- `RING EDIT MODE`: клик по линии выбирает кольцо, колесо меняет скорость,
  `Q/E` двигают фазу, `R` разворачивает направление, `Space` ставит кольцо
  на паузу.

## Pattern Combat iteration

Общий toggle `PATTERN COMBAT` отключает все новые боевые реакции, оставляя
визуальные links и trails для прямого A/B-сравнения.

- `LINK NODE` создаёт PAIRS, CHAIN или ALL NEARBY связи. Проверка попаданий
  централизована и распределена по кадрам; используется общий cooldown цели.
- `ORBITAL ALIGNMENT RESONANCE` поддерживает RADIAL VOLLEY, BEAM, SHOCKWAVE
  и CYCLE, а также VISUAL ONLY.
- Movement presets: GEAR, FLOWER, WAVE, SYNC, CHAOS и обратимый FREEZE.
- Trails: OFF, SHORT, MEDIUM, HYPNOTIC.
- Formations: DISTRIBUTE, CLUSTER, FRONT ARC, ALTERNATE и FREE MOUNT PHASE.
- Shapes: CIRCLE, ELLIPSE, BREATHING, WOBBLE.
- Ring fields: GHOST, SLOW, PULSE, CUT, CONDUCTOR.
- Comparison presets: PATTERN FLOWER, COMBAT WEB, ORBITAL FORTRESS,
  HYPNOSIS и DIRECTED FORTRESS.
- Меню Unity: `Tools > Prototype > Build Orbital Combat Lab` пересобирает сцену.

Прототип не добавлен в основной run и не использует production progression,
инвентарь, экономику или каталоги контента. Визуалы создаются один раз из
простых runtime-примитивов; толпа, projectiles и impact pulses используют pool.
