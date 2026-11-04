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

public sealed class TelekineticWeaponTooltipTests
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

    private static TelekineticRig CreateRig(GameObject owner)
    {
        var station = owner.AddComponent<OrbitalStationRuntime>();
        var state = OrbitalRunState.CreateDefault(42);
        state.Telekinetic = new TelekineticBuild();
        typeof(OrbitalStationRuntime).GetProperty("State").SetValue(station, state);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TelekineticProductionAuthoring.PrefabPath);
        var rig = Object.Instantiate(prefab, owner.transform).GetComponent<TelekineticRig>();
        typeof(TelekineticRig).GetField("station", InstanceFields).SetValue(rig, station);
        return rig;
    }

    [Test]
    public void DamageModulesStackOnlyOnDirectWeaponNeighbours()
    {
        var owner = new GameObject("Two DMG modules");
        try
        {
            var rig = CreateRig(owner);
            var pistol = rig.Build.Grant(TelekineticPart.Pistol);
            var side = rig.Build.Grant(TelekineticPart.DMG);
            var above = rig.Build.Grant(TelekineticPart.DMG);
            Assert.That(rig.Build.Install(pistol, Vector2Int.down), Is.True);
            Assert.That(rig.Build.Install(side, new Vector2Int(1, -1)), Is.True);
            Assert.That(rig.Build.Install(above, Vector2Int.up, 1), Is.True);
            Assert.That(TelekineticBuild.Adjacent(above, side), Is.True);
            Assert.That(TelekineticBuild.Adjacent(above, pistol), Is.False);

            void AssertDamage(float multiplier, int connectedModules)
            {
                float expected = rig.BaseDamage(pistol) * multiplier;
                var attack = typeof(TelekineticRig).GetMethod("CaptureAttack", InstanceFields).Invoke(rig, new object[] { pistol });
                float actual = (float)attack.GetType().GetField("Damage", InstanceFields).GetValue(attack);
                Assert.That(actual, Is.EqualTo(expected).Within(.001f));
                var tooltip = (string)typeof(TelekineticRig).GetMethod("CompactEffect", InstanceFields).Invoke(rig, new object[] { pistol });
                Assert.That(tooltip.Split('\n'), Does.Contain($"Урон: {expected:0.##}"));
                Assert.That(tooltip.Split('\n').Count(line => line == "DMG I"), Is.EqualTo(connectedModules));
            }

            AssertDamage(1.75f, 1);
            Assert.That(rig.Build.Move(above, new Vector2Int(-1, -1), 0), Is.True);
            AssertDamage(2.5f, 2);
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [TestCase(TelekineticPart.DMG)]
    [TestCase(TelekineticPart.FR)]
    [TestCase(TelekineticPart.Double)]
    [TestCase(TelekineticPart.RIC)]
    [TestCase(TelekineticPart.Pierce)]
    [TestCase(TelekineticPart.Splash)]
    [TestCase(TelekineticPart.Chain)]
    [TestCase(TelekineticPart.Crit)]
    [TestCase(TelekineticPart.Slow)]
    [TestCase(TelekineticPart.Shred)]
    [TestCase(TelekineticPart.Knock)]
    [TestCase(TelekineticPart.Echo)]
    public void InstalledModuleImmediatelyAffectsTooltipAndAttack(TelekineticPart kind)
    {
        var owner = new GameObject("Weapon tooltip fixture");
        try
        {
            const BindingFlags instance = InstanceFields;
            var rig = CreateRig(owner);
            var weapon = rig.Build.Grant(TelekineticPart.Pistol);
            var module = rig.Build.Grant(kind);
            module.Level = 3;
            Assert.That(rig.Build.Install(weapon, Vector2Int.right), Is.True);
            string Tooltip() => (string)typeof(TelekineticRig).GetMethod("CompactEffect", instance).Invoke(rig, new object[] { weapon });
            string baseline = Tooltip();
            Assert.That(rig.Build.Install(module, new Vector2Int(1, 1)), Is.True);
            string linked = Tooltip();
            Assert.That(linked, Is.Not.EqualTo(baseline));
            if (kind == TelekineticPart.DMG)
            {
                Assert.That(rig.DamageFactor(weapon), Is.EqualTo(2.5f).Within(.001f), "DMG III applies as soon as the module is connected");
                Assert.That(linked, Does.Contain($"Урон: {rig.BaseDamage(weapon) * 2.5f:0.##}"));
                var energy = rig.Build.Grant(TelekineticPart.Energy);
                Assert.That(rig.Build.Install(energy, new Vector2Int(2, 1)), Is.True);
                Assert.That(rig.DamageFactor(weapon), Is.EqualTo(2.875f).Within(.001f), "ENERGY strengthens the adjacent DMG");
                Assert.That(Tooltip(), Does.Contain($"Урон: {rig.BaseDamage(weapon) * 2.875f:0.##}"));
                Assert.That(rig.Build.Stow(energy), Is.True);
            }

            var attack = typeof(TelekineticRig).GetMethod("CaptureAttack", instance).Invoke(rig, new object[] { weapon });
            T Read<T>(object value, string field) => (T)value.GetType().GetField(field, instance).GetValue(value);
            var profile = Read<OrbitalWeaponSettings>(attack, "Profile");
            var impact = Read<object>(attack, "Impact");
            string effect = kind switch
            {
                TelekineticPart.DMG or TelekineticPart.Crit => $"Урон: {Read<float>(attack, "Damage"):0.##}",
                TelekineticPart.FR => $"Скорость атаки: {Read<float>(attack, "AttacksPerSecond"):0.##}/с",
                TelekineticPart.Double => $"Залпов за атаку: {Read<int>(attack, "Volleys")}",
                TelekineticPart.RIC => $"Рикошеты: +{Read<int>(attack, "Ricochets")}",
                TelekineticPart.Pierce => $"Пробитие целей: {profile.BodyPierces}",
                TelekineticPart.Splash => $"Урон по области: {Read<float>(impact, "SplashDamage"):0.##}",
                TelekineticPart.Chain => $"Урон по цепи: {Read<float>(impact, "ChainDamage"):0.##}",
                TelekineticPart.Slow => $"Замедление: {1 - Read<float>(impact, "Slow"):P0}",
                TelekineticPart.Shred when profile.BypassShields => "Обход щитов",
                TelekineticPart.Knock => $"Отталкивание: {profile.Knockback:0.##}",
                TelekineticPart.Echo => $"Повторов: {Read<int>(attack, "Echoes")}",
                _ => throw new InvalidOperationException($"Missing effect for {kind}")
            };
            Assert.That(linked, Does.Contain(effect), "Tooltip must describe the current attack");
            Assert.That(baseline, Does.Not.Contain(effect), "A module must change its own stat or add its effect");
            Assert.That(linked, Does.Not.Contain("КД").And.Not.Contain("заряд"));

            Assert.That(rig.Build.Move(module, Vector2Int.left, 2), Is.True);
            Assert.That(Tooltip(), Is.EqualTo(baseline), "A module across the core no longer buffs this weapon");
            Assert.That(rig.DamageFactor(weapon), Is.EqualTo(1));
            Assert.That(rig.RateFactor(weapon), Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(owner); }
    }
}

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
        Assert.That(build.Place(TelekineticPart.Sniper,new Vector2Int(-1,0),2),Is.True);
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
    [Test] public void RetiredFrameReturnsDetachedPartsToReserve()
    {
        var state = OrbitalRunState.CreateDefault(42);
        state.EnableTelekineticRig();
        var build = state.Telekinetic;
        Assert.That(build.Reserve.Any(item => item.Kind == TelekineticPart.Frame), Is.False);
        Assert.That(OrbitalRewardProvider.GetRigPart((OrbitalRewardKind)28), Is.Null);

        build.Grant(TelekineticPart.Frame);
        Assert.That(build.Place(TelekineticPart.Frame, Vector2Int.right), Is.True);
        var module = build.Grant(TelekineticPart.FR);
        module.Level = 2;
        Assert.That(build.Install(module, new Vector2Int(2, 0)), Is.True);
        var gun = build.Items.Single(item => item.IsWeapon);

        state.EnableTelekineticRig();
        Assert.That(build.Items, Is.EquivalentTo(new[] { gun }));
        Assert.That(build.Reserve, Is.EquivalentTo(new[] { module }));
        Assert.That(module.Level, Is.EqualTo(2));
        Assert.That(build.ValidateInventory(), Is.True);
        int revision = state.Revision;
        state.EnableTelekineticRig();
        Assert.That(state.Revision, Is.EqualTo(revision));
    }

    [Test] public void OldSniperFootprintsKeepValidPlacementsAndStowConflicts()
    {
        var state = OrbitalRunState.CreateDefault(42);
        state.EnableTelekineticRig();
        var build = state.Telekinetic;
        var blocked = build.Grant(TelekineticPart.Sniper);
        var branch = build.Grant(TelekineticPart.FR);
        var stays = build.Grant(TelekineticPart.Sniper);
        Assert.That(build.Install(blocked, Vector2Int.left), Is.True);
        Assert.That(build.Install(branch, new Vector2Int(-2, 0)), Is.True);
        Assert.That(build.Install(stays, Vector2Int.right), Is.True);

        // With the old one-cell shape this facing didn't overlap the core.
        blocked.QuarterTurns = 1;
        blocked.Level = 2;
        state.Version = 5;
        state.MigrateRetiredModules();

        Assert.That(build.ValidateInventory(), Is.True);
        Assert.That(build.Reserve, Is.EquivalentTo(new[] { blocked, branch }));
        Assert.That(blocked.Level, Is.EqualTo(2));
        Assert.That(build.At(new Vector2Int(1, 1)), Is.SameAs(stays));
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
        Assert.That(rig.DamageFactor(weapon),Is.GreaterThan(1),"Adjacent DMG applies without a timer");
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
        float rateBeforeRestore = rig.RateFactor(weapon);
        Assert.That(rateBeforeRestore,Is.GreaterThan(1));
        var savedBuild=rig.Build;
        Assert.That(station.SimulateSectorRestore(),Is.True);
        rig=station.Rig;
        Assert.That(rig.Build,Is.SameAs(savedBuild));
        Assert.That(rig.RateFactor(weapon),Is.EqualTo(rateBeforeRestore).Within(.001f),"FR follows the restored layout immediately");
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
