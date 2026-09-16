using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public enum CombatFeelGroup
{
    Global,
    Shot,
    Projectile,
    Hit,
    Target,
    Kill,
    Camera,
    Time,
    Crowd,
    Experimental
}

public enum CombatFeelParameter
{
    MasterIntensity,
    DamageInfluence, MinFeedback, MaxFeedback, CritMultiplier,
    BasicWeight, EliteWeight, BossWeight, DirectionInfluence,
    DirectionRandomness, TangentialSpread,

    ShotInputDelay, FireResponseOffset, ShotAnimationLead, ShotFxLead,
    FireRateFeelMultiplier, CadenceVariance, BurstRhythmBias, FirstShotEmphasis,
    WeaponKickDistance, WeaponKickDuration, WeaponReturnDuration,
    WeaponOvershoot, WeaponSettleDuration, WeaponKickRotation,
    WeaponKickRandomness, WeaponScalePunchX, WeaponScalePunchY,
    PlayerVisualRecoil, PlayerVisualRecoilDuration, PlayerSquash,
    PlayerStretch, PlayerRotationKick, PlayerReturnSpring,
    PhysicalRecoil, PlayerRecoilVelocity, MovementDamp, MovementDampDuration,
    MuzzleScale, MuzzleDuration, MuzzleBrightness, MuzzleRandomRotation,
    MuzzleStretch, MuzzleSparks, MuzzleDirectionality,
    MuzzleRingScale, MuzzleRingDuration, MuzzleRingOpacity,

    ProjectileScale, ProjectileStretch, ProjectileSquash, ProjectileAlign,
    ProjectileSpin, ProjectileGlow, ProjectileBrightness, ProjectilePulse,
    ProjectilePulseSpeed, TrailWidth, TrailLength, TrailLifetime,
    TrailOpacity, TrailTaper, SpeedIllusion, ForwardVisualOffset,
    InitialStreakLength, InitialStreakLifetime,

    ImpactScale, ImpactLifetime, ImpactBrightness, ImpactRotationRandomness,
    ImpactDirectionality, ImpactRingSize, ImpactRingSpeed, ImpactRingLifetime,
    ImpactSparks, ImpactDebris, ForwardSpray, BackSpray, SideSpray,
    FlashStrength, FlashAttack, FlashHold, FlashRelease, BrightnessPunch,
    SaturationPunch, ContrastPunch,
    PopupInitialScale, PopupScalePunch, PopupRiseSpeed, PopupRiseDistance,
    PopupLifetime, PopupFadeDelay, PopupFadeDuration, PopupHorizontalDrift,
    PopupDriftRandomness, PopupRotation, PopupRotationRandomness,
    CritPopupScale, CritPopupRise, CritPopupPunch, CritPopupLifetime,
    PopupDelay,

    VisualHitPush, HitPushDuration, HitReturnDuration, HitOvershoot,
    HitSquashX, HitStretchY, HitSquashDuration, HitRestoreDuration,
    HitRotation, HitRotationRandomness, HitRotationReturn,
    WobbleStrength, WobbleFrequency, WobbleDamping, VisualStagger,
    PhysicalStagger,

    DeathPush, DeathRotation, DeathSquash, DeathStretch, DeathScalePunch,
    DeathHold, DeathFlash, DeathFlashDuration, DeathFade, DeathShrink,
    DeathExpand, DeathParticleAmount, DeathParticleScale, DeathParticleSpeed,
    DeathParticleLifetime, DeathDirectionality, DeathRingSize, DeathRingSpeed,
    DeathRingOpacity, GhostEnabled, GhostLifetime, GhostPush, GhostScale,
    GhostFade, OverkillThreshold, OverkillFeedback, OverkillParticles,
    OverkillDeathPush,

    ShotShakeAmplitude, ShotShakeFrequency, ShotShakeDuration,
    HitShakeAmplitude, HitShakeFrequency, HitShakeDuration,
    KillShakeAmplitude, KillShakeDuration, DirectionalKickDistance,
    DirectionalKickDuration, DirectionalReturn, DirectionalOvershoot,
    ShotZoomPunch, HitZoomPunch, KillZoomPunch, ZoomAttack, ZoomReturn,
    CameraSpring, CameraDamping, CameraOvershoot, TowardShot,
    AwayFromShot, TowardHit,

    ShotFreeze, ShotSlowdown, ShotSlowdownScale, ShotRecovery,
    HitFreeze, HitSlowdown, HitSlowdownScale, HitRecovery,
    KillFreeze, KillSlowdown, KillSlowdownScale, KillRecovery,
    EliteTimeEmphasis, BossTimeEmphasis, LocalEnemyFreeze, GlobalFreeze,
    FreezeBlend,

    CrowdRadius, CrowdStrength, CrowdFalloff, CrowdMaxTargets,
    CrowdFlash, CrowdWobble, KillShockwave,
    RapidKillWindow, RapidKillGain, RapidKillMax, RapidKillDecay,

    HitVignette, KillVignette, ScreenBrightness, ScreenSaturation,
    LocalHitLight, LocalHitRadius, LocalHitIntensity, LocalHitLifetime,
    PreFireDuration, PreFireGlow, PreFireCompression, PreFireAimEmphasis,

    MouseLookAhead, LookAheadDistance, LookAheadResponse, LookAheadReturn,
    LookAheadDeadZone, LookAheadCurve, HorizontalStrength, VerticalStrength,
    MaxScreenFraction
}

public readonly struct CombatFeelConsumerDeclaration
{
    public readonly string RuntimePath;
    public readonly string Target;
    public CombatFeelConsumerDeclaration(string runtimePath, string target)
    { RuntimePath = runtimePath; Target = target; }
}

public static class CombatFeelConsumerRegistry
{
    public const int ParametersBeforeAudit = 209;
    private static readonly HashSet<CombatFeelParameter> removed = new()
    {
        CombatFeelParameter.TangentialSpread,
        CombatFeelParameter.ShotInputDelay, CombatFeelParameter.FireResponseOffset,
        CombatFeelParameter.ShotAnimationLead, CombatFeelParameter.ShotFxLead,
        CombatFeelParameter.FireRateFeelMultiplier, CombatFeelParameter.CadenceVariance,
        CombatFeelParameter.BurstRhythmBias, CombatFeelParameter.MovementDampDuration,
        CombatFeelParameter.MuzzleDirectionality, CombatFeelParameter.MuzzleRingScale,
        CombatFeelParameter.MuzzleRingDuration, CombatFeelParameter.MuzzleRingOpacity,
        CombatFeelParameter.ProjectileAlign, CombatFeelParameter.ProjectileGlow,
        CombatFeelParameter.TrailLength, CombatFeelParameter.TrailTaper,
        CombatFeelParameter.ImpactDirectionality, CombatFeelParameter.ImpactRingSize,
        CombatFeelParameter.ImpactRingSpeed, CombatFeelParameter.ImpactRingLifetime,
        CombatFeelParameter.ImpactDebris, CombatFeelParameter.ForwardSpray,
        CombatFeelParameter.BackSpray, CombatFeelParameter.SideSpray,
        CombatFeelParameter.FlashStrength, CombatFeelParameter.FlashAttack,
        CombatFeelParameter.FlashHold, CombatFeelParameter.FlashRelease,
        CombatFeelParameter.BrightnessPunch, CombatFeelParameter.SaturationPunch,
        CombatFeelParameter.ContrastPunch, CombatFeelParameter.PopupRiseDistance,
        CombatFeelParameter.VisualStagger, CombatFeelParameter.PhysicalStagger,
        CombatFeelParameter.DeathFlash, CombatFeelParameter.DeathFlashDuration,
        CombatFeelParameter.DeathShrink, CombatFeelParameter.DeathExpand,
        CombatFeelParameter.DeathParticleAmount, CombatFeelParameter.DeathParticleScale,
        CombatFeelParameter.DeathParticleSpeed, CombatFeelParameter.DeathParticleLifetime,
        CombatFeelParameter.DeathDirectionality, CombatFeelParameter.DeathRingSize,
        CombatFeelParameter.DeathRingSpeed, CombatFeelParameter.DeathRingOpacity,
        CombatFeelParameter.GhostPush, CombatFeelParameter.OverkillParticles,
        CombatFeelParameter.OverkillDeathPush, CombatFeelParameter.ShotShakeFrequency,
        CombatFeelParameter.HitShakeFrequency, CombatFeelParameter.CameraSpring,
        CombatFeelParameter.CameraDamping, CombatFeelParameter.CameraOvershoot,
        CombatFeelParameter.TowardShot, CombatFeelParameter.AwayFromShot,
        CombatFeelParameter.CrowdFlash, CombatFeelParameter.CrowdWobble,
        CombatFeelParameter.KillShockwave, CombatFeelParameter.HitVignette,
        CombatFeelParameter.KillVignette, CombatFeelParameter.ScreenBrightness,
        CombatFeelParameter.ScreenSaturation, CombatFeelParameter.LocalHitLight,
        CombatFeelParameter.LocalHitRadius, CombatFeelParameter.LocalHitIntensity,
        CombatFeelParameter.LocalHitLifetime, CombatFeelParameter.PreFireDuration,
        CombatFeelParameter.PreFireGlow, CombatFeelParameter.PreFireCompression,
        CombatFeelParameter.PreFireAimEmphasis
    };
    private static readonly HashSet<CombatFeelParameter> fixedBroken = new()
    {
        CombatFeelParameter.ImpactScale, CombatFeelParameter.ImpactLifetime,
        CombatFeelParameter.ImpactBrightness, CombatFeelParameter.ImpactRotationRandomness,
        CombatFeelParameter.ImpactSparks,
        CombatFeelParameter.WeaponKickDistance, CombatFeelParameter.WeaponKickDuration,
        CombatFeelParameter.WeaponReturnDuration, CombatFeelParameter.WeaponOvershoot,
        CombatFeelParameter.WeaponSettleDuration, CombatFeelParameter.WeaponKickRotation,
        CombatFeelParameter.WeaponKickRandomness, CombatFeelParameter.WeaponScalePunchX,
        CombatFeelParameter.WeaponScalePunchY, CombatFeelParameter.PlayerRotationKick,
        CombatFeelParameter.DirectionalKickDistance, CombatFeelParameter.DirectionalKickDuration,
        CombatFeelParameter.DirectionalReturn, CombatFeelParameter.DirectionalOvershoot,
        CombatFeelParameter.GlobalFreeze, CombatFeelParameter.MuzzleBrightness,
        CombatFeelParameter.ProjectileBrightness,
        CombatFeelParameter.ProjectileScale, CombatFeelParameter.ProjectileStretch,
        CombatFeelParameter.ProjectileSquash, CombatFeelParameter.ProjectilePulse,
        CombatFeelParameter.SpeedIllusion, CombatFeelParameter.InitialStreakLength,
        CombatFeelParameter.TrailWidth, CombatFeelParameter.TrailLifetime,
        CombatFeelParameter.TrailOpacity
    };

