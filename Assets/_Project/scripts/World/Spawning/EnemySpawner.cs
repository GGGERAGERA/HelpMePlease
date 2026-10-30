using Random = GameplayRandom.SpawnRandom;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemySpawnStage
{
    public float startTime;
    public GameObject[] enemyPrefabs;
}

public partial class EnemySpawner : MonoBehaviour
{
    private static Collider2D[] additionalWaveOverlapBuffer =
        new Collider2D[32];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool recoilingShootersExperiment;
    private int recoilingEligibleSpawns;
    private readonly List<EnemyShooterMovement> recoilingShooters = new();
    public bool RecoilingShootersExperiment => recoilingShootersExperiment;

    public void DebugSetRecoilingShootersExperiment(bool enabled)
    {
        if (enabled)
        {
            if (RunStateManager.Instance == null || !isActiveAndEnabled) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        recoilingShootersExperiment = enabled;
        if (enabled) return;
        foreach (var shooter in recoilingShooters)
            if (shooter != null) shooter.ConfigureRecoilingExperiment(false);
        recoilingShooters.Clear();
        recoilingEligibleSpawns = 0;
    }

    public static bool IsRecoilingShooterPrefabEligible(GameObject enemy) =>
        enemy != null &&
        enemy.GetComponent<EnemyShooterMovement>() is { HasRecoilingVisual: true } shooter &&
        shooter.ProjectilePrefab != null &&
        enemy.GetComponent<EnemyHealth>() is { IsBoss: false, HasExperienceLoot: true } &&
        enemy.GetComponent<CarrierEscapeBehaviour>() == null &&
        enemy.GetComponent<TurretEnemyBehaviour>() == null &&
        !(enemy.GetComponent<EnemyChaseMovement>() is { HasAssaultDestination: true });

    public static bool IsRecoilingShooterEligible(GameObject enemy) =>
        IsRecoilingShooterPrefabEligible(enemy) &&
        enemy.GetComponent<EnemyShooterMovement>() is
            { CanUseRecoiling: true, RecoilingExperiment: false };

    private bool AssignRecoilingShooter(GameObject enemy)
    {
        if (!recoilingShootersExperiment || RunStateManager.Instance == null ||
            !IsRecoilingShooterEligible(enemy)) return false;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var shooter = enemy.GetComponent<EnemyShooterMovement>();
        shooter.ConfigureRecoilingExperiment(true);
        if (!shooter.RecoilingExperiment) return false;
        recoilingShooters.RemoveAll(item => item == null || !item.RecoilingExperiment);
        recoilingShooters.Add(shooter);
        return true;
    }

    private void TryAssignRecoilingShooter(GameObject enemy, bool ordinarySpawn)
    {
        if (!recoilingShootersExperiment || !ordinarySpawn || RunStateManager.Instance == null ||
            !IsRecoilingShooterEligible(enemy)) return;
        if (++recoilingEligibleSpawns % 4 == 0) AssignRecoilingShooter(enemy);
    }

    public GameObject DebugSpawnRecoilingShooterAround(GameObject prefab, Vector3 origin, out string status)
    {
        status = "Включите эксперимент стрелка с отходом.";
        if (!recoilingShootersExperiment) return null;
        status = "Нужен активный спавнер в готовом MVP.";
        var run = RunStateManager.Instance;
        if (!isActiveAndEnabled || !initialized || run == null || run.IsRunEnded ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MVP" ||
            SceneTransitionOverlay.IsTransitioning) return null;
        var livePlayer = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        status = "Нет готового живого игрока.";
        if (livePlayer == null || !livePlayer.gameObject.activeInHierarchy ||
            PlayerRuntimeReference.PlayerHealth == null || PlayerRuntimeReference.PlayerHealth.IsDead) return null;
        var station = livePlayer.GetComponentInChildren<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>();
        var choice = FindFirstObjectByType<LevelChoiceManager>();
        status = "Занято модальное состояние игрока.";
        if (station == null || !station.IsInitialized || station.IsRunEnded ||
            station.InputOwner == null || !station.InputOwner.IsIdle ||
            UpgradeManager.Instance == null || !UpgradeManager.Instance.IsRewardQueueIdle ||
            WorldLootRewardReel.IsActive || (choice != null && choice.IsChoosing)) return null;
        status = "В профиле нужен подходящий стрелок с отдельной моделью.";
        if (!IsRecoilingShooterPrefabEligible(prefab)) return null;
        run.DebugMarkDevelopmentRun();
        var enemy = SpawnSpecificEnemyAround(prefab, origin, 3f, 8f, 3f, false, .45f);
        status = "Нет безопасного места для стрелка с отходом.";
        if (enemy == null) return null;
        if (!AssignRecoilingShooter(enemy))
        {
            Destroy(enemy);
            status = "Созданный стрелок не готов к отходу.";
            return null;
        }
        status = "Стрелок с отходом создан.";
        return enemy;
    }

    private bool interceptingBombersExperiment;
    private int interceptingBomberEligibleSpawns;
    private readonly List<EnemyBomberMovement> interceptingBombers = new();
    public bool InterceptingBombersExperiment => interceptingBombersExperiment;

    public void DebugSetInterceptingBombersExperiment(bool enabled)
    {
        if (enabled)
        {
            if (RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        interceptingBombersExperiment = enabled;
        if (!enabled)
        {
            foreach (var bomber in interceptingBombers)
                if (bomber != null) bomber.ConfigureInterceptingExperiment(false);
            interceptingBombers.Clear();
            interceptingBomberEligibleSpawns = 0;
        }
    }

    // Does not depend on runtime Awake state and never initializes the prefab.
    public static bool IsInterceptingBomberPrefabEligible(GameObject enemy) =>
        enemy != null &&
        enemy.GetComponent<EnemyBomberMovement>() is { HasInterceptingVisual: true } &&
        enemy.GetComponent<EnemyHealth>() is { IsBoss: false, HasExperienceLoot: true } &&
        enemy.GetComponent<CarrierEscapeBehaviour>() == null &&
        enemy.GetComponent<TurretEnemyBehaviour>() == null;

    public static bool IsInterceptingBomberEligible(GameObject enemy) =>
        IsInterceptingBomberPrefabEligible(enemy) &&
        enemy.GetComponent<EnemyBomberMovement>() is
            { CanUseIntercepting: true, InterceptingExperiment: false };

    private bool AssignInterceptingBomber(GameObject enemy)
    {
        if (!interceptingBombersExperiment || RunStateManager.Instance == null ||
            !IsInterceptingBomberEligible(enemy)) return false;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var bomber = enemy.GetComponent<EnemyBomberMovement>();
        bomber.ConfigureInterceptingExperiment(true);
        if (!bomber.InterceptingExperiment) return false;
        interceptingBombers.RemoveAll(item => item == null || !item.InterceptingExperiment);
        interceptingBombers.Add(bomber);
        return true;
    }

    private void TryAssignInterceptingBomber(GameObject enemy, bool ordinarySpawn)
    {
        if (!interceptingBombersExperiment || !ordinarySpawn ||
            RunStateManager.Instance == null || !IsInterceptingBomberEligible(enemy)) return;
        if (++interceptingBomberEligibleSpawns % 4 == 0) AssignInterceptingBomber(enemy);
    }

    public GameObject DebugSpawnInterceptingBomberAround(GameObject prefab, Vector3 origin, out string status)
    {
        status = "Включите эксперимент перехвата.";
        if (!interceptingBombersExperiment) return null;
        status = "Нужен активный спавнер в готовом MVP.";
        var run = RunStateManager.Instance;
        if (!isActiveAndEnabled || !initialized || run == null || run.IsRunEnded ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MVP" ||
            SceneTransitionOverlay.IsTransitioning) return null;
        var livePlayer = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        status = "Нет готового живого игрока.";
        if (livePlayer == null || !livePlayer.gameObject.activeInHierarchy ||
            PlayerRuntimeReference.PlayerHealth == null || PlayerRuntimeReference.PlayerHealth.IsDead) return null;
        var station = livePlayer.GetComponentInChildren<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>();
        var choice = FindFirstObjectByType<LevelChoiceManager>();
        status = "Занято модальное состояние игрока.";
        if (station == null || !station.IsInitialized || station.IsRunEnded ||
            station.InputOwner == null || !station.InputOwner.IsIdle ||
            UpgradeManager.Instance == null || !UpgradeManager.Instance.IsRewardQueueIdle ||
            WorldLootRewardReel.IsActive || (choice != null && choice.IsChoosing)) return null;
        status = "В профиле нужен подходящий подрывник с отдельной моделью.";
        if (!IsInterceptingBomberPrefabEligible(prefab)) return null;
        run.DebugMarkDevelopmentRun();
        var enemy = SpawnSpecificEnemyAround(prefab, origin, 3f, 8f, 3f, false, .45f);
        status = "Нет безопасного места для подрывника-перехватчика.";
        if (enemy == null) return null;
        if (!AssignInterceptingBomber(enemy))
        {
            Destroy(enemy);
            status = "Созданный подрывник не готов к перехвату.";
            return null;
        }
        status = "Подрывник-перехватчик создан.";
        return enemy;
    }

    private bool strafingShootersExperiment;
    private int strafingEligibleSpawns;
    private int strafingAssignedSpawns;
    private readonly List<EnemyShooterMovement> strafingShooters = new();
    public bool StrafingShootersExperiment => strafingShootersExperiment;

    public void DebugSetStrafingShootersExperiment(bool enabled)
    {
        if (enabled)
        {
            if (RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        strafingShootersExperiment = enabled;
        if (!enabled)
        {
            foreach (var shooter in strafingShooters)
                if (shooter != null) shooter.ConfigureStrafingExperiment(false, false);
            strafingShooters.Clear();
            strafingEligibleSpawns = 0;
            strafingAssignedSpawns = 0;
        }
    }

    public static bool IsStrafingShooterEligible(GameObject enemy) =>
        enemy != null &&
        enemy.GetComponent<EnemyShooterMovement>() is { CanUseStrafing: true, StrafingExperiment: false } &&
        enemy.GetComponent<EnemyHealth>() is { IsBoss: false, IsDead: false, HasExperienceLoot: true } &&
        enemy.GetComponent<CarrierEscapeBehaviour>() == null &&
        enemy.GetComponent<TurretEnemyBehaviour>() == null;

    private bool AssignStrafingShooter(GameObject enemy)
    {
        if (!strafingShootersExperiment || RunStateManager.Instance == null ||
            !IsStrafingShooterEligible(enemy)) return false;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var shooter = enemy.GetComponent<EnemyShooterMovement>();
        shooter.ConfigureStrafingExperiment(true, strafingAssignedSpawns % 2 == 0);
        if (!shooter.StrafingExperiment) return false;
        strafingAssignedSpawns++;
        strafingShooters.RemoveAll(item => item == null);
        strafingShooters.Add(shooter);
        return true;
    }

    private void TryAssignStrafingShooter(GameObject enemy, bool ordinarySpawn)
    {
        if (!strafingShootersExperiment || !ordinarySpawn || !IsStrafingShooterEligible(enemy)) return;
        if (++strafingEligibleSpawns % 4 == 0) AssignStrafingShooter(enemy);
    }

    public GameObject DebugSpawnStrafingShooterAround(GameObject prefab, Vector3 origin, out string status)
    {
        status = "Включите эксперимент траверса; нужен подходящий стрелок и Development-попытка.";
        if (!strafingShootersExperiment || RunStateManager.Instance == null ||
            !IsStrafingShooterEligible(prefab)) return null;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var enemy = SpawnSpecificEnemyAround(prefab, origin, 3f, 8f, 3f, false, .45f);
        status = "Нет безопасного места для стрелка на траверсе.";
        if (enemy == null) return null;
        if (!AssignStrafingShooter(enemy))
        {
            Destroy(enemy);
            status = "Созданный стрелок не подходит для траверса.";
            return null;
        }
        status = "Стрелок на траверсе создан.";
        return enemy;
    }

    private bool blockLockedShieldsExperiment;
    private int blockLockedShieldEligibleSpawns;
    private readonly List<EnemyDirectionalShield> blockLockedShields = new();
    public bool BlockLockedShieldsExperiment => blockLockedShieldsExperiment;

    public void DebugSetBlockLockedShieldsExperiment(bool enabled)
    {
        if (enabled)
        {
            if (RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        blockLockedShieldsExperiment = enabled;
        if (!enabled)
        {
            foreach (var shield in blockLockedShields)
                if (shield != null) shield.ConfigureBlockLockedExperiment(false);
            blockLockedShields.Clear();
            blockLockedShieldEligibleSpawns = 0;
        }
    }

    // Assets have not run OnEnable: durability/death are meaningful only on live actors.
    public static bool IsBlockLockedShieldPrefabEligible(GameObject prefab) =>
        HasBlockLockedShieldComponents(prefab) &&
        prefab.GetComponent<EnemyDirectionalShield>() is var shield &&
        (shield.Kind == EnemyDirectionalShield.ShieldKind.Durable || shield.MaximumBlockedHits > 0);

    public static bool IsBlockLockedShieldEligible(GameObject enemy) =>
        HasBlockLockedShieldComponents(enemy) &&
        !enemy.GetComponent<EnemyDirectionalShield>().IsBroken &&
        !enemy.GetComponent<EnemyHealth>().IsDead;

    private static bool HasBlockLockedShieldComponents(GameObject enemy) =>
        enemy != null &&
        enemy.GetComponent<EnemyDirectionalShield>() is { enabled: true } &&
        enemy.GetComponent<EnemyHealth>() is { IsBoss: false, HasExperienceLoot: true } &&
        (enemy.GetComponent<EnemyChaseMovement>() is { enabled: true, HasAssaultDestination: false } ||
         enemy.GetComponent<EnemyShooterMovement>() is { enabled: true }) &&
        !(enemy.GetComponent<EnemyChaseMovement>() is { HasAssaultDestination: true }) &&
        enemy.GetComponent<CarrierEscapeBehaviour>() == null &&
        enemy.GetComponent<TurretEnemyBehaviour>() == null;

    private void AssignBlockLockedShield(GameObject enemy)
    {
        if (RunStateManager.Instance == null || !IsBlockLockedShieldEligible(enemy)) return;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var shield = enemy.GetComponent<EnemyDirectionalShield>();
        shield.ConfigureBlockLockedExperiment(true);
        blockLockedShields.RemoveAll(item => item == null);
        if (!blockLockedShields.Contains(shield)) blockLockedShields.Add(shield);
    }

    private void TryAssignBlockLockedShield(GameObject enemy, bool ordinarySpawn)
    {
        if (!blockLockedShieldsExperiment || !ordinarySpawn || !IsBlockLockedShieldEligible(enemy)) return;
        if (++blockLockedShieldEligibleSpawns % 4 == 0) AssignBlockLockedShield(enemy);
    }

    public GameObject DebugSpawnBlockLockedShieldAround(GameObject prefab, Vector3 origin, out string status)
    {
        status = "Включите эксперимент щита; нужен подходящий prefab и Development-попытка.";
        if (!blockLockedShieldsExperiment || RunStateManager.Instance == null || !IsBlockLockedShieldPrefabEligible(prefab))
            return null;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var enemy = SpawnSpecificEnemyAround(prefab, origin, 3f, 8f, 3f, false, .45f);
        status = "Нет безопасного места для заклинивающего щита.";
        if (enemy == null) return null;
        // Manual spawn does not participate in ordinary cadence.
        AssignBlockLockedShield(enemy);
        if (!enemy.GetComponent<EnemyDirectionalShield>().IsBlockLockedExperiment)
        {
            Destroy(enemy);
            status = "Созданный враг не подходит для эксперимента щита.";
            return null;
        }
        status = "Враг с заклинивающим щитом создан.";
        return enemy;
    }

    private bool woundedRetreatExperiment;
    private int retreatEligibleSpawns;
    private readonly List<EnemyChaseMovement> retreatChasers = new();
    public bool WoundedRetreatExperiment => woundedRetreatExperiment;

    public void DebugSetWoundedRetreatExperiment(bool enabled)
    {
        if (enabled)
        {
            if (!isActiveAndEnabled || RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        woundedRetreatExperiment = enabled;
        if (!enabled)
        {
            foreach (var chase in retreatChasers)
                if (chase != null) chase.ConfigureWoundedRetreatExperiment(false);
            retreatChasers.Clear();
            retreatEligibleSpawns = 0;
        }
    }

    public static bool IsWoundedRetreatEligible(GameObject enemy) =>
        enemy != null && enemy.GetComponent<EnemyChaseMovement>() is
            { CanUseWoundedRetreat: true, HasAssaultDestination: false, IsFlankingExperiment: false, IsCommittedRamExperiment: false, IsWoundedRetreatExperiment: false } &&
        MatchesDebugArchetype(enemy, DebugEnemyArchetype.Basic) &&
        enemy.GetComponent<EnemyHealth>() is { IsBoss: false, HasExperienceLoot: true } &&
        enemy.GetComponent<EnemyShooterMovement>() == null &&
        enemy.GetComponent<CarrierEscapeBehaviour>() == null &&
        enemy.GetComponent<TurretEnemyBehaviour>() == null &&
        enemy.GetComponent<EnemyDirectionalShield>() == null;

    private void AssignWoundedRetreat(GameObject enemy)
    {
        if (RunStateManager.Instance == null || !IsWoundedRetreatEligible(enemy)) return;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var chase = enemy.GetComponent<EnemyChaseMovement>();
        chase.ConfigureWoundedRetreatExperiment(true);
        retreatChasers.RemoveAll(item => item == null);
        if (!chase.IsWoundedRetreatExperiment) return;
        if (!retreatChasers.Contains(chase)) retreatChasers.Add(chase);
    }

    private void TryAssignWoundedRetreat(GameObject enemy, bool ordinarySpawn)
    {
        if (!woundedRetreatExperiment || !ordinarySpawn || !IsWoundedRetreatEligible(enemy)) return;
        if (++retreatEligibleSpawns % 4 == 0) AssignWoundedRetreat(enemy);
    }

    public GameObject DebugSpawnWoundedRetreatAround(GameObject prefab, Vector3 origin, out string status)
    {
        status = "Нужны активный спавнер, живой игрок и подходящий prefab текущего профиля; включите эксперимент отхода.";
        var playerHealth = PlayerRuntimeReference.ResolvePlayerHealth(forceLookup: true);
        if (!isActiveAndEnabled || playerHealth == null || playerHealth.IsDead ||
            !woundedRetreatExperiment || RunStateManager.Instance == null || !IsWoundedRetreatEligible(prefab))
            return null;
        bool inCurrentProfile = false;
        if (spawnProfile?.Phases != null)
            foreach (var phase in spawnProfile.Phases)
                if (phase?.enemies != null)
                    foreach (var entry in phase.enemies)
                        if (entry?.enemyPrefab == prefab) inCurrentProfile = true;
        if (!inCurrentProfile) return null;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var enemy = SpawnSpecificEnemyAround(prefab, origin, 3f, 8f, 3f, false, .45f);
        status = "Нет безопасного места для отступающего преследователя.";
        if (enemy == null) return null;
        AssignWoundedRetreat(enemy);
        if (!enemy.GetComponent<EnemyChaseMovement>().IsWoundedRetreatExperiment)
        {
            Destroy(enemy);
            status = "Созданный враг не подходит для отхода.";
            return null;
        }
        status = "Отступающий преследователь создан.";
        return enemy;
    }

    private bool committedRamExperiment;
    private int ramEligibleSpawns;
    private readonly List<EnemyChaseMovement> ramChasers = new();
    public bool CommittedRamExperiment => committedRamExperiment;

    public void DebugSetCommittedRamExperiment(bool enabled)
    {
        if (enabled)
        {
            if (RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        committedRamExperiment = enabled;
        if (!enabled)
        {
            foreach (var chase in ramChasers)
                if (chase != null) chase.ConfigureCommittedRamExperiment(false);
            ramChasers.Clear();
            ramEligibleSpawns = 0;
        }
    }

    public static bool IsCommittedRamEligible(GameObject enemy) =>
        enemy != null && enemy.GetComponent<EnemyChaseMovement>() is
            { CanUseCommittedRam: true, HasAssaultDestination: false, IsFlankingExperiment: false } &&
        MatchesDebugArchetype(enemy, DebugEnemyArchetype.Basic) &&
        enemy.GetComponent<EnemyHealth>() is { IsBoss: false, HasExperienceLoot: true } &&
        enemy.GetComponent<CarrierEscapeBehaviour>() == null &&
        enemy.GetComponent<TurretEnemyBehaviour>() == null &&
        enemy.GetComponent<EnemyDirectionalShield>() == null;

    private void AssignCommittedRam(GameObject enemy)
    {
        if (RunStateManager.Instance == null || !IsCommittedRamEligible(enemy)) return;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var chase = enemy.GetComponent<EnemyChaseMovement>();
        chase.ConfigureCommittedRamExperiment(true);
        ramChasers.RemoveAll(item => item == null);
        if (!ramChasers.Contains(chase)) ramChasers.Add(chase);
    }

    private void TryAssignCommittedRam(GameObject enemy, bool ordinarySpawn)
    {
        if (!committedRamExperiment || !ordinarySpawn || !IsCommittedRamEligible(enemy)) return;
        if (++ramEligibleSpawns % 4 == 0) AssignCommittedRam(enemy);
    }

    public GameObject DebugSpawnCommittedRamAround(GameObject prefab, Vector3 origin, out string status)
    {
        status = "Включите таранный эксперимент; нужен подходящий prefab и Development-попытка.";
        if (!committedRamExperiment || RunStateManager.Instance == null || !IsCommittedRamEligible(prefab))
            return null;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var enemy = SpawnSpecificEnemyAround(prefab, origin, 3f, 8f, 3f, false, .45f);
        status = "Нет безопасного места для таранного преследователя.";
        if (enemy == null) return null;
        AssignCommittedRam(enemy);
        status = "Таранный преследователь создан.";
        return enemy;
    }

    private bool committedVolleyExperiment;
    private int volleyEligibleSpawns;
    private readonly List<EnemyShooterMovement> volleyShooters = new();
    public bool CommittedVolleyExperiment => committedVolleyExperiment;

    public void DebugSetCommittedVolleyExperiment(bool enabled)
    {
        if (enabled)
        {
            if (RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        committedVolleyExperiment = enabled;
        if (!enabled)
        {
            foreach (var shooter in volleyShooters)
                if (shooter != null) shooter.ConfigureCommittedVolleyExperiment(false);
            volleyShooters.Clear();
            volleyEligibleSpawns = 0;
        }
    }

    public static bool IsCommittedVolleyEligible(GameObject enemy) =>
        enemy != null &&
        enemy.GetComponent<EnemyShooterMovement>() is { CanUseCommittedVolley: true } &&
        enemy.GetComponent<EnemyHealth>() is { IsBoss: false, HasExperienceLoot: true } &&
        enemy.GetComponent<CarrierEscapeBehaviour>() == null &&
        enemy.GetComponent<TurretEnemyBehaviour>() == null &&
        enemy.GetComponent<EnemyDirectionalShield>() == null;

    private void AssignCommittedVolley(GameObject enemy)
    {
        if (RunStateManager.Instance == null || !IsCommittedVolleyEligible(enemy)) return;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var shooter = enemy.GetComponent<EnemyShooterMovement>();
        shooter.ConfigureCommittedVolleyExperiment(true);
        volleyShooters.RemoveAll(item => item == null);
        volleyShooters.Add(shooter);
    }

    private void TryAssignCommittedVolley(GameObject enemy, bool ordinarySpawn)
    {
        if (!committedVolleyExperiment || !ordinarySpawn || !IsCommittedVolleyEligible(enemy)) return;
        if (++volleyEligibleSpawns % 3 == 0) AssignCommittedVolley(enemy);
    }

    public GameObject DebugSpawnCommittedVolleyAround(GameObject prefab, Vector3 origin, out string status)
    {
        status = "Включите эксперимент залпового стрелка; нужен подходящий prefab и Development-попытка.";
        if (!committedVolleyExperiment || RunStateManager.Instance == null ||
            !IsCommittedVolleyEligible(prefab)) return null;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var enemy = SpawnSpecificEnemyAround(prefab, origin, 3f, 8f, 3f, false, 0.45f);
        status = "Нет безопасного места для залпового стрелка.";
        if (enemy == null) return null;
        AssignCommittedVolley(enemy);
        status = "Залповый стрелок создан.";
        return enemy;
    }

    private bool flankingChasersExperiment;
    private int flankingEligibleSpawns;
    private int flankingAssignedSpawns;
    private readonly List<EnemyChaseMovement> flankingChasers = new();
    public bool FlankingChasersExperiment => flankingChasersExperiment;

    public void DebugSetFlankingChasersExperiment(bool enabled)
    {
        if (enabled)
        {
            // Never enable experimental pressure without the attempt's progress guard.
            if (RunStateManager.Instance == null) return;
            RunStateManager.Instance.DebugMarkDevelopmentRun();
        }
        flankingChasersExperiment = enabled;
        if (!enabled)
        {
            foreach (var chase in flankingChasers)
                if (chase != null) chase.ConfigureFlankingExperiment(false, false);
            flankingChasers.Clear();
            flankingEligibleSpawns = 0;
            flankingAssignedSpawns = 0;
        }
    }

    private void ResetFlankingChasersExperiment()
    {
        DebugSetFlankingChasersExperiment(false);
    }

    private void AssignFlankingChaser(GameObject enemy, bool clockwise)
    {
        var chase = enemy.GetComponent<EnemyChaseMovement>();
        chase.ConfigureFlankingExperiment(true, clockwise);
        flankingChasers.RemoveAll(item => item == null);
        if (!flankingChasers.Contains(chase)) flankingChasers.Add(chase);
    }

    private void TryAssignFlankingChaser(GameObject enemy, bool ordinarySpawn)
    {
        if (!flankingChasersExperiment || !ordinarySpawn) return;
        if (!IsFlankingChaserEligible(enemy)) return;
        if (++flankingEligibleSpawns % 4 != 0) return;
        AssignFlankingChaser(enemy, (++flankingAssignedSpawns & 1) == 0);
    }

    public static bool IsFlankingChaserEligible(GameObject enemy) =>
        enemy != null && enemy.GetComponent<EnemyChaseMovement>() is { HasAssaultDestination: false } &&
        MatchesDebugArchetype(enemy, DebugEnemyArchetype.Basic) &&
        enemy.GetComponent<EnemyDirectionalShield>() == null &&
        enemy.GetComponent<EnemyHealth>() is { HasExperienceLoot: true };

    public GameObject DebugSpawnFlankingChaserAround(GameObject prefab, Vector3 origin, out string status)
    {
        status = "Включите эксперимент флангового преследователя.";
        if (!flankingChasersExperiment) return null;
        status = "RunStateManager не готов: Development-попытка недоступна.";
        if (RunStateManager.Instance == null) return null;
        status = "Нет подходящего prefab флангового преследователя.";
        if (!IsFlankingChaserEligible(prefab)) return null;
        RunStateManager.Instance.DebugMarkDevelopmentRun();
        var enemy = SpawnSpecificEnemyAround(prefab, origin, 3f, 8f, 3f, false, 0.45f);
        status = "Нет безопасного места для флангового преследователя.";
        if (enemy == null) return null;
        // Manual instances do not advance either ordinary-spawn cadence counter.
        AssignFlankingChaser(enemy, false);
        status = "Фланговый преследователь создан.";
        return enemy;
    }

    public enum DebugEnemyArchetype
    {
        Basic,
        Elite,
        Shooter,
        Bomber,
        Eyes,
        Turret
    }

    [Header("Feel Test Production Prefab Catalog")]
    [SerializeField] private GameObject debugBasicPrefab;
    [SerializeField] private GameObject debugElitePrefab;
    [SerializeField] private GameObject debugShooterPrefab;
#endif
    [Header("Legacy Spawn Settings")]
    [Tooltip("Used when the selected level has no EnemySpawnProfile.")]
    public GameObject[] enemyPrefabs;
    public float spawnInterval = 2f;
    public int maxEnemies = 10;

    [Header("Spawn Distance")]
    public float minSpawnDistance = 5f;
    public float maxSpawnDistance = 12f;
    public float spawnRadius = 360f;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 16;
    [SerializeField] private GameplayAreaService gameplayArea;

    [Header("Enemy Projectile Pools")]
    [SerializeField, Min(0)] private int projectilePrewarmPerPrefab = 24;
    [SerializeField, Min(1)] private int projectilePoolMaximum = 256;

    [Header("Enemy Feedback Pools")]
    [SerializeField, Min(0)] private int feedbackPrewarmPerPrefab = 16;
    [SerializeField, Min(1)] private int feedbackPoolMaximum = 256;

    [Header("Legacy Difficulty Scaling")]
    [SerializeField] private float difficultyIncreaseInterval = 30f;
    [SerializeField] private float spawnRateMultiplier = 0.9f;
    [SerializeField] private float enemyHealthMultiplier = 1.1f;
    [SerializeField] private float enemySpeedMultiplier = 1.1f;
    [SerializeField, Min(0)] private int maxEnemiesIncreasePerStep = 5;
    [SerializeField, Min(1)] private int legacyStepsPerBatchIncrease = 1;

    [Header("Spawn Density Scaling")]
    [SerializeField, Min(1)] private int baseEnemiesPerCycle = 1;
    [SerializeField, Min(1)] private int phasesPerBatchIncrease = 2;
    [SerializeField, Min(0.01f)] private float spawnPressurePerBatchIncrease = 0.5f;
    [SerializeField, Min(1)] private int maxEnemiesPerCycle = 4;

    [Header("Difficulty Limits")]
    [SerializeField] private float minSpawnInterval = 0.4f;
    [SerializeField] private float maxHealthMultiplier = 3f;
    [SerializeField] private float maxSpeedMultiplier = 1.8f;
    [SerializeField, Min(1)] private int maxAliveLimit = 120;

    [Header("Legacy Spawn Stages")]
    [SerializeField] private EnemySpawnStage[] spawnStages;

    private sealed class SpawnedEnemy
    {
        public GameObject instance;
        public GameObject sourcePrefab;
        public string enemyId;
        public EnemyMovement movement;
        public float baseSpeedMultiplier;
    }

    private readonly List<SpawnedEnemy> activeEnemies = new();
    private readonly Dictionary<GameObject, SimplePrefabPool>
        enemyProjectilePools = new();
    private readonly Dictionary<GameObject, SimplePrefabPool>
        enemyFeedbackPools = new();

    private Transform player;
    private CharacterSpawner characterSpawner;
    public void BindScene(CharacterSpawner characters, GameplayAreaService area)
    {
        characterSpawner = characters;
        gameplayArea = area;
    }
    private EnemySpawnProfile spawnProfile;
    private EnemySpawnPhase activePhase;
    private int activePhaseIndex = -1;
    private int currentRunLevel = 1;

    private float spawnTimer;
    private float difficultyTimer;
    private float runTime;
    private int legacyDifficultySteps;
    private bool spawningEnabled = true;
    private bool initialized;

    private float currentHealthMultiplier = 1f;
    private float currentSpeedMultiplier = 1f;
    private float currentSpawnPressure = 1f;
    private float worldRuleSpawnPressureMultiplier = 1f;
    private float worldEventSpawnPressureMultiplier = 1f;
    private float worldAccelerationMultiplier = 1f;

    private bool runThreatControlsPhase;
    private float runThreatSpawnIntervalMultiplier = 1f;
    private int runThreatMaxAliveCap;
    private int runThreatBatchSize = 1;

    public float WorldEventSpawnPressureMultiplier =>
        worldEventSpawnPressureMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool IsSpawningEnabled => spawningEnabled;
    public int DebugTrackedEnemyCount => activeEnemies.Count;
#endif

    private float baseSpawnInterval;
    private int baseMaxEnemies;
    private float baseHealthMultiplier;
    private float baseSpeedMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool debugFixedExplorationPressure;
#endif

    private void Start()
    {
        player = characterSpawner != null ? characterSpawner.SpawnedPlayer?.transform : PlayerRuntimeReference.CachedPlayer?.transform;
        ResolveGameplayArea();
        if (!initialized) CaptureBaseSettings();
        PrewarmProjectilePools(enemyPrefabs);
        ResetAssaultEvents();
    }

    private void Update()
    {
        if (!spawningEnabled || Time.timeScale == 0f)
            return;
        if (TutorialController.IsTutorialSector) return;

        if (player == null)
            player = characterSpawner != null ? characterSpawner.SpawnedPlayer?.transform : PlayerRuntimeReference.CachedPlayer?.transform;

        if (player == null)
            return;

        runTime += Time.deltaTime;
        if (!TutorialController.IsActive) UpdateAssaultEvents();
        if (!runThreatControlsPhase)
            UpdateActivePhase();

        if (spawnProfile != null && activePhase == null)
            return;

        if (spawnProfile == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugFixedExplorationPressure)
#endif
                UpdateLegacyDifficulty();
        }

        if (IsAssaultBreathing)
        {
            spawnTimer = 0f;
            return;
        }
        float refillSpeed = NormalRefillMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        refillSpeed *= debugCaptureDensity;
#endif
        spawnTimer += Time.deltaTime * refillSpeed;

        if (spawnTimer < GetCurrentSpawnInterval())
            return;

        spawnTimer = 0f;
        SpawnCycle();
    }

    public void SetSpawnProfile(EnemySpawnProfile profile, int runLevel)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugSetInterceptingBombersExperiment(false);
        DebugSetStrafingShootersExperiment(false);
        DebugSetRecoilingShootersExperiment(false);
        DebugSetCommittedVolleyExperiment(false);
        DebugSetCommittedRamExperiment(false);
        DebugSetWoundedRetreatExperiment(false);
        DebugSetBlockLockedShieldsExperiment(false);
        ResetFlankingChasersExperiment();
#endif
        spawnProfile = profile;
        currentRunLevel = Mathf.Max(1, runLevel);
        activePhase = null;
        activePhaseIndex = -1;
        runThreatControlsPhase = false;
        runThreatSpawnIntervalMultiplier = 1f;
        runThreatMaxAliveCap = 0;
        runThreatBatchSize = 1;
        runTime = 0f;
        spawnTimer = 0f;
        ResetAssaultEvents();
        legacyDifficultySteps = 0;

        if (spawnProfile == null)
        {
            Debug.Log("[EnemySpawner] No spawn profile selected; using legacy Inspector settings.");
            return;
        }

        PrewarmProjectilePools(spawnProfile);
        UpdateActivePhase();
    }

    public void SetRunThreatPreset(
        int presetIndex,
        float spawnIntervalMultiplier,
        int maxAliveCap,
        int batchSize)
    {
        runThreatControlsPhase = true;
        runThreatSpawnIntervalMultiplier = Mathf.Max(
            0.1f,
            spawnIntervalMultiplier
        );
        runThreatMaxAliveCap = Mathf.Clamp(
            maxAliveCap,
            1,
            Mathf.Min(40, Mathf.Max(1, maxAliveLimit))
        );
        runThreatBatchSize = Mathf.Clamp(
            batchSize,
            1,
            Mathf.Max(1, maxEnemiesPerCycle)
        );

        ApplyProfilePhase(presetIndex);
    }

    public void SetLevelScaling(
        float healthMultiplier,
        float speedMultiplier,
        float spawnPressure)
    {
        if (!initialized)
            CaptureBaseSettings();

        currentHealthMultiplier = Mathf.Max(0.1f, healthMultiplier);
        currentSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        currentSpawnPressure = Mathf.Max(0.1f, spawnPressure);

        if (spawnProfile == null)
        {
            spawnInterval = Mathf.Max(
                minSpawnInterval,
                baseSpawnInterval / currentSpawnPressure
            );
            maxEnemies = Mathf.Clamp(
                Mathf.RoundToInt(baseMaxEnemies * currentSpawnPressure),
                baseMaxEnemies,
                Mathf.Max(baseMaxEnemies, maxAliveLimit)
            );
        }

        Debug.Log(
            $"[EnemySpawner] Level scaling: HP x{currentHealthMultiplier:F2}, " +
            $"speed x{currentSpeedMultiplier:F2}, pressure x{currentSpawnPressure:F2}."
        );
    }

    public void StopSpawning()
    {
        EndAssault();
        breathRemaining = recoveryRemaining = 0f;
        spawnTimer = 0f;
        spawningEnabled = false;
        CancelPendingAssault();
        Debug.Log("[EnemySpawner] Spawning stopped.");
    }

    private void OnEnable() => RunStateManager.EnsureExists().RegisterSceneCleanup(ReleaseRunScene);
    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugSetInterceptingBombersExperiment(false);
        DebugSetStrafingShootersExperiment(false);
        DebugSetRecoilingShootersExperiment(false);
        ResetFlankingChasersExperiment();
        DebugSetCommittedVolleyExperiment(false);
        DebugSetCommittedRamExperiment(false);
        DebugSetWoundedRetreatExperiment(false);
        DebugSetBlockLockedShieldsExperiment(false);
#endif
        RunStateManager.Instance?.UnregisterSceneCleanup(ReleaseRunScene);
        CancelPendingAssault();
    }

    private void ReleaseRunScene()
    {
        StopSpawning();
        enabled = false;
    }

    public void ResumeSpawning()
    {
        spawningEnabled = true;
        Debug.Log("[EnemySpawner] Spawning resumed.");
    }

    public EnemyHealth SpawnTutorialEnemy(Vector3 origin)
    {
        var candidates = new List<GameObject>();
        if (enemyPrefabs != null) candidates.AddRange(enemyPrefabs);
        if (spawnProfile?.Phases != null)
            foreach (var phase in spawnProfile.Phases)
                if (phase?.enemies != null)
                    foreach (var entry in phase.enemies) if (entry?.enemyPrefab != null) candidates.Add(entry.enemyPrefab);
        if (spawnStages != null)
            foreach (var stage in spawnStages) if (stage?.enemyPrefabs != null) candidates.AddRange(stage.enemyPrefabs);
        var prefab = candidates.Find(candidate => candidate != null &&
            candidate.GetComponent<EnemyHealth>() is { HasExperienceLoot: true } &&
            candidate.GetComponent<EnemyChaseMovement>() != null &&
            candidate.GetComponent<EnemyShooterMovement>() == null &&
            candidate.GetComponent<EnemyBomberMovement>() == null &&
            candidate.GetComponent<EyesEnemyBehaviour>() == null &&
            candidate.GetComponent<TurretEnemyBehaviour>() == null &&
            !(candidate.GetComponent<EnemyIdentity>()?.EnemyId ?? "").StartsWith("Elite_", System.StringComparison.OrdinalIgnoreCase));
        var spawned = SpawnSpecificEnemyAround(prefab, origin, 5f, 7f, 4.5f);
        var health = spawned != null ? spawned.GetComponent<EnemyHealth>() : null;
        if (health != null)
        {
            health.SetRuntimeMaxHealth(2f);
            spawned.GetComponent<EnemyMovement>()?.SetSpeedMultiplier(0.35f);
        }
        return health;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public GameObject FindDebugEnemyPrefab(DebugEnemyArchetype archetype)
    {
        GameObject catalogPrefab = archetype switch
        {
            DebugEnemyArchetype.Basic => debugBasicPrefab,
            DebugEnemyArchetype.Elite => debugElitePrefab,
            DebugEnemyArchetype.Shooter => debugShooterPrefab,
            _ => null
        };
        if (archetype == DebugEnemyArchetype.Basic ||
            archetype == DebugEnemyArchetype.Elite ||
            archetype == DebugEnemyArchetype.Shooter)
        {
            return catalogPrefab;
        }

        GameObject match = FindDebugEnemyPrefab(enemyPrefabs, archetype);
        if (match != null)
            return match;

        if (spawnStages != null)
        {
            for (int i = 0; i < spawnStages.Length; i++)
            {
                match = FindDebugEnemyPrefab(
                    spawnStages[i]?.enemyPrefabs,
                    archetype
                );
                if (match != null)
                    return match;
            }
        }

        EnemySpawnPhase[] phases = spawnProfile != null
            ? spawnProfile.Phases
            : null;
        if (phases == null)
            return null;

        for (int i = 0; i < phases.Length; i++)
        {
            EnemySpawnEntry[] entries = phases[i]?.enemies;
            if (entries == null)
                continue;

            for (int j = 0; j < entries.Length; j++)
            {
                GameObject prefab = entries[j]?.enemyPrefab;
                if (MatchesDebugArchetype(prefab, archetype))
                    return prefab;
            }
        }

        return null;
    }

    private static GameObject FindDebugEnemyPrefab(
        GameObject[] prefabs,
        DebugEnemyArchetype archetype)
    {
        if (prefabs == null)
            return null;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (MatchesDebugArchetype(prefabs[i], archetype))
                return prefabs[i];
        }

        return null;
    }

    private static bool MatchesDebugArchetype(
        GameObject prefab,
        DebugEnemyArchetype archetype)
    {
        if (prefab == null)
            return false;

        bool shooter = prefab.GetComponent<EnemyShooterMovement>() != null;
        bool bomber = prefab.GetComponent<EnemyBomberMovement>() != null;
        bool eyes = prefab.GetComponent<EyesEnemyBehaviour>() != null;
        bool turret = prefab.GetComponent<TurretEnemyBehaviour>() != null;
        EnemyIdentity identity = prefab.GetComponent<EnemyIdentity>();
        bool elite = identity != null &&
            !string.IsNullOrWhiteSpace(identity.EnemyId) &&
            identity.EnemyId.StartsWith(
                "Elite_", System.StringComparison.OrdinalIgnoreCase);

        return archetype switch
        {
            DebugEnemyArchetype.Shooter => shooter,
            DebugEnemyArchetype.Elite => elite,
            DebugEnemyArchetype.Bomber => bomber,
            DebugEnemyArchetype.Eyes => eyes,
            DebugEnemyArchetype.Turret => turret,
            _ => !elite && !shooter && !bomber && !eyes && !turret
        };
    }

    public GameObject SpawnDebugEnemyAt(GameObject prefab, Vector3 position)
    {
        if (prefab == null || !IsFinite(position))
            return null;

        // Intentionally bypasses SpawnCycle, maxAlive and wave gates while
        // reusing the single production initialization path.
        return SpawnEnemyAt(prefab, position, false);
    }

    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    public void ConfigureDebugExplorationPressure(
        GameObject[] prefabs,
        float interval,
        int maxAlive,
        int batchSize,
        float minimumSpawnDistance = 8f,
        float maximumSpawnDistance = 16f)
    {
        spawnProfile = null;
        activePhase = null;
        activePhaseIndex = -1;
        enemyPrefabs = prefabs ?? System.Array.Empty<GameObject>();
        spawnInterval = Mathf.Max(0.1f, interval);
        maxEnemies = Mathf.Max(1, maxAlive);
        baseEnemiesPerCycle = Mathf.Max(1, batchSize);
        maxEnemiesPerCycle = Mathf.Max(maxEnemiesPerCycle, batchSize);
        minSpawnDistance = Mathf.Max(1f, minimumSpawnDistance);
        maxSpawnDistance = Mathf.Max(
            minSpawnDistance,
            maximumSpawnDistance
        );
        currentHealthMultiplier = 1f;
        currentSpeedMultiplier = 1f;
        currentSpawnPressure = 1f;
        worldRuleSpawnPressureMultiplier = 1f;
        legacyDifficultySteps = 0;
        difficultyTimer = 0f;
        spawnTimer = 0f;
        debugFixedExplorationPressure = true;
        spawningEnabled = true;
    }

    public void StopDebugExplorationPressure()
    {
        debugFixedExplorationPressure = false;
        worldEventSpawnPressureMultiplier = 1f;
        spawningEnabled = false;
    }

    public void ClearDebugSpawnedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            GameObject instance = activeEnemies[i].instance;
            if (instance != null)
                Destroy(instance);
        }

        activeEnemies.Clear();
    }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float debugCaptureDensity = 1f;
    public float DebugCaptureDensity => debugCaptureDensity;
    public int DebugCaptureMaxAlive => GetCurrentMaxAlive();
    public float DebugCaptureRefillInterval => GetCurrentSpawnInterval() / debugCaptureDensity;
    public int DebugCaptureAliveCount { get { RemoveDestroyedEnemies(); return activeEnemies.Count; } }

    public void DebugSetCaptureDensity(float value)
    {
        if (!float.IsFinite(value)) return;
        debugCaptureDensity = Mathf.Clamp(value, .5f, 2f);
        RemoveDestroyedEnemies();
        // Trim only tracked ordinary spawns, without damage, kills or rewards.
        int cap = GetCurrentMaxAlive();
        for (int i = activeEnemies.Count - 1; i >= cap; i--)
        {
            var instance = activeEnemies[i].instance;
            if (instance != null) { instance.SetActive(false); Destroy(instance); }
            activeEnemies.RemoveAt(i);
        }
    }

    public void DebugFillCaptureEnemies()
    {
        if (!spawningEnabled || TutorialController.IsActive || IsAssaultBreathing) return;
        int budget = Mathf.Min(10, GetCurrentMaxAlive() - DebugCaptureAliveCount);
        while (budget > 0)
        {
            int before = activeEnemies.Count;
            SpawnCycle(budget);
            int added = activeEnemies.Count - before;
            if (added <= 0) break;
            budget -= added;
        }
    }
#endif

    public void SetWorldAcceleration(float multiplier)
    {
        worldAccelerationMultiplier = Mathf.Max(0.1f, multiplier);
        RefreshActiveEnemySpeeds();
    }

    public void SetWorldRuleSpawnPressureMultiplier(float multiplier)
    {
        worldRuleSpawnPressureMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void SetWorldEventSpawnPressureMultiplier(float multiplier)
    {
        worldEventSpawnPressureMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ResetForNewLevel()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugSetInterceptingBombersExperiment(false);
        DebugSetStrafingShootersExperiment(false);
        DebugSetRecoilingShootersExperiment(false);
        DebugSetCommittedVolleyExperiment(false);
        DebugSetCommittedRamExperiment(false);
        DebugSetWoundedRetreatExperiment(false);
        DebugSetBlockLockedShieldsExperiment(false);
        ResetFlankingChasersExperiment();
#endif
        ResetAssaultEvents();
        spawnTimer = 0f;
        difficultyTimer = 0f;
        runTime = 0f;
        activePhase = null;
        activePhaseIndex = -1;
        legacyDifficultySteps = 0;
        worldEventSpawnPressureMultiplier = 1f;
        runThreatControlsPhase = false;
        runThreatSpawnIntervalMultiplier = 1f;
        runThreatMaxAliveCap = 0;
        runThreatBatchSize = 1;
        spawningEnabled = true;
    }

    public void ResetSpawner()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugSetInterceptingBombersExperiment(false);
        DebugSetStrafingShootersExperiment(false);
        DebugSetRecoilingShootersExperiment(false);
        DebugSetCommittedVolleyExperiment(false);
        DebugSetCommittedRamExperiment(false);
        DebugSetWoundedRetreatExperiment(false);
        DebugSetBlockLockedShieldsExperiment(false);
        ResetFlankingChasersExperiment();
#endif
        ResetAssaultEvents();
        if (!initialized)
            CaptureBaseSettings();

        spawnInterval = baseSpawnInterval;
        maxEnemies = baseMaxEnemies;
        currentHealthMultiplier = baseHealthMultiplier;
        currentSpeedMultiplier = baseSpeedMultiplier;
        currentSpawnPressure = 1f;
        worldRuleSpawnPressureMultiplier = 1f;
        worldEventSpawnPressureMultiplier = 1f;
        legacyDifficultySteps = 0;
        runThreatControlsPhase = false;
        runThreatSpawnIntervalMultiplier = 1f;
        runThreatMaxAliveCap = 0;
        runThreatBatchSize = 1;
    }

    public void SpawnAdditionalWave(
        Vector3 origin,
        int enemyCount,
        float minDistance = 1f,
        float maxDistance = 3f,
        float minimumDistanceFromPlayer = 0f)
    {
        if (!spawningEnabled) return;

        if (gameplayArea == null)
            ResolveGameplayArea();

        if (player == null)
            player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);

        RemoveDestroyedEnemies();

        int count = Mathf.Max(0, enemyCount);

        for (int i = 0; i < count; i++)
        {
            GameObject selectedPrefab = activePhase != null
                ? SelectWeightedEnemy(activePhase)
                : SelectLegacyEnemy();

            if (selectedPrefab == null ||
                gameplayArea == null ||
                !TryGetAdditionalWaveSpawnPosition(
                    origin,
                    minDistance,
                    maxDistance,
                    minimumDistanceFromPlayer,
                    0f,
                    out Vector3 spawnPosition))
            {
                break;
            }

            SpawnEnemyAtInternal(selectedPrefab, spawnPosition, true, false);
        }
    }

    // Event-owned spawn: production initialization and tracking, without ordinary
    // experiment cadence. The caller owns cancellation of the returned actor.
    public EnemyHealth SpawnEventEnemyAt(GameObject prefab, Vector3 position,
        float clearance, float minimumDistanceFromPlayer)
    {
        if (!spawningEnabled || prefab == null || !prefab.activeSelf ||
            prefab.GetComponent<EnemyHealth>() == null ||
            !FiniteEventValue(position.x) || !FiniteEventValue(position.y) ||
            !FiniteEventValue(position.z) || !FiniteEventValue(clearance) || clearance <= 0f ||
            !FiniteEventValue(minimumDistanceFromPlayer) || minimumDistanceFromPlayer < 0f)
            return null;
        if (gameplayArea == null) ResolveGameplayArea();
        if (player == null)
            player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);
        if (gameplayArea == null || player == null ||
            !gameplayArea.IsInsidePlayableArea(position, clearance) ||
            !gameplayArea.IsInsideSpawnArea(position, clearance) ||
            Vector2.Distance(position, player.position) < minimumDistanceFromPlayer ||
            !IsAdditionalWaveSpawnPositionClear(position, clearance)) return null;
        GameObject instance = SpawnEnemyAtInternal(prefab, position, true, false, false);
        return instance != null ? instance.GetComponent<EnemyHealth>() : null;
    }

    private static bool FiniteEventValue(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private bool TryGetAdditionalWaveSpawnPosition(
        Vector3 origin,
        float minDistance,
        float maxDistance,
        float minimumDistanceFromPlayer,
        float spawnClearance,
        out Vector3 spawnPosition)
    {
        float safePlayerDistance = Mathf.Max(0f, minimumDistanceFromPlayer);
        float minimumRadius = Mathf.Max(0f, minDistance);
        float maximumRadius = Mathf.Max(minimumRadius, maxDistance);

        for (int i = 0; i < Mathf.Max(1, spawnPositionAttempts); i++)
        {
            if (!gameplayArea.TryGetSpawnPosition(
                    origin,
                    minDistance,
                    maxDistance,
                    1,
                    out Vector3 candidate))
            {
                continue;
            }

            float originDistance = Vector2.Distance(candidate, origin);

            if (originDistance < minimumRadius ||
                originDistance > maximumRadius)
            {
                continue;
            }

            if (player != null &&
                Vector2.Distance(candidate, player.position) <
                safePlayerDistance)
            {
                continue;
            }

            if (!IsAdditionalWaveSpawnPositionClear(
                    candidate,
                    spawnClearance))
            {
                continue;
            }

            spawnPosition = candidate;
            return true;
        }

        spawnPosition = default;
        return false;
    }

    private static bool IsAdditionalWaveSpawnPositionClear(
        Vector2 position,
        float clearance)
    {
        float safeClearance = Mathf.Max(0f, clearance);

        if (safeClearance <= 0f)
            return true;

        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = true;
        int overlapCount;

        do
        {
            overlapCount = Physics2D.OverlapCircle(
                position,
                safeClearance,
                filter,
                additionalWaveOverlapBuffer);

            if (overlapCount < additionalWaveOverlapBuffer.Length)
                break;

            System.Array.Resize(
                ref additionalWaveOverlapBuffer,
                additionalWaveOverlapBuffer.Length * 2);
        }
        while (true);

        bool isClear = true;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = additionalWaveOverlapBuffer[i];
            additionalWaveOverlapBuffer[i] = null;

            if (overlap != null && overlap.enabled && !overlap.isTrigger)
                isClear = false;
        }

        return isClear;
    }

    private void CaptureBaseSettings()
    {
        baseSpawnInterval = Mathf.Max(0.1f, spawnInterval);
        baseMaxEnemies = Mathf.Max(1, maxEnemies);
        baseHealthMultiplier = currentHealthMultiplier;
        baseSpeedMultiplier = currentSpeedMultiplier;
        initialized = true;
    }

    private void UpdateLegacyDifficulty()
    {
        if (difficultyIncreaseInterval <= 0f)
            return;

        difficultyTimer += Time.deltaTime;

        if (difficultyTimer < difficultyIncreaseInterval)
            return;

        difficultyTimer = 0f;
        legacyDifficultySteps++;
        spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval * spawnRateMultiplier);
        maxEnemies = Mathf.Min(
            Mathf.Max(baseMaxEnemies, maxAliveLimit),
            maxEnemies + Mathf.Max(0, maxEnemiesIncreasePerStep)
        );
        currentHealthMultiplier = Mathf.Min(
            maxHealthMultiplier,
            currentHealthMultiplier * enemyHealthMultiplier
        );
        currentSpeedMultiplier = Mathf.Min(
            maxSpeedMultiplier,
            currentSpeedMultiplier * enemySpeedMultiplier
        );

        Debug.Log(
            $"[EnemySpawner] Legacy difficulty increased: interval {spawnInterval:F2}, " +
            $"batch {GetCurrentEnemiesPerCycle()}, max alive {maxEnemies}, " +
            $"HP x{currentHealthMultiplier:F2}, speed x{currentSpeedMultiplier:F2}."
        );
    }

    private void UpdateActivePhase()
    {
        if (spawnProfile == null || spawnProfile.Phases == null)
            return;

        EnemySpawnPhase[] phases = spawnProfile.Phases;
        int nextIndex = -1;
        float latestStartTime = float.MinValue;

        for (int i = 0; i < phases.Length; i++)
        {
            EnemySpawnPhase phase = phases[i];

            if (phase != null &&
                runTime >= phase.startTime &&
                phase.startTime >= latestStartTime)
            {
                nextIndex = i;
                latestStartTime = phase.startTime;
            }
        }

        ApplyProfilePhase(nextIndex);
    }

    private void ApplyProfilePhase(int requestedIndex)
    {
        EnemySpawnPhase[] phases = spawnProfile != null
            ? spawnProfile.Phases
            : null;

        if (phases == null || phases.Length == 0)
        {
            activePhaseIndex = -1;
            activePhase = null;
            return;
        }

        int nextIndex = Mathf.Clamp(requestedIndex, 0, phases.Length - 1);

        if (nextIndex == activePhaseIndex && activePhase != null)
            return;

        activePhaseIndex = nextIndex;
        activePhase = phases[nextIndex];
        spawnTimer = 0f;

        if (activePhase != null)
        {
            Debug.Log(
                $"[EnemySpawner] Phase {activePhaseIndex + 1}: " +
                $"interval {activePhase.spawnInterval:F2}, " +
                $"max alive {activePhase.maxAlive}."
            );
        }
    }

    private float GetCurrentSpawnInterval()
    {
        if (TutorialController.IsActive) return 6f;
        float interval = activePhase != null
            ? activePhase.spawnInterval / currentSpawnPressure
            : spawnInterval;

        interval *= runThreatSpawnIntervalMultiplier;
        float limitedInterval = Mathf.Max(minSpawnInterval, interval);
        return Mathf.Max(
            0.1f,
            limitedInterval /
            GetExternalSpawnPressureMultiplier() /
            worldAccelerationMultiplier
        );
    }

    public GameObject SpawnSpecificEnemyAround(
        GameObject enemyPrefab,
        Vector3 origin,
        float minDistance,
        float maxDistance,
        float minimumDistanceFromPlayer = 0f,
        bool countTowardSpawnLimits = true,
        float spawnClearance = 0f)
    {
        // Explicit untracked debug spawns remain available when auto-spawn is paused.
        if (!spawningEnabled && countTowardSpawnLimits) return null;

        if (enemyPrefab == null)
            return null;

        if (gameplayArea == null)
            ResolveGameplayArea();

        if (player == null)
            player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);

        RemoveDestroyedEnemies();

        if (gameplayArea == null ||
            !TryGetAdditionalWaveSpawnPosition(
                origin,
                minDistance,
                maxDistance,
                minimumDistanceFromPlayer,
                spawnClearance,
                out Vector3 spawnPosition))
        {
            return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!countTowardSpawnLimits)
            return SpawnDebugEnemyAt(enemyPrefab, spawnPosition);
#endif
        return SpawnEnemyAtInternal(
            enemyPrefab,
            spawnPosition,
            countTowardSpawnLimits,
            false
        );
    }

    private int GetCurrentMaxAlive()
    {
        if (TutorialController.IsActive) return 4;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugFixedExplorationPressure)
            return CaptureMaxAlive(Mathf.Max(1, maxEnemies));
#endif
        float effectivePressure = GetEffectiveSpawnPressure();

        if (activePhase == null)
        {
            int legacyMinimumAlive = Mathf.Max(1, maxEnemies);
            return CaptureMaxAlive(Mathf.Clamp(
                Mathf.RoundToInt(maxEnemies *
                    GetExternalSpawnPressureMultiplier()),
                legacyMinimumAlive,
                Mathf.Max(legacyMinimumAlive, maxAliveLimit)
            ));
        }

        int scaledMaxAlive = Mathf.RoundToInt(
            activePhase.maxAlive * effectivePressure
        );

        int minimumAlive = Mathf.Max(1, activePhase.maxAlive);

        int result = Mathf.Clamp(
            scaledMaxAlive,
            minimumAlive,
            Mathf.Max(minimumAlive, maxAliveLimit)
        );

        return CaptureMaxAlive(runThreatMaxAliveCap > 0
            ? Mathf.Min(result, runThreatMaxAliveCap)
            : result);
    }

    private int CaptureMaxAlive(int baseline)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Mathf.Approximately(debugCaptureDensity, 1f))
            return Mathf.Clamp(Mathf.RoundToInt(baseline * debugCaptureDensity), 1, Mathf.Min(40, Mathf.Max(1, maxAliveLimit)));
#endif
        return baseline;
    }

    private int GetCurrentEnemiesPerCycle()
    {
        float effectivePressure = GetEffectiveSpawnPressure();
        int phaseBonus = activePhase != null
            ? Mathf.Max(0, activePhaseIndex) / Mathf.Max(1, phasesPerBatchIncrease)
            : 0;
        int pressureBonus = Mathf.FloorToInt(
            Mathf.Max(0f, effectivePressure - 1f) /
            Mathf.Max(0.01f, spawnPressurePerBatchIncrease)
        );
        int legacyBonus = activePhase == null
            ? legacyDifficultySteps / Mathf.Max(1, legacyStepsPerBatchIncrease)
            : 0;

        int calculatedBatch = Mathf.Clamp(
            baseEnemiesPerCycle + phaseBonus + pressureBonus + legacyBonus,
            1,
            Mathf.Max(1, maxEnemiesPerCycle)
        );

        return runThreatControlsPhase
            ? Mathf.Max(calculatedBatch, runThreatBatchSize)
            : calculatedBatch;
    }

    private void SpawnCycle(int maxBatch = int.MaxValue)
    {
        RemoveDestroyedEnemies();

        int availableSlots = GetCurrentMaxAlive() - activeEnemies.Count;

        if (availableSlots <= 0)
            return;

        int spawnCount = Mathf.Min(maxBatch, Mathf.Min(TutorialController.IsActive ? 1 : GetCurrentEnemiesPerCycle(), availableSlots));

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject selectedPrefab = activePhase != null
                ? SelectWeightedEnemy(activePhase)
                : SelectLegacyEnemy();

            if (selectedPrefab == null)
                break;

            SpawnEnemy(selectedPrefab);
        }
    }

