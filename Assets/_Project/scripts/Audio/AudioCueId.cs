public enum AudioCueId
{
    None = 0,

    BunkerMusic = 1,
    RunMusic = 2,
    BunkerAmbience = 3,
    StartScreenMusic = 4,

    UIHover = 10,
    UIConfirm = 11,

    PistolShot = 20,
    LaserShot = 21,
    [System.Obsolete("Serialized ID reserved; rocket launch removed.")]
    RocketLaunch = 22,
    Explosion = 23,

    PlayerHurt = 30,
    PlayerDeath = 31,

    CommonEnemyDeath = 40,
    BossSpawn = 41,
    BossDeath = 42,

    XPPickup = 50,
    LevelUp = 51,

    Purchase = 60,
    PurchaseFail = 61,
    StartRun = 62,
    EnemyHit = 70,
    EnemyCritical = 71,
    OrbitalSwordHit = 80,
    OrbitalImpulseFire = 81,
    OrbitalArcFire = 82,
    CorePulse = 92,
    CoreCascade = 93,
    ModuleInstall = 94,
    RewardSelect = 95,
    CapsuleGlassTap = 100,
    // 101/102 retired: capsule unlock and vent are no longer part of the intro.
    CapsuleBodyResonance = 103,
    CapsuleGlassBreak = 104,

    PlayerDash = 110,
    BulletTimeEnter = 111,
    BulletTimeExit = 112,
    RocketWarning = 120,
    ElectricWarning = 121,
    BeamWarning = 122,
    RelayNodeActivated = 123,
    RelayPhaseChanged = 124,
    RelayCompleted = 125,
    RelayFailed = 126,
    MedkitPickup = 130,
    MagnetPickup = 131,
    GoldPickup = 132,
    CoreInvest = 133,
    CoreInvestComplete = 134,
    LootReelStart = 140,
    LootReelTick = 141,
    LootReelStop = 142,
    BomberExplosion = 143,
    RocketExplosion = 144,
    ChargedSniperFire = 145
}