    public static int RemovedCount => removed.Count;
    public static int FixedBrokenCount => fixedBroken.Count;
    public static IReadOnlyCollection<CombatFeelParameter> RemovedParameters => removed;
    public static bool WasRemoved(CombatFeelParameter parameter) => removed.Contains(parameter);
    public static string GetAuditStatus(CombatFeelParameter parameter) =>
        removed.Contains(parameter)
            ? parameter == CombatFeelParameter.TrailLength ? "REDUNDANT" : "UNWIRED"
            : fixedBroken.Contains(parameter) ? "WORKING (FIXED)" : "WORKING";
    public static string GetRemovalReason(CombatFeelParameter parameter) =>
        parameter == CombatFeelParameter.TrailLength
            ? "REDUNDANT: дублировал TrailLifetime через тот же TrailRenderer.time."
            : "UNWIRED: отсутствовал runtime consumer вне settings/UI.";

    public static bool TryGet(CombatFeelParameter parameter, CombatFeelGroup group,
        out CombatFeelConsumerDeclaration declaration)
    {
        if (removed.Contains(parameter)) { declaration = default; return false; }
        string name = parameter.ToString();
        declaration = group switch
        {
            CombatFeelGroup.Projectile => new(
                "ProjectileFireBehaviour → CombatFeelProjectileVisual.Configure/Update",
                "отдельный visual child, SpriteRenderer или TrailRenderer снаряда"),
            CombatFeelGroup.Hit when name.StartsWith("Impact", StringComparison.Ordinal) => new(
                "WeaponHitResolver → WeaponFxPlayer.PlayHit → CombatFeelParticleOverride",
                "ParticleSystem эффекта попадания"),
            CombatFeelGroup.Hit => new(
                "EnemyHealth.ShowDamagePopup → DamagePopup.ConfigureFeel/UpdateFeel",
                "Transform и TextMeshPro цифры урона"),
            CombatFeelGroup.Target => new(
                "WeaponHitResolver → PhysicalCombatFeedbackRuntime.PlayEnemyHit/LateUpdate",
                "presentation root цели; gameplay root не перемещается"),
            CombatFeelGroup.Kill => new(
                "WeaponHitResolver/EnemyHealth.Die → PhysicalCombatFeedbackRuntime",
                "отделённый death visual или presentation root цели"),
            CombatFeelGroup.Camera when IsMouseLookAhead(parameter) => new(
                "CameraFollow.LateUpdate → screen-space Mouse Look-Ahead layer",
                "CameraFollow root presentation offset после normal follow"),
            CombatFeelGroup.Camera => new(
                "PhysicalCombatFeedbackRuntime → CameraShake debug layer/Camera orthographicSize",
                "presentation transform камеры или orthographic zoom"),
            CombatFeelGroup.Time => new(
                "PhysicalCombatFeedbackRuntime.RequestEventTime/UpdateHitStop",
                "Time.timeScale либо Animator.speed конкретной цели"),
            CombatFeelGroup.Crowd => new(
                "PhysicalCombatFeedbackRuntime.PlayCrowdResponse → PlayEnemyHit",
                "presentation roots соседних живых врагов"),
            CombatFeelGroup.Shot when name.StartsWith("Muzzle", StringComparison.Ordinal) => new(
                "WeaponFxPlayer.PlayFire → CombatFeelParticleOverride",
                "ParticleSystem muzzle-FX выбранного оружия"),
            CombatFeelGroup.Shot => new(
                "WeaponFxPlayer.PlayFire → PhysicalCombatFeedbackRuntime",
                "weapon/player presentation layer, Rigidbody2D, Camera или Time.timeScale"),
            _ => new(
                "WeaponHitResolver/PhysicalCombatFeedbackRuntime.GetImpactStrength",
                "коэффициент событий shot/hit/kill presentation pipeline")
        };
        return true;
    }

    public static bool IsGameplayAffecting(CombatFeelParameter parameter,
        CombatFeelGroup group) => group == CombatFeelGroup.Time || parameter is
        CombatFeelParameter.PhysicalRecoil or CombatFeelParameter.PlayerRecoilVelocity or
        CombatFeelParameter.MovementDamp or CombatFeelParameter.GlobalFreeze;

    public static bool IsMouseLookAhead(CombatFeelParameter parameter) => parameter is
        CombatFeelParameter.MouseLookAhead or CombatFeelParameter.LookAheadDistance or
        CombatFeelParameter.LookAheadResponse or CombatFeelParameter.LookAheadReturn or
        CombatFeelParameter.LookAheadDeadZone or CombatFeelParameter.LookAheadCurve or
        CombatFeelParameter.HorizontalStrength or CombatFeelParameter.VerticalStrength or
        CombatFeelParameter.MaxScreenFraction;
}

public readonly struct CombatFeelDescriptor
{
    public readonly CombatFeelParameter Parameter;
    public readonly CombatFeelGroup Group;
    public readonly string Name;
    public readonly string Tooltip;
    public readonly float Neutral;
    public readonly float Minimum;
    public readonly float Maximum;
    public readonly float Hard;
    public readonly bool Toggle;
    public readonly CombatFeelParameterMetadata Metadata;

    public CombatFeelDescriptor(
        CombatFeelParameter parameter, CombatFeelGroup group, string name,
        float neutral, float minimum, float maximum, float hard,
        string tooltip = null, bool toggle = false)
    {
        Metadata = CombatFeelParameterMetadata.Create(
            parameter, group, name, neutral, minimum, maximum, hard, tooltip, toggle);
        Parameter = parameter;
        Group = group;
        Name = Metadata.RussianName;
        Tooltip = Metadata.DescriptionRu;
        Neutral = neutral;
        Minimum = Metadata.Minimum;
        Maximum = Metadata.Maximum;
        Hard = hard;
        Toggle = toggle;
    }
}

public sealed class CombatFeelParameterMetadata
{
    private static readonly Dictionary<string, string> Words = new(StringComparer.Ordinal)
    {
        ["Master"]="Общая", ["Intensity"]="интенсивность", ["Damage"]="урон", ["Influence"]="влияние",
        ["Min"]="минимум", ["Max"]="максимум", ["Feedback"]="отдача", ["Crit"]="критическая",
        ["Multiplier"]="множитель", ["Basic"]="обычный враг", ["Elite"]="элитный враг", ["Boss"]="босс",
        ["Weight"]="вес", ["Direction"]="направление", ["Randomness"]="разброс", ["Tangential"]="боковой",
        ["Spread"]="разлёт", ["Shot"]="выстрела", ["Input"]="ввода", ["Delay"]="задержка",
        ["Fire"]="огня", ["Response"]="отклик", ["Offset"]="смещение", ["Animation"]="анимация",
        ["Lead"]="опережение", ["Fx"]="эффектов", ["Rate"]="темпа", ["Feel"]="ощущения",
        ["Cadence"]="каденс", ["Variance"]="вариативность", ["Burst"]="очереди", ["Rhythm"]="ритм",
        ["Bias"]="акцент", ["First"]="первого", ["Emphasis"]="акцент", ["Weapon"]="оружия",
        ["Kick"]="рывок", ["Distance"]="дистанция", ["Duration"]="длительность", ["Return"]="возврат",
        ["Overshoot"]="перелёт", ["Settle"]="успокоение", ["Rotation"]="поворот", ["Scale"]="масштаб",
        ["Punch"]="импульс", ["Player"]="игрока", ["Visual"]="визуальная", ["Recoil"]="отдача",
        ["Squash"]="сжатие", ["Stretch"]="растяжение", ["Spring"]="пружина", ["Physical"]="физическая",
        ["Velocity"]="скорость", ["Movement"]="движение", ["Damp"]="торможение", ["On"]="при",
        ["Muzzle"]="дульной вспышки", ["Brightness"]="яркость", ["Random"]="случайный", ["Sparks"]="искры",
        ["Directionality"]="направленность", ["Ring"]="кольцо", ["Opacity"]="прозрачность",
        ["Projectile"]="снаряда", ["Align"]="ориентация", ["Spin"]="вращение", ["Glow"]="свечение",
        ["Pulse"]="пульсация", ["Speed"]="скорость", ["Trail"]="следа", ["Width"]="ширина",
        ["Length"]="длина", ["Lifetime"]="время жизни", ["Taper"]="сужение", ["Illusion"]="иллюзия",
        ["Forward"]="вперёд", ["Initial"]="начальный", ["Streak"]="штрих", ["Impact"]="попадания",
        ["Size"]="размер", ["Debris"]="осколки", ["Back"]="назад", ["Side"]="в стороны",
        ["Spray"]="выброс", ["Flash"]="вспышка", ["Strength"]="сила", ["Attack"]="нарастание",
        ["Hold"]="удержание", ["Release"]="затухание", ["Saturation"]="насыщенность", ["Contrast"]="контраст",
        ["Popup"]="цифр урона", ["Rise"]="подъём", ["Fade"]="исчезновение", ["Horizontal"]="горизонтальный",
        ["Drift"]="дрейф", ["Spawn"]="появление", ["Hit"]="попадания", ["Push"]="толчок",
        ["Restore"]="восстановление", ["Wobble"]="качание", ["Frequency"]="частота", ["Damping"]="затухание",
        ["Stagger"]="ошеломление", ["Death"]="смерти", ["Disappear"]="исчезновением", ["Shrink"]="уменьшение",
        ["Expand"]="расширение", ["Particle"]="частиц", ["Amount"]="количество", ["Ghost"]="призрак",
        ["Enabled"]="включён", ["Overkill"]="оверкилл", ["Threshold"]="порог", ["Particles"]="частицы",
        ["Shake"]="тряска", ["Amplitude"]="амплитуда", ["Directional"]="направленный", ["Zoom"]="зум",
        ["Camera"]="камеры", ["Toward"]="к", ["Away"]="от", ["Freeze"]="стоп-кадр",
        ["Slowdown"]="замедление", ["Recovery"]="восстановление", ["Time"]="времени", ["Local"]="локальный",
        ["Enemy"]="врага", ["Global"]="глобальный", ["Blend"]="смешивание", ["Crowd"]="толпы",
        ["Radius"]="радиус", ["Falloff"]="спад", ["Targets"]="цели", ["Kill"]="убийства",
        ["Shockwave"]="ударная волна", ["Rapid"]="серии", ["Window"]="окно", ["Gain"]="усиление",
        ["Decay"]="спад", ["Vignette"]="виньетка", ["Screen"]="экрана", ["Light"]="свет",
        ["Pre"]="пред", ["Compression"]="сжатие", ["Aim"]="прицел", ["Across"]="поперёк",
        ["Along"]="вдоль", ["From"]="от", ["X"]="X", ["Y"]="Y", ["Normal"]="обычный",
        ["Mouse"]="мышью", ["Look"]="взгляд", ["Ahead"]="вперёд", ["Dead"]="мёртвая",
        ["Zone"]="зона", ["Curve"]="кривая", ["Horizontal"]="горизонтальная",
        ["Vertical"]="вертикальная", ["Fraction"]="доля"
    };

