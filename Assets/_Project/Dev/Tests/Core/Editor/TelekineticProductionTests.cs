#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using Subject42.Combat.Telekinetic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class TelekineticProductionTests
{
    [Test] public void WholeItemGlitchPreservesValidLayoutAndRejectsInvalidPairs()
    {
        var build=new TelekineticBuild { AvailableRadius=2 };
        Assert.That(build.Place(TelekineticPart.Shotgun,new Vector2Int(0,1)),Is.True);
        Assert.That(build.Place(TelekineticPart.Pistol,new Vector2Int(1,0)),Is.True);
        Assert.That(build.Place(TelekineticPart.Frame,new Vector2Int(1,1)),Is.True);
        var shotgun=build.Items[0]; var pistol=build.Items[1];
        shotgun.Remaining=.37f; string before=JsonUtility.ToJson(build);
        Assert.That(build.TrySwap(shotgun,pistol),Is.False,"Whole bar collides with frame");
        Assert.That(JsonUtility.ToJson(build),Is.EqualTo(before),"Rejected swap is atomic");
        Assert.That(build.Place(TelekineticPart.Sniper,new Vector2Int(-1,0)),Is.True);
        int count=build.Items.Count; var ids=build.Items.Select(i=>i.Id).ToArray();
        Assert.That(build.TrySwap(pistol,build.Items.Last()),Is.True);
        for (int i=0;i<20;i++) { build.TryGlitch(new System.Random(i)); Assert.That(build.Validate(),Is.True); }
        Assert.That(build.Items.Select(i=>i.Id),Is.EqualTo(ids)); Assert.That(build.Items.Count,Is.EqualTo(count));
        Assert.That(shotgun.Remaining,Is.EqualTo(.37f));
        var noPair=new TelekineticBuild(); noPair.Place(TelekineticPart.Pistol,Vector2Int.right);
        before=JsonUtility.ToJson(noPair); Assert.That(noPair.TryGlitch(new System.Random(42)),Is.False);
        Assert.That(JsonUtility.ToJson(noPair),Is.EqualTo(before));
    }
    [Test] public void ReserveIdentityFootprintsMergeAndSerializationSurviveRestore()
    {
        var build=new TelekineticBuild { AvailableRadius=2 };
        var first=build.Grant(TelekineticPart.FR); var second=build.Grant(TelekineticPart.FR);
        Assert.That(build.Install(first,Vector2Int.right),Is.True);
        Assert.That(build.Install(second,Vector2Int.up),Is.True);
        Assert.That(build.Merge(first,second),Is.True); Assert.That(first.Level,Is.EqualTo(2));
        Assert.That(build.Stow(first),Is.True);
        var clone=JsonUtility.FromJson<TelekineticBuild>(JsonUtility.ToJson(build));
        Assert.That(clone.Reserve.Single().Kind,Is.EqualTo(TelekineticPart.FR));
        Assert.That(clone.Reserve.Single().Id,Is.EqualTo(first.Id)); Assert.That(clone.Reserve.Single().Level,Is.EqualTo(2));
        Assert.That(clone.Expand(),Is.True); Assert.That(clone.AvailableRadius,Is.EqualTo(3));
        Assert.That(clone.Install(clone.Reserve.Single(),Vector2Int.right),Is.True);
        Assert.That(clone.Grant(TelekineticPart.Energy).Id,Is.GreaterThan(first.Id));
        var bar=clone.Grant(TelekineticPart.DMG);
        Assert.That(clone.Install(bar,new Vector2Int(3,3)),Is.False,"Entire footprint must fit");
        Assert.That(clone.Reserve.Contains(bar),Is.True); Assert.That(clone.Validate(),Is.True);
        var state=OrbitalRunState.CreateDefault(42);
        state.EnableSegmentedOrbit(); state.UnlockedSegmentedLevels=3;
        state.Modules[0].DamageLevel=2;
        state.EnableTelekineticRig();
        Assert.That(state.Telekinetic.AvailableRadius,Is.EqualTo(4),"Previously opened space must survive migration");
        Assert.That(state.Telekinetic.Items.Single(c=>c.IsWeapon).Level,Is.EqualTo(3));
        Assert.That(state.Modules[0].DamageLevel,Is.EqualTo(2),"Keep original run data as a rollback source");
        Assert.That(AssetDatabase.GetDependencies(TelekineticProductionAuthoring.PrefabPath,true).Any(path=>path.StartsWith("Assets/_Project/Dev/",StringComparison.Ordinal)),Is.False,"Rig prefab must carry only production dependencies");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.EndSandbox();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
    }
    [UnityTest, Timeout(90000)] public IEnumerator BunkerRewardControlsAnomaliesAndReturn()
    {
        PlayerPrefs.BeginSandbox(false);
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey,1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey,1);
        BunkerDebugTools.SetGameView1600();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        // Captured closures must be created after the domain reload, as in Phase7SmokeTests.
        yield return ExerciseProductionRig();
    }
    private static IEnumerator ExerciseProductionRig()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName &&
            Object.FindFirstObjectByType<BunkerRunStarter>()!=null && !SceneTransitionOverlay.IsTransitioning);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter=Object.FindFirstObjectByType<BunkerRunStarter>(); starter.StartRun(starter.transform);
        float loadDeadline=Time.realtimeSinceStartup+30;
        while (!(SceneManager.GetActiveScene().name=="MVP" &&
            Object.FindFirstObjectByType<CharacterSpawner>() is { SpawnedPlayer:not null, Station:{IsInitialized:true} } &&
            ProductionSceneComposition.Active is { IsReady:true } && !SceneTransitionOverlay.IsTransitioning) && Time.realtimeSinceStartup<loadDeadline) yield return null;
        var spawner=Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(spawner,Is.Not.Null,$"Player spawner after transition: scene={SceneManager.GetActiveScene().name}, transition={SceneTransitionOverlay.IsTransitioning}");
        var station=spawner.Station;
        Assert.That(station,Is.Not.Null,"Spawner must finish station binding");
        Debug.Log($"[RigSmoke] scene={SceneManager.GetActiveScene().name}; spawner={spawner}; station={station}; rig={station?.Rig}; initialized={station?.IsInitialized}");
        var rig=station.Rig;
        Assert.That(rig,Is.Not.Null); Assert.That(station.Rings,Is.Empty); Assert.That(station.Modules,Is.Empty,"Legacy combat has no live modules");
        var startingWeapon=rig.Build.Items.First(c=>c.IsWeapon);
        Debug.Log($"[RigSmoke] model ready; starting weapon={startingWeapon.Kind}");
        var metaKey=(string)typeof(MetaProgressionManager).GetMethod("GetOrbitalModuleLevelKey",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{OrbitalModuleKind.Pistol});
        PlayerPrefs.SetInt(metaKey,3);
        var captured=typeof(TelekineticRig).GetMethod("CaptureAttack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(rig,new object[]{startingWeapon});
        float capturedDamage=(float)captured.GetType().GetField("Damage",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(captured);
        Assert.That(capturedDamage,Is.EqualTo(rig.Pistol.Damage*station.GetModuleMetaDamageMultiplier(OrbitalModuleKind.Pistol)).Within(.001f),"Purchased weapon progression must survive rig transfer");
        Assert.That(station.PresentationExtents.x,Is.GreaterThan(1));
        Assert.That(station.SlowFieldRadius,Is.GreaterThan(1));
        spawner.SpawnedPlayer.GetComponent<PlayerHealth>().DebugInvulnerable=true;
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        Assert.That(rig.Place(TelekineticPart.Pistol,Vector2Int.left),Is.False,"No editing during live combat");
        foreach (var kind in new[]{OrbitalRewardKind.DamageLink,OrbitalRewardKind.FireRateLink})
        {
            int total=rig.Build.Items.Count+rig.Build.Reserve.Count;
            Assert.That(UpgradeManager.Instance.DebugForceOrbitalReward(kind),Is.True);
            Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0),Is.True);
            yield return CoreTestSupport.Await(()=>rig.Editing);
            Assert.That(Time.timeScale,Is.Zero); Assert.That(rig.Build.Items.Count+rig.Build.Reserve.Count,Is.EqualTo(total+1));
            var part=OrbitalRewardProvider.GetRigPart(kind).Value;
            bool placed=false;
            for (int y=-2;y<=2 && !placed;y++) for(int x=-2;x<=2 && !placed;x++)
            {
                if (part==TelekineticPart.FR && !TelekineticBuild.Adjacent(new Vector2Int(x,y),startingWeapon.Cell(0))) continue;
                placed=rig.Place(part,new Vector2Int(x,y));
            }
            Assert.That(placed,Is.True); station.Interaction.CloseConstructor();
            yield return CoreTestSupport.Await(()=>!UpgradeManager.Instance.IsChoosingUpgrade && Time.timeScale>0);
        }
        station.enabled=false; MoveNeutral(station);
        rig.ResetAnomaly(); rig.AutoRotate=false;
        float angle=rig.Angle; rig.AdvanceRotation(.2f,true,false);
        Assert.That(Mathf.DeltaAngle(angle,rig.Angle),Is.EqualTo(18).Within(.01));
        angle=rig.Angle; rig.AdvanceRotation(.2f,false,true);
        Assert.That(Mathf.DeltaAngle(angle,rig.Angle),Is.EqualTo(-18).Within(.01));
        rig.AutoRotate=true; angle=rig.Angle; rig.AdvanceRotation(.5f,false,false);
        Assert.That(Mathf.DeltaAngle(angle,rig.Angle),Is.EqualTo(6).Within(.01));
        station.State.RotationEnergy=0; angle=rig.Angle; rig.AdvanceRotation(.2f,true,false);
        Assert.That(rig.Angle,Is.EqualTo(angle));
        var weapon=rig.Build.Items.First(c=>c.IsWeapon); var dmg=rig.Build.Items.First(c=>c.Kind==TelekineticPart.DMG);
        Assert.That(TelekineticBuild.Adjacent(dmg,weapon),Is.True);
        weapon.ShotRemaining=1; dmg.Remaining=0; rig.Tick(.02f,false,false,true);
        Assert.That(dmg.Activations,Is.GreaterThan(0)); Assert.That(rig.DamageFactor(weapon),Is.GreaterThan(1));
        var shift=station.GetComponent<OrbitalCenterShift>();
        if (Application.isFocused)
            typeof(OrbitalCenterShift).GetMethod("AdvancePointer",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(shift,
                new object[]{true,true,false,(Vector2)station.Owner.Transform.position+Vector2.right*2,.2f});
        else
        {
            Assert.That(shift.CanControl,Is.False,"Unfocused application must reject live pointer input");
            typeof(OrbitalCenterShift).GetMethod("Advance",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(shift,
                new object[]{Vector2.right,true,.2f});
            Debug.Log("[RigSmoke] Editor lacks OS input focus: centre transform checked through its existing runtime driver; physical pointer input is unconfirmed.");
        }
        Assert.That(shift.Offset.x,Is.GreaterThan(0)); shift.ResetOffset();
        station.BulletTime.Tick(true,.1f);
        station.InputOwner.TickBulletTime(true,station.BulletTime,.1f);
        Assert.That(Time.timeScale,Is.LessThan(1),"Existing production time owner must apply Bullet Time");
        Assert.That(station.BulletTime.IsActive,Is.True); station.DebugBulletTimeHeld=false; station.BulletTime.Release(); station.InputOwner.ReleaseBulletTime(); station.enabled=false;
        var zones=Object.FindObjectsByType<LocalAnomalyZone>(FindObjectsSortMode.None);
        foreach (var type in new[]{LocalAnomalyType.Stasis,LocalAnomalyType.Berserk,LocalAnomalyType.Glitch,LocalAnomalyType.Gravity,LocalAnomalyType.ExplosiveZone})
        {
            var controller=LevelAnomalyController.Instance;
            var zone=zones.FirstOrDefault(z=>z.AnomalyType==type && z.FocusArea!=null &&
                ReferenceEquals(controller.FindOrbitZone(z.FocusArea.bounds.center),z));
            if (zone==null)
            {
                // Exploration may provide only a special Gravity event. Keep it untouched and
                // admit an ordinary authored zone through the existing production registry.
                MoveNeutral(station);
                var data=AssetDatabase.LoadAssetAtPath<LocalAnomalyData>($"Assets/_Project/Data/Anomalies/LocalAnomaly_{type}.asset");
                zone=controller.SpawnSiteZone(data,station.Owner.Transform.position,new Vector2(2,2));
                Assert.That(zone,Is.Not.Null);
            }
            MovePlayer(station,zone.FocusArea.bounds.center);
            rig.Tick(0,false,false,true); Assert.That(rig.ActiveAnomaly,Is.EqualTo(type));
            if(type==LocalAnomalyType.Stasis || type==LocalAnomalyType.Berserk)
            {
                station.State.RotationEnergy=100;
                angle=rig.Angle; rig.AdvanceRotation(.2f,true,false);
                Assert.That(Mathf.DeltaAngle(angle,rig.Angle),Is.EqualTo(type==LocalAnomalyType.Stasis?9:18).Within(.01f));
                Assert.That(rig.RotationEnergy,Is.EqualTo(type==LocalAnomalyType.Berserk?91.25f:95).Within(.01f));
            }
            if(type==LocalAnomalyType.Gravity)
            {
                rig.Tick(.7f,false,false,true);
                Assert.That(Vector2.Distance(rig.DistortCenter(Vector2.right),Vector2.right),Is.GreaterThan(.1f));
            }
            if(type==LocalAnomalyType.Glitch)
            {
                rig.Tick(9.1f,false,false,true); Assert.That(rig.GlitchWarning,Is.True);
                rig.Tick(1,false,false,true); Assert.That(rig.Build.Validate(),Is.True);
            }
            if(type==LocalAnomalyType.ExplosiveZone)
            {
                Assert.That(rig.Suppressed,Is.True); Assert.That(rig.DamageFactor(weapon),Is.EqualTo(1));
                int shots=rig.ShotsFired; weapon.ShotRemaining=0; rig.Tick(.02f,false,false,true);
                Assert.That(rig.ShotsFired,Is.GreaterThan(shots),"Suppression preserves base shooting");
                for(int frame=0;frame<8;frame++) yield return null;
                ScreenCapture.CaptureScreenshot(TelekineticProductionAuthoring.Output+"/suppression.png"); yield return null;
            }
            rig.ResetAnomaly(); Assert.That(rig.ActiveAnomaly,Is.Null); Assert.That(rig.Build.Validate(),Is.True);
        }
        MoveNeutral(station); station.enabled=true;
        Assert.That(UpgradeManager.Instance.DebugForceOrbitalReward(OrbitalRewardKind.RigExpansion),Is.True);
        Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0),Is.True);
        yield return CoreTestSupport.Await(()=>rig.Editing);
        Assert.That(rig.Build.AvailableRadius,Is.EqualTo(3));
        station.Interaction.CloseConstructor();
        yield return CoreTestSupport.Await(()=>!UpgradeManager.Instance.IsChoosingUpgrade && Time.timeScale>0);
        station.enabled=false; rig.ResetAnomaly();
        var rapid=rig.Build.Items.First(c=>c.Kind==TelekineticPart.FR);
        rapid.Remaining=0; weapon.ShotRemaining=1; rig.Tick(.01f,false,false,true);
        Assert.That(rig.RateFactor(weapon),Is.GreaterThan(1));
        float remaining=rapid.Remaining;
        var savedBuild=rig.Build;
        Assert.That(station.SimulateSectorRestore(),Is.True);
        rig=station.Rig;
        Assert.That(rig.Build,Is.SameAs(savedBuild));
        Assert.That(rapid.Remaining,Is.EqualTo(remaining).Within(.001f),"Recreating art preserves module charge");
        Assert.That(rig.RateFactor(weapon),Is.EqualTo(1),"FR must not keep timestamps from a previous rig clock");
        Assert.That(station.Rings,Is.Empty); Assert.That(station.Modules,Is.Empty);
        Assert.That(station.Interaction.OpenConstructor(),Is.True);
        ScreenCapture.CaptureScreenshot(TelekineticProductionAuthoring.Output+"/constructor.png"); yield return null;
        station.Interaction.CloseConstructor();
        yield return null;
        ScreenCapture.CaptureScreenshot(TelekineticProductionAuthoring.Output+"/combat.png"); yield return null;
        RunEndService.Instance.ReturnToBunker();
        yield return CoreTestSupport.Await(()=>SceneManager.GetActiveScene().name==RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(Object.FindObjectsByType<TelekineticRig>(FindObjectsSortMode.None),Is.Empty);
    }
    private static void MovePlayer(OrbitalStationRuntime station,Vector2 point)
    {
        var player=station.Owner.Transform; player.position=point;
        if(player.TryGetComponent<Rigidbody2D>(out var body)){body.position=point;body.linearVelocity=Vector2.zero;}
        Physics2D.SyncTransforms();
    }
    private static void MoveNeutral(OrbitalStationRuntime station)
    {
        var bounds=GameplayAreaService.Instance.PlayableArea.bounds;
        for(int y=1;y<20;y++)for(int x=1;x<20;x++)
        {
            var point=new Vector2(Mathf.Lerp(bounds.min.x,bounds.max.x,x/20f),Mathf.Lerp(bounds.min.y,bounds.max.y,y/20f));
            if(LevelAnomalyController.Instance.FindOrbitZone(point)==null){MovePlayer(station,point);return;}
        }
        Assert.Fail("Fixture requires a neutral point");
    }
}
#endif
