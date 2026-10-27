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