    public CombatFeelParameter Parameter { get; private set; }
    public CombatFeelGroup Group { get; private set; }
    public string TechnicalName { get; private set; }
    public string RussianName { get; private set; }
    public string DescriptionRu { get; private set; }
    public string Unit { get; private set; }
    public string ConsumerPath { get; private set; }
    public string ConsumerTarget { get; private set; }
    public string WhatToWatchRu { get; private set; }
    public string MinimumMeaningRu { get; private set; }
    public string MaximumMeaningRu { get; private set; }
    public bool GameplayAffecting { get; private set; }
    public string AuditStatus { get; private set; }
    public float Production { get; private set; }
    public float Neutral { get; private set; }
    public float AuthoredMinimum { get; private set; }
    public float AuthoredMaximum { get; private set; }
    public float Minimum { get; private set; }
    public float Maximum { get; private set; }
    public float SafeRandomMinimum { get; private set; }
    public float SafeRandomMaximum { get; private set; }
    public float DiagnosticExtreme { get; private set; }
    public bool Toggle { get; private set; }

    public static CombatFeelParameterMetadata Create(CombatFeelParameter parameter,
        CombatFeelGroup group, string englishName, float neutral, float minimum,
        float maximum, float hard, string authoredHint, bool toggle)
    {
        string technical = parameter.ToString();
        string russian = BuildRussianName(technical);
        string unit = GetUnit(technical, toggle);
        CombatFeelConsumerRegistry.TryGet(parameter, group,
            out CombatFeelConsumerDeclaration consumer);
        ExpandRange(technical, neutral, minimum, maximum, toggle,
            out float experimentMin, out float experimentMax);
        float safeA = toggle ? 0f : Mathf.Lerp(neutral, hard, .15f);
        float safeB = toggle ? 1f : Mathf.Lerp(neutral, hard, .85f);
        float diagnosticExtreme = toggle ? 1f : hard > neutral ? experimentMax
            : hard < neutral ? experimentMin
            : experimentMax - neutral >= neutral - experimentMin
                ? experimentMax : experimentMin;
        return new CombatFeelParameterMetadata
        {
            Parameter = parameter, Group = group, TechnicalName = technical,
            RussianName = russian, Unit = unit, Production = neutral, Neutral = neutral,
            ConsumerPath = consumer.RuntimePath, ConsumerTarget = consumer.Target,
            GameplayAffecting = CombatFeelConsumerRegistry.IsGameplayAffecting(parameter, group),
            AuditStatus = CombatFeelConsumerRegistry.GetAuditStatus(parameter),
            WhatToWatchRu = BuildWhatToWatch(technical, group),
            MinimumMeaningRu = BuildEdgeMeaning(technical, group, false),
            MaximumMeaningRu = BuildEdgeMeaning(technical, group, true),
            AuthoredMinimum = minimum, AuthoredMaximum = maximum,
            Minimum = experimentMin, Maximum = experimentMax,
            SafeRandomMinimum = Mathf.Clamp(Mathf.Min(safeA, safeB), experimentMin, experimentMax),
            SafeRandomMaximum = Mathf.Clamp(Mathf.Max(safeA, safeB), experimentMin, experimentMax),
            DiagnosticExtreme = diagnosticExtreme,
            Toggle = toggle,
            DescriptionRu = BuildDescription(technical, group, russian, toggle, authoredHint)
        };
    }

    public string FormatValue(float value)
    {
        if (Toggle) return value >= .5f ? "ВКЛ" : "ВЫКЛ";
        if (Unit.StartsWith("%", StringComparison.Ordinal))
            return (value * 100f).ToString("0.#") + Unit;
        string format = Mathf.Abs(value) < 1f ? "0.###" : "0.##";
        return value.ToString(format) + (string.IsNullOrEmpty(Unit) ? string.Empty : " " + Unit);
    }

    public static string GetGroupDescriptionRu(CombatFeelGroup group) => group switch
    {
        CombatFeelGroup.Global => "Общие коэффициенты, связывающие урон, класс врага и направление со всей визуальной отдачей.",
        CombatFeelGroup.Shot => "Отклик оружия и игрока в момент выстрела: рывок, вспышка, ритм и декоративная отдача.",
        CombatFeelGroup.Projectile => "Только внешний вид полёта снаряда и его следа; физическая скорость не меняется.",
        CombatFeelGroup.Hit => "Эффекты точки попадания и цифры урона: вспышка, частицы, кольцо и popup-анимация.",
        CombatFeelGroup.Target => "Реакция живой цели: визуальный толчок, деформация, поворот и качание.",
        CombatFeelGroup.Kill => "Читаемость и зрелищность смерти: деформация, частицы, призрак и оверкилл.",
        CombatFeelGroup.Camera => "Импульсы камеры и зума от выстрела, попадания и убийства.",
        CombatFeelGroup.Time => "Короткие стоп-кадры и замедления; экстремумы остаются ограничены безопасными значениями.",
        CombatFeelGroup.Crowd => "Передача импульса соседним врагам и усиление серии быстрых убийств.",
        _ => "Экспериментальные экранные и предвыстрельные акценты, не добавляющие новых игровых механик."
    };

    public static string GetGroupShortNameRu(CombatFeelGroup group) => group switch
    {
        CombatFeelGroup.Global => "ОБЩЕЕ", CombatFeelGroup.Shot => "ВЫСТРЕЛ",
        CombatFeelGroup.Projectile => "СНАРЯД", CombatFeelGroup.Hit => "ПОПАДАНИЕ",
        CombatFeelGroup.Target => "ЦЕЛЬ", CombatFeelGroup.Kill => "УБИЙСТВО",
        CombatFeelGroup.Camera => "КАМЕРА", CombatFeelGroup.Time => "ВРЕМЯ",
        CombatFeelGroup.Crowd => "ТОЛПА", _ => "ЭКСПЕРИМЕНТ"
    };

    public static string GetPresetDescriptionRu(string key) => key switch
    {
        "CLEAN" => "Чистый и сдержанный профиль: минимум камеры и времени, ясный силуэт выстрела.",
        "PUNCHY" => "Резкий профиль: сильнее выстрелы и попадания, умеренная камера и стоп-кадры.",
        "HEAVY" => "Тяжёлый профиль: медленнее восстановление, сильные убийства и временные акценты.",
        "ARCADE" => "Яркий аркадный профиль: заметные снаряды, вспышки и реакции толпы.",
        "CHAOTIC" => "Предельно выразительный общий профиль для стресс-теста читаемости.",
        "PRODUCTION" => "Сразу вернуть все группы к сохранённым production-значениям.",
        "OFF" => "Вернуть параметры открытой группы к production-значениям.",
        "SOFT" => "Лёгкое отклонение группы от production в сторону авторского акцента.",
        "MEDIUM" => "Средняя, хорошо заметная сила авторского акцента группы.",
        "HARD" => "Сильный авторский акцент без выхода к экспериментальному краю диапазона.",
        "STRONG" => "Сразу применить сильный авторский акцент ко всем группам.",
        "INSANE" => "Очевидный экстремум: уводит группу далеко за HARD, но остаётся в безопасных границах.",
        "SAVE A" => "Сохранить текущие значения всех параметров в слот A.",
        "LOAD A" => "Восстановить ранее сохранённый снимок A.",
        "SAVE B" => "Сохранить текущие значения всех параметров в слот B.",
        "LOAD B" => "Восстановить ранее сохранённый снимок B.",
        "RANDOMIZE" => "Случайно изменить открытую группу только внутри её безопасных random-диапазонов.",
        "UNDO" => "Точно отменить последнюю рандомизацию.",
        "SOLO" => "Оставить активной открытую группу, временно нейтрализовав остальные.",
        "UNSOLO" => "Вернуть одновременное действие всех групп.",
        "RESET GROUP" => "Вернуть всю открытую группу к production-значениям.",
        _ => "Выбрать раздел параметров лаборатории: " + key + "."
    };

