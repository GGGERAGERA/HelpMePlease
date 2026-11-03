#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

// One focused production-scene check, using the existing preference-safe startup/cleanup.
public sealed class MvpMiniTurretSmoke
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object target,string name)=>target.GetType().GetField(name,Hidden).GetValue(target);
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Hidden).Invoke(target,args);
    [SetUp] public void Preserve()=>CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Begin()=>CoreTestSupport.BeginRun();
    [UnityTearDown] public IEnumerator Cleanup()=>CoreTestSupport.CleanupPlayMode();

    [UnityTest,Timeout(45000)]
    public IEnumerator AppearanceShootAndDestruction()
    {
        var run=RunStateManager.Instance;
        var previous=run.CurrentSector;
        var stage=AssetDatabase.LoadAssetAtPath<StageProfileData>("Assets/_Project/Data/Stages/StageProfiles/StageProfile_03.asset");
        run.SetCurrentSector(new RunSector(3,stage,previous.WorldRule,previous.LocalAnomaly));
        yield return SceneManager.LoadSceneAsync("MVP");
        yield return CoreTestSupport.Await(()=>Object.FindFirstObjectByType<OrbitalStationRuntime>() is {IsInitialized:true} && !SceneTransitionOverlay.IsTransitioning);
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();
        var station=Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        Assert.That(run.IsDevelopmentRun,Is.False,"actual ordinary MVP bootstrap");
        Assert.That(Field(spawner,"spawnProfile"),Is.SameAs(stage.SpawnProfile));
        spawner.enabled=false; station.enabled=false;
        foreach(var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.DestroyImmediate(enemy.gameObject);
        Call(spawner,"RemoveDestroyedEnemies");
        // Select the existing 12-second phase directly; no weights, timing data or roster changes.
        Call(spawner,"ApplyProfilePhase",1);
        var phase=(EnemySpawnPhase)Field(spawner,"activePhase");
        var entry=phase.enemies.Single(e=>e.enemyPrefab.GetComponent<TurretEnemyBehaviour>()!=null);
        Assert.That(AssetDatabase.GetAssetPath(entry.enemyPrefab),Is.EqualTo(MvpMiniTurretSelection.ApprovedPath));
        Assert.That(entry.weight,Is.EqualTo(3)); Assert.That(entry.maxAliveOfType,Is.EqualTo(1));
        Assert.That((bool)Call(spawner,"CanSpawn",entry),Is.True);
        var original=AssetDatabase.LoadAssetAtPath<GameObject>(MvpMiniTurretSelection.OldPath);
        Assert.That(MvpMiniTurretSelection.Gameplay(entry.enemyPrefab),Is.EqualTo(MvpMiniTurretSelection.Gameplay(original)));
        var actor=(GameObject)Call(spawner,"SpawnEnemyAt",entry.enemyPrefab,player.transform.position+Vector3.left*6f,true);
        Assert.That(actor.GetComponentsInChildren<SpriteRenderer>().Any(s=>s.enabled && s.sprite!=null &&
            AssetDatabase.GetAssetPath(s.sprite).EndsWith("newEnemyes1/MiniTurret1.png")),Is.True,"approved colored MiniTurret4 renders in MVP");
        var health=actor.GetComponent<EnemyHealth>();
        var policy=actor.AddComponent<LabEnemyDummy>(); policy.Initialize(); policy.Invulnerable=false; // Only this transient check actor suppresses rewards.
        var playerBody=player.GetComponent<Rigidbody2D>(); playerBody.constraints=RigidbodyConstraints2D.FreezeAll;
        int before=Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length;
        float deadline=Time.realtimeSinceStartup+2.5f;
        while(Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length<=before && Time.realtimeSinceStartup<deadline) yield return null;
        var bullets=Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        Assert.That(bullets.Length,Is.GreaterThan(before),"unchanged real turret AI releases a real projectile after aiming");
        Assert.That((float)Field(actor.GetComponent<TurretEnemyBehaviour>(),"timer"),Is.InRange(2f,2.5f));
        var bullet=bullets.Last(); var sourceBullet=actor.GetComponent<TurretEnemyBehaviour>().ProjectilePrefab.GetComponent<EnemyProjectile>();
        Assert.That((int)Field(bullet,"damage"),Is.EqualTo((int)Field(sourceBullet,"damage")));
        Assert.That((float)Field(bullet,"speed"),Is.EqualTo((float)Field(sourceBullet,"speed")));
        Assert.That(actor.GetComponent<EnemyWhiteFlash>().TargetRenderer.enabled,Is.True,"hit feedback targets approved visible art");
        ScreenCapture.CaptureScreenshot(MvpMiniTurretSelection.Output+"/mvp-shot.png");
        yield return new WaitForSecondsRealtime(.15f);
        float hp=health.CurrentHealth;
        ((ProductionOrbitalCombatAdapter)station.Combat).ApplyDamage(health,1f,actor.transform.position);
        Assert.That(health.CurrentHealth,Is.EqualTo(hp-1).Within(.001f));
        ((ProductionOrbitalCombatAdapter)station.Combat).ApplyDamage(health,health.CurrentHealth+1,actor.transform.position);
        Assert.That(health.IsDead,Is.True);
        yield return null;
        Assert.That(actor==null,Is.True,"ordinary health/death destroys the turret");
        Call(spawner,"RemoveDestroyedEnemies");
        Assert.That(Object.FindFirstObjectByType<EnemyOrbitalLabLaserTurret>(),Is.Null,"Interception has not entered production");
        Assert.That(Object.FindFirstObjectByType<EnemyOrbitalLabController>(),Is.Null);
    }
}
#endif