    private void SpawnEnemy(GameObject selectedPrefab)
    {
        if (gameplayArea == null)
            ResolveGameplayArea();

        if (gameplayArea == null ||
            !gameplayArea.TryGetSpawnPosition(
                player.position,
                minSpawnDistance,
                maxSpawnDistance,
                spawnPositionAttempts,
                out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                "[EnemySpawner] No valid position exists inside the spawn area.",
                this
            );
            return;
        }

        SpawnEnemyAt(selectedPrefab, spawnPosition);
    }

    private GameObject SpawnEnemyAt(
        GameObject selectedPrefab,
        Vector3 spawnPosition,
        bool countTowardSpawnLimits = true)
        => SpawnEnemyAtInternal(selectedPrefab, spawnPosition, countTowardSpawnLimits, countTowardSpawnLimits);

    private GameObject SpawnEnemyAtInternal(
        GameObject selectedPrefab,
        Vector3 spawnPosition,
        bool countTowardSpawnLimits,
        bool ordinaryShieldSpawn,
        bool allowOrdinaryExperiments = true)
    {
        GameObject enemy = Instantiate(
            selectedPrefab,
            spawnPosition,
            Quaternion.identity
        );
        ConfigureEnemyProjectilePool(enemy);
        Subject42.Visuals.LiveVisualRuntime.RegisterHierarchy(enemy, selectedPrefab);
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();

        if (health != null)
        {
            ConfigureEnemyFeedbackPools(health);
            float phaseHealth = activePhase != null ? activePhase.healthMultiplier : 1f;
            health.SetMaxHealthMultiplier(currentHealthMultiplier * phaseHealth);
        }

        EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
        float phaseSpeed = activePhase != null ? activePhase.speedMultiplier : 1f;
        float baseEnemySpeedMultiplier = currentSpeedMultiplier * phaseSpeed;

        EnemyIdentity identity = enemy.GetComponent<EnemyIdentity>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (allowOrdinaryExperiments)
        {
        TryAssignFlankingChaser(enemy, countTowardSpawnLimits);
        TryAssignCommittedRam(enemy, countTowardSpawnLimits);
        TryAssignWoundedRetreat(enemy, countTowardSpawnLimits);
        TryAssignCommittedVolley(enemy, countTowardSpawnLimits);
        TryAssignStrafingShooter(enemy, ordinaryShieldSpawn);
        TryAssignRecoilingShooter(enemy, ordinaryShieldSpawn);
        TryAssignInterceptingBomber(enemy, ordinaryShieldSpawn);
        TryAssignBlockLockedShield(enemy, ordinaryShieldSpawn);
        }
#endif

        if (countTowardSpawnLimits)
        {
            activeEnemies.Add(new SpawnedEnemy
            {
                instance = enemy,
                sourcePrefab = selectedPrefab,
                enemyId = identity != null ? identity.EnemyId : string.Empty,
                movement = movement,
                baseSpeedMultiplier = baseEnemySpeedMultiplier
            });
        }

        if (movement != null)
            movement.SetSpeedMultiplier(
                baseEnemySpeedMultiplier * worldAccelerationMultiplier
            );

        health?.NotifySpawnConfigured();
        return enemy;
    }

