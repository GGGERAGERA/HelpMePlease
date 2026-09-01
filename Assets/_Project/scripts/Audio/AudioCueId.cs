public enum AudioCueId
{
    None = 0,

    BunkerMusic = 1,
    RunMusic = 2,
    BunkerAmbience = 3,

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
    StartRun = 62
}
