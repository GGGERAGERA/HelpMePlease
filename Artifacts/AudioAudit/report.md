# Subject#42 — практический аудит аудио, 12 сентября 2026

**Главный вывод:** инфраструктура уже есть, но production ORBITAL почти полностью немой. Готовый PistolShot относится к старому ProjectileWeapon, а не к OrbitalPistolModule. Добавление файлов без подключения актуальных событий этого не исправит.

Проверены текущие Assets, GUID-ссылки, enabled Build Settings: StartScreen → MainMenu → MVP. Текущий RunRoute — три сектора, затем финальный босс внутри последнего сектора; старое описание маршрута 4+1 не использовалось как доказательство.

Выполнен один реальный существующий Golden Path: seed 48151623, 5×, PASS, 3 сектора, 1 boss spawn, boss kill, 1 victory, возврат, повторный старт и cleanup. Длительность теста около 62 секунд. Аудионаблюдатель опрашивал источники каждые 100 мс; фиксировал только музыку. Короткий звук между опросами мог быть пропущен. Бот не проверяет ручной hover, все пять сборок оружия, все варианты core и все окна. Для них выводы основаны на текущем коде и prefab wiring. Прослушивание тембра, воспринимаемой громкости и clipping на 1× не выполнено; «сомнительно» ниже означает конкретную проблему подключения/контекста, а не субъективную оценку записи.

## A. Что есть

- **490 клипов:** 59 WAV, 431 MP3; OGG, tracker и других AudioImporter-клипов не найдено. Полный список и все места ссылок: `inventory.md`, машинный вариант `inventory.json`.
- Единственный AudioCatalog содержит 19 ID: 6 с клипами, 13 пустых, включая зарезервированный удалённый RocketLaunch. Каталог: `Assets/_Project/Scriptable Objects/VerticalSliceAudioCatalog.asset`.
- **AudioService:** DDOL singleton, фиксированный общий пул 20 SFX/UI, два музыкальных источника для crossfade 0.75 с и один ambience. Cue cooldown, maxSimultaneous, pitch variation, позиционирование и API managed loops уже реализованы. Активных вызовов StartLoop вне самого сервиса не найдено.
- **AudioSceneDirector:** MainMenu выбирает один из пяти bunker tracks, MVP играет Blue Ruin Path. Это подтверждено runtime. На боссе продолжает играть та же музыка. Между секторами cue не перезапускается, пока играет текущий RunMusic.
- **Микшер:** `Resources/Audio/VerticalSliceAudioMixer.mixer`, Master → Music → Ambience и Master → SFX → UI. Все четыре группы существуют. Настройки пользователя — Master/Music/Sounds; Sounds также меняет UI, Music также меняет Ambience. Отдельных сохранённых UI/Ambience sliders нет.
- **UIHover:** Button_Hover.wav, в том числе PF_UpgradeCard → MVPUpgradePanel, PausePanel и части bunker/settings buttons. **UIConfirm:** Button_confirm.wav, выбор персонажа, часть bunker selections, подтверждение sector/world-rule выбора. Это не глобальная озвучка каждого Button.
- **Boundary warning:** error_006.mp3 подключён к PlayerBoundaryHazard, используется при выходе за границу.
- **Bunker intro:** glassCrack, systemError, metalDrop подключены к трём отдельным intro AudioSource; humanRecording, radioNoise и alarmPulse пусты. Эти звуки условны: играют только при соответствующем шаге intro, а не во время обычного посещения Bunker. В данном bot pass intro не прослушивалось.
- **Старые пути:** ProjectileWeapon → PistolShot, LaserWeapon → LaserAudioController → LaserShot существуют. Их нельзя засчитывать как звук новых ORBITAL-модулей. MusicPlayer присутствует в MVP, но его Start прекращает автоматическое воспроизведение при существующем AudioService. UISoundPlayer есть как код; сериализованных экземпляров в сценах/prefabs не найдено.