    private static string BuildRussianName(string technical)
    {
        string authored = technical switch
        {
            "MouseLookAhead" => "Взгляд камеры за мышью",
            "LookAheadDistance" => "Дистанция взгляда мышью",
            "LookAheadResponse" => "Скорость реакции взгляда",
            "LookAheadReturn" => "Скорость возврата взгляда",
            "LookAheadDeadZone" => "Мёртвая зона взгляда",
            "LookAheadCurve" => "Кривая реакции взгляда",
            "HorizontalStrength" => "Горизонтальная сила взгляда",
            "VerticalStrength" => "Вертикальная сила взгляда",
            "MaxScreenFraction" => "Порог насыщения экрана",
            _ => null
        };
        if (authored != null) return authored;
        List<string> tokens = Split(technical);
        StringBuilder result = new();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (result.Length > 0) result.Append(' ');
            result.Append(Words.TryGetValue(tokens[i], out string word) ? word : tokens[i]);
        }
        if (result.Length == 0) return technical;
        result[0] = char.ToUpperInvariant(result[0]);
        return result.ToString();
    }

    private static List<string> Split(string value)
    {
        List<string> result = new();
        int start = 0;
        for (int i = 1; i < value.Length; i++)
            if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
            { result.Add(value.Substring(start, i - start)); start = i; }
        result.Add(value.Substring(start));
        return result;
    }

    private static string GetUnit(string name, bool toggle)
    {
        if (toggle) return "вкл/выкл";
        if (name == "LookAheadDistance") return "ед.";
        if (name is "LookAheadResponse" or "LookAheadReturn") return "1/с";
        if (name is "LookAheadDeadZone" or "MaxScreenFraction") return "% экрана";
        if (name is "MuzzleDuration" or "ImpactLifetime" or "TrailLifetime" or
            "CritPopupLifetime") return "×";
        if (name == "ProjectileSpin") return "°/с";
        if (name == "PlayerRecoilVelocity" || name == "PopupRiseSpeed") return "ед./с";
        if (name == "WobbleStrength") return "°";
        if (name == "WobbleDamping") return "1/с";
        if (name.Contains("ShakeAmplitude") || name.Contains("ZoomPunch")) return "ед.";
        if (name == "MovementDamp" || name == "DirectionRandomness" ||
            name.Contains("Opacity") || name.Contains("Fade") || name.Contains("Blend")) return "%";
        if ((name.Contains("Freeze") && !name.Contains("Blend")) ||
            name.EndsWith("Slowdown", StringComparison.Ordinal)) return "с";
        if (name.Contains("Duration") || name.Contains("Lifetime") || name.Contains("Delay") ||
            name.Contains("Recovery") || name.Contains("Return") || name.Contains("Window") ||
            name.Contains("Hold") || name.Contains("Attack") || name.Contains("Release")) return "с";
        if (name.Contains("Rotation") || name.Contains("Spin")) return "°";
        if (name.Contains("Frequency")) return "Гц";
        if (name.Contains("CrowdMaxTargets")) return "целей";
        if (name.Contains("Distance") || name.Contains("Radius") || name.Contains("Offset") || name.Contains("Push")) return "ед.";
        return "×";
    }

    private static string BuildWhatToWatch(string name, CombatFeelGroup group)
    {
        if (IsMouseLookAheadName(name))
            return "Не стреляя, ведите курсор от центра к краям экрана и следите за положением игрока внутри кадра.";
        if (name.StartsWith("Weapon", StringComparison.Ordinal))
            return "Смотрите на спрайт оружия относительно fire point и рук персонажа.";
        if (name.StartsWith("Player", StringComparison.Ordinal))
            return "Смотрите на visual персонажа; gameplay root и точка выстрела должны оставаться на месте.";
        if (name.StartsWith("Muzzle", StringComparison.Ordinal))
            return "Выберите оружие с muzzle-FX (например Laser) и смотрите на вспышку в момент выстрела.";
        if (group == CombatFeelGroup.Projectile)
            return "Следите за visual снаряда и его TrailRenderer во время полёта, не за скоростью Rigidbody.";
        if (name.StartsWith("Impact", StringComparison.Ordinal))
            return "Выберите оружие с impact-FX и смотрите точно в точку попадания.";
        if (name.StartsWith("Popup", StringComparison.Ordinal) || name.StartsWith("CritPopup", StringComparison.Ordinal))
            return "Смотрите на цифру урона от момента появления до полного исчезновения.";
        if (group is CombatFeelGroup.Target or CombatFeelGroup.Kill or CombatFeelGroup.Crowd)
            return "Смотрите на presentation root врага относительно неподвижного gameplay collider/root.";
        if (group == CombatFeelGroup.Camera)
            return name.Contains("Zoom") ? "Смотрите на изменение масштаба кадра вокруг Camera.main."
                : "Смотрите на смещение всего кадра относительно мира.";
        if (group == CombatFeelGroup.Time)
            return name.Contains("Local") ? "Сравнивайте анимацию поражённого врага с остальным миром."
                : "Сравнивайте паузу/скорость мира сразу после события.";
        return "Сравнивайте силу всей реакции на одинаковый выстрел и одинаковую цель.";
    }

    private static string BuildEdgeMeaning(string name, CombatFeelGroup group, bool maximum)
    {
        if (name == "LookAheadDistance")
            return maximum ? "Намеренно огромное диагностическое смещение камеры."
                : "Look-ahead не добавляет позиционного offset.";
        if (name == "LookAheadDeadZone")
            return maximum ? "Камера реагирует только далеко от центра экрана."
                : "Камера реагирует на любое заметное движение мыши.";
        if (name == "LookAheadCurve")
            return maximum ? "Основное движение начинается только у края экрана."
                : "Камера почти сразу набирает большую часть offset.";
        if (name is "HorizontalStrength" or "VerticalStrength")
            return maximum ? "Намеренно чрезмерное усиление выбранной оси."
                : "Выбранная ось полностью отключена.";
        if (name.Contains("SlowdownScale"))
            return maximum ? "Мир сохраняет полную скорость: slow-motion практически отсутствует."
                : "Самое сильное безопасное slow-motion, почти стоп-кадр.";
        if (name.Contains("Opacity"))
            return maximum ? "Максимально плотный/непрозрачный visual."
                : "Visual полностью прозрачен и практически исчезает.";
        if (!maximum)
        {
            if (name.Contains("Duration") || name.Contains("Lifetime") || name.Contains("Delay"))
                return "Эффект почти мгновенный или отсутствует.";
            if (name.Contains("Scale") && !name.Contains("Punch")) return "Минимальный безопасный размер visual.";
            return "Минимально допустимое влияние этого компонента.";
        }
        if (name.Contains("Rotation") || name.Contains("Spin")) return "Намеренно чрезмерный, сразу заметный поворот.";
        if (name.Contains("Squash")) return "Сильная мультяшная деформация без схлопывания transform.";
        if (name.Contains("Stretch") || name.Contains("Scale") || name.Contains("Size")) return "Очень крупный диагностический visual.";
        if (name.Contains("Duration") || name.Contains("Lifetime") || name.Contains("Delay") || name.Contains("Return")) return "Намеренно долгая реакция, удобная для сравнения тайминга.";
        if (group == CombatFeelGroup.Camera) return "Сильное смещение/масштабирование кадра, которое невозможно пропустить.";
        if (group == CombatFeelGroup.Time) return "Предельный безопасный временной акцент для диагностического сравнения.";
        return "Диагностический перебор: эффект должен быть очевиден без всматривания.";
    }

    private static string BuildDescription(string name, CombatFeelGroup group,
        string russianName, bool toggle, string authoredHint)
    {
        string subject = GetGroupDescriptionRu(group).Split('.')[0].ToLowerInvariant();
        string increase;
        if (name == "MouseLookAhead")
            increase = "Включение позволяет камере мягко смотреть в сторону курсора. Сам игрок и точка выстрела остаются на месте.";
        else if (name == "LookAheadDistance")
            increase = "Чем выше значение, тем дальше камера уходит в сторону курсора.";
        else if (name == "LookAheadResponse")
            increase = "Увеличение сокращает время реакции камеры на новое положение курсора.";
        else if (name == "LookAheadReturn")
            increase = "Увеличение ускоряет плавный возврат камеры к обычному положению за игроком.";
        else if (name == "LookAheadDeadZone")
            increase = "Увеличение расширяет область около центра экрана, где мышь не вызывает смещение.";
        else if (name == "LookAheadCurve")
            increase = "Значения выше 1 откладывают сильную реакцию до края экрана; ниже 1 включают её раньше.";
        else if (name == "HorizontalStrength")
            increase = "Увеличение усиливает движение камеры влево и вправо за курсором.";
        else if (name == "VerticalStrength")
            increase = "Увеличение усиливает движение камеры вверх и вниз за курсором.";
        else if (name == "MaxScreenFraction")
            increase = "Увеличение требует увести курсор дальше от центра, чтобы камера дошла до полного смещения.";
        else if (name == "PhysicalRecoil")
            increase = "Включение действительно отталкивает игрока назад при выстреле и влияет на движение.";
        else if (name == "PlayerRecoilVelocity")
            increase = "Увеличение сильнее отбрасывает игрока назад; работает только при включённой физической отдаче.";
        else if (name == "MovementDamp")
            increase = "Увеличение сильнее притормаживает движение игрока в момент выстрела.";
        else if (name == "GlobalFreeze")
            increase = "Включение на короткое время замирает весь мир после события, даже если обычный стоп-кадр выключен.";
        else if (name.Contains("SlowdownScale"))
            increase = "Увеличение оставляет больше скорости во время замедления; MIN даёт самый сильный slow-motion.";
        else if (toggle) increase = "Включение добавляет этот явно обозначенный режим; выключение оставляет production-поведение.";
        else if (name.Contains("Duration") || name.Contains("Lifetime") || name.Contains("Hold") || name.Contains("Window"))
            increase = "Увеличение дольше удерживает эффект на экране и делает реакцию протяжённее.";
        else if (name.Contains("Delay") || name.Contains("Attack") || name.Contains("Recovery") || name.Contains("Return"))
            increase = "Увеличение замедляет наступление или возврат эффекта, делая движение более вязким.";
        else if (name.Contains("Speed") || name.Contains("Frequency"))
            increase = "Увеличение ускоряет визуальное движение или колебание; игровая скорость не меняется, если это не указано отдельно.";
        else if (name.Contains("Rotation") || name.Contains("Spin"))
            increase = "Увеличение усиливает поворот по часовой стрелке; отрицательные значения меняют направление.";
        else if (name.Contains("Random") || name.Contains("Variance") || name.Contains("Spread"))
            increase = "Увеличение добавляет больше различий между событиями и менее ровный силуэт реакции.";
        else if (name.Contains("Opacity") || name.Contains("Brightness") || name.Contains("Glow") || name.Contains("Flash") || name.Contains("Light") || name.Contains("Vignette"))
            increase = "Увеличение делает экранный или световой акцент ярче и заметнее.";
        else if (name.Contains("Scale") || name.Contains("Size") || name.Contains("Width") || name.Contains("Length") || name.Contains("Radius") || name.Contains("Expand"))
            increase = "Увеличение визуально расширяет эффект и делает его силуэт крупнее.";
        else if (name.Contains("Damping") || name.Contains("Damp") || name.Contains("Falloff") || name.Contains("Decay"))
            increase = "Увеличение сильнее гасит движение или быстрее ослабляет эффект от центра/во времени.";
        else if (name.Contains("Amount") || name.Contains("Particles") || name.Contains("Sparks") || name.Contains("Debris") || name.Contains("Targets"))
            increase = "Увеличение показывает больше элементов или затрагивает больше визуальных целей.";
        else if (name.Contains("Squash") || name.Contains("Compression") || name.Contains("Shrink"))
            increase = "Увеличение сильнее сжимает визуальный силуэт; геометрия ограничена безопасным диапазоном.";
        else
            increase = "Увеличение усиливает видимую выраженность этого компонента реакции.";
        string note = GetSafetyNote(name, authoredHint);
        return $"Меняет «{russianName.ToLowerInvariant()}»: {subject}. {increase}{note}";
    }

    private static bool IsMouseLookAheadName(string name) =>
        name.Contains("LookAhead", StringComparison.Ordinal) || name is
            "HorizontalStrength" or "VerticalStrength" or "MaxScreenFraction";

    private static string GetSafetyNote(string name, string authoredHint)
    {
        if (string.IsNullOrWhiteSpace(authoredHint)) return string.Empty;
        if (name == "PhysicalRecoil") return " Внимание: режим явно меняет скорость Rigidbody и по умолчанию выключен.";
        if (name == "PhysicalStagger") return " Внимание: экспериментально приостанавливает AI/анимацию и по умолчанию выключен.";
        if (name == "ShotInputDelay") return " Это только задержка презентации: снаряд и DPS срабатывают сразу.";
        if (name == "FireRateFeelMultiplier") return " Меняется только визуальный ритм, не cooldown оружия.";
        if (name == "SpeedIllusion") return " Физическая скорость снаряда не меняется.";
        if (name == "BossWeight") return " Смещение масштабируется отдельно от акцентов камеры и времени.";
        return string.Empty;
    }

    private static void ExpandRange(string name, float neutral, float minimum,
        float maximum, bool toggle, out float expandedMin, out float expandedMax)
    {
        if (toggle) { expandedMin = 0f; expandedMax = 1f; return; }
        float span = Mathf.Max(maximum - minimum, .001f);
        float factor = name.Contains("Freeze") ? 1.5f :
            name.Contains("Rotation") || name.Contains("Spin") ? 2f :
            name.Contains("Duration") || name.Contains("Lifetime") || name.Contains("Delay") ? 2f : 1.75f;
        expandedMin = minimum < neutral ? neutral - (neutral - minimum) * factor : minimum;
        expandedMax = maximum > neutral ? neutral + (maximum - neutral) * factor : maximum;
        if (minimum >= 0f) expandedMin = Mathf.Max(0f, expandedMin);
        if (name.Contains("Scale") && !name.Contains("Punch") && neutral > 0f)
            expandedMin = Mathf.Max(.01f, expandedMin);
        if (name.Contains("SlowdownScale")) { expandedMin = Mathf.Max(.02f, expandedMin); expandedMax = Mathf.Min(1f, expandedMax); }
        if (name.Contains("Opacity") || name.Contains("Blend") || name.Contains("Randomness") || name.Contains("MovementDamp"))
        {
            float cap = name.Contains("Opacity") || name.Contains("Blend") ? 1f : 2f;
            expandedMax = Mathf.Min(Mathf.Max(maximum, cap), expandedMax);
        }
        if (name.Contains("Freeze") && !name.Contains("Blend")) expandedMax = Mathf.Min(.75f, expandedMax);
        if (name is "LookAheadDeadZone" or "MaxScreenFraction")
        {
            expandedMin = Mathf.Max(name == "MaxScreenFraction" ? .01f : 0f,
                expandedMin);
            expandedMax = Mathf.Min(1f, expandedMax);
        }
        if (name == "LookAheadCurve")
        {
            expandedMin = Mathf.Max(.1f, expandedMin);
            expandedMax = Mathf.Min(8f, expandedMax);
        }
        if (name is "LookAheadResponse" or "LookAheadReturn")
        {
            expandedMin = Mathf.Max(.1f, expandedMin);
            expandedMax = Mathf.Min(60f, expandedMax);
        }
        expandedMin = Mathf.Min(expandedMin, neutral);
        expandedMax = Mathf.Max(expandedMax, neutral);
    }
}

