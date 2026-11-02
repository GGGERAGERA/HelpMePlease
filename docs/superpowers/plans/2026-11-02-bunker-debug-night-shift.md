# Bunker Debug — план технической смены

> Исполнение: `superpowers:executing-plans`, последовательно в текущей рабочей копии. Пользователь разрешил самостоятельные технические решения; запретил commits, staging, push и смену веток.

**Цель:** независимое developer-only окно MainMenu (F9), безопасное тестовое сохранение и короткий воспроизводимый путь проверки.

**Архитектура:** storage facade перед Unity PlayerPrefs с изолированным overlay для developer-сессии; реальные Currency/Meta/Station/Mission/Map/Bark API. Отдельные controller/view в Dev. Release перенаправляет storage прямо в Unity, developer-функции исключены компилятором. Без изменения сцен, баланса и принятого дизайна.

**Стек:** Unity 6000.3.13f1, C#, существующий TestRunnerApi / TutorialVerificationRunner.

**Спецификация:** пользовательское задание «SUBJECT#42 — Ночная техническая смена», 4–5 часов, строгий порядок приоритетов.

## Ограничения и источники

- Git исходно чист, ветка TestByDantes. Работать здесь, без операций с индексом/ветками.
- GDD Core прочитан. Связанные разделы Bible: §8.6 (существующая мета), §9.1–9.3 (production-бункер/реплики), §7 (награды); сверка `docs/gdd-reconciliation-2026-10-26.md` задаёт границы доказательств.
- Игровое поведение и решение игрока не меняются. Developer override действует только в тестовой сессии; новые игровые идеи в отчёте остаются гипотезами.
- Только адресные тесты, никакой категории Core/Golden Path/batch. Пользовательские ассеты и лабораторные системы сохранять.

## Review focus

1. Ни один debug setter и производное вознаграждение не должны писать основной профиль.
2. Перекомпиляция/Play Mode/переходы не должны незаметно возвращать запись в основной профиль.
3. Возврат к исходному тестовому состоянию должен обновлять runtime-кэши, карту, миссии и UI.
4. Ворота переключаются через BunkerGateVisual, без независимого collider hack; комнаты и станции — через свои API.
5. Release не содержит bootstrap/UI/override и по-прежнему компилируется без Dev.

## Задачи

- [x] Зафиксировать Git/замеры/исходные сохранения; проверить существующий короткий bunker smoke.
- [x] Написать и выполнить failing test на изоляцию ресурсов/произвольных ключей/удалений; реализовать storage и проверить восстановление основного профиля.
- [x] Реализовать controller и вкладки экономика, доступ, прогресс, реплики, сохранения; добавить только необходимые developer API в реальные системы.
- [x] Короткий production smoke: F9 UI, золото/reset, ворота, мета/станция, реплика, обычный запуск/возврат; 1600×900 screenshots. Проверка release/development scripts.
- [x] Проверить GUID/код/сцены/Resources у ограниченного списка cleanup-кандидатов; удалить только доказанный мусор либо записать причины сохранения.
- [x] Создать `docs/DEV_FAST_WORKFLOW.md`, компактный отчёт с фактическими замерами и тремя гипотезами интересности. Независимый итоговый code review; оставить Unity MainMenu.

## Ledger

- Начало: пользовательские Git-изменения отсутствуют; снимок PlayerPrefs и известного изменяемого TMP material сохранён в игнорируемом `Artifacts/GeneratedQA/BunkerDebug`.
- Решение: не создавать worktree и не делать коммиты — прямой запрет пользователя сильнее стандартного skill workflow.
- Решение: полную регрессию не запускать — прямое ограничение пользователя; адресные проверки хранения/бункера/production-пути и release compilation.
- Хранение: RED 2/2 отсутствующая граница; GREEN 2/2 native-значения и новые receipt изолированы. `storage-red.xml` и последующие результаты в GeneratedQA/BunkerDebug.
- Бункер: первое 3/3, затем расширенный smoke 5/5: ресурсы/reset, ворота, мета/станция, реплика, все вкладки, обычный старт/возврат, pending audio/language/AutoAim, миссионные стадии/receipt на доступной временной копии, task payout и три реальных этапа Escape.
- Отдельный production MVP smoke: PASS 1/1, 38,51 с по case XML; StartScreen → MainMenu → MVP → authored Relay/реальная наградная карточка → MainMenu.
- Независимый обзор: P1 uGUI input под IMGUI; P2 static AutoAim cache; P2 actual Screen state в development player. Первые два воспроизведены RED и пройдены GREEN. Все исправлены; actual Screen restore требует отдельного ручного development-player запуска и не проверяется resize-веткой Editor.
- Решение: сохранять production-недоступность SIGNAL TRACE (зависит от отключённого False Signal). Тест стадий использует только временные клоны, по существующему MissionServiceTests; production assets неизменны. Цена неверного решения — невозможность тестировать доступную миссию через production до отдельного решения по её контенту; скрытый обход доступности был бы изменением игры.
- Очистка: удалены только MusicPlayer.cs/.meta. Ноль сериализованных GUID-ссылок и code/dynamic/Resources/Editor/Lab потребителей; единственное упоминание вне самих файлов — исторический VerticalSlice_Audit. OrbitalCombatV2 helpers, OldRed/OldGrey и OLD-бункер сохраняются из-за действующих потребителей.
- Повтор release compile потребовался после удаления файла до Refresh: старый AssetDatabase перечислял удалённый MusicPlayer.cs (CS2001). Исходный FAIL сохранён; повтор только после нового Editor domain и актуального rsp. Это не production code dependency.
- Дополнительный RED: no-Dev development compile выявил существующий прямой BotRunSession в ProductionSceneComposition. Заменён optional partial callback с той же проверкой роли и тем же вызовом, реализованным в Dev/Debug/GoldenPathLab; без Dev реализация/вызов стираются компилятором. Это исправление границы компиляции, без изменения обычного запуска или лабораторного поведения.
- Финальные compiler checks: Editor/Release/Development PASS; no-Dev 3/3, 466 runtime sources, 85 Dev sources исключены. Бинарная release/development проверка типов и sandbox API — PASS.
- Удержание ввода после внешнего re-enable: дополнительный RED; кэшированные input components блокируются в Update/LateUpdate; адресный bunker case GREEN 1/1 (13,96 с по case XML).
- После финального callback/input изменения production MVP case повторно GREEN 1/1: 47,71 с по test-case.duration. Корневой NUnit duration сбрасывается при domain reload; исходный bark case — 19,12 с, а не 11,68 с корневого таймера.
- Передача 02:17 МСК: MainMenu / Play / sandbox=True / visible=True / 1600×900; физическая F9 закрыла и открыла окно. Последний Play без ошибок, native gameplay differences=0, исходный TMP damage material побайтно сохранён, git diff --check PASS, индекс пуст, ветка TestByDantes. Независимый обзор выполнен, замечания исправлены с адресной проверкой; новых игровых решений/баланса/production-data изменений нет.
