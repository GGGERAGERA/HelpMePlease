# Night Shift Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline. Пользователь запросил автономную работу без лишней мультиагентной инфраструктуры; коммиты и новые ветки запрещены.

**Goal:** Проверяемая локальная автономия для улучшения существующего забега Subject#42.
**Architecture:** Один Node.js-контроллер, read-only выбор задачи через Codex CLI, workspace-write исполнение, allowlist Unity-проверок на хосте.
**Tech Stack:** Node.js built-ins, PowerShell, Codex CLI 0.164.0-alpha.11.1, Unity 6000.3.13f1.
**Spec:** [design](../specs/2026-10-27-night-shift-design.md).

## Global Constraints

Никаких git mutations, публикации, sandbox bypass, изменения сохранений. Максимум семь часов. Изменения дизайна подчинены AGENTS.md. Вторая задача выбирается по свежему результату первой.

## Review Focus

Пустой/повреждённый XML; таймаут дочернего процесса; STOP во время работы; параллельный Editor/контроллер; неполный smoke не открывает ночной запуск.

## Tasks

### 28.10: восстановление и запуск пятичасовой сессии

Прямой запрос пользователя заменяет согласование плана; работаем inline в текущей чистой ветке TestByDantes, без Git mutations и новой инфраструктуры.
Причина старой остановки: cycle-8 decision.action=stop после timeout отдельного теста; cycleLoop безусловно завершал ночь. Исключение wrapper также выходило во внешний catch.

- [x] Воспроизвести регрессиями: отклонённый stop, восстановимая ошибка дочернего задания, два неуспешных ремонта → BLOCKED и независимая задача, жёсткий дедлайн.
- [x] В существующих controller/wrapper отделить восстановимые результаты от угрозы состоянию, авторизации/квот и потери контроля процессов. Сохранять все FAIL, диагностику и восстановление PlayerPrefs.
- [x] Добавить heartbeat/process ledger, startedAt/deadlineAt, запрет длительной работы за 15 минут, компактный финальный NIGHT_REPORT. Убрать лимит 40 циклов как скрытый срок окончания ночи.
- [x] Обновить существующую политику: приоритет движущегося ORBITAL V2, затем Live Visual Studio; адресные фильтры вместо постоянного Golden Path. Не менять production V2/Bullet Time и художественные решения.
- [x] Короткий сквозной smoke реальными дочерними процессами: FAIL → восстановление → timeout → успешное следующее задание, затем реальная адресная Unity-проверка с восстановлением настроек.
- [x] Запустить Night на 5 часов и проверить живой PID, heartbeat, точный дедлайн, первый реальный Codex и отсутствие конкурентного Unity.

Проверки: node --test Tools/NightShift/night-shift.test.mjs, PowerShell launcher regression, recovery smoke и Unity XML; git diff --check. Сквозной smoke — свидетельство устойчивости контроллера, не игрового качества.

Свидетельства 28.10: Node 20/20 (новые четыре регрессии сначала RED); launcher regression PASS (mock PID 12345, не реальный запуск); smoke `2026-10-27T22-46-11-187Z-smoke` — recovery probe, реальный Codex/schema и `OrbitalCombatV2LabTests.FixedShotsUseRadialMuzzleWithoutTargetsAndKeepTheProfileCadence` 1/1, Unity exit 0, восстановлены 27 PlayerPrefs-значений. Реальный Night PID **20068**, session `2026-10-27T22-47-35-880Z-night`, startedAt **2026-10-28 01:47:35.881 МСК**, deadlineAt **06:47:35.881 МСК**. Metadata, heartbeat и CommandLine подтверждены. Это запуск сессии, не заявление о завершённых пяти часах или качестве игры.

Уточнение пользователя: просыпается **в 06:00 МСК**. Этот срок заменяет 06:47. Первый контроллер штатно остановлен в 01:49:54; текущий patch исполнителя сохранён в его session/interrupted-worker.patch, незавершённый вызов ещё не существующего helper удалён адресной правкой (исторический fixture восстановлен), карточка гипотезы сохранена. Добавлены абсолютный DeadlineAt и ResumeFrom: первоначальное начало/история сохраняются, срок может только сократиться в пределах исходного бюджета. Node 21/21; повторный короткий smoke проверяет актуальную версию до продолжения.

