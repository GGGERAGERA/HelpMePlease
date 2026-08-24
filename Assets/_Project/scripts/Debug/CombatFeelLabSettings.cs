using System;
using System.Collections.Generic;
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
    PreFireDuration, PreFireGlow, PreFireCompression, PreFireAimEmphasis
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

    public CombatFeelDescriptor(
        CombatFeelParameter parameter, CombatFeelGroup group, string name,
        float neutral, float minimum, float maximum, float hard,
        string tooltip = null, bool toggle = false)
    {
        Parameter = parameter;
        Group = group;
        Name = name;
        Tooltip = tooltip;
        Neutral = neutral;
        Minimum = minimum;
        Maximum = maximum;
        Hard = hard;
        Toggle = toggle;
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
    private Dictionary<CombatFeelParameter, float> slotA;
    private Dictionary<CombatFeelParameter, float> slotB;
    private Dictionary<CombatFeelParameter, float> randomUndo;

    public static IReadOnlyList<CombatFeelDescriptor> Descriptors => descriptors;
    public CombatFeelGroup? SoloGroup { get; private set; }
    public bool HasA => slotA != null;
    public bool HasB => slotB != null;
    public bool CanUndoRandomize => randomUndo != null;
    public int Version { get; private set; }

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
            {
                float low = Mathf.Lerp(d.Neutral, d.Hard, 0.18f);
                float high = Mathf.Lerp(d.Neutral, d.Hard, 0.82f);
                Set(d.Parameter, UnityEngine.Random.Range(
                    Mathf.Min(low, high), Mathf.Max(low, high)));
            }
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
        float amount = preset switch
        {
            GroupPreset.Soft => 0.25f,
            GroupPreset.Medium => 0.5f,
            GroupPreset.Hard => 0.78f,
            _ => 1f
        };
        for (int i = 0; i < descriptors.Count; i++)
        {
            CombatFeelDescriptor d = descriptors[i];
            if (d.Group != group) continue;
            Set(d.Parameter, d.Toggle
                ? (amount >= 0.45f ? d.Hard : d.Neutral)
                : Mathf.Lerp(d.Neutral, d.Hard, amount));
        }
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

    private Dictionary<CombatFeelParameter, float> Snapshot() => new(values);
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
        F(CombatFeelParameter.TrailOpacity, CombatFeelGroup.Projectile, "Trail Opacity", 1, 0, 3, 1.25f);
        F(CombatFeelParameter.TrailTaper, CombatFeelGroup.Projectile, "Trail Taper", 1, 0, 2, 1.25f);
        F(CombatFeelParameter.SpeedIllusion, CombatFeelGroup.Projectile, "Speed Illusion Strength", 0, 0, 2, .65f, "Visual-only stretch/offset; physics speed is unchanged.");
        F(CombatFeelParameter.ForwardVisualOffset, CombatFeelGroup.Projectile, "Forward Visual Offset", 0, 0, 1, .12f);
        F(CombatFeelParameter.InitialStreakLength, CombatFeelGroup.Projectile, "Initial Streak Length", 0, 0, 4, .8f);
        F(CombatFeelParameter.InitialStreakLifetime, CombatFeelGroup.Projectile, "Initial Streak Lifetime", .06f, .01f, .3f, .08f);

        AddHit(d, F);
        AddTarget(d, F, T);
        AddKill(d, F, T);
        AddCamera(d, F);
        AddTime(d, F, T);
        AddCrowd(d, F);
        AddExperimental(d, F);
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

    private static void AddCamera(List<CombatFeelDescriptor> _, FloatAdder F)
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