public sealed class CombatFeelLabSettings
{
    public enum CharacterPreset { Clean, Punchy, Heavy, Arcade, Chaotic }
    public enum GroupPreset { Off, Soft, Medium, Hard, Insane }

    private static readonly List<CombatFeelDescriptor> descriptors = Build();
    private static readonly Dictionary<CombatFeelParameter, CombatFeelDescriptor>
        descriptorMap = BuildMap();
    private readonly Dictionary<CombatFeelParameter, float> values = new();
    private Dictionary<CombatFeelParameter, float> savedValues = new();
    private Dictionary<CombatFeelParameter, float> slotA;
    private Dictionary<CombatFeelParameter, float> slotB;
    private Dictionary<CombatFeelParameter, float> randomUndo;

    public static IReadOnlyList<CombatFeelDescriptor> Descriptors => descriptors;
    public CombatFeelGroup? SoloGroup { get; private set; }
    public bool HasA => slotA != null;
    public bool HasB => slotB != null;
    public bool CanUndoRandomize => randomUndo != null;
    public int Version { get; private set; }
    public bool HasUnsavedChanges => !SnapshotsEqual(values, savedValues);

    public float Get(CombatFeelParameter parameter)
    {
        CombatFeelDescriptor descriptor = descriptorMap[parameter];
        if (SoloGroup.HasValue && descriptor.Group != CombatFeelGroup.Global &&
            descriptor.Group != SoloGroup.Value)
            return descriptor.Neutral;
        return values.TryGetValue(parameter, out float value)
            ? value : descriptor.Neutral;
    }

    public float GetRaw(CombatFeelParameter parameter) =>
        values.TryGetValue(parameter, out float value)
            ? value : descriptorMap[parameter].Neutral;

    public void Set(CombatFeelParameter parameter, float value)
    {
        CombatFeelDescriptor descriptor = descriptorMap[parameter];
        values[parameter] = descriptor.Toggle
            ? (value >= 0.5f ? 1f : 0f)
            : Mathf.Clamp(value, descriptor.Minimum, descriptor.Maximum);
        Version++;
    }

    public void Reset(CombatFeelParameter parameter)
    {
        if (values.Remove(parameter)) Version++;
    }
    public void ResetGroup(CombatFeelGroup group)
    {
        for (int i = 0; i < descriptors.Count; i++)
            if (descriptors[i].Group == group)
                values.Remove(descriptors[i].Parameter);
        Version++;
    }

    public void ResetAll()
    {
        values.Clear();
        SoloGroup = null;
        randomUndo = null;
        Version++;
    }

    public bool IsNeutral(CombatFeelParameter parameter) =>
        Mathf.Approximately(GetRaw(parameter), descriptorMap[parameter].Neutral);

    public void ToggleSolo(CombatFeelGroup group)
    {
        SoloGroup = SoloGroup == group ? null : group;
        Version++;
    }

    public void SaveA() => slotA = Snapshot();
    public void SaveB() => slotB = Snapshot();
    public bool LoadA() => Load(slotA);
    public bool LoadB() => Load(slotB);

    public IReadOnlyDictionary<CombatFeelParameter, float> ExportValues() =>
        Snapshot();

    public void MarkSaved()
    {
        savedValues = Snapshot();
        Version++;
    }

    public void Randomize(CombatFeelGroup? group = null)
    {
        randomUndo = Snapshot();
        for (int i = 0; i < descriptors.Count; i++)
        {
            CombatFeelDescriptor d = descriptors[i];
            if (group.HasValue && d.Group != group.Value)
                continue;
            if (d.Parameter == CombatFeelParameter.MasterIntensity)
                continue;
            if (d.Toggle)
                Set(d.Parameter, UnityEngine.Random.value < 0.42f ? 1f : 0f);
            else
                Set(d.Parameter, UnityEngine.Random.Range(
                    d.Metadata.SafeRandomMinimum, d.Metadata.SafeRandomMaximum));
        }
    }

    public bool UndoRandomize()
    {
        if (randomUndo == null) return false;
        Dictionary<CombatFeelParameter, float> restore = randomUndo;
        randomUndo = null;
        return Load(restore);
    }

    public void ApplyGroupPreset(CombatFeelGroup group, GroupPreset preset)
    {
        if (preset == GroupPreset.Off)
        {
            ResetGroup(group);
            return;
        }
        for (int i = 0; i < descriptors.Count; i++)
        {
            CombatFeelDescriptor d = descriptors[i];
            if (d.Group != group) continue;
            if (d.Toggle)
            {
                Set(d.Parameter, d.Parameter == CombatFeelParameter.MouseLookAhead
                    ? d.Hard
                    : preset >= GroupPreset.Medium ? d.Hard : d.Neutral);
                continue;
            }
            float value = preset switch
            {
                GroupPreset.Soft => Mathf.Lerp(d.Neutral, d.Hard, .32f),
                GroupPreset.Medium => Mathf.Lerp(d.Neutral, d.Hard, .68f),
                GroupPreset.Hard => d.Hard,
                _ => GetInsaneValue(d)
            };
            Set(d.Parameter, value);
        }
    }

    public void ApplyAllGroupsPreset(GroupPreset preset)
    {
        ResetAll();
        if (preset == GroupPreset.Off)
            return;
        foreach (CombatFeelGroup group in Enum.GetValues(typeof(CombatFeelGroup)))
            ApplyGroupPreset(group, preset);
    }

    public void ApplyCharacterPreset(CharacterPreset preset)
    {
        ResetAll();
        float shot = 0.45f, target = 0.45f, kill = 0.45f,
            camera = 0.35f, time = 0.2f, projectile = 0.35f, crowd = 0.15f;
        switch (preset)
        {
            case CharacterPreset.Clean:
                shot = 0.28f; target = 0.24f; kill = 0.22f;
                camera = 0.12f; time = 0f; projectile = 0.22f; crowd = 0f;
                break;
            case CharacterPreset.Punchy:
                shot = 0.62f; target = 0.68f; kill = 0.62f;
                camera = 0.48f; time = 0.35f; projectile = 0.45f;
                break;
            case CharacterPreset.Heavy:
                shot = 0.52f; target = 0.72f; kill = 0.82f;
                camera = 0.58f; time = 0.65f; projectile = 0.38f;
                break;
            case CharacterPreset.Arcade:
                shot = 0.68f; target = 0.72f; kill = 0.72f;
                camera = 0.38f; time = 0.05f; projectile = 0.72f; crowd = 0.55f;
                break;
            case CharacterPreset.Chaotic:
                shot = target = kill = camera = projectile = crowd = 0.95f;
                time = 0.72f;
                break;
        }
        ApplyAmount(CombatFeelGroup.Shot, shot);
        ApplyAmount(CombatFeelGroup.Hit, target);
        ApplyAmount(CombatFeelGroup.Target, target);
        ApplyAmount(CombatFeelGroup.Kill, kill);
        ApplyAmount(CombatFeelGroup.Camera, camera);
        ApplyAmount(CombatFeelGroup.Time, time);
        ApplyAmount(CombatFeelGroup.Projectile, projectile);
        ApplyAmount(CombatFeelGroup.Crowd, crowd);
    }

    public string GetCompactConfig()
    {
        StringBuilder result = new("COMBAT FEEL CONFIG");
        foreach (CombatFeelGroup group in Enum.GetValues(typeof(CombatFeelGroup)))
        {
            bool wroteHeader = false;
            for (int i = 0; i < descriptors.Count; i++)
            {
                CombatFeelDescriptor d = descriptors[i];
                if (d.Group != group || IsNeutral(d.Parameter)) continue;
                if (!wroteHeader)
                {
                    result.AppendLine().AppendLine().Append(group.ToString().ToUpperInvariant())
                        .AppendLine(":");
                    wroteHeader = true;
                }
                result.Append(d.Name).Append(" = ")
                    .Append(GetRaw(d.Parameter).ToString("0.###")).AppendLine();
            }
        }
        if (SoloGroup.HasValue)
            result.AppendLine().Append("SOLO = ").Append(SoloGroup.Value);
        return result.ToString().TrimEnd();
    }

    public string GetFullConfig()
    {
        StringBuilder result = new("COMBAT FEEL CONFIG — ALL CURRENT VALUES");
        CombatFeelGroup? currentGroup = null;
        for (int i = 0; i < descriptors.Count; i++)
        {
            CombatFeelDescriptor descriptor = descriptors[i];
            if (currentGroup != descriptor.Group)
            {
                currentGroup = descriptor.Group;
                result.AppendLine().AppendLine()
                    .Append(descriptor.Group.ToString().ToUpperInvariant())
                    .AppendLine(":");
            }
            result.Append(descriptor.Parameter).Append(" = ")
                .Append(GetRaw(descriptor.Parameter).ToString(
                    "0.######", CultureInfo.InvariantCulture))
                .Append("    # Production: ")
                .Append(descriptor.Neutral.ToString(
                    "0.######", CultureInfo.InvariantCulture))
                .AppendLine();
        }
        if (SoloGroup.HasValue)
            result.AppendLine().Append("SOLO = ").Append(SoloGroup.Value);
        return result.ToString().TrimEnd();
    }

