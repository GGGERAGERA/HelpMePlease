# Vertical Slice — ручной Test Plan

Общее правило для всех тестов: очистить Console перед началом; warning допустим только если он прямо указан. Любой `NullReferenceException`, `MissingReferenceException`, missing script, scene load error, Animator parameter warning или повторяющийся warning — провал. Для проверок сохранений записывать начальные значения gold/HP/XP/levels.

## A. Запуск через MainMenu

- Подготовка: открыть `MainMenu`, очистить Console, Play.
- Действия: дождаться bootstrap, пройтись по бункеру, не открывая MVP напрямую.
- Ожидается: bunker активен; player/cursor interaction работает; modal panels закрыты; persistent services по одному.
- Допустимые логи: bootstrap/load informational logs.
- Ошибка: красный log, Missing Script, открытая блокирующая панель, отсутствует context/service.

## B. Выбор персонажа и оружия

- Подготовка: unlock defaults, selection пуст.
- Действия: открыть character station, выбрать доступную карту, затем weapon station и оружие.
- Ожидается: details соответствуют data; highlight один; выбор записан; разные `CharacterCardView`/`WeaponCardView` работают независимо.
- Допустимые логи: `Character selected`, `Weapon selected` один раз на confirm.
- Ошибка: выбор locked card, duplicate callbacks, неверная картинка/data, missing manager.

## C. Запуск без выбора

- Подготовка: перезапустить MainMenu, ничего не выбирать.
- Действия: активировать дверь; повторить с выбранным только персонажем.
- Ожидается: MVP не загружается; notification объясняет, чего не хватает.
- Допустимые логи: warning пользователя/blocked start без stack trace.
- Ошибка: загрузка MVP, NRE, создание RunState с null selection.

## D. Покупка метаулучшения

- Подготовка: известный gold >= стоимости; записать level и gold.
- Действия: открыть shop, купить один upgrade, закрыть/открыть panel, перезапустить MainMenu.
- Ожидается: gold списан один раз; level +1; цена обновилась; значения пережили reload.
- Допустимые логи: один purchase/save log.
- Ошибка: двойное списание, UI сам меняет gold, уровень теряется, missing Currency.

## E. Первый уровень

- Подготовка: полный selection через MainMenu.
- Действия: начать run, двигаться/стрелять до первых spawn.
- Ожидается: выбранные player/weapon; timer от 05:00 вниз; HUD health/XP/kills; default weather; enemy spawn.
- Допустимые логи: spawn/meta apply/`LEVEL 1` informational.
- Ошибка: default asset вместо выбора, два player/weapon, no spawner/timer, red Console.

## F. XP и run-upgrades

- Подготовка: MVP, получить достаточно XP.
- Действия: подобрать XP, вызвать level-up, выбрать upgrade, повторить для stackable upgrade.
- Ожидается: XP растёт; FX realtime; timeScale 0 только во время выбора; effect применяется ровно один раз за выбор; игра resumes.
- Допустимые логи: level-up, registered upgrade.
- Ошибка: soft-lock, duplicate panel, upgrade не действует/действует дважды, unimplemented type.

## G. Смерть игрока

- Подготовка: MVP с ненулевыми kills/time; записать total gold.
- Действия: получить lethal damage; нажать MainMenu (не Restart).
- Ожидается: движение/render скрыты, GameOver panel, затем централизованный `EndRun(PlayerDied)`, один reward, MainMenu summary.
- Допустимые логи: `Player died`, один `Run ended`, один `AddGold`.
- Ошибка: прямой reload, reward отсутствует/дважды, timeScale остаётся 0, summary отсутствует.

## H. Смерть босса

- Подготовка: дождаться 5:00 (или безопасно ускорить timer Inspector до Play); boss prefab корректен.
- Действия: убить boss одним финальным hit.
- Ожидается: boss HP скрыт; completion зарегистрирован один раз; spawn остановлен; remaining enemies удалены без loot/reward; сообщение boss defeated.
- Допустимые логи: one completion, spawning stopped, removed count.
- Ошибка: Victory/portal, повторный completion, враги продолжают spawn, missing flow/cleaner.