В Assets всего 204 сериализованных AudioSource, большая часть — сторонние FX prefabs. В статическом графе production build-scene dependencies: MainMenu 3, MVP 1, p_Player1 1, p_LaserWeapon 1; граф не включает автоматически все Resources. Runtime дополнительно создаёт пул и подгружает authored prefabs. Максимум в проходе — **42 источника, включая неактивные**, и **2 играющих**, во время музыкального crossfade. Это наблюдение одного маршрута, не нагрузочный предел.

## B. Покрытие важных событий

Обозначения: **есть** — вызов и клип подключены; **пусто** — событие вызывается, но клип не задан; **нет** — отдельного аудиовызова в текущем пути нет; **частично** — ограниченное покрытие. Общая музыка не считается SFX события.

| Событие | Статус / конкретное основание |
|---|---|
| ORBITAL Pistol | **Нет.** OrbitalPistolModule → ProductionOrbitalCombatAdapter.SpawnProjectile; AudioService не вызывается. Существующий PistolShot можно переиспользовать. |
| Laser Sword | **Нет.** OrbitalLaserSwordModule напрямую наносит урон; ни атаки, ни контакта клинка. |
| Impulse Gun | **Нет.** Урон/импульс/визуальная реакция без SFX. |
| Arc Emitter | **Нет.** Цепочка FlashLink + ApplyDamage без SFX. |
| Link Node | **Нет.** Работа связей и передача энергии без SFX; сам модуль пассивный, звук нужен при активации связи, не каждый Tick. |
| Core Pulse / Cascade / другие core-effects | **Нет.** OnCoreWave/OnCoreRing, волны и искры без аудио; нет отдельной озвучки Repulse. |
| Установка на mount | **Нет.** InstallModule/SyncCommitted/ModuleFlight без звукового подтверждения. |
| Выбор mount/ring | **Нет.** Arena selection визуальный, общий UIHover на него автоматически не распространяется. |
| Добавление ring/mount | **Нет.** Изменение состояния и presentation без SFX. |
| Вращение / движение ORBITAL | **Нет.** Непрерывного loop или дискретного feedback нет. Не предлагаю озвучивать каждый кадр. |
| Сжатие | **Нет.** UpdateCompression без аудио. |
| Release / overshoot | **Нет.** Есть release-анимация с overshoot 1.15, SFX отсутствует. |
| Смена направления | **Нет.** ReverseRotation реализован, доступен переключением режима через DebugMenu; production default — CompressRings. |
| Выстрел в текущем ORBITAL combat | **Нет**, несмотря на старые Pistol/Laser cues. |
| Попадание / critical | **Пусто.** EnemyHealth.hitSound и critSound не назначены у Enemy/Boss prefabs; наследуемые варианты не добавляют аудиоклипов. |
| Смерть обычного врага | **Пусто.** CommonEnemyDeath вызывается, clips=[] . |
| Смерть крупного / элитного | **Пусто.** Не-boss использует тот же пустой CommonEnemyDeath; отдельного тяжёлого death cue нет. |
| Урон игроку | **Пусто.** PlayerHurt clips=[] . Старый GunShoot в PlayerHitSound не спасает: fallback вызывается только при отсутствии AudioService. |
| Смерть игрока | **Пусто.** PlayerDeath clips=[] . Код проверен, текущий успешный проход не проигрывал смерть игрока. |
| Обычный enemy spawn | **Нет.** Не считаю обязательным: озвучивать каждого рядового при массовом спавне не нужно. |
| Появление босса / начало final encounter | **Пусто.** FinalBossRoutine вызывает BossSpawn, clips=[] . |
| Атаки босса | **Нет отдельной озвучки.** Самостоятельного boss attack cue нет. |
| Смерть босса | **Пусто.** BossDeath clips=[] . |
| XP pickup | **Пусто.** XPPickup clips=[]; pickupCoin.wav записан в ExperiencePickup.pickupSound, но поле не читается при pickup. |
| Level Up | **Пусто.** UpgradeManager.ShowChoiceRequest → LevelUp. ExperienceManager вызывает его самостоятельно только без UpgradeManager; явного двойного вызова тут нет. |
| Появление reward cards | **Нет отдельного cue.** LevelUp тоже пока пустой; chest/reel optional clips пусты. |
| Hover reward card | **Есть:** PF_UpgradeCard имеет UIButtonHoverSound, получает общий UIHover даже с hoverSound=null. Бот не эмулирует pointer hover. |
| Выбор reward card | **Нет.** UpgradeCardView/UpgradeManager.SelectUpgrade не играют UIConfirm. Подтверждение карты сектора — другой путь и озвучено. |
| ModuleFlight / установка награды | **Нет** звука полёта и фиксации. |
| Золото | **Нет.** GoldenCoinPickup / gold reward без pickup SFX; Purchase в bunker — отдельный пустой cue. |
| Breakable hit / destruction | **Нет.** WorldBreakable делает визуальную реакцию, particles и loot, без AudioService. |
| Crate physics/contact | **Нет** отдельной контактной озвучки. |
| Event reward container | **Пусто/нет.** WorldLootRewardReel имеет reelStart/cardTick/stop/reward, но все clips и optional AudioSource пусты. |
| Ворота | **Нет** открытия/закрытия в BunkerGateVisual. |
| Вход/выход сектора | **Нет отдельного cue.** SceneTransitionOverlay приглушает Master; музыка продолжается. |
| Bunker/UI buttons / hover | **Частично.** Hover на перечисленных prefabs; общего click-handler со звуком для всех Button нет. |
| Подтверждение | **Частично:** character/часть bunker/sector confirm есть; Purchase/PurchaseFail пусты. |
| Открытие/закрытие окон | **Нет в текущем BunkerPanelManager.** Старый UISoundPlayer.PlayPanelSwitch не подключён. |
| Запуск run | **Пусто.** BunkerRunStarter вызывает StartRun, clips=[] . |
| Возвращение после run | **Есть смена на BunkerMusic**, отдельного arrival/result SFX нет. |

