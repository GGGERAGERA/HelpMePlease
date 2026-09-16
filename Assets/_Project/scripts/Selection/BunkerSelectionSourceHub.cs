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

    public CharacterData GetDefaultCharacter()
    {
        if (characters == null)
            return null;

        foreach (CharacterData character in characters)
        {
            if (character != null && character.ProductionPrefab != null &&
                IsUnlocked(character.unlockData))
                return character;
        }

        return null;
    }

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
                CharacterData character = FindCharacter(characters, entryId);
                if (character != null && IsUnlocked(character.unlockData) && RunSelectionManager.Instance != null)
                {
                    RunSelectionManager.Instance.SelectCharacter(character);
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
            LocalizationService.Instance.Get("bunker.select_subject"),
            LocalizationService.Instance.Get("bunker.subjects"),
            LocalizationService.Instance.Get("bunker.select_subject"),
            LocalizationService.Instance.Get("bunker.select"),
            BunkerStationId.Character);
        CharacterData current = RunSelectionManager.Instance?.SelectedCharacter;
        model.SelectedId = current != null ? current.Id.ToString() : null;

        AddNonNull(characters, character =>
        {
            int requiredLevel = BunkerSelectionUnlockRules.GetRequiredStationLevel(
                character.unlockData,
                BunkerStationId.Character);
            bool unlocked = IsUnlocked(character.unlockData);
            var entry = BuildCharacterEntry(character, unlocked);
            entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.health"), character.maxHealth.ToString("0")));
            entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.speed"), character.moveSpeed.ToString("0.#")));
            AddVisibleEntry(model, entry, requiredLevel);
        });
        FinalizeUnlockPresentation(model);
        return model;
    }

    public static BunkerSelectionEntryModel BuildCharacterEntryForTests(
        CharacterData character,
        bool selected)
    {
        return BuildCharacterEntry(character, true);
    }

    private static BunkerSelectionEntryModel BuildCharacterEntry(
        CharacterData character,
        bool unlocked)
    {
        return new BunkerSelectionEntryModel
        {
            Id = character.Id.ToString(),
            DisplayName = character.LocalizedName,
            Category = character.LocalizedCombatTypeDisplayName.ToUpperInvariant(),
            Icon = character.Portrait,
            CharacterVisual = character.GameplayIcon,
            IsCharacter = true,
            Feature = character.LocalizedCombatTypeDescription,
            Description = character.LocalizedDescription,
            Locked = !unlocked,
            LockReason = GetLockReason(character.unlockData),
            CanConfirm = unlocked
        };
    }

    private BunkerSelectionWindowModel BuildWeapons()
    {
        var model = NewModel(
            LocalizationService.Instance.Get("bunker.modules"),
            LocalizationService.Instance.Get("bunker.modules_unlocked"),
            string.Empty,
            string.Empty,
            BunkerStationId.Weapon);
        model.CloseOnConfirm = false;
        model.ShowConfirmButton = false;
        MetaProgressionManager progression = MetaProgressionManager.EnsureExists();
        progression.ReloadFromStorage();
        AddOrbitalModuleCard(model, OrbitalModuleKind.Pistol, LocalizationService.Instance.Get("bunker.module_name.pistol"),
            LocalizationService.Instance.Get("bunker.module_auto"),
            LocalizationService.Instance.Get("bunker.module_auto_desc"), progression);
        AddOrbitalModuleCard(model, OrbitalModuleKind.LaserSword, LocalizationService.Instance.Get("bunker.module_name.laser_sword"),
            LocalizationService.Instance.Get("bunker.module_contact"),
            LocalizationService.Instance.Get("bunker.module_contact_desc"), progression);
        AddOrbitalModuleCard(model, OrbitalModuleKind.ImpulseGun, LocalizationService.Instance.Get("bunker.module_name.impulse_gun"),
            LocalizationService.Instance.Get("bunker.module_impulse"),
            LocalizationService.Instance.Get("bunker.module_impulse_desc"), progression);
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
                ? string.Format(LocalizationService.Instance.Get("bunker.cap"), cap)
                : string.Format(LocalizationService.Instance.Get("bunker.next_damage"), MetaProgressionManager.GetOrbitalModuleDamageMultiplier(level + 1)) +
                  string.Format(LocalizationService.Instance.Get("bunker.cap"), cap),
            Locked = capped || unavailable,
            SupportsPartialInvestment = true,
            LockReason = capped
                ? string.Format(LocalizationService.Instance.Get("bunker.required_weapon_station"), GetRequiredStationLevel(BunkerStationId.Weapon, level + 1, progression.MaxLevel))
                : unavailable ? LocalizationService.Instance.Get("bunker.progress_unavailable") : null,
            ButtonText = LocalizationService.Instance.Get("bunker.upgrade_module"),
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
        return string.Format(LocalizationService.Instance.Get("bunker.module_damage"), MetaProgressionManager.GetOrbitalModuleDamageMultiplier(level));
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
            LocalizationService.Instance.Get("bunker.level"), $"{level} / {progression.MaxLevel}"));
        entry.Stats.Add(new BunkerSelectionStatModel(
            LocalizationService.Instance.Get("bunker.damage"), damage.ToString("0.#")));
        switch (kind)
        {
            case OrbitalModuleKind.Pistol:
                entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.range"), "8"));
                entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.interval"), LocalizationService.Instance.Get("bunker.interval_auto")));
                break;
            case OrbitalModuleKind.LaserSword:
                entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.radius"), "0.75"));
                entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.interval"), LocalizationService.Instance.Get("bunker.interval_contact")));
                break;
            case OrbitalModuleKind.ImpulseGun:
                entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.range"), "5"));
                entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.interval"), LocalizationService.Instance.Get("bunker.interval_impulse")));
                break;
        }
    }

    private static Sprite ResolveOrbitalModuleIcon(
        OrbitalModuleKind kind,
        out Color color)
    {
        var presentation = OrbitalRewardIconResolver.Resolve(kind);
        color = presentation.ImageTint;
        return presentation.Sprite;
    }

    private BunkerSelectionWindowModel BuildUpgrades()
    {
        MetaProgressionManager progression = MetaProgressionManager.EnsureExists();
        progression.ReloadFromStorage();
        var model = NewModel(
            LocalizationService.Instance.Get("bunker.upgrades"),
            LocalizationService.Instance.Get("bunker.permanent_upgrades"),
            LocalizationService.Instance.Get("bunker.select_upgrade"),
            LocalizationService.Instance.Get("bunker.hold_upgrade"),
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
                DisplayName = LocalizationService.Instance.Get(presentation.Title),
                Category = string.IsNullOrWhiteSpace(presentation.Category)
                    ? LocalizationService.Instance.Get("bunker.meta_upgrade")
                    : LocalizationService.Instance.Get(presentation.Category),
                Feature = GetUpgradeBonus(presentation.Type, level),
                Description = LocalizationService.Instance.Get(presentation.Description),
                Enabled = true,
                CanConfirm = false
            };
            entry.Progression = BuildUpgradeProgression(
                progression,
                presentation.Type,
                LocalizationService.Instance.Get(presentation.Title));
            entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.level"), $"{level} / {progression.MaxLevel}"));
            entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.invested"), maxed
                ? LocalizationService.Instance.Get("bunker.max")
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
            LocalizationService.Instance.Get("bunker.stabilization"),
            LocalizationService.Instance.Get("bunker.stabilizers"),
            LocalizationService.Instance.Get("bunker.select_stabilizer"),
            LocalizationService.Instance.Get("bunker.select"),
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
                Category = LocalizationService.Instance.Get("bunker.stabilizer"),
                Feature = GetAnomalyEffect(
                    anomaly,
                    progression.GetEffectValue(anomaly)),
                Description = anomaly.Description,
                Locked = !unlocked,
                LockReason = unlocked
                    ? null
                    : string.Format(LocalizationService.Instance.Get("bunker.required_station"), anomaly.RequiredStationLevel),
                CanConfirm = unlocked
            };
            entry.Progression = BuildAnomalyProgression(progression, anomaly, unlocked);
            entry.Stats.Add(new BunkerSelectionStatModel(LocalizationService.Instance.Get("bunker.requirement"), string.Format(LocalizationService.Instance.Get("bunker.require_level"), anomaly.RequiredStationLevel)));
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
            LevelPrefix = LocalizationService.Instance.Get("bunker.station_level"),
            Level = level,
            MaxLevel = data.MaxLevel,
            Progress = service.GetInvestedGold(stationId),
            RequiredProgress = cost,
            Cost = cost,
            AvailableCurrency = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0,
            ContextText = string.Empty,
            SupportsPartialInvestment = true,
            ButtonText = LocalizationService.Instance.Get("bunker.upgrade_station"),
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
                ? string.Format(LocalizationService.Instance.Get("bunker.cap"), cap)
                : string.Format(LocalizationService.Instance.Get("bunker.next_bonus"), GetUpgradeBonus(type, level + 1), cap),
            Locked = capped,
            SupportsPartialInvestment = true,
            LockReason = capped
                ? string.Format(LocalizationService.Instance.Get("bunker.required_upgrade_station"), GetRequiredStationLevel(BunkerStationId.Upgrades, level + 1, progression.MaxLevel))
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
            ? string.Format(LocalizationService.Instance.Get("bunker.required_station"), anomaly.RequiredStationLevel)
            : capped
                ? string.Format(LocalizationService.Instance.Get("bunker.required_anomaly_station"), GetRequiredStationLevel(BunkerStationId.Anomaly, level + 1, anomaly.MaxMetaLevel))
                : level < anomaly.MaxMetaLevel && cost <= 0
                    ? LocalizationService.Instance.Get("bunker.progress_unavailable")
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
                ? string.Format(LocalizationService.Instance.Get("bunker.cap"), cap)
                : string.Format(LocalizationService.Instance.Get("bunker.next_anomaly"), GetAnomalyEffect(anomaly, anomaly.GetMetaEffectValue(level + 1)), cap),
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
            return LocalizationService.Instance.Get("bunker.unlock_unavailable");
        return condition.type == UnlockConditionType.StationLevelRequirement
            ? string.Format(LocalizationService.Instance.Get("bunker.require_station_named"), GetStationName(condition.stationId), Mathf.Max(1, condition.requiredAmount))
            : string.Format(LocalizationService.Instance.Get("bunker.unlock_progress"), GetUnlockProgress(data), Mathf.Max(1, condition.requiredAmount));
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
            BunkerStationId.Character => LocalizationService.Instance.Get("bunker.station_character"),
            BunkerStationId.Weapon => LocalizationService.Instance.Get("bunker.station_weapon"),
            BunkerStationId.Upgrades => LocalizationService.Instance.Get("bunker.station_upgrades"),
            BunkerStationId.Anomaly => LocalizationService.Instance.Get("bunker.station_anomaly"),
            _ => LocalizationService.Instance.Get("bunker.station")
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
            MetaUpgradeType.Hp => string.Format(LocalizationService.Instance.Get("bunker.bonus_hp"), level),
            MetaUpgradeType.Damage => string.Format(LocalizationService.Instance.Get("bunker.bonus_damage"), level * 5),
            MetaUpgradeType.MoveSpeed => string.Format(LocalizationService.Instance.Get("bunker.bonus_speed"), level * 3),
            MetaUpgradeType.XpGain => string.Format(LocalizationService.Instance.Get("bunker.bonus_xp"), level * 5),
            MetaUpgradeType.GoldGain => string.Format(LocalizationService.Instance.Get("bunker.bonus_gold"), level * 10),
            MetaUpgradeType.PickupRadius => string.Format(LocalizationService.Instance.Get("bunker.bonus_pickup"), level * 5),
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
            AnomalyStabilizerEffectType.ZoneSize => LocalizationService.Instance.Get("bunker.zone_size"),
            AnomalyStabilizerEffectType.GoldInsideAnomaly => LocalizationService.Instance.Get("bunker.anomaly_gold"),
            AnomalyStabilizerEffectType.StasisPlayerEffect => LocalizationService.Instance.Get("bunker.stasis_effect"),
            AnomalyStabilizerEffectType.GravityPlayerForce => LocalizationService.Instance.Get("bunker.gravity_force"),
            _ => LocalizationService.Instance.Get("bunker.effect")
        };
        return $"{label}: {value:0.##}";
    }

    private CharacterData FindByName(CharacterData[] source, string id)
    {
        if (source == null)
            return null;
        return Array.Find(source, value => value != null && value.name == id);
    }

    private CharacterData FindCharacter(CharacterData[] source, string id)
    {
        if (source == null || string.IsNullOrWhiteSpace(id))
            return null;
        if (Enum.TryParse(id, ignoreCase: true, out CharacterId characterId) &&
            characterId != CharacterId.None)
        {
            CharacterData byId = Array.Find(source, value =>
                value != null && value.Id == characterId);
            if (byId != null)
                return byId;
        }

        // Backward compatibility for old UI/debug callers that used asset names
        // or legacy display identifiers before CharacterId existed.
        return Array.Find(source, value => value != null &&
            (value.name == id || value.characterName == id));
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