## I. Карточки через 5 секунд

- Подготовка: состояние сразу после H; измерять unscaled real time.
- Действия: ничего не нажимать 5 секунд.
- Ожидается: примерно через configured delay открывается panel с 3 уникальными cards; timeScale становится 0 при открытии.
- Допустимые логи: level choice opened.
- Ошибка: раньше/никогда, меньше 3 при pool >=3, duplicate nodes, soft-lock/exception.

## J. Выбор дождя

- Подготовка: среди cards есть Rain; записать HP/XP/upgrade/stats.
- Действия: выбрать Rain один раз.
- Ожидается: card callback один; state snapshot, level +1, selected node Rain, load MVP.
- Допустимые логи: stats committed, saved XP/health, advanced/selected node.
- Ошибка: двойная загрузка/advance, click не работает, timeScale остаётся 0.

## K. Переход на новый уровень

- Подготовка: J.
- Действия: дождаться полного старта нового MVP.
- Ожидается: новый scene player/enemies; `CurrentLevel = 2`; новый `RunStatsManager`; один экземпляр каждого singleton.
- Допустимые логи: apply selected node/scaling.
- Ошибка: старые scene objects живы, два player/manager, номер не вырос.

## L. Сохранение HP/XP/upgrades

- Подготовка: до J иметь неполное HP, частичный XP и заметный upgrade.
- Действия: сравнить значения после K до нового damage/pickup.
- Ожидается: current/max HP, character XP level/current XP и все picked upgrades совпадают; upgrade не удвоен.
- Допустимые логи: `Applied XP`, `Applied to spawned player`.
- Ошибка: full HP вместо snapshot, XP reset, effect потерян/удвоен.

## M. Применение дождя

- Подготовка: selected Rain, новый MVP.
- Действия: осмотреть environment и spawner scaling.
- Ожидается: rain active, snow inactive, normal light; node + endless multipliers применены один раз.
- Допустимые логи: one `Applied node`, one scaling log.
- Ошибка: несколько weather одновременно, null spawner, repeated scaling.

## N. Смерть второго босса

- Подготовка: уровень 2, Rain active.
- Действия: дойти до boss и убить.
- Ожидается: completedLevels = 2; scene-2 stats committed только при следующем выборе/end; новый choice через 5 секунд.
- Допустимые логи: one completion for level 2.
- Ошибка: total levels 1/3, старый flow Victory/portal, duplicate modifier progress.

## O. Unlock за дождь

- Подготовка: Rain unlock content locked, progress 0; выполнить N.
- Действия: после регистрации вернуться/закончить run и открыть character cards.
- Ожидается: `targetId Rain` content progress +1 и unlock; соответствующая карта доступна.
- Допустимые логи: `Unlocked` один раз.
- Ошибка: иной content, case mismatch, карта остаётся locked после refresh/reload.

## P. Возврат через паузу

- Подготовка: активный run с kills/time/completed level; записать gold.
- Действия: Escape, затем MainMenu/Return to bunker.
- Ожидается: `EndRun(ReturnedToBunker)` один раз, timeScale 1, MainMenu загружен.
- Допустимые логи: one end/return.
- Ошибка: прямой LoadScene без reward/summary, повторный click удваивает end.

## Q. Расчёт золота

- Подготовка: записать accumulated kills, seconds, completed levels, reason и meta gold percent.
- Действия: вручную вычислить base: `kills/5 + floor(seconds/60)*5 + levels*100`; death умножает на 0.75; применить один meta multiplier при начислении.
- Ожидается: изменение total gold равно формуле; summary ясно показывает согласованное значение (если показывает base — зафиксировать UX discrepancy).
- Допустимые логи: один reward calculation/end и одно AddGold.
- Ошибка: bonus зависит от числа reload, отрицательное/двойное начисление.

## R. Итоги в бункере

- Подготовка: завершить run P/G.
- Действия: дождаться notification; закрыть/открыть UI, reload MainMenu без нового end.
- Ожидается: reason, completed levels, kills, earned gold; summary показывается один раз.
- Допустимые логи: fallback summary warning только если notification намеренно отключён.
- Ошибка: summary повторяется после reload, показывает только последнюю сцену, начисляет gold.

