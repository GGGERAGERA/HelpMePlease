# First production SFX pass — проверка 12 сентября 2026

## Результаты

- **Golden Path 5×: PASS.** Seed 48151623, batch `3953989d30f2443fa5b0a6e938f4d91f`. Три сектора, 9 наград, один boss spawn, одна победа, чистое возвращение в бункер и корректный старт следующего run. Batch занял около 58 секунд реального времени. Доказательство: `golden-final.json`; testcase `GoldenPathAtFive` в `runtime-first-results.xml` прошёл (весь тот ранний suite был красным из-за ещё незавершённого smoke fixture).
- **Combat smoke 1×: PASS**, последний запуск около 23 секунд вместе с переходами сцен. `smoke-final-results.xml`. Реальные production MainMenu/MVP, четыре типа ORBITAL оружия, 30 врагов, 240 запросов массовых попаданий, групповое убийство 15 врагов, 15 одновременных XP pickups. Затем отдельные фазы настоящего reward flow/ModuleFlight, compression, core и lifecycle. Это управляемая runtime-проверка; настройки здоровья, состав оружия и принудительная готовность core менялись только в тестовом fixture.
- **Инфраструктура: 4/4 PASS**, `infrastructure-final-results.xml`: настоящий прямой Editor boot MVP и переход StartScreen → MainMenu → MVP; единственный каталог с исходным GUID; audio variation не изменяет Unity gameplay RNG; насыщение пула/PlayerHurt/pause/inactive owner.
- Компиляция Unity прошла; `git diff --check` без ошибок. Предупреждения Git о LF/CRLF не являются ошибками Unity.

## Что измерено

Пул фиксирован: **20 SFX/UI + 2 music + 1 ambience**. В тесте насыщения 20 занятых голосов блокируют ещё один обычный cue, но пропускают PlayerHurt через вытеснение менее важного. Source count остаётся 23.

Golden Path: максимум 10 одновременно играющих pooled voices, 11 играющих источников во всей сцене. Последний smoke: 9 и 10 соответственно. Максимум всех AudioSource в загруженных сценах — 42 (включая существующие неиграющие сценовые/UI источники); это не 42 выделенных на попадания голоса. Максимумы sampled каждые 100 ms; жёсткий предел дополнительно проверен отдельным saturation test.

Состав и instance IDs источников AudioService не меняются от 240 запросов попаданий; на 30 EnemyHealth не появляется ни одного AudioSource. Из 15 одновременных XP Collect допускается не больше одного XP cue. Хиты, смерти и XP намеренно агрегируются cooldown/cap, поэтому количество звуков не равно количеству событий.

Последний smoke зарегистрировал принятые к playback cues: Pistol 12, hit 34, critical 1, death 1, hurt 1, sword 21, impulse 6, arc 6, XP 2, LevelUp 1, RewardSelect 1, install 1, Pulse 1, Cascade 3. Compression 6 и release 2 включают отдельные lifecycle-сценарии. Проверка одного непрерывного hold из 60 шагов не перезапускает loop.

ModuleInstall отсутствует при выборе карточки и срабатывает после FinishFlight. Loop прекращается на release, pause, end run, смерти и загрузке сцены; проверка inactive owner выполнена отдельно. Pulse/Cascade срабатывают на WaveStarted, не на каждый ring/target.

## Warnings и ограничения

В финальном smoke warning/error telemetry пустая. В Golden Path остались известные неаудио сообщения: ParticleSystem duration assert в WorldRuleVisual.EnsureWindResources (узко ожидается существующим Golden Path fixture), отсутствующий Bool IsRunning у Boss2_0 и повторная регистрация завершения Sector 3. Полные сообщения: `golden-warnings.txt`. Эти системы не менялись.

Ранние запуски тестов были красными: сначала выявлены отсутствие Resources-каталога, отсутствие приоритетного вытеснения и вмешательство audio random в gameplay RNG; затем исправлялись сам runtime fixture/EnterPlayMode domain reload и пауза от лишнего LevelUp. Красные артефакты сохранены отдельно; актуальные результаты перечислены выше.

Проверка подтверждает принятие cues, длительности, уровни отдельных файлов, лимиты и lifecycle. Субъективное прослушивание суммарного микса и замер его пиков не выполнялись: нельзя считать доказанными отсутствие клиппинга или идеальную слышимость PlayerHurt. Его приоритет и запас относительно тихих hit/XP подтверждены технически. Compression MP3 остаётся функциональной заменой; бесшовность loop не полировалась.

## Изменённые production files

- `Assets/_Project/Resources/Audio/VerticalSliceAudioCatalog.asset` и `.meta` — перенос из `Scriptable Objects`, сохранён GUID `6ee2dbe59cb5456f944b59a72f4a0c31`, новые cue settings.
- `Assets/_Project/scripts/Audio/AudioCueId.cs` — IDs.
- `Assets/_Project/scripts/Audio/AudioCatalog.cs` — priority и отдельный audio RNG.
- `Assets/_Project/scripts/Audio/AudioService.cs` — bounded priority reuse, loop lifecycle/intensity, development telemetry event.
- `Assets/_Project/scripts/Combat/Enemies/EnemyHealth.cs` — pooled hit/critical, удалено создание отдельного hit AudioSource.
- `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalModuleRuntime.cs` — Pistol/Sword/Impulse/Arc hooks.
- `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalStationRuntime.cs` — compression/release/core hooks.
- `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalRewardFlowController.cs` — FinishFlight install.
- `Assets/_Project/scripts/Progression/RunUpgrades/UpgradeManager.cs` — reward selection и однократный LevelUp.
- `Assets/_Project/scripts/Run/Flow/RunFlowController.cs` — остановка managed loops при завершении gameplay.

Добавлены два Editor test файла с meta: `Subject42ProductionAudioTests.cs`, `Subject42AudioRuntimeTests.cs`. MainMenu.unity и TMP fallback уже были изменены до этого прохода; изменения сохранены. Аудиофайлы не создавались, не редактировались и не удалялись. Gameplay balance, progression и визуалы не изменялись.