    private void ApplyAmount(CombatFeelGroup group, float amount)
    {
        for (int i = 0; i < descriptors.Count; i++)
        {
            CombatFeelDescriptor d = descriptors[i];
            if (d.Group != group) continue;
            Set(d.Parameter, d.Toggle
                ? (amount >= 0.48f ? d.Hard : d.Neutral)
                : Mathf.Lerp(d.Neutral, d.Hard, amount));
        }
    }

    private static float GetInsaneValue(CombatFeelDescriptor descriptor)
    {
        float towardMaximum = descriptor.Maximum - descriptor.Neutral;
        float towardMinimum = descriptor.Neutral - descriptor.Minimum;
        float edge;
        if (descriptor.Hard > descriptor.Neutral) edge = descriptor.Maximum;
        else if (descriptor.Hard < descriptor.Neutral) edge = descriptor.Minimum;
        else edge = towardMaximum >= towardMinimum
            ? descriptor.Maximum : descriptor.Minimum;
        return Mathf.Lerp(descriptor.Hard, edge, .78f);
    }

    private Dictionary<CombatFeelParameter, float> Snapshot() => new(values);

    private static bool SnapshotsEqual(
        IReadOnlyDictionary<CombatFeelParameter, float> left,
        IReadOnlyDictionary<CombatFeelParameter, float> right)
    {
        for (int i = 0; i < descriptors.Count; i++)
        {
            CombatFeelDescriptor descriptor = descriptors[i];
            float leftValue = left.TryGetValue(descriptor.Parameter, out float l)
                ? l : descriptor.Neutral;
            float rightValue = right.TryGetValue(descriptor.Parameter, out float r)
                ? r : descriptor.Neutral;
            float epsilon = Mathf.Max(.00001f,
                (descriptor.Maximum - descriptor.Minimum) * .00001f);
            if (Mathf.Abs(leftValue - rightValue) > epsilon)
                return false;
        }
        return true;
    }

    private bool Load(Dictionary<CombatFeelParameter, float> source)
    {
        if (source == null) return false;
        values.Clear();
        foreach (KeyValuePair<CombatFeelParameter, float> pair in source)
            values[pair.Key] = pair.Value;
        Version++;
        return true;
    }

    private static Dictionary<CombatFeelParameter, CombatFeelDescriptor> BuildMap()
    {
        Dictionary<CombatFeelParameter, CombatFeelDescriptor> map = new();
        for (int i = 0; i < descriptors.Count; i++)
            map[descriptors[i].Parameter] = descriptors[i];
        return map;
    }

    private static List<CombatFeelDescriptor> Build()
    {
        List<CombatFeelDescriptor> d = new();
        void F(CombatFeelParameter p, CombatFeelGroup g, string n,
            float neutral, float min, float max, float hard, string tip = null) =>
            d.Add(new CombatFeelDescriptor(p, g, n, neutral, min, max, hard, tip));
        void T(CombatFeelParameter p, CombatFeelGroup g, string n, string tip = null) =>
            d.Add(new CombatFeelDescriptor(p, g, n, 0f, 0f, 1f, 1f, tip, true));

        F(CombatFeelParameter.MasterIntensity, CombatFeelGroup.Global, "Master Intensity", 1, 0, 2, 1.25f);
        F(CombatFeelParameter.DamageInfluence, CombatFeelGroup.Global, "Damage → Feedback Influence", 0, 0, 2, 1);
        F(CombatFeelParameter.MinFeedback, CombatFeelGroup.Global, "Min Feedback", 0.7f, .1f, 1.5f, .55f);
        F(CombatFeelParameter.MaxFeedback, CombatFeelGroup.Global, "Max Feedback", 1.4f, 1, 3, 2);
        F(CombatFeelParameter.CritMultiplier, CombatFeelGroup.Global, "Crit Feedback Multiplier", 1, 1, 3, 1.7f);
        F(CombatFeelParameter.BasicWeight, CombatFeelGroup.Global, "Basic Enemy Weight", 1, .1f, 2, 1);
        F(CombatFeelParameter.EliteWeight, CombatFeelGroup.Global, "Elite Weight", 1, .1f, 2, .8f);
        F(CombatFeelParameter.BossWeight, CombatFeelGroup.Global, "Boss Weight", 1, .05f, 2, .55f, "Scales displacement; camera/time have their own emphasis.");
        F(CombatFeelParameter.DirectionInfluence, CombatFeelGroup.Global, "Direction Influence", 1, 0, 2, 1.35f);
        F(CombatFeelParameter.DirectionRandomness, CombatFeelGroup.Global, "Direction Randomness", 0, 0, 1, .18f);
        F(CombatFeelParameter.TangentialSpread, CombatFeelGroup.Global, "Tangential Spread", 0, 0, 1, .22f);

        F(CombatFeelParameter.ShotInputDelay, CombatFeelGroup.Shot, "Shot Input Delay", 0, 0, .12f, .025f, "Presentation delay only; projectile and DPS stay immediate.");
        F(CombatFeelParameter.FireResponseOffset, CombatFeelGroup.Shot, "Fire Response Offset", 0, -.08f, .12f, .018f);
        F(CombatFeelParameter.ShotAnimationLead, CombatFeelGroup.Shot, "Shot Animation Lead", 0, 0, .12f, .025f);
        F(CombatFeelParameter.ShotFxLead, CombatFeelGroup.Shot, "Shot FX Lead", 0, 0, .12f, .015f);
        F(CombatFeelParameter.FireRateFeelMultiplier, CombatFeelGroup.Shot, "Fire Rate Feel Multiplier", 1, .5f, 2, 1.2f, "Presentation rhythm only; does not alter weapon cooldown.");
        F(CombatFeelParameter.CadenceVariance, CombatFeelGroup.Shot, "Cadence Variance", 0, 0, 1, .16f);
        F(CombatFeelParameter.BurstRhythmBias, CombatFeelGroup.Shot, "Burst Rhythm Bias", 0, -1, 1, .35f);
        F(CombatFeelParameter.FirstShotEmphasis, CombatFeelGroup.Shot, "First Shot Emphasis", 0, 0, 2, .65f);
        F(CombatFeelParameter.WeaponKickDistance, CombatFeelGroup.Shot, "Weapon Kick Distance", 0, 0, 1, .18f);
        F(CombatFeelParameter.WeaponKickDuration, CombatFeelGroup.Shot, "Weapon Kick Duration", .025f, .005f, .15f, .035f);
        F(CombatFeelParameter.WeaponReturnDuration, CombatFeelGroup.Shot, "Weapon Return Duration", .08f, .01f, .4f, .11f);
        F(CombatFeelParameter.WeaponOvershoot, CombatFeelGroup.Shot, "Weapon Overshoot", 0, 0, .6f, .16f);
        F(CombatFeelParameter.WeaponSettleDuration, CombatFeelGroup.Shot, "Weapon Settle Duration", .06f, 0, .4f, .09f);
        F(CombatFeelParameter.WeaponKickRotation, CombatFeelGroup.Shot, "Weapon Kick Rotation", 0, -20, 20, 5);
        F(CombatFeelParameter.WeaponKickRandomness, CombatFeelGroup.Shot, "Weapon Kick Randomness", 0, 0, 1, .18f);
        F(CombatFeelParameter.WeaponScalePunchX, CombatFeelGroup.Shot, "Weapon Scale Punch X", 0, -.5f, .8f, .16f);
        F(CombatFeelParameter.WeaponScalePunchY, CombatFeelGroup.Shot, "Weapon Scale Punch Y", 0, -.5f, .8f, -.08f);
        F(CombatFeelParameter.PlayerVisualRecoil, CombatFeelGroup.Shot, "Player Visual Recoil", 0, 0, 1, .08f);
        F(CombatFeelParameter.PlayerVisualRecoilDuration, CombatFeelGroup.Shot, "Player Visual Recoil Duration", .08f, .01f, .4f, .12f);
        F(CombatFeelParameter.PlayerSquash, CombatFeelGroup.Shot, "Player Squash", 0, 0, .5f, .08f);
        F(CombatFeelParameter.PlayerStretch, CombatFeelGroup.Shot, "Player Stretch", 0, 0, .5f, .05f);
        F(CombatFeelParameter.PlayerRotationKick, CombatFeelGroup.Shot, "Player Rotation Kick", 0, -12, 12, 2.5f);
        F(CombatFeelParameter.PlayerReturnSpring, CombatFeelGroup.Shot, "Player Return Spring", 1, .1f, 3, 1.4f);
        T(CombatFeelParameter.PhysicalRecoil, CombatFeelGroup.Shot, "Physical Recoil", "Explicitly changes Rigidbody velocity; off by default.");
        F(CombatFeelParameter.PlayerRecoilVelocity, CombatFeelGroup.Shot, "Player Recoil Velocity", 0, 0, 5, .8f);
        F(CombatFeelParameter.MovementDamp, CombatFeelGroup.Shot, "Movement Damp On Shot", 0, 0, 1, .18f);
        F(CombatFeelParameter.MovementDampDuration, CombatFeelGroup.Shot, "Movement Damp Duration", .06f, 0, .3f, .08f);
        F(CombatFeelParameter.MuzzleScale, CombatFeelGroup.Shot, "Muzzle Flash Scale", 1, 0, 4, 1.55f);
        F(CombatFeelParameter.MuzzleDuration, CombatFeelGroup.Shot, "Muzzle Flash Duration", 1, .1f, 3, 1.25f);
        F(CombatFeelParameter.MuzzleBrightness, CombatFeelGroup.Shot, "Muzzle Flash Brightness", 1, 0, 4, 1.5f);
        F(CombatFeelParameter.MuzzleRandomRotation, CombatFeelGroup.Shot, "Muzzle Random Rotation", 0, 0, 180, 28);
        F(CombatFeelParameter.MuzzleStretch, CombatFeelGroup.Shot, "Muzzle Stretch", 1, .2f, 4, 1.65f);
        F(CombatFeelParameter.MuzzleSparks, CombatFeelGroup.Shot, "Muzzle Sparks Multiplier", 1, 0, 4, 1.5f);
        F(CombatFeelParameter.MuzzleDirectionality, CombatFeelGroup.Shot, "Muzzle Directionality", 1, 0, 2, 1.3f);
        F(CombatFeelParameter.MuzzleRingScale, CombatFeelGroup.Shot, "Expanding Ring Scale", 0, 0, 4, 1.2f);
        F(CombatFeelParameter.MuzzleRingDuration, CombatFeelGroup.Shot, "Ring Duration", .08f, .01f, .4f, .1f);
        F(CombatFeelParameter.MuzzleRingOpacity, CombatFeelGroup.Shot, "Ring Opacity", 0, 0, 1, .35f);

        F(CombatFeelParameter.ProjectileScale, CombatFeelGroup.Projectile, "Projectile Visual Scale", 1, .1f, 4, 1.25f);
        F(CombatFeelParameter.ProjectileStretch, CombatFeelGroup.Projectile, "Stretch Along Velocity", 1, .2f, 6, 1.7f);
        F(CombatFeelParameter.ProjectileSquash, CombatFeelGroup.Projectile, "Squash Across Velocity", 1, .2f, 3, .82f);
        T(CombatFeelParameter.ProjectileAlign, CombatFeelGroup.Projectile, "Rotation Alignment");
        F(CombatFeelParameter.ProjectileSpin, CombatFeelGroup.Projectile, "Projectile Spin", 0, -720, 720, 140);
        F(CombatFeelParameter.ProjectileGlow, CombatFeelGroup.Projectile, "Projectile Glow", 1, 0, 4, 1.45f);
        F(CombatFeelParameter.ProjectileBrightness, CombatFeelGroup.Projectile, "Projectile Brightness", 1, 0, 4, 1.35f);
        F(CombatFeelParameter.ProjectilePulse, CombatFeelGroup.Projectile, "Projectile Pulse Amount", 0, 0, 1, .12f);
        F(CombatFeelParameter.ProjectilePulseSpeed, CombatFeelGroup.Projectile, "Projectile Pulse Speed", 8, 0, 30, 12);
        F(CombatFeelParameter.TrailWidth, CombatFeelGroup.Projectile, "Trail Width", 1, 0, 5, 1.5f);
        F(CombatFeelParameter.TrailLength, CombatFeelGroup.Projectile, "Trail Length", 1, 0, 4, 1.45f);
        F(CombatFeelParameter.TrailLifetime, CombatFeelGroup.Projectile, "Trail Lifetime", 1, 0, 4, 1.35f);
        F(CombatFeelParameter.TrailOpacity, CombatFeelGroup.Projectile, "Trail Opacity", 1, 0, 3, .25f);
        F(CombatFeelParameter.TrailTaper, CombatFeelGroup.Projectile, "Trail Taper", 1, 0, 2, 1.25f);
        F(CombatFeelParameter.SpeedIllusion, CombatFeelGroup.Projectile, "Speed Illusion Strength", 0, 0, 2, .65f, "Visual-only stretch/offset; physics speed is unchanged.");
        F(CombatFeelParameter.ForwardVisualOffset, CombatFeelGroup.Projectile, "Forward Visual Offset", 0, 0, 1, .12f);
        F(CombatFeelParameter.InitialStreakLength, CombatFeelGroup.Projectile, "Initial Streak Length", 0, 0, 4, .8f);
        F(CombatFeelParameter.InitialStreakLifetime, CombatFeelGroup.Projectile, "Initial Streak Lifetime", .06f, .01f, .3f, .08f);

        AddHit(d, F);
        AddTarget(d, F, T);
        AddKill(d, F, T);
        AddCamera(d, F, T);
        AddTime(d, F, T);
        AddCrowd(d, F);
        AddExperimental(d, F);
        d.RemoveAll(descriptor =>
            !CombatFeelConsumerRegistry.TryGet(descriptor.Parameter,
                descriptor.Group, out _));
        return d;
    }