| Место | Music | Ambience |
|---|---|---|
| Bunker | Работает, 5 кандидатов в каталоге, случайный один на вход | BunkerAmbience пуст; intro radio noise также пуст |
| Surface/combat sectors | Один RunMusic, Blue Ruin Path | AudioSceneDirector явно StopAmbience |
| Boss encounter | Продолжается RunMusic; отдельного перехода нет | Отдельного ambience нет |

**Сомнительное/слабое по реализации:** старые оружейные cues не доходят до ORBITAL; игрок имеет legacy hit clip, который маскируется пустым новым cue; карточки дают hover, но не дают commit; elite не имеет собственной death-категории; boss не меняет музыкальный контекст. Субъективно оценивать звучание самих файлов без прослушивания нельзя.

## C. Файлы без использования

**435 без сериализованных ссылок во всём Assets**, из них 423 внутри _Project и 12 сторонних Epic Toon FX. Поиск runtime audio APIs не выявил загрузки этих файлов по строковому имени. Это список кандидатов, не команда на удаление. Полный перечень: `unreferenced.txt`.

- В корне AudioEffects: crit.mp3, dragon-studio-zombie-scream-324752.mp3, Laser_bullet.mp3, random (1).wav, random.wav, Sword_Laser.mp3, z_uk-vystrel-s-pistoleta (mp3cut.net).wav, Зомби, получение урона.mp3.
- SFX/Game/Зомби_SFX.mp3 и SFX/UI/Menu/Button_click.mp3.
- Все 3 BossTheme(пока рано): Cryo Mutant Siege, Red Zone Requiem, Red Zone Requiem_2.
- Все 7 new, в том числе Level_up_test.mp3, и 2 troll.
- Неиспользуемые Kenney: impact-sounds 128, interface-sounds 98, rpg-audio 51, sci-fi-sounds 73, ui-audio 51.

**Дополнительно 39 клипов имеют ссылки только в сторонних FX/demo assets**, не входят в найденный граф production и не наблюдались в Golden Path. Не смешивать их с 435 полностью без ссылок.