    private void ConfigureEnemyProjectilePool(GameObject enemy)
    {
        EnemyShooterMovement shooter =
            enemy.GetComponent<EnemyShooterMovement>();
        if (shooter != null)
        {
            shooter.SetProjectilePool(
                GetEnemyProjectilePool(shooter.ProjectilePrefab));
        }

        TurretEnemyBehaviour turret =
            enemy.GetComponent<TurretEnemyBehaviour>();
        if (turret != null)
        {
            turret.SetProjectilePool(
                GetEnemyProjectilePool(turret.ProjectilePrefab));
        }
    }

    private SimplePrefabPool GetEnemyProjectilePool(GameObject projectile)
    {
        if (projectile == null)
            return null;

        if (enemyProjectilePools.TryGetValue(
                projectile,
                out SimplePrefabPool existing))
        {
            return existing;
        }

        SimplePrefabPool created = new(
            this,
            projectile,
            projectilePrewarmPerPrefab,
            projectilePoolMaximum);
        enemyProjectilePools.Add(projectile, created);
        return created;
    }

    private void ConfigureEnemyFeedbackPools(EnemyHealth health)
    {
        var presentation = health.GetComponent<EnemyHealthPresentation>();
        if (presentation == null) return;
        presentation.SetFeedbackPools(
            GetEnemyFeedbackPool(presentation.DamagePopupPrefab),
            GetEnemyFeedbackPool(presentation.BloodHitPrefab),
            GetEnemyFeedbackPool(presentation.DeathFxPrefab));
    }