    private delegate void FloatAdder(CombatFeelParameter p, CombatFeelGroup g,
        string n, float neutral, float min, float max, float hard, string tip = null);
    private delegate void ToggleAdder(CombatFeelParameter p, CombatFeelGroup g,
        string n, string tip = null);

    private static void AddHit(List<CombatFeelDescriptor> _, FloatAdder F)
    {
        CombatFeelGroup g = CombatFeelGroup.Hit;
        F(CombatFeelParameter.ImpactScale,g,"Impact Scale",1,0,4,1.55f); F(CombatFeelParameter.ImpactLifetime,g,"Impact Lifetime",1,.1f,4,1.25f);
        F(CombatFeelParameter.ImpactBrightness,g,"Impact Brightness",1,0,4,1.5f); F(CombatFeelParameter.ImpactRotationRandomness,g,"Impact Rotation Randomness",0,0,180,32);
        F(CombatFeelParameter.ImpactDirectionality,g,"Impact Directionality",1,0,2,1.4f); F(CombatFeelParameter.ImpactRingSize,g,"Impact Ring Size",0,0,4,1.1f);
        F(CombatFeelParameter.ImpactRingSpeed,g,"Impact Ring Speed",1,0,4,1.4f); F(CombatFeelParameter.ImpactRingLifetime,g,"Impact Ring Lifetime",.08f,.01f,.4f,.1f);
        F(CombatFeelParameter.ImpactSparks,g,"Impact Sparks",1,0,4,1.5f); F(CombatFeelParameter.ImpactDebris,g,"Impact Debris",1,0,4,1.35f);
        F(CombatFeelParameter.ForwardSpray,g,"Forward Spray",1,0,3,1.3f); F(CombatFeelParameter.BackSpray,g,"Back Spray",1,0,3,.7f); F(CombatFeelParameter.SideSpray,g,"Side Spray",1,0,3,1.2f);
        F(CombatFeelParameter.FlashStrength,g,"Flash Strength",1,0,3,1.4f); F(CombatFeelParameter.FlashAttack,g,"Flash Attack",0,0,.2f,.015f);
        F(CombatFeelParameter.FlashHold,g,"Flash Hold",1,0,3,1.25f); F(CombatFeelParameter.FlashRelease,g,"Flash Release",0,0,.4f,.08f);
        F(CombatFeelParameter.BrightnessPunch,g,"Brightness Punch",0,0,2,.45f); F(CombatFeelParameter.SaturationPunch,g,"Saturation Punch",0,-1,2,.2f); F(CombatFeelParameter.ContrastPunch,g,"Contrast Punch",0,-1,2,.2f);
        F(CombatFeelParameter.PopupInitialScale,g,"Popup Initial Scale",1,.1f,4,1.15f); F(CombatFeelParameter.PopupScalePunch,g,"Popup Scale Punch",0,0,2,.35f);
        F(CombatFeelParameter.PopupRiseSpeed,g,"Popup Rise Speed",1.5f,0,8,2.2f); F(CombatFeelParameter.PopupRiseDistance,g,"Popup Rise Distance",1.5f,0,6,2.1f);
        F(CombatFeelParameter.PopupLifetime,g,"Popup Lifetime",1,.1f,4,1.15f); F(CombatFeelParameter.PopupFadeDelay,g,"Popup Fade Delay",.55f,0,3,.65f);
        F(CombatFeelParameter.PopupFadeDuration,g,"Popup Fade Duration",.45f,.01f,2,.35f); F(CombatFeelParameter.PopupHorizontalDrift,g,"Popup Horizontal Drift",0,-3,3,.25f);
        F(CombatFeelParameter.PopupDriftRandomness,g,"Popup Drift Randomness",0,0,2,.35f); F(CombatFeelParameter.PopupRotation,g,"Popup Rotation",0,-90,90,4);
        F(CombatFeelParameter.PopupRotationRandomness,g,"Popup Rotation Randomness",0,0,90,8); F(CombatFeelParameter.CritPopupScale,g,"Crit Popup Scale",1.2f,.5f,4,1.55f);
        F(CombatFeelParameter.CritPopupRise,g,"Crit Popup Rise",1,0,3,1.25f); F(CombatFeelParameter.CritPopupPunch,g,"Crit Popup Punch",1,0,3,1.5f);
        F(CombatFeelParameter.CritPopupLifetime,g,"Crit Popup Lifetime",1,0,3,1.25f); F(CombatFeelParameter.PopupDelay,g,"Popup Spawn Delay",0,0,.2f,.025f);
    }

    private static void AddTarget(List<CombatFeelDescriptor> _, FloatAdder F, ToggleAdder T)
    {
        CombatFeelGroup g=CombatFeelGroup.Target;
        F(CombatFeelParameter.VisualHitPush,g,"Visual Hit Push",0,0,1,.18f); F(CombatFeelParameter.HitPushDuration,g,"Push Duration",.03f,.005f,.2f,.04f);
        F(CombatFeelParameter.HitReturnDuration,g,"Return Duration",.09f,.01f,.5f,.13f); F(CombatFeelParameter.HitOvershoot,g,"Push Overshoot",0,0,.8f,.18f);
        F(CombatFeelParameter.HitSquashX,g,"Hit Squash X",0,-.8f,.8f,-.18f); F(CombatFeelParameter.HitStretchY,g,"Hit Stretch Y",0,-.8f,.8f,.15f);
        F(CombatFeelParameter.HitSquashDuration,g,"Squash Duration",.04f,.005f,.2f,.045f); F(CombatFeelParameter.HitRestoreDuration,g,"Restore Duration",.1f,.01f,.5f,.12f);
        F(CombatFeelParameter.HitRotation,g,"Hit Rotation",0,-30,30,6); F(CombatFeelParameter.HitRotationRandomness,g,"Rotation Randomness",0,0,30,5);
        F(CombatFeelParameter.HitRotationReturn,g,"Rotation Return",.12f,.01f,.5f,.15f); F(CombatFeelParameter.WobbleStrength,g,"Wobble Strength",0,0,30,6);
        F(CombatFeelParameter.WobbleFrequency,g,"Wobble Frequency",12,1,40,16); F(CombatFeelParameter.WobbleDamping,g,"Wobble Damping",8,.1f,30,10);
        F(CombatFeelParameter.VisualStagger,g,"Visual Stagger Duration",0,0,.3f,.035f); T(CombatFeelParameter.PhysicalStagger,g,"Physical Stagger","Explicit experimental AI/animation pause; off by default.");
    }

