# Цвета reward-карточек

Общий prefab: `Assets/_Project/prefabs/UI/_UpdateUI/PF_UpgradeCard.prefab`.
Полные копии карточки и отдельные варианты layout не нужны: три ScriptableObject presets
в `RewardVisuals/` используют одну prefab-структуру.

- `WeaponCore.asset`: Pistol, LaserSword, ImpulseGun, ArcEmitter, LinkPair,
  CoreUpgrade, LinkMatrix, ModuleDamage — красный.
- `Ring.asset`: RingSpeed, RingPower, AddMount, NewRing, RingCapacity — синий.
- `Subject.asset`: MaxHealth, MoveSpeed — зелёный; fallback для legacy UpgradeData.

Mapping хранится в поле `rewardKinds` каждого preset. Список presets и fallback
назначены на `RewardCardButton` базового prefab. CORE не объединён с WEAPON в данных.
Новый visual type: создать `UI > Reward Card Visual Preset`, назначить rewardKinds,
настроить States и добавить asset в Visual Presets базового prefab.

В Inspector доступны Normal / Highlighted / Pressed / Selected / Disabled,
множитель и длительность перехода, ссылки Target Graphic (рамка) и Header Accent.
В UpgradeCardView явно назначены icon, title, description, iconFrameImage и существующие
декоративные ссылки. Спрайты Background, Title, Header, Frame находятся в
`Assets/_Project/art/UI/RewardCards/`; это статические PNG, обычные prefab Images.
Рамка и мелкие детали входят в Frame. Процедурного построения карточки нет.
Существующий RewardPixelPanelImage остаётся у внешнего окна наград, вне карточки.

В runtime остались передача данных, поиск подходящего preset и применение цветов
Button к рамке/header. Иконки и их исходный tint, текст, Animator, размеры, raycast,
reward flow, tutorial и callbacks сохранены. Все RectTransform и компоненты
иконки/текстов сравнены с prefab до изменения без различий.

Повторная проверка: `Tools > Subject42 > Verify Reward Colors`.
Тест `FourRewardTypesAndStates` размещает четыре экземпляра общего prefab вместе
только для QA: WEAPON, CORE, RING, SUBJECT. В игровом flow по-прежнему три карточки.
Кадры: `Artifacts/GeneratedQA/RewardStyle/types-*.png`.
Результаты тестов: `Artifacts/GeneratedQA/RewardColors/results.xml`.

Проверено в Unity 6000.3.13f1, 1920×1080: обе визуальные проверки, level-up,
normal anomaly и special anomaly — PASS (5 из 6 проверок общего прогона).
Существующий `WorldLootChestReelGrantsExactlyTheStoppedReward` дважды остановился
на строке 48 до открытия UI: ожидался Opening, получен Closed. Gameplay-код
сундука не менялся; успешный полный прогон не заявляется.
Полный результат этих шести проверок сохранён в `RewardColors/regression-results.xml`.
