# Night Shift — Subject#42

Один последовательный локальный процесс. Требования: Node.js, авторизованный официальный Codex CLI в PATH, PowerShell 7 (`pwsh`), Unity 6000.3.13f1. Npm-пакетов нет. Unity Editor и отдельную игру на время работы закрыть. Компьютер должен оставаться включённым и не переходить в сон; настройки питания скрипт не меняет.

Из корня проекта:

```powershell
node --test Tools/NightShift/night-shift.test.mjs
pwsh -NoProfile -File Tools/NightShift/launcher.test.ps1
pwsh -NoProfile -File Tools/NightShift/Start-NightShift.ps1 -Mode Smoke -Hours 1
pwsh -NoProfile -File Tools/NightShift/Start-NightShift.ps1 -Mode Night -Hours 7
pwsh -NoProfile -File Tools/NightShift/Start-NightShift.ps1 -Mode Status
pwsh -NoProfile -File Tools/NightShift/Start-NightShift.ps1 -Mode Stop
```

Night запускается скрыто отдельным процессом и требует успешного smoke не старше суток с тем же HEAD и fingerprint рабочего содержимого (кроме двух обновляемых отчётов). Smoke — **два реальных последовательных успешных** выбора/исполнения/Unity-проверки; один профиль пары обязательно Golden Path. При падении директор получает исходный FAIL и может выполнить ремонт/проверку, максимум 8 циклов за сессию. Все FAIL сохраняются в истории; допуск появляется только после двух новых последовательных PASS. Следующая сессия читает историю предыдущей и не начинает диагностику с нуля. `--hours` ограничен семью часами, можно меньше; остановка возможна раньше по решению директора.

`Artifacts/GeneratedQA/NightShift/latest-session.json` указывает журнал сессии. В `cycle-N` лежат решение с кандидатами и основаниями, JSONL planner/worker, итог исполнителя, Unity `results.xml`, лог и `verification.json`. `cycles.json` связывает последовательность. Файлы генерируются в Git-ignored каталоге. UTC в машинных логах; пользовательская дата работы — 27.10.2026, Europe/Moscow.

Planner: read-only. Worker: workspace-write. CLI не наследует пользовательский config; явно использует never approval и Windows unelevated sandbox. Sandbox bypass, внешние MCP, субагенты и произвольные Unity-команды из ответов модели не используются. Хост запускает только фиксированные существующие fixture-профили. Ограничения Git дополнительно проверяются по HEAD/ветке/индексу после фаз; это контроль обнаружения, не обещание отдельной ОС-изоляции всех допустимых файлов. Перед работой можно сохранить собственную резервную копию проекта.

Подтверждённые C# compiler errors (error CSxxxx + Unity compilation marker, exit≠0, XML ещё нет) записываются как `CompileFailed`, 0 выполненных тестов, и передаются следующему циклу для ремонта. Это не PASS и не потерянный тест. Для любого результата обязателен `wrapper-outcome.json`: штатный выход Unity и успешно проверенное восстановление настроек. Отсутствие этого marker, XML без признаков compiler failure, сбой лицензии или таймаут останавливают процесс.

`STOP` запрещает новый запуск, прерывает CLI и останавливает принадлежащий скрипту Unity с восстановлением PlayerPrefs. После штатного завершения lock удаляется. После аварии ОС/процесса не удалять lock вслепую: сначала проверить PID и незавершённые `playerprefs-backup.clixml`. Их поиск выполняется по всем прошлым сессиям; новый Output не обходит восстановление. Снимок включает пользовательские значения реестра Editor/Player для Life Up Studio / Subject#42; он локальный и игнорируется Git. `playerprefs-restored.clixml` и `playerprefs-restore-verification.json` означают выполненное восстановление и сверку значений/типов. При аварийном завершении восстановить конкретный снимок, когда Editor/игра закрыты:

```powershell
pwsh -NoProfile -File Tools/NightShift/Run-UnityTests.ps1 -RestoreOnly -Unity 'G:/unity/editor/6000.3.13f1/Editor/Unity.exe' -Project 'D:/fork/HelpMePlease' -Output 'D:/fork/HelpMePlease/Artifacts/GeneratedQA/NightShift/SESSION/cycle-N/unity' -Filter 'Subject42GoldenPathTests'
```

Только после проверки восстановления и отсутствия старого процесса можно удалить **конкретный** старый lock/STOP. Нельзя запускать игру параллельно с восстановлением: это перезаписало бы новые изменения прогресса.

Unity/TMP пересчитывает ratios общего Damage Numbers material в Play Mode. Wrapper сохраняет этот известный изменяемый asset после работы исполнителя и восстанавливает его после проверки (включая recovery). Защита индекса сравнивает staged entries, а не служебный stat-cache, обновляемый обычным `git status`.

Отчёты: `NIGHT_REPORT.md` и `PLAYTEST_CHECKLIST.md`. Они описывают реальные свидетельства; зелёный бот не заменяет ручной плейтест, субъективную оценку и проверку обычной release-сборки. Новое содержимое — Experimental, по умолчанию выключено. Git add/commit/branch/push/merge и публикация запрещены; коммит делает пользователь в Fork.

Источники интерфейса: [официальный Codex non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode), [Windows sandbox](https://learn.chatgpt.com/docs/windows/windows-sandbox), [Unity 6000.3 command line tests](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/test-framework-introduction/running-tests/run-tests-from-command-line). Локальные флаги проверены через `codex exec --help`; `--full-auto` в этой версии не поддерживается. Unity `-runTests` запускается без `-quit`, чтобы не прерывать тесты, и без `-nographics`, поскольку часть сценариев использует рендеринг.