    private static void AddKill(List<CombatFeelDescriptor> _, FloatAdder F, ToggleAdder T)
    {
        CombatFeelGroup g=CombatFeelGroup.Kill;
        F(CombatFeelParameter.DeathPush,g,"Death Push",0,0,2,.35f); F(CombatFeelParameter.DeathRotation,g,"Death Rotation",0,-90,90,18);
        F(CombatFeelParameter.DeathSquash,g,"Death Squash",0,0,.9f,.2f); F(CombatFeelParameter.DeathStretch,g,"Death Stretch",0,0,1,.28f);
        F(CombatFeelParameter.DeathScalePunch,g,"Death Scale Punch",0,0,2,.38f); F(CombatFeelParameter.DeathHold,g,"Hold Before Disappear",0,0,.4f,.09f);
        F(CombatFeelParameter.DeathFlash,g,"Death Flash",0,0,2,.6f); F(CombatFeelParameter.DeathFlashDuration,g,"Death Flash Duration",.06f,.01f,.3f,.08f);
        F(CombatFeelParameter.DeathFade,g,"Death Fade",0,0,1,.7f); F(CombatFeelParameter.DeathShrink,g,"Death Shrink",0,0,1,.15f); F(CombatFeelParameter.DeathExpand,g,"Death Expand",0,0,2,.25f);
        F(CombatFeelParameter.DeathParticleAmount,g,"Particle Amount",1,0,5,1.7f); F(CombatFeelParameter.DeathParticleScale,g,"Particle Scale",1,0,5,1.5f);
        F(CombatFeelParameter.DeathParticleSpeed,g,"Particle Speed",1,0,5,1.4f); F(CombatFeelParameter.DeathParticleLifetime,g,"Particle Lifetime",1,0,4,1.25f);
        F(CombatFeelParameter.DeathDirectionality,g,"Death Directionality",1,0,3,1.4f); F(CombatFeelParameter.DeathRingSize,g,"Death Ring Size",0,0,5,1.4f);
        F(CombatFeelParameter.DeathRingSpeed,g,"Death Ring Speed",1,0,5,1.5f); F(CombatFeelParameter.DeathRingOpacity,g,"Death Ring Opacity",0,0,1,.4f);
        T(CombatFeelParameter.GhostEnabled,g,"Ghost Enabled"); F(CombatFeelParameter.GhostLifetime,g,"Ghost Lifetime",.08f,.01f,.3f,.12f);
        F(CombatFeelParameter.GhostPush,g,"Ghost Push",0,0,2,.3f); F(CombatFeelParameter.GhostScale,g,"Ghost Scale",1,.1f,3,1.12f); F(CombatFeelParameter.GhostFade,g,"Ghost Fade",1,0,1,1);
        F(CombatFeelParameter.OverkillThreshold,g,"Overkill Threshold",1,1,5,1.25f); F(CombatFeelParameter.OverkillFeedback,g,"Overkill Feedback Multiplier",1,1,4,1.7f);
        F(CombatFeelParameter.OverkillParticles,g,"Overkill Particle Multiplier",1,1,5,2); F(CombatFeelParameter.OverkillDeathPush,g,"Overkill Death Push",1,1,4,1.6f);
    }

    private static void AddCamera(List<CombatFeelDescriptor> _, FloatAdder F, ToggleAdder T)
    {
        CombatFeelGroup g=CombatFeelGroup.Camera;
        F(CombatFeelParameter.ShotShakeAmplitude,g,"Shot Shake Amplitude",0,0,1,.08f); F(CombatFeelParameter.ShotShakeFrequency,g,"Shot Shake Frequency",25,1,80,32);
        F(CombatFeelParameter.ShotShakeDuration,g,"Shot Shake Duration",0,0,.5f,.07f); F(CombatFeelParameter.HitShakeAmplitude,g,"Hit Shake Amplitude",0,0,1,.1f);
        F(CombatFeelParameter.HitShakeFrequency,g,"Hit Shake Frequency",25,1,80,30); F(CombatFeelParameter.HitShakeDuration,g,"Hit Shake Duration",0,0,.5f,.075f);
        F(CombatFeelParameter.KillShakeAmplitude,g,"Kill Shake Amplitude",0,0,2,.2f); F(CombatFeelParameter.KillShakeDuration,g,"Kill Shake Duration",0,0,.8f,.12f);
        F(CombatFeelParameter.DirectionalKickDistance,g,"Directional Kick Distance",0,-1,1,.12f); F(CombatFeelParameter.DirectionalKickDuration,g,"Directional Kick Duration",.03f,.005f,.2f,.035f);
        F(CombatFeelParameter.DirectionalReturn,g,"Directional Return",.1f,.01f,.6f,.13f); F(CombatFeelParameter.DirectionalOvershoot,g,"Directional Overshoot",0,0,.8f,.15f);
        F(CombatFeelParameter.ShotZoomPunch,g,"Shot Zoom Punch",0,-.2f,.2f,-.018f); F(CombatFeelParameter.HitZoomPunch,g,"Hit Zoom Punch",0,-.2f,.2f,-.012f);
        F(CombatFeelParameter.KillZoomPunch,g,"Kill Zoom Punch",0,-.3f,.3f,-.03f); F(CombatFeelParameter.ZoomAttack,g,"Zoom Attack Time",.025f,.005f,.2f,.02f);
        F(CombatFeelParameter.ZoomReturn,g,"Zoom Return Time",.12f,.01f,.8f,.15f); F(CombatFeelParameter.CameraSpring,g,"Camera Spring Strength",1,.1f,4,1.4f);
        F(CombatFeelParameter.CameraDamping,g,"Camera Spring Damping",1,.1f,4,1.2f); F(CombatFeelParameter.CameraOvershoot,g,"Camera Overshoot",0,0,.8f,.12f);
        F(CombatFeelParameter.TowardShot,g,"Toward Shot Direction",0,-1,1,.05f); F(CombatFeelParameter.AwayFromShot,g,"Away From Shot Direction",0,-1,1,0);
        F(CombatFeelParameter.TowardHit,g,"Toward Hit Position",0,0,1,.08f);
        T(CombatFeelParameter.MouseLookAhead,g,"Mouse Look-Ahead");
        F(CombatFeelParameter.LookAheadDistance,g,"Look-Ahead Distance",0,0,6,1.2f);
        F(CombatFeelParameter.LookAheadResponse,g,"Look-Ahead Response",8,.25f,30,14);
        F(CombatFeelParameter.LookAheadReturn,g,"Look-Ahead Return",6,.25f,24,12);
        F(CombatFeelParameter.LookAheadDeadZone,g,"Look-Ahead Dead Zone",.08f,0,.45f,.05f);
        F(CombatFeelParameter.LookAheadCurve,g,"Look-Ahead Curve",1,.25f,4,.8f);
        F(CombatFeelParameter.HorizontalStrength,g,"Horizontal Strength",1,0,3,1.15f);
        F(CombatFeelParameter.VerticalStrength,g,"Vertical Strength",1,0,3,1.15f);
        F(CombatFeelParameter.MaxScreenFraction,g,"Max Screen Fraction",.65f,.1f,1,.5f);
    }

    private static void AddTime(List<CombatFeelDescriptor> _, FloatAdder F, ToggleAdder T)
    {
        CombatFeelGroup g=CombatFeelGroup.Time;
        F(CombatFeelParameter.ShotFreeze,g,"Shot Freeze",0,0,.12f,.012f); F(CombatFeelParameter.ShotSlowdown,g,"Shot Slowdown",0,0,.4f,.035f);
        F(CombatFeelParameter.ShotSlowdownScale,g,"Shot Slowdown Scale",1,.05f,1,.75f); F(CombatFeelParameter.ShotRecovery,g,"Shot Recovery",.08f,.01f,.5f,.09f);
        F(CombatFeelParameter.HitFreeze,g,"Hit Freeze",0,0,.15f,.018f); F(CombatFeelParameter.HitSlowdown,g,"Hit Slowdown",0,0,.5f,.045f);
        F(CombatFeelParameter.HitSlowdownScale,g,"Hit Slowdown Scale",1,.05f,1,.65f); F(CombatFeelParameter.HitRecovery,g,"Hit Recovery",.1f,.01f,.6f,.12f);
        F(CombatFeelParameter.KillFreeze,g,"Kill Freeze",0,0,.2f,.032f); F(CombatFeelParameter.KillSlowdown,g,"Kill Slowdown",0,0,.8f,.08f);
        F(CombatFeelParameter.KillSlowdownScale,g,"Kill Slowdown Scale",1,.02f,1,.5f); F(CombatFeelParameter.KillRecovery,g,"Kill Recovery",.14f,.01f,1,.18f);
        F(CombatFeelParameter.EliteTimeEmphasis,g,"Elite Time Emphasis",1,0,3,1.3f); F(CombatFeelParameter.BossTimeEmphasis,g,"Boss Time Emphasis",1,0,3,1.5f);
        F(CombatFeelParameter.LocalEnemyFreeze,g,"Local Enemy Freeze",0,0,.2f,.025f); T(CombatFeelParameter.GlobalFreeze,g,"Global Freeze Enabled");
        F(CombatFeelParameter.FreezeBlend,g,"Local / Global Blend",0,0,1,.2f,"0 = local visual freeze, 1 = global time effect.");
    }

    private static void AddCrowd(List<CombatFeelDescriptor> _, FloatAdder F)
    {
        CombatFeelGroup g=CombatFeelGroup.Crowd;
        F(CombatFeelParameter.CrowdRadius,g,"Nearby Reaction Radius",0,0,12,4); F(CombatFeelParameter.CrowdStrength,g,"Nearby Reaction Strength",0,0,1,.12f);
        F(CombatFeelParameter.CrowdFalloff,g,"Nearby Reaction Falloff",1,.1f,4,1.5f); F(CombatFeelParameter.CrowdMaxTargets,g,"Max Nearby Targets",0,0,32,8);
        F(CombatFeelParameter.CrowdFlash,g,"Nearby Flash",0,0,1,.12f); F(CombatFeelParameter.CrowdWobble,g,"Nearby Wobble",0,0,20,2.5f);
        F(CombatFeelParameter.KillShockwave,g,"Kill Shockwave Visual",0,0,2,.5f); F(CombatFeelParameter.RapidKillWindow,g,"Rapid Kill Window",.5f,.05f,3,.65f);
        F(CombatFeelParameter.RapidKillGain,g,"Intensity Gain Per Kill",0,0,1,.12f); F(CombatFeelParameter.RapidKillMax,g,"Max Rapid-Kill Intensity",0,0,3,.6f);
        F(CombatFeelParameter.RapidKillDecay,g,"Rapid-Kill Decay Speed",1,.1f,5,1.5f);
    }

    private static void AddExperimental(List<CombatFeelDescriptor> _, FloatAdder F)
    {
        CombatFeelGroup g=CombatFeelGroup.Experimental;
        F(CombatFeelParameter.HitVignette,g,"Hit Vignette Pulse",0,0,1,.08f,"Applied only when an existing vignette target is available.");
        F(CombatFeelParameter.KillVignette,g,"Kill Vignette Pulse",0,0,1,.16f); F(CombatFeelParameter.ScreenBrightness,g,"Screen Brightness Pulse",0,0,1,.08f);
        F(CombatFeelParameter.ScreenSaturation,g,"Screen Saturation Pulse",0,-1,1,.08f); F(CombatFeelParameter.LocalHitLight,g,"Local Hit Light",0,0,1,.18f,"No heavy light stack is created; uses available impact particles.");
        F(CombatFeelParameter.LocalHitRadius,g,"Local Hit Radius",1,0,6,1.5f); F(CombatFeelParameter.LocalHitIntensity,g,"Local Hit Intensity",0,0,3,.35f);
        F(CombatFeelParameter.LocalHitLifetime,g,"Local Hit Lifetime",.05f,.01f,.3f,.065f); F(CombatFeelParameter.PreFireDuration,g,"Heavy Attack Pre-Fire Duration",0,0,.5f,.08f);
        F(CombatFeelParameter.PreFireGlow,g,"Pre-Fire Glow",0,0,2,.35f); F(CombatFeelParameter.PreFireCompression,g,"Pre-Fire Scale Compression",0,0,.5f,.1f);
        F(CombatFeelParameter.PreFireAimEmphasis,g,"Pre-Fire Aim Emphasis",0,0,1,.2f,"Opt-in presentation hook; never applied to all weapons automatically.");
    }
}
#endif