## S. Повторный запуск забега

- Подготовка: завершённый run и MainMenu.
- Действия: заново выбрать character/weapon, start.
- Ожидается: level 1, zero accumulated stats, no picked run upgrades/snapshots, meta/unlocks/gold сохранены.
- Допустимые логи: `New run`.
- Ошибка: старое HP/XP/upgrades/level, summary снова показывается, selection сам заполнен после clear.

## T. Нет двойных singleton

- Подготовка: Development Editor, пройти MainMenu -> MVP -> MVP -> MainMenu -> MVP.
- Действия: на каждом шаге найти live instances persistent/scenовых managers.
- Ожидается: ровно один `RunSelectionManager`, `RunStateManager`, Unlock, Currency, Meta; ровно один scene manager каждого типа.
- Допустимые логи: нет duplicate warnings; уничтожение fallback Currency без ошибок.
- Ошибка: два live instances, stale reference, события приходят дважды.

## U. Нет двойного золота

- Подготовка: фиксированная тестовая статистика, записать total.
- Действия: быстро нажать end button дважды; дождаться MainMenu; повторно открыть summary.
- Ожидается: `runEnded/isEndingRun` guards; один delta/PlayerPrefs save.
- Допустимые логи: одна строка AddGold.
- Ошибка: два delta или reward при показе UI.

## V. Нет повторного применения upgrades

- Подготовка: выбрать измеримый +damage/+health upgrade; записать runtime before/after.
- Действия: пройти 3 последовательных MVP и на каждом записывать stat сразу после spawn.
- Ожидается: выбранный upgrade stack сохраняется, но каждый экземпляр player получает его ровно один раз; meta gold percent не растёт по reload.
- Допустимые логи: количество `Registered` равно выборам; restore count стабилен.
- Ошибка: stat геометрически растёт или сбрасывается.

## W. Сохранение после 3 уровней

- Подготовка: разные damage/HP/XP/upgrades на уровнях 1–3.
- Действия: три boss -> choice -> reload; затем return via Pause.
- Ожидается: snapshots корректны каждый раз; accumulated stats — сумма трёх сцен; completedLevels = 3; одна reward/summary.
- Допустимые логи: по одному commit на instance.
- Ошибка: потеря state, duplicate commit, reward multiplier зависит от reload.

## X. Закрытые карточки

- Подготовка: reset только unlock keys через debug unlock reset; не `DeleteAll`, если не нужен полный reset.
- Действия: открыть character/weapon selection, нажать locked cards/confirm.
- Ожидается: gray/locked description, confirm disabled; default cards доступны.
- Допустимые логи: нет ошибок.
- Ошибка: locked content выбирается, пустое описание, service null делает locked доступным вопреки default flag.

## Y. Reset Unlocks и debug hotkeys

- Подготовка: иметь unlocked content/progress; Editor build.
- Действия: DebugResetAll, refresh/reopen cards; затем debug progress/unlock all.
- Ожидается: unlock/progress keys сброшены и восстанавливаются командами; default content остаётся доступным логически.
- Допустимые логи: explicit DEBUG reset/unlocked.
- Ошибка: удаляется gold/meta при unlock-only reset, команды доступны в release, registry missing.

## Z. Console

- Подготовка: Development Play Mode, Console Collapse off, clear.
- Действия: выполнить полный A–Y минимум один раз, включая 3 levels и оба end reasons.
- Ожидается: 0 красных ошибок; 0 missing script/Animator/NRE warnings; informational logs конечны и понятны.
- Допустимые логи: перечисленные debug/info сообщения; warning только для намеренно проведённого negative test и после него должен исчезнуть.
- Ошибка: любая красная запись, повторяющийся warning, stack trace на обычное AddGold, log spam каждый frame/hit.

## Матрица фиксации результата

Для каждого теста записать: Unity version/build target, commit, сцена старта, PlayerPrefs profile, фактические числа до/после, screenshot Console и статус `PASS / FAIL / BLOCKED`. Статический аудит не заменяет эту матрицу.
