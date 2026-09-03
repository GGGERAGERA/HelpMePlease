using System;
using System.Collections.Generic;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public sealed class BunkerSelectionSourceHub : MonoBehaviour
{
    [Serializable]
    public sealed class UpgradePresentation
    {
        public MetaUpgradeType Type;
        public string Title;
        [TextArea(2, 4)] public string Description;
        public string Category;
        [Range(1, 3)] public int RequiredStationLevel = 1;
    }

    private enum SourceKind
    {
        Characters,
        Weapons,
        Upgrades,
        Anomalies
    }

    private sealed class SourceAdapter : IBunkerSelectionSource
    {
        private readonly BunkerSelectionSourceHub hub;
        private readonly SourceKind kind;

        public SourceAdapter(BunkerSelectionSourceHub hub, SourceKind kind)
        {
            this.hub = hub;
            this.kind = kind;
        }

        public event Action Changed
        {
            add => hub.changed += value;
            remove => hub.changed -= value;
        }

        public void Prepare() => hub.Prepare(kind);
        public BunkerSelectionWindowModel BuildModel() => hub.Build(kind);
        public void Confirm(string entryId) => hub.Confirm(kind, entryId);
    }

    [Header("Production Content")]
    [SerializeField] private CharacterData[] characters;
    [SerializeField] private WeaponData[] weapons;
    [SerializeField] private UpgradePresentation[] upgrades;
    [SerializeField] private AnomalyStabilizerData[] anomalies;

    private Action changed;
    private SourceAdapter characterSource;
    private SourceAdapter weaponSource;
    private SourceAdapter upgradeSource;
    private SourceAdapter anomalySource;
    private BunkerStationProgressionService boundProgression;
    private CurrencyManager boundCurrency;
    private MetaProgressionManager boundMeta;
    private AnomalyMetaProgressionManager boundAnomalyMeta;

    public IBunkerSelectionSource Characters =>
        characterSource ??= new SourceAdapter(this, SourceKind.Characters);
    public IBunkerSelectionSource Weapons =>
        weaponSource ??= new SourceAdapter(this, SourceKind.Weapons);
    public IBunkerSelectionSource Upgrades =>
        upgradeSource ??= new SourceAdapter(this, SourceKind.Upgrades);
    public IBunkerSelectionSource Anomalies =>
        anomalySource ??= new SourceAdapter(this, SourceKind.Anomalies);

    private void OnEnable() => BindServices();
    private void Update() => BindServices();
    private void OnDisable() => UnbindServices();

    private void BindServices()
    {
        if (boundProgression != BunkerStationProgressionService.Instance)
        {
            if (boundProgression != null)
            {
                boundProgression.StationLevelChanged -= HandleStationLevelChanged;
                boundProgression.StationInvestmentChanged -= HandleStationInvestmentChanged;
            }
            boundProgression = BunkerStationProgressionService.Instance;
            if (boundProgression != null)
            {
                boundProgression.StationLevelChanged += HandleStationLevelChanged;
                boundProgression.StationInvestmentChanged += HandleStationInvestmentChanged;
            }
        }

        if (boundCurrency != CurrencyManager.Instance)
        {
            if (boundCurrency != null)
                boundCurrency.OnGoldUpdated -= HandleGoldChanged;
            boundCurrency = CurrencyManager.Instance;
            if (boundCurrency != null)
                boundCurrency.OnGoldUpdated += HandleGoldChanged;
        }

        MetaProgressionManager currentMeta = MetaProgressionManager.Instance;
        if (boundMeta != currentMeta)
        {
            if (boundMeta != null)
                boundMeta.ProgressChanged -= HandleMetaChanged;
            boundMeta = currentMeta;
            if (boundMeta != null)
                boundMeta.ProgressChanged += HandleMetaChanged;
        }

        AnomalyMetaProgressionManager currentAnomalyMeta =
            AnomalyMetaProgressionManager.Instance;
        if (boundAnomalyMeta != currentAnomalyMeta)
        {
            if (boundAnomalyMeta != null)
                boundAnomalyMeta.ProgressChanged -= HandleMetaChanged;
            boundAnomalyMeta = currentAnomalyMeta;
            if (boundAnomalyMeta != null)
                boundAnomalyMeta.ProgressChanged += HandleMetaChanged;
        }
    }

    private void UnbindServices()
    {
        if (boundProgression != null)
        {
            boundProgression.StationLevelChanged -= HandleStationLevelChanged;
            boundProgression.StationInvestmentChanged -= HandleStationInvestmentChanged;
        }
        if (boundCurrency != null)
            boundCurrency.OnGoldUpdated -= HandleGoldChanged;
        if (boundMeta != null)
            boundMeta.ProgressChanged -= HandleMetaChanged;
        if (boundAnomalyMeta != null)
            boundAnomalyMeta.ProgressChanged -= HandleMetaChanged;
        boundProgression = null;
        boundCurrency = null;
        boundMeta = null;
        boundAnomalyMeta = null;
    }

    private void Prepare(SourceKind kind)
    {
        BindServices();
        if (kind == SourceKind.Upgrades)
        {
            MetaProgressionManager.EnsureExists().ReloadFromStorage();
            BindServices();
        }
        else if (kind == SourceKind.Anomalies)
        {
            AnomalyMetaProgressionManager.EnsureExists();
            BindServices();
        }
    }

    private BunkerSelectionWindowModel Build(SourceKind kind)
    {
        return kind switch
        {
            SourceKind.Characters => BuildCharacters(),
            SourceKind.Weapons => BuildWeapons(),
            SourceKind.Upgrades => BuildUpgrades(),
            SourceKind.Anomalies => BuildAnomalies(),
            _ => null
        };
    }

    private void Confirm(SourceKind kind, string entryId)
    {
        switch (kind)
        {
            case SourceKind.Characters:
                CharacterData character = FindByName(characters, entryId);
                if (character != null && IsUnlocked(character.unlockData))
                {
                    RunSelectionManager.Instance?.SelectCharacter(character);
                    AudioService.Instance?.Play(AudioCueId.UIConfirm);
                }
                break;

            case SourceKind.Weapons:
                // Weapon Station cards describe Orbital unlocks. Starting
                // equipment is fixed and the cards intentionally do not
                // mutate the legacy weapon selection.
                break;

            case SourceKind.Upgrades:
                if (Enum.TryParse(entryId, out MetaUpgradeType type))
                {
                    bool invested = MetaProgressionManager.EnsureExists()
                        .TryInvestGold(type, 1, out _);
                    AudioService.Instance?.Play(invested
                        ? AudioCueId.Purchase
                        : AudioCueId.PurchaseFail);
                    changed?.Invoke();
                }
                break;

            case SourceKind.Anomalies:
                AnomalyStabilizerData anomaly = FindAnomaly(entryId);
                if (anomaly != null && anomaly.RequiredStationLevel <= GetStationLevel(BunkerStationId.Anomaly))
                {
                    RunSelectionManager.Instance?.SelectAnomalyStabilizer(anomaly);
                    AudioService.Instance?.Play(AudioCueId.UIConfirm);
                }
                break;
        }
    }

    private BunkerSelectionWindowModel BuildCharacters()
    {
        var model = NewModel(
            "ВЫБЕРИТЕ ПЕРСОНАЖА",
            "ДОСТУПНЫЕ СУБЪЕКТЫ",
            "ВЫБЕРИТЕ ПЕРСОНАЖА",
            "ВЫБРАТЬ",
            BunkerStationId.Character);
        CharacterData current = RunSelectionManager.Instance?.SelectedCharacter;
        model.SelectedId = current != null ? current.name : null;

        AddNonNull(characters, character =>
        {
            int requiredLevel = BunkerSelectionUnlockRules.GetRequiredStationLevel(
                character.unlockData,
                BunkerStationId.Character);
            bool unlocked = IsUnlocked(character.unlockData);
            var entry = new BunkerSelectionEntryModel
            {
                Id = character.name,
                DisplayName = character.characterName,
                Category = string.IsNullOrWhiteSpace(character.combatTypeDisplayName)
                    ? character.combatType.ToString().ToUpperInvariant()
                    : character.combatTypeDisplayName.ToUpperInvariant(),
                Icon = character.portrait,
                Feature = character.combatTypeDescription,
                Description = character.description,
                Locked = !unlocked,
                LockReason = GetLockReason(character.unlockData),
                CanConfirm = unlocked
            };
            entry.Stats.Add(new BunkerSelectionStatModel("ЗДОРОВЬЕ", character.maxHealth.ToString("0")));
            entry.Stats.Add(new BunkerSelectionStatModel("СКОРОСТЬ", character.moveSpeed.ToString("0.#")));
            AddVisibleEntry(model, entry, requiredLevel);
        });
        FinalizeUnlockPresentation(model);
        return model;
    }

    private BunkerSelectionWindowModel BuildWeapons()
    {
        var model = NewModel(
            "ОРБИТАЛЬНЫЕ МОДУЛИ",
            "ОТКРЫТЫЕ МОДУЛИ",
            string.Empty,
            string.Empty,
            BunkerStationId.Weapon);
        model.CloseOnConfirm = false;
        model.ShowConfirmButton = false;
        MetaProgressionManager progression = MetaProgressionManager.EnsureExists();
        progression.ReloadFromStorage();
        AddOrbitalModuleCard(model, OrbitalModuleKind.Pistol, "Pistol",
            "АВТОМАТИЧЕСКОЕ ОРУЖИЕ",
            "Автоматическая стрельба по ближайшим целям.", progression);
        AddOrbitalModuleCard(model, OrbitalModuleKind.LaserSword, "Laser Sword",
            "КОНТАКТНОЕ ОРУЖИЕ",
            "Контактный урон врагам на траектории орбиты.", progression);
        AddOrbitalModuleCard(model, OrbitalModuleKind.ImpulseGun, "Impulse Gun",
            "ИМПУЛЬСНОЕ ОРУЖИЕ",
            "Периодический импульс с уроном и отталкиванием.", progression);
        model.SelectedId = model.Entries.Count > 0
            ? model.Entries[0].Id : null;
        FinalizeUnlockPresentation(model);
        return model;
    }

    private static void AddOrbitalModuleCard(
        BunkerSelectionWindowModel model,
        OrbitalModuleKind kind,
        string displayName,
        string role,
        string description,
        MetaProgressionManager progression)
    {
        int requiredLevel = OrbitalRewardProvider
            .GetRequiredWeaponStationLevel(kind);
        model.Unlocks.Add(new BunkerSelectionUnlockModel(
            displayName, requiredLevel));
        if (!OrbitalRewardProvider.IsModuleUnlocked(kind))
            return;
        Sprite icon = ResolveOrbitalModuleIcon(kind, out Color iconColor);
        var entry = new BunkerSelectionEntryModel
        {
            Id = kind.ToString(),
            DisplayName = displayName,
            Category = role,
            Icon = icon,
            IconColor = iconColor,
            // The shared progression panel already presents the meta-damage
            // bonus. Keeping the generic feature block empty leaves the same
            // vertical space for stats and description as the other
            // progression stations.
            Feature = string.Empty,
            Description = description,
            Locked = false,
            CanConfirm = false,
            RequiredStationLevel = requiredLevel
        };
        entry.Progression = BuildOrbitalModuleProgression(
            progression, kind, displayName);
        AddOrbitalModuleStats(entry, progression, kind);
        model.Entries.Add(entry);
    }

    private static BunkerProgressionModel BuildOrbitalModuleProgression(
        MetaProgressionManager progression,
        OrbitalModuleKind kind,
        string displayName)
    {
        int level = progression.GetOrbitalModuleLevel(kind);
        int cap = progression.GetCurrentOrbitalModuleLevelCap();
        int cost = progression.GetOrbitalModuleUpgradeCost(kind);
        bool capped = level < progression.MaxLevel && level >= cap;
        bool unavailable = level < progression.MaxLevel && cost <= 0;
        return new BunkerProgressionModel
        {
            TargetId = $"orbital-module:{kind}",
            Title = displayName,
            Level = level,
            MaxLevel = progression.MaxLevel,
            Cost = cost,
            Progress = progression.GetOrbitalModuleInvestedGold(kind),
            RequiredProgress = cost,
            AvailableCurrency = CurrencyManager.Instance != null
                ? CurrencyManager.Instance.TotalGold : 0,
            BonusText = GetOrbitalModuleBonus(kind, progression),
            ContextText = level >= progression.MaxLevel
                ? $"ТЕКУЩИЙ ЛИМИТ: {cap}"
                : $"СЛЕДУЮЩИЙ: УРОН ×{MetaProgressionManager.GetOrbitalModuleDamageMultiplier(level + 1):0.0}\n" +
                  $"ТЕКУЩИЙ ЛИМИТ: {cap}",
            Locked = capped || unavailable,
            SupportsPartialInvestment = true,
            LockReason = capped
                ? $"ТРЕБУЕТСЯ УРОВЕНЬ СТАНЦИИ {GetRequiredStationLevel(BunkerStationId.Weapon, level + 1, progression.MaxLevel)}"
                : unavailable ? "ПРОГРЕССИЯ НЕДОСТУПНА" : null,
            ButtonText = "УЛУЧШИТЬ МОДУЛЬ",
            CanUpgrade = () => MetaProgressionManager.Instance != null &&
                MetaProgressionManager.Instance.CanInvestOrbitalModule(kind),
            Invest = amount =>
            {
                MetaProgressionManager manager = MetaProgressionManager.EnsureExists();
                int oldLevel = manager.GetOrbitalModuleLevel(kind);
                if (manager.TryInvestGoldOrbitalModule(kind, amount, out _) &&
                    manager.GetOrbitalModuleLevel(kind) > oldLevel)
                    AudioService.Instance?.Play(AudioCueId.Purchase);
            }
        };
    }

    private static string GetOrbitalModuleBonus(
        OrbitalModuleKind kind,
        MetaProgressionManager progression)
    {
        int level = progression.GetOrbitalModuleLevel(kind);
        return $"META-УРОН ×{MetaProgressionManager.GetOrbitalModuleDamageMultiplier(level):0.0}";
    }

    private static void AddOrbitalModuleStats(
        BunkerSelectionEntryModel entry,
        MetaProgressionManager progression,
        OrbitalModuleKind kind)
    {
        int level = progression.GetOrbitalModuleLevel(kind);
        float multiplier = MetaProgressionManager
            .GetOrbitalModuleDamageMultiplier(level);
        float damage = OrbitalModuleRuntime.GetBaseDamage(kind) * multiplier;
        entry.Stats.Add(new BunkerSelectionStatModel(
            "УРОВЕНЬ", $"{level} / {progression.MaxLevel}"));
        entry.Stats.Add(new BunkerSelectionStatModel(
            "УРОН", damage.ToString("0.#")));
        switch (kind)
        {
            case OrbitalModuleKind.Pistol:
                entry.Stats.Add(new BunkerSelectionStatModel("ДАЛЬНОСТЬ", "8"));
                entry.Stats.Add(new BunkerSelectionStatModel("ИНТЕРВАЛ", "0.55 С"));
                break;
            case OrbitalModuleKind.LaserSword:
                entry.Stats.Add(new BunkerSelectionStatModel("РАДИУС", "0.75"));
                entry.Stats.Add(new BunkerSelectionStatModel("ИНТЕРВАЛ", "0.32 С"));
                break;
            case OrbitalModuleKind.ImpulseGun:
                entry.Stats.Add(new BunkerSelectionStatModel("ДАЛЬНОСТЬ", "5"));
                entry.Stats.Add(new BunkerSelectionStatModel("ИНТЕРВАЛ", "1.25 С"));
                break;
        }
    }

    private static Sprite ResolveOrbitalModuleIcon(
        OrbitalModuleKind kind,
        out Color color)
    {
        color = Color.white;
        GameObject prefab = OrbitalPresentationConfig.Active.GetPrefab(kind);
        if (prefab == null)
            return null;
        SpriteRenderer[] renderers =
            prefab.GetComponentsInChildren<SpriteRenderer>(true);
        string preferredName = kind switch
        {
            OrbitalModuleKind.Pistol => "barrel",
            OrbitalModuleKind.LaserSword => "blade",
            OrbitalModuleKind.ImpulseGun => "p1",
            _ => string.Empty
        };
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null ||
                !renderer.gameObject.name.ToLowerInvariant()
                    .Contains(preferredName))
                continue;
            color = renderer.color;
            return renderer.sprite;
        }
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].sprite != null)
            {
                color = renderers[i].color;
                return renderers[i].sprite;
            }
        return null;
    }

    private BunkerSelectionWindowModel BuildUpgrades()
    {
        MetaProgressionManager progression = MetaProgressionManager.EnsureExists();
        progression.ReloadFromStorage();
        var model = NewModel(
            "УЛУЧШЕНИЯ БУНКЕРА",
            "ПОСТОЯННЫЕ УЛУЧШЕНИЯ",
            "ВЫБЕРИТЕ УЛУЧШЕНИЕ",
            "УДЕРЖИВАЙТЕ УЛУЧШИТЬ",
            BunkerStationId.Upgrades);
        model.CloseOnConfirm = false;

        AddNonNull(upgrades, presentation =>
        {
            int level = progression.GetLevel(presentation.Type);
            int cost = progression.GetUpgradeCost(presentation.Type);
            bool maxed = level >= progression.MaxLevel;
            var entry = new BunkerSelectionEntryModel
            {
                Id = presentation.Type.ToString(),
                DisplayName = presentation.Title,
                Category = string.IsNullOrWhiteSpace(presentation.Category)
                    ? "META UPGRADE"
                    : presentation.Category,
                Feature = GetUpgradeBonus(presentation.Type, level),
                Description = presentation.Description,
                Enabled = true,
                CanConfirm = false
            };
            entry.Progression = BuildUpgradeProgression(
                progression,
                presentation.Type,
                presentation.Title);
            entry.Stats.Add(new BunkerSelectionStatModel("УРОВЕНЬ", $"{level} / {progression.MaxLevel}"));
            entry.Stats.Add(new BunkerSelectionStatModel("ВЛОЖЕНО", maxed
                ? "MAX"
                : $"{progression.GetInvestedGold(presentation.Type)} / {cost}"));
            AddVisibleEntry(model, entry, presentation.RequiredStationLevel);
        });
        FinalizeUnlockPresentation(model);
        return model;
    }

    private BunkerSelectionWindowModel BuildAnomalies()
    {
        int stationLevel = GetStationLevel(BunkerStationId.Anomaly);
        AnomalyMetaProgressionManager progression =
            AnomalyMetaProgressionManager.EnsureExists();
        var model = NewModel(
            "СТАБИЛИЗАЦИЯ АНОМАЛИЙ",
            "ДОСТУПНЫЕ СТАБИЛИЗАТОРЫ",
            "ВЫБЕРИТЕ СТАБИЛИЗАТОР",
            "ВЫБРАТЬ",
            BunkerStationId.Anomaly);
        AnomalyStabilizerData current = RunSelectionManager.Instance?.SelectedAnomalyStabilizer;
        model.SelectedId = current != null ? current.Id : null;

        AddNonNull(anomalies, anomaly =>
        {
            bool unlocked = BunkerSelectionUnlockRules.IsVisible(
                stationLevel,
                anomaly.RequiredStationLevel);
            var entry = new BunkerSelectionEntryModel
            {
                Id = anomaly.Id,
                DisplayName = anomaly.DisplayName,
                Category = "СТАБИЛИЗАТОР",
                Feature = GetAnomalyEffect(
                    anomaly,
                    progression.GetEffectValue(anomaly)),
                Description = anomaly.Description,
                Locked = !unlocked,
                LockReason = unlocked
                    ? null
                    : $"ТРЕБУЕТСЯ УРОВЕНЬ СТАНЦИИ {anomaly.RequiredStationLevel}",
                CanConfirm = unlocked
            };
            entry.Progression = BuildAnomalyProgression(progression, anomaly, unlocked);
            entry.Stats.Add(new BunkerSelectionStatModel("ТРЕБОВАНИЕ", $"LV {anomaly.RequiredStationLevel}"));
            AddVisibleEntry(model, entry, anomaly.RequiredStationLevel);
        });
        FinalizeUnlockPresentation(model);
        return model;
    }

    private BunkerSelectionWindowModel NewModel(
        string title,
        string section,
        string empty,
        string confirm,
        BunkerStationId stationId)
    {
        return new BunkerSelectionWindowModel
        {
            Title = title,
            SectionTitle = section,
            EmptyText = empty,
            ConfirmText = confirm,
            Station = BuildStationProgress(stationId)
        };
    }

    private static BunkerStationProgressModel BuildStationProgress(BunkerStationId stationId)
    {
        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service == null || !service.TryGetData(stationId, out BunkerStationProgressionData data))
            return null;

        int level = service.GetLevel(stationId);
        int cost = service.GetUpgradeCost(stationId);
        return new BunkerStationProgressModel
        {
            TargetId = $"station:{stationId}",
            Title = data.DisplayName,
            LevelPrefix = "УРОВЕНЬ СТАНЦИИ",
            Level = level,
            MaxLevel = data.MaxLevel,
            Progress = service.GetInvestedGold(stationId),
            RequiredProgress = cost,
            Cost = cost,
            AvailableCurrency = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0,
            ContextText = string.Empty,
            SupportsPartialInvestment = true,
            ButtonText = "УЛУЧШИТЬ СТАНЦИЮ",
            CanUpgrade = () => BunkerStationProgressionService.Instance != null &&
                BunkerStationProgressionService.Instance.CanInvest(stationId),
            Invest = amount =>
            {
                if (BunkerStationProgressionService.Instance != null)
                    BunkerStationProgressionService.Instance.TryInvestGold(stationId, amount, out _);
            }
        };
    }

    private static void AddVisibleEntry(
        BunkerSelectionWindowModel model,
        BunkerSelectionEntryModel entry,
        int requiredStationLevel)
    {
        if (model == null || entry == null)
            return;
        int required = BunkerSelectionUnlockRules.NormalizeRequiredLevel(
            requiredStationLevel);
        entry.RequiredStationLevel = required;
        model.Unlocks.Add(new BunkerSelectionUnlockModel(
            entry.DisplayName,
            required));
        int stationLevel = model.Station != null ? model.Station.Level : 1;
        if (BunkerSelectionUnlockRules.IsVisible(stationLevel, required))
            model.Entries.Add(entry);
    }

    private static void FinalizeUnlockPresentation(BunkerSelectionWindowModel model)
    {
        if (model?.Station == null)
            return;
        model.Station.ContextText = BunkerSelectionUnlockRules.BuildNextUnlockText(
            model.Unlocks,
            model.Station.Level,
            model.Station.MaxLevel);
    }

    private BunkerProgressionModel BuildUpgradeProgression(
        MetaProgressionManager progression,
        MetaUpgradeType type,
        string title)
    {
        int level = progression.GetLevel(type);
        int cap = progression.GetCurrentLevelCap();
        int cost = progression.GetUpgradeCost(type);
        bool capped = level < progression.MaxLevel && level >= cap;
        return new BunkerProgressionModel
        {
            TargetId = $"upgrade:{type}",
            Title = title,
            Level = level,
            MaxLevel = progression.MaxLevel,
            Cost = cost,
            Progress = progression.GetInvestedGold(type),
            RequiredProgress = cost,
            AvailableCurrency = CurrencyManager.Instance != null
                ? CurrencyManager.Instance.TotalGold : 0,
            BonusText = GetUpgradeBonus(type, level),
            ContextText = level >= progression.MaxLevel
                ? $"ТЕКУЩИЙ ЛИМИТ: {cap}"
                : $"СЛЕДУЮЩИЙ: {GetUpgradeBonus(type, level + 1)}\nТЕКУЩИЙ ЛИМИТ: {cap}",
            Locked = capped,
            SupportsPartialInvestment = true,
            LockReason = capped
                ? $"ТРЕБУЕТСЯ УРОВЕНЬ СТАНЦИИ {GetRequiredStationLevel(BunkerStationId.Upgrades, level + 1, progression.MaxLevel)}"
                : null,
            CanUpgrade = () => MetaProgressionManager.Instance != null &&
                MetaProgressionManager.Instance.CanInvest(type),
            Invest = amount =>
            {
                MetaProgressionManager manager = MetaProgressionManager.EnsureExists();
                int oldLevel = manager.GetLevel(type);
                if (manager.TryInvestGold(type, amount, out _) &&
                    manager.GetLevel(type) > oldLevel)
                    AudioService.Instance?.Play(AudioCueId.Purchase);
            }
        };
    }

    private BunkerProgressionModel BuildAnomalyProgression(
        AnomalyMetaProgressionManager progression,
        AnomalyStabilizerData anomaly,
        bool contentUnlocked)
    {
        int level = progression.GetLevel(anomaly);
        int cap = progression.GetCurrentLevelCap(anomaly);
        int cost = anomaly.GetMetaUpgradeCost(level);
        bool capped = level < anomaly.MaxMetaLevel && level >= cap;
        bool locked = !contentUnlocked || capped ||
            (level < anomaly.MaxMetaLevel && cost <= 0);
        string reason = !contentUnlocked
            ? $"ТРЕБУЕТСЯ УРОВЕНЬ СТАНЦИИ {anomaly.RequiredStationLevel}"
            : capped
                ? $"ТРЕБУЕТСЯ УРОВЕНЬ СТАНЦИИ {GetRequiredStationLevel(BunkerStationId.Anomaly, level + 1, anomaly.MaxMetaLevel)}"
                : level < anomaly.MaxMetaLevel && cost <= 0
                    ? "ПРОГРЕССИЯ НЕДОСТУПНА"
                    : null;
        return new BunkerProgressionModel
        {
            TargetId = $"anomaly:{anomaly.Id}",
            Title = anomaly.DisplayName,
            Level = level,
            MaxLevel = anomaly.MaxMetaLevel,
            Cost = cost,
            Progress = progression.GetInvestedGold(anomaly),
            RequiredProgress = cost,
            AvailableCurrency = CurrencyManager.Instance != null
                ? CurrencyManager.Instance.TotalGold : 0,
            BonusText = GetAnomalyEffect(anomaly, progression.GetEffectValue(anomaly)),
            ContextText = level >= anomaly.MaxMetaLevel
                ? $"ТЕКУЩИЙ ЛИМИТ: {cap}"
                : $"СЛЕДУЮЩИЙ: {GetAnomalyEffect(anomaly, anomaly.GetMetaEffectValue(level + 1))}\nТЕКУЩИЙ ЛИМИТ: {cap}",
            Locked = locked,
            SupportsPartialInvestment = true,
            LockReason = reason,
            CanUpgrade = () => AnomalyMetaProgressionManager.Instance != null &&
                AnomalyMetaProgressionManager.Instance.CanInvest(anomaly),
            Invest = amount =>
            {
                AnomalyMetaProgressionManager manager =
                    AnomalyMetaProgressionManager.EnsureExists();
                int oldLevel = manager.GetLevel(anomaly);
                if (manager.TryInvestGold(anomaly, amount, out _) &&
                    manager.GetLevel(anomaly) > oldLevel)
                    AudioService.Instance?.Play(AudioCueId.Purchase);
            }
        };
    }

    private static int GetRequiredStationLevel(
        BunkerStationId stationId,
        int targetLevel,
        int maxLevel)
    {
        for (int stationLevel = 1; stationLevel <= 3; stationLevel++)
        {
            if (BunkerItemProgressionRules.GetLevelCap(
                stationId,
                stationLevel,
                maxLevel) >= targetLevel)
                return stationLevel;
        }
        return 3;
    }

    private static bool IsUnlocked(UnlockableContentData data)
    {
        return data == null || UnlockProgressService.IsUnlockedNow(data);
    }

    private static string GetLockReason(UnlockableContentData data)
    {
        if (data == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(data.lockedDescription))
            return data.lockedDescription;
        UnlockConditionData condition = data.condition;
        if (condition == null)
            return "УСЛОВИЕ ОТКРЫТИЯ НЕ ЗАДАНО";
        return condition.type == UnlockConditionType.StationLevelRequirement
            ? $"ТРЕБУЕТСЯ {GetStationName(condition.stationId)} LV {Mathf.Max(1, condition.requiredAmount)}"
            : $"ПРОГРЕСС: {GetUnlockProgress(data)} / {Mathf.Max(1, condition.requiredAmount)}";
    }

    private static int GetUnlockProgress(UnlockableContentData data)
    {
        return UnlockProgressService.Instance != null
            ? UnlockProgressService.Instance.GetProgress(data)
            : 0;
    }

    private static string GetStationName(BunkerStationId stationId)
    {
        return stationId switch
        {
            BunkerStationId.Character => "СТАНЦИЯ ПЕРСОНАЖЕЙ",
            BunkerStationId.Weapon => "ОРУЖЕЙНАЯ СТАНЦИЯ",
            BunkerStationId.Upgrades => "СТАНЦИЯ УЛУЧШЕНИЙ",
            BunkerStationId.Anomaly => "СТАНЦИЯ АНОМАЛИЙ",
            _ => "СТАНЦИЯ"
        };
    }

    private static int GetStationLevel(BunkerStationId id)
    {
        return BunkerStationProgressionService.Instance != null
            ? BunkerStationProgressionService.Instance.GetLevel(id)
            : BunkerStationProgressionService.GetStoredLevel(id);
    }

    private static string GetUpgradeBonus(MetaUpgradeType type, int level)
    {
        return type switch
        {
            MetaUpgradeType.Hp => $"ТЕКУЩИЙ БОНУС: +{level} HP",
            MetaUpgradeType.Damage => $"ТЕКУЩИЙ БОНУС: +{level * 5}% УРОНА",
            MetaUpgradeType.MoveSpeed => $"ТЕКУЩИЙ БОНУС: +{level * 3}% СКОРОСТИ",
            MetaUpgradeType.XpGain => $"ТЕКУЩИЙ БОНУС: +{level * 5}% ОПЫТА",
            MetaUpgradeType.GoldGain => $"ТЕКУЩИЙ БОНУС: +{level * 10}% ЗОЛОТА",
            MetaUpgradeType.PickupRadius => $"ТЕКУЩИЙ БОНУС: +{level * 5}% РАДИУСА",
            _ => string.Empty
        };
    }

    private static string GetAnomalyEffect(AnomalyStabilizerData anomaly)
    {
        return GetAnomalyEffect(anomaly, anomaly.Value);
    }

    private static string GetAnomalyEffect(AnomalyStabilizerData anomaly, float value)
    {
        string label = anomaly.EffectType switch
        {
            AnomalyStabilizerEffectType.ZoneSize => "РАЗМЕР ЗОНЫ",
            AnomalyStabilizerEffectType.GoldInsideAnomaly => "ЗОЛОТО В АНОМАЛИИ",
            AnomalyStabilizerEffectType.StasisPlayerEffect => "ЭФФЕКТ СТАЗИСА",
            AnomalyStabilizerEffectType.GravityPlayerForce => "СИЛА ГРАВИТАЦИИ",
            _ => "ЭФФЕКТ"
        };
        return $"{label}: {value:0.##}";
    }

    private CharacterData FindByName(CharacterData[] source, string id)
    {
        if (source == null)
            return null;
        return Array.Find(source, value => value != null && value.name == id);
    }

    private WeaponData FindByName(WeaponData[] source, string id)
    {
        if (source == null)
            return null;
        return Array.Find(source, value => value != null && value.name == id);
    }

    private AnomalyStabilizerData FindAnomaly(string id)
    {
        if (anomalies == null)
            return null;
        return Array.Find(anomalies, value => value != null && value.Id == id);
    }

    private static void AddNonNull<T>(IEnumerable<T> values, Action<T> add) where T : class
    {
        if (values == null)
            return;
        foreach (T value in values)
        {
            if (value != null)
                add(value);
        }
    }

    private void HandleStationLevelChanged(BunkerStationId id, int level) => changed?.Invoke();
    private void HandleStationInvestmentChanged(BunkerStationId id, int value) => changed?.Invoke();
    private void HandleGoldChanged(int value) => changed?.Invoke();
    private void HandleMetaChanged() => changed?.Invoke();
}
