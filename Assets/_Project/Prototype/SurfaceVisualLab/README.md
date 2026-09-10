# Subject#42 — Surface Visual Lab / Cold Ash

Актуальное решение от 2026-09-10: Preset 4 — спокойный Cold Ash ground и редкий микро-декор. Крупные ёмкости, бетон, трубы, кабели и cyan stains из этого preset удалены. Ground и микро-декор перенесены в `Scenes/MainBuild/MVP.unity` обычным environment-prefab. Лаборатория остаётся отдельной сценой вне build.

## Управление

Открыть `SurfaceVisualLab.unity` непосредственно из Edit Mode и включить Play.

| Клавиши | Действие |
|---|---|
| 1–4 | Мгновенно переключить только окружение |
| F5 | Игрок один |
| F6 | Игрок + 24 неподвижных production-врага |
| F7 | 24 цели + настоящий ORBITAL, XP, ящики, FX |
| F8 | 42 врага с production AI, стрельбой и bomber-поведением |
| WASD / стрелки, Space | Production movement / dash |
| R | Восстановить игрока, уничтоженные цели, XP и ящики |
| H | Скрыть подсказки |

У игрока отключён входящий урон, здоровье целей повышено для сравнения. В F6/F7 Rigidbody целей фиксирован, F8 — живой бой. Сброс пересоздаёт enemy-prefab, отменяя в том числе отложенный взрыв bomber. Сцена не заменяет существующий активный run: запускать из Edit Mode.

Используются реальные `Player_0_p_Player3` через `OrbitalPresentationConfig` для Gera, новые visual roots `p_Enemy_classic/default/Shooter/Bomber`, `p_Hex1`, `p_Case1`, blood/death FX из `EnemyHealth`. ORBITAL штатным Mid preset создаёт четыре кольца и восемь модулей. Старую standalone-симуляцию OrbitalCombatLab заменяют текущая production-система и её тесты; при cleanup она удалена.

## Presets

| Preset | Ground | Назначение |
|---|---|---|
| 1 Quiet Wasteland | #353D44 | Исторический контрольный вариант с редкими камнями/сухими кустами |
| 2 Biopunk Wasteland | #333B3F | Сравнение с лабораторными props и слабым cyan-остатком |
| 3 Ruined Facility Outskirts | #3B3F42 | Сравнение с промышленными крупными формами |
| 4 Cold Ash | #363C42 | Текущее направление: только ground и микро-декор |

Первые три варианта оставлены для A/B-сравнения, их крупный декор не переносится в MVP.

Cold Ash: четыре бесшовных 64×64 tile на сетке 2×2 units, 32 PPU, Point, без mipmaps/сжатия/коллайдеров. Редкие связные кластеры RGB −2/+3 дают фактуру без попиксельного шума. Микро-декор: ветка, тонкая трещина, два камня, две сухие травинки и одно слабое пятно; рисунки помещаются в 24×24 px, фактическая форма ещё меньше. Cyan нет. Позиции заранее расставлены с минимальным интервалом 9 units, без повторяемого мотива; радиус 6 units у старта свободен.

Одна Editor-функция `ColdAshSurfaceAuthoring.CreateField` авторит и lab 72×56, и production 144×144. В runtime — только сериализованный Tilemap и SpriteRenderer. Общие арты находятся в `art/Sprites/Environment/ColdAsh`, production-prefab — `art/Sprites/Environment/ColdAsh/ColdAshSurface.prefab`. MVP не зависит от Prototype-папки.

В production используется отдельный `ColdAshLit.mat` на уже существующем `Subject42/Environment Tile Lit`: brightness 2.5 компенсирует baseline global light 0.33. Темнота, свет игрока и anomaly-focus продолжают действовать. Lab использует нейтральный свет 1.0.

Lab camera: штатный URP Pixel Perfect Camera, 768×432, 32 PPU, Upscale Render Texture/Point/Windowbox, без MSAA/HDR/post. Production camera и игровые post-effects сохранены; текущие дробные zoom/scales персонажей и motion blur не становятся pixel-perfect автоматически от замены ground.

## Проверка

Меню `Tools → Subject42 → Surface Visual Lab` пересобирает lab и снимает 12 сравнений + живую толпу. Съёмка идёт из Play Mode gameplay camera, 768×432 → 1536×864 nearest 2×.

`ColdAshProductionTests` входит через настоящий бункер и MVP, проверяет движение, толпы, четыре кольца, реальные попадания, XP, ящик, event, переходы 1–3 и босса. Снимки сохраняются через `ScreenCapture.CaptureScreenshot` из production Game View с HUD. После тестового телепорта камера получает время на стабилизацию; момент боя ненадолго ставится на паузу для capture. Это не Scene View и не композит из спрайтов. Прогресс пользователя сохраняется/восстанавливается существующей test fixture.

Выходные файлы: `Artifacts/GeneratedQA/SurfaceVisualLab/4-solo.png`, `4-combat.png`, `4-dense-live.png`; настоящая MVP — `Artifacts/GeneratedQA/SurfaceVisualLab/Production/01-…08-*.png`.

## Компактные assets

Production Cold Ash — ровно четыре файла без `.meta` в `art/Sprites/Environment/ColdAsh`: `ColdAsh.png` (11 sliced Sprite regions), `ColdAshTiles.asset` (четыре стандартных Tile в одном файле), существующие `ColdAshLit.mat` и `ColdAshSurface.prefab`. Микро-декор ссылается непосредственно на sliced Sprite, дополнительных assets/prefab/config нет.

Presets 1–3 используют `Art/SurfaceComparisons.png` (20 regions) и `SurfaceComparisonsTiles.asset` (12 Tile). Ранее существовавшие 31 PNG и 16 отдельных Tile упакованы без перерисовки. Source RGBA и GPU-отрисовка всех 31 спрайта совпали пиксель-в-пиксель. PPU, pivot, filtering и размеры сохранены.

Authoring только читает существующие sheets/Tile/material; он больше не рисует и не сохраняет PNG/Tile. Для сохранения утверждённого распределения микро-декора одно слабое пятно дважды используется в восьмишаговом цикле; это один Sprite, не два ассета. GUID prefab при переносе сохранён. Стабильность Sprite/Tile fileID проверяется принудительным переимпортом.