**Назначены, но не работают в текущем Golden Path:** Pistol_shot_SFX.mp3 / Laser_Shot_SFX.wav относятся к старому оружию; pickupCoin.wav — неиспользуемое поле XP; GunShoot.wav — legacy player-hit fallback, подавленный наличием сервиса. 16 аудиофайлов в статическом графе зависимостей build scenes не означают 16 реально играющих файлов.

## D–E. Первые 15 добавлений и конкретные файлы

Это 15 приоритетных событий/небольших семейств. Имена ниже — требуемые deliverables, а не утверждение, что файл уже существует. Сначала проверить пригодность имеющихся записей: Pistol_shot_SFX, Sword_Laser, crit, zombie sounds, Level_up_test и Kenney; генерировать только недостающие подходящие варианты. Новые записи не создавались в ходе аудита.

| № | Максимальный практический эффект | Найти/подготовить файл |
|---|---|---|
| 1 | Вернуть базовому ORBITAL Pistol факт выстрела | Подключить существующий Pistol_shot_SFX.mp3; новый файл пока не нужен |
| 2 | Сделать урон врагу читаемым | sfx_enemy_hit_01–03.wav — короткий мягкий impact, без длинного хвоста |
| 3 | Предупредить об уроне игроку | sfx_player_hurt_01–02.wav — отчётливый, отличающийся от enemy hit |
| 4 | Зафиксировать поражение | sfx_player_death.wav — одно законченное событие |
| 5 | Дать вес обычной смерти | sfx_enemy_death_01–03.wav; затем sfx_elite_death.wav |
| 6 | Отличить Laser Sword | sfx_sword_contact_01–02.wav — энергетический slash/contact |
| 7 | Отличить Impulse Gun | sfx_impulse_fire.wav — короткий низкий толчок |
| 8 | Отличить электричество и связь | sfx_arc_discharge_01–02.wav; sfx_link_transfer.wav — один accent на activation, не на каждый сегмент |
| 9 | Сделать core событием | sfx_core_pulse.wav; sfx_core_cascade.wav — волна/усиленная развязка |
| 10 | Дать тактильность RMB | sfx_orbit_compress.wav; sfx_orbit_release.wav — натяжение и короткий release/overshoot |
| 11 | Подтвердить сборку | sfx_module_install.wav — latch/click; можно пока делить с add mount/ring |
| 12 | Сделать XP ощутимым | sfx_xp_pickup_01–03.wav — очень короткий тихий tick; сначала проверить pickupCoin.wav |
| 13 | Завершить цикл награды | sfx_level_up.wav; sfx_reward_select.wav — chime и commit; hover уже есть |
| 14 | Выделить босса | sfx_boss_intro.wav, sfx_boss_attack_warning.wav, sfx_boss_death.wav — читаемый сигнал, телеграф, завершение |
| 15 | Дать вес миру | sfx_crate_hit_01–03.wav, sfx_crate_break.wav — общая группа для breakables/crates |

Следующий небольшой пакет после этих приоритетов: sfx_mount_select.wav, sfx_reward_reveal.wav, sfx_module_flight.wav, sfx_gold_pickup.wav, sfx_reward_container_open.wav, sfx_gate_open.wav / close.wav, sfx_sector_transition.wav, sfx_run_start.wav / return.wav, sfx_ui_open.wav / close.wav. Для card/select/button confirm сначала можно переиспользовать Button_confirm.wav, не заказывать десяток почти одинаковых файлов.

Ambience отдельно: amb_bunker_loop.wav (вентиляция/электрика) и amb_surface_loop.wav (ветер/далёкое окружение), бесшовные, без постоянных ярких транзиентов. Для boss music сначала прослушать три уже лежащих boss tracks и выбрать один; новая композиция пока не обязательна. Постоянный звук вращения и рядовых spawn оставил бы низким приоритетом.

## F. Что действительно исправить перед подключением