    private SimplePrefabPool GetEnemyFeedbackPool(GameObject prefab)
    {
        if (prefab == null)
            return null;

        if (enemyFeedbackPools.TryGetValue(
                prefab,
                out SimplePrefabPool existing))
        {
            return existing;
        }

        SimplePrefabPool created = new(
            this,
            prefab,
            feedbackPrewarmPerPrefab,
            feedbackPoolMaximum);
        enemyFeedbackPools.Add(prefab, created);
        return created;
    }

    private void PrewarmProjectilePools(GameObject[] prefabs)
    {
        if (prefabs == null)
            return;

        for (int i = 0; i < prefabs.Length; i++)
            PrewarmProjectilePool(prefabs[i]);
    }

    private void PrewarmProjectilePools(EnemySpawnProfile profile)
    {
        EnemySpawnPhase[] phases = profile != null ? profile.Phases : null;
        if (phases == null)
            return;

        for (int phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
        {
            EnemySpawnEntry[] entries = phases[phaseIndex]?.enemies;
            if (entries == null)
                continue;

            for (int entryIndex = 0;
                 entryIndex < entries.Length;
                 entryIndex++)
            {
                PrewarmProjectilePool(entries[entryIndex]?.enemyPrefab);
            }
        }
    }

    private void PrewarmProjectilePool(GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
            return;

        EnemyShooterMovement shooter =
            enemyPrefab.GetComponent<EnemyShooterMovement>();
        if (shooter != null)
            GetEnemyProjectilePool(shooter.ProjectilePrefab);

        TurretEnemyBehaviour turret =
            enemyPrefab.GetComponent<TurretEnemyBehaviour>();
        if (turret != null)
            GetEnemyProjectilePool(turret.ProjectilePrefab);

        EnemyHealthPresentation presentation = enemyPrefab.GetComponent<EnemyHealthPresentation>();
        if (presentation != null)
        {
            GetEnemyFeedbackPool(presentation.DamagePopupPrefab);
            GetEnemyFeedbackPool(presentation.BloodHitPrefab);
            GetEnemyFeedbackPool(presentation.DeathFxPrefab);
        }
    }

    private void OnDestroy()
    {
        foreach (SimplePrefabPool projectilePool in
                 enemyProjectilePools.Values)
        {
            projectilePool.Dispose();
        }

        enemyProjectilePools.Clear();

        foreach (SimplePrefabPool feedbackPool in enemyFeedbackPools.Values)
            feedbackPool.Dispose();

        enemyFeedbackPools.Clear();
    }

    private GameObject SelectWeightedEnemy(EnemySpawnPhase phase)
    {
        if (phase.enemies == null || phase.enemies.Length == 0)
            return null;

        float totalWeight = 0f;

        for (int i = 0; i < phase.enemies.Length; i++)
        {
            EnemySpawnEntry entry = phase.enemies[i];

            if (CanSpawn(entry))
                totalWeight += EffectiveWeight(entry);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;

        for (int i = 0; i < phase.enemies.Length; i++)
        {
            EnemySpawnEntry entry = phase.enemies[i];

            if (!CanSpawn(entry))
                continue;

            roll -= EffectiveWeight(entry);

            if (roll <= 0f)
                return entry.enemyPrefab;
        }

        return null;
    }

    private static float EffectiveWeight(EnemySpawnEntry entry) => entry.weight * (RunStateManager.Instance?.CurrentConfig.EnemyWeight(entry.enemyPrefab) ?? 1f);

    private bool CanSpawn(EnemySpawnEntry entry)
    {
        if (entry == null || entry.enemyPrefab == null || EffectiveWeight(entry) <= 0f)
            return false;

        if (currentRunLevel < Mathf.Max(1, entry.minimumRunLevel))
            return false;

        if (entry.maxAliveOfType <= 0)
            return true;

        int minimumTypeLimit = Mathf.Max(1, entry.maxAliveOfType);

        if (entry.useExactAliveLimit)
            return CountAlive(entry) < minimumTypeLimit;

        float effectivePressure = GetEffectiveSpawnPressure();
        int scaledTypeLimit = Mathf.Clamp(
            Mathf.RoundToInt(entry.maxAliveOfType * effectivePressure),
            minimumTypeLimit,
            Mathf.Max(minimumTypeLimit, maxAliveLimit)
        );

        return CountAlive(entry) < scaledTypeLimit;
    }

    private float GetEffectiveSpawnPressure()
    {
        return currentSpawnPressure * GetExternalSpawnPressureMultiplier();
    }

    private float GetExternalSpawnPressureMultiplier()
    {
        return worldRuleSpawnPressureMultiplier *
            worldEventSpawnPressureMultiplier;
    }

    private int CountAlive(EnemySpawnEntry entry)
    {
        int count = 0;
        EnemyIdentity prefabIdentity =
            entry.enemyPrefab.GetComponent<EnemyIdentity>();
        string targetEnemyId = prefabIdentity != null
            ? prefabIdentity.EnemyId
            : string.Empty;
        bool useIdentity = !string.IsNullOrWhiteSpace(targetEnemyId);

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            SpawnedEnemy activeEnemy = activeEnemies[i];

            if (activeEnemy.instance != null &&
                (useIdentity
                    ? activeEnemy.enemyId == targetEnemyId
                    : activeEnemy.sourcePrefab == entry.enemyPrefab))
            {
                count++;
            }
        }

        return count;
    }

    private void RemoveDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i].instance == null)
                activeEnemies.RemoveAt(i);
        }
    }

    private void RefreshActiveEnemySpeeds()
    {
        RefreshAssaultEnemySpeeds();
        RemoveDestroyedEnemies();

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            SpawnedEnemy enemy = activeEnemies[i];

            if (enemy.movement != null)
            {
                enemy.movement.SetSpeedMultiplier(
                    enemy.baseSpeedMultiplier * worldAccelerationMultiplier
                );
            }
        }
    }

    private GameObject SelectLegacyEnemy()
    {
        GameObject[] available = GetLegacyAvailableEnemies();

        if (available == null || available.Length == 0)
            return null;

        return available[Random.Range(0, available.Length)];
    }

    private GameObject[] GetLegacyAvailableEnemies()
    {
        GameObject[] result = enemyPrefabs;

        if (spawnStages == null)
            return result;

        for (int i = 0; i < spawnStages.Length; i++)
        {
            if (runTime >= spawnStages[i].startTime)
                result = spawnStages[i].enemyPrefabs;
        }

        return result;
    }

    private void ResolveGameplayArea()
    {
        if (gameplayArea == null)
            gameplayArea = GameplayAreaService.Instance;
    }
}