- [x] Написать `Tools/NightShift/night-shift.test.mjs`: динамический второй выбор, ошибки тестов, пустой XML, stop/deadline, smoke gate. Запустить и увидеть ожидаемое отсутствие реализации.
- [x] Реализовать `night-shift.mjs`, `Run-UnityTests.ps1`, schema/prompt и локальный запуск; пройти unit-проверки.
- [x] Запустить два реальных CLI-цикла; сохранить решения/логи/XML, изучить свежий Golden Path, устранить воспроизводимые дефекты в рамках лимита smoke.
- [x] Проверить diff и ограничения; заполнить NIGHT_REPORT/PLAYTEST_CHECKLIST и инструкцию запуска. Если smoke зелёный, запустить ночную сессию локально; иначе зафиксировать точную причину остановки.

## Ledger

- Прямой запрос на автономию заменяет вопросы о выборе подхода/одобрении плана из общих skills. Код исполняется inline; для независимого read-only ревью используется один reviewer, без параллельных исполнителей.
- CLI no-tool probe: exit 0, `NIGHT_SHIFT_PROBE_OK`, read-only sandbox, ChatGPT login. Это ещё не доказательство shell/write доступа или Unity-проверок.
- Независимый read-only review: исправлены 5 конкретных дефектов (orphan PlayerPrefs, неверный Corridor fixture/частичный фильтр, непроверенная работа при deadline, fingerprint допуска, lock до finally). Добавлены реальные проверки timeout/STOP; unit 12/12.
- Предварительная сессия 22:25 UTC штатно остановлена через STOP ради safety-правок до окончания Unity: нет XML, не считать игровой регрессией/успехом. PlayerPrefs восстановлены. Итоговый двухцикловый smoke запущен заново 22:33 UTC на исправленной версии.
- Несколько repair-циклов выявили устаревшие reward/cleanup/localization oracle и production HUD-дефекты; все исходные FAIL сохранены в NIGHT_REPORT. Проходивший не во всех попытках Rocket Storm admission не объявлен исправленным.
- Финальная версия launcher исправляет несколько Get-Command node путей; regression red→green и текущий controller unit 13/13. Финальный smoke через Start-NightShift.ps1: Golden Path 6/6 → onboarding 12/12, exit 0; source fingerprint совпал перед запуском Night.
- 27.10.2026 03:04:58 МСК: реальный detached Night PID 16412, deadline 08:21 МСК. Metadata и лог подтверждают mode=night; это не mock PID 12345. План развёртывания выполнен; дальнейшая подготовка демоверсии продолжается в этой сессии, её результат нельзя объявлять заранее.
- Первая ночь остановилась на compiler error в новом диагностическом тесте. Исправлен доступ теста через reflection; onboarding 13/13. Контроллер различает CompileFailed и инфраструктурную ошибку по normal Unity exit + проверенному восстановлению wrapper-outcome.json; unit red→green 14/14, независимое ревью без важных замечаний.
- 27.10.2026 03:37:55 МСК: после нового реального smoke Golden Path 6/6 → 6/6 и совпадения fingerprint запущен новый detached Night PID 20936. Deadline прежний 08:21 МСК, оставшийся бюджет около 4 ч 43 мин. Метаданные, process command line и начало read-only выбора проверены. Предыдущие FAIL сохранены; полная ночь, ручной плейтест и готовность выпуска ещё не подтверждены.

Продолжение подтверждено: реальный PID 10748, session 2026-10-27T22-51-43-105Z-night; metadata.startedAt=2026-10-27T22:47:35.881Z, resumedAt=22:51:43.105Z, deadlineAt=2026-10-28T03:00:00Z (06:00 МСК). Повторный smoke 22:50:45 — recovery, Codex/schema и Unity 1/1 PASS; PlayerPrefs восстановлены. Launcher regression после снятия штатного STOP — PASS. Heartbeat и CommandLine проверены. Продолжение автономии идёт; конечные игровые выводы появятся по фактическим артефактам.