1. **Не направлять новые массовые hits через EnemyHealth.PlayHitSound или PlayExternalOneShot.** Первый создаёт/использует AudioSource на каждом враге при назначенном hitSound и делает неограниченный PlayOneShot на попаданиях. Второй создаёт GameObject+AudioSource на каждый вызов, вообще вне 20-slot пула, и удаляет через Destroy(delay). Задержка удаления зависит от scaled time, а воспроизведение — нет: при pause объекты могут переживать клип. Сейчас hitSound/critSound пусты. У crit есть общий static throttle 0.08 scaled секунд, но у external API общего cap нет. Подключить частые события через существующий пул + per-cue cooldown/maxSimultaneous; сложный новый менеджер не нужен.
2. **Починить один источник истины каталога для direct MVP.** AudioService ищет Resources/Audio/VerticalSliceAudioCatalog, но каталог лежит в Scriptable Objects. Нормальный путь получает его из MainMenu через AdoptCatalogIfMissing — это зафиксировано в runtime. При чистом прямом запуске MVP этот путь отсутствует, сервис есть, каталог null, вызовы молча возвращают false. Сделать доступным тот же каталог из bootstrap/Resources, без копий с расходящимися настройками.
3. **Подключать новые ORBITAL-события именно в OrbitalModuleRuntime / StationRuntime / RewardFlow.** Заполнение старого WeaponData.attackSound или PistolShot само по себе не подключает ORBITAL. PlayerHealth также не использует legacy fallback при failed Play; для рабочего пути проще заполнить PlayerHurt.

Необязательное небольшое улучшение после появления SFX: общий пул UI/SFX не резервирует место для player hurt/death/confirm. При заполненных 20 слотах новое событие просто пропускается; достаточно небольшого резерва/приоритета, если подтвердится на плотной сцене. Не нужен рефакторинг всей системы.

**Громкость:** ограничения пула сдерживают число голосов, но не гарантируют отсутствие clipping. В микшере только Attenuation, без limiter/compressor; Unity настроена на 32 real / 512 virtual voices. Большая доля cues 2D/почти 2D, поэтому удалённые события могут суммироваться. После подключения сделать один короткий тест плотного боя на 1× и выставить headroom; нынешний проход с одной музыкой не доказывает безопасность будущего полного микса.

## Доказательства и состояние workspace

- `golden-path-results.xml`: 1 test, Passed, failed=0.
- `golden-path.json`: GoldenPathPassed=1, boss spawn=1, victory=1, AssertionsFailed=0.
- `runtime.txt`: фактически наблюдавшиеся клипы, mixer routing и sampled source maxima.
- `inventory.md` / `inventory.json`: все 490 клипов и места ссылок; `unreferenced.txt`: 435 без ссылок.
- `audio-sources.json`: сериализованные AudioSource; `scan.py`: воспроизводимый статический обход. Сторонние demo assets учитываются в инвентаризации, но не в выводах о production.
- `Subject42AudioAuditProbe.cs.txt`: копия временного наблюдателя и обёртки над существующим Golden Path. Исполняемый файл и .meta удалены из Editor после проверки. Production C# не редактировался, исправлений аудио не делалось.
- Во время работы Unity сохранила изменения MainMenu.unity (сериализованные debug references и baseMoveSpeed override) и TMP fallback font. Они не являются аудиоправками; оставлены без отката, поскольку могут включать ранее несохранённое состояние открытого Editor. Стандартный runner также обновил свои BotBatches/QA artifacts; копия результата этого аудита сохранена здесь.

Ключевые исходники: scripts/Audio/AudioService.cs, AudioSceneDirector.cs, AudioSettingsService.cs; scripts/Combat/OrbitalStation/OrbitalModuleRuntime.cs, OrbitalStationAdapters.cs, OrbitalStationRuntime.cs, OrbitalRewardFlowController.cs; scripts/Combat/Enemies/EnemyHealth.cs; scripts/Combat/Player/PlayerHealth.cs; scripts/Progression/Experience/ExperiencePickup.cs; scripts/Progression/RunUpgrades/UpgradeManager.cs; scripts/World/Loot/WorldLootRewardReel.cs. Все пути относятся к текущему Assets/_Project, не архивам.
