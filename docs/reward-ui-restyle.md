# Окно наград: визуальное обновление

Обновлены `MVPUpgradePanel.prefab` и `PF_UpgradeCard.prefab`: графитовые подложки, ступенчатые рамки, металлические детали, светлая типографика и янтарный selected. Использован существующий шрифт Subject42 UI SDF с существующим материалом DeathText без тяжёлой обводки. Иконки и их tint не изменены.

`RewardPixelPanelImage` заменяет декоративные Image, сохраняя совместимость ссылок и прямоугольные raycast-области. `RewardCardButton` наследует штатный Button: сначала вызывает исходный переход состояния/Animator, затем окрашивает рамку. Исходный Animator и его scale-кривые сохранены. Общий UICardHoverAnimation не изменён.

Все сериализованные RectTransform обоих prefab'ов, количество и порядок карточек, icon Image, callback-ссылки, навигация и время появления сохранены. У заголовков карточек добавлен внутренний текстовый margin 10 единиц; сами текстовые блоки не перемещались. UpgradeManager, UpgradeCardView, UpgradePanelView, данные и локализация, tutorial и выбор следующей локации не изменены.

## Проверено 24.09.2026

- Unity 6000.3.13f1, кадры 1920×1080: normal, hover, selected, unavailable и награда за событие.
- Все 15 текущих production-представлений карточек, включая длинные названия и три уровня ядра; проверки TMP overflow/truncation прошли.
- Selected показан как существующее состояние Button, без добавления стадии подтверждения. Unavailable проверен установкой interactable=false только в QA; правила доступности наград не изменены.
- Существующие тесты level-up/click/grant и сундука: 2/2 PASS.
- Существующий tutorial: все 8 шагов, отмена placement, повторный запуск и reset — PASS.

Кадры и XML: `Artifacts/GeneratedQA/RewardStyle/`. Повторяемая проверка: `RewardStylePresentationTests.AllProductionCardsAndButtonStatesAt1080p`.

Полный список текущих карточек: `reward-card-texts.ru.txt`. Тексты взяты из runtime provider/localization без редакторских изменений, переводы строк сведены в пробелы. Для изменяемых чисел кольца показано состояние кольца 1 из визуального прогона; в игре номер кольца и значения меняются с состоянием забега. ModuleDamage и LinkMatrix не входят в текущий production-пул и в список не включены.
