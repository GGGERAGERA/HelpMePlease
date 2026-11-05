# Night Shift

Один локальный Node.js-контроллер запускает последовательные Codex planner/worker и адресные Unity-проверки. Правила пользователя и AGENTS.md имеют приоритет. Новая политика 28.10.2026 заменяет прежний обязательный двухцикловый Golden Path smoke и правило остановки при любом timeout.

## Запуск

Из корня проекта, при закрытом Unity Editor:

```powershell
pwsh -NoProfile -File Tools/NightShift/Start-NightShift.ps1 -Mode Smoke -Hours 0.25
pwsh -NoProfile -File Tools/NightShift/Start-NightShift.ps1 -Mode Night -Hours 5
pwsh -NoProfile -File Tools/NightShift/Start-NightShift.ps1 -Mode Status
pwsh -NoProfile -File Tools/NightShift/Start-NightShift.ps1 -Mode Stop
```

Smoke проверяет восстановление после FAIL и timeout, отклонение досрочного stop, доступ sandbox к двум production-файлам карты, создание и удаление временного файла в их каталоге, связь Codex/schema и одну Unity-проверку outward firing. Содержимое production-файлов сохраняется. Gate recovery-v3 требует успешной проверки записи и привязан к HEAD, workspace и времени (24 часа). После правок кода повторить короткий smoke.

Launcher передаёт контроллеру абсолютный путь к нативному codex.exe. Если его нет в PATH, проверяется установленный CLI в LocalAppData/OpenAI/Codex/bin. Авторизация проверяется реальным запросом Smoke.

Night работает скрыто, по умолчанию пять часов от startedAt, без автоматического продления. Артефакты: Artifacts/GeneratedQA/NightShift/latest-session.json → каталог session. В metadata.json записаны startedAt/deadlineAt. heartbeat.json обновляется раз в 10 секунд и содержит фазу, PID дочернего процесса, его timeout и оставшееся время. session.lock запрещает второго контроллера. Не удалять lock живого процесса.

Для явно изменённого пользователем утреннего срока: после штатного Stop и короткого smoke передай Night `-DeadlineAt '2026-10-28T06:00:00+03:00' -ResumeFrom '<абсолютный путь прежней session>'`. Контроллер сохраняет первоначальный startedAt и историю в контексте следующего выбора; более поздняя дата не продлевает исходный бюджет. DeadlineAt задаёт абсолютный момент с часовым поясом. Восстанови компилируемость прерванного частичного изменения перед smoke, сохранив patch и диагностику.

За 15 минут до дедлайна новые обычные задачи не запускаются. Контроллер завершает адресную проверку, готовит короткий read-only итог и наблюдает до фиксированного deadline. Дочерние бюджеты ограничены общим остатком с резервом на восстановление. Нет скрытого ограничения количества циклов. STOP прекращает Codex; Unity-wrapper завершает собственный Editor и восстанавливает настройки перед выходом.

## Ошибки

Обычные FAIL/CompileFailed/MissingXml/восстановленный timeout сохраняются в cycles.json и передаются следующему свежему планировщику. Два неуспеха одного стабильного issueKey блокируют только эту проблему; нужна независимая задача. Планировщик перечисляет изменяемые файлы в writePaths, хост проверяет доступ к ним в sandbox перед каждым исполнителем. Отказ записи фиксируется как WorkerAccessDenied.

NoSafeWork не завершает смену. Контроллер сохраняет причину в replan-pending.json, показывает фазу replanning в heartbeat, выдерживает две минуты и запрашивает новый выбор работы до резерва завершения. ReplanPending не блокирует issueKey. Read-only права планировщика не описывают права отдельного исполнителя. Успешная проверка также не завершает ночь.

Неудавшееся восстановление PlayerPrefs, неизвестное состояние wrapper, потеря контроля процессов, авторизация/квота, изменение HEAD/branch/index либо оркестратора останавливают сессию с completed=false. После проверенного review Safety/AuthQuota/ProcessControl также сохраняется ошибка. completed=true для Night означает достижение фиксированного дедлайна без пользовательского STOP. allChecksPassed отдельно отражает всю историю проверок и не подтверждает выполнение UX-задачи.

Unity запускается только хостом, без конкурентного Editor. Wrapper сохраняет оба registry PlayerPrefs-пути и известное побочное изменение TMP-материала; восстанавливает в finally, проверяет значения, пишет wrapper-outcome.json. При оставшемся playerprefs-backup.clixml новый запуск запрещён. Восстановление конкретного снимка (без работающего Editor):

```powershell
pwsh -NoProfile -File Tools/NightShift/Run-UnityTests.ps1 -Unity G:/unity/editor/6000.3.13f1/Editor/Unity.exe -Project D:/fork/HelpMePlease -Output <каталог unity со снимком> -Filter unused -RestoreOnly
```

Не удалять снимки ради обхода блокировки. Отсутствующий до теста registry key восстанавливается как отсутствующий. Не закрывать пользовательский Editor; wrapper отказывается стартовать при любом открытом Unity.

## Проверки контроллера

```powershell
node --test Tools/NightShift/night-shift.test.mjs
pwsh -NoProfile -File Tools/NightShift/launcher.test.ps1
git diff --check
```

Приоритеты и дизайн-ограничения — director-prompt.md. Профили включают существующие Orbital V2 и Live Visual fixtures; testFilter позволяет адресный существующий метод. Новый framework не нужен. Подробные worker-summary/XML/log сохраняются отдельно, финальный NIGHT_REPORT.md компактный, предыдущий отчёт архивируется в session. Git mutations, публикации и перенос V2 в production запрещены.
