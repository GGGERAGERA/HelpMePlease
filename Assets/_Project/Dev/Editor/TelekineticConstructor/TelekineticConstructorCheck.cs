#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Subject42.DebugLabs.Telekinetic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Reusable, bounded scene-local check. No Test Runner, batch build, profile writes or production scene changes.
[InitializeOnLoad]
public static class TelekineticConstructorCheck
{
    public const string Output="Artifacts/GeneratedQA/TelekineticConstructor";
    private const string Key="Subject42.Telekinetic.ShortCheck";
    private static IEnumerator routine;
    private static double next,deadline;
    private static readonly List<string> report=new();
    private static int errors;
    static TelekineticConstructorCheck()
    {
        EditorApplication.update+=Update;
        EditorApplication.playModeStateChanged+=state=>
        {
            if (state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            { report.Clear(); errors=0; routine=Checks(); next=0; deadline=EditorApplication.timeSinceStartup+35; }
            if (state==PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(Key);
        };
        Application.logMessageReceived+=(message,stack,type)=>
        { if (routine!=null && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert)) { errors++; report.Add("UNITY ERROR: "+message); } };
    }
    [MenuItem("Tools/Subject42/Dev/Telekinetic Constructor/Short check")]
    public static void Run() => StartCheck();
    [MenuItem("Tools/Subject42/Dev/Telekinetic Constructor/Short visual check")]
    public static void RunVisual() => StartCheck();
    private static void StartCheck()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Output); TelekineticConstructorAuthoring.Create();
        EditorSceneManager.OpenScene(TelekineticConstructorLab.ScenePath); TelekineticConstructorAuthoring.ConfigureStructureView(); SetGameView();
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    private static void Update()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && routine==null && File.Exists(Output+"/editor.request"))
        {
            string command=File.ReadAllText(Output+"/editor.request").Trim(); File.Delete(Output+"/editor.request");
            if (command=="check") Run();
            else if (command=="visual") RunVisual();
            else if (command=="open") TelekineticConstructorAuthoring.Open();
            else if (command=="play" && !EditorApplication.isPlayingOrWillChangePlaymode) { TelekineticConstructorAuthoring.Open(); SetGameView(); EditorApplication.EnterPlaymode(); }
            else if (command=="stop") EditorApplication.ExitPlaymode();
            else if (command=="refresh") AssetDatabase.Refresh();
            else if (command=="capture" && EditorApplication.isPlaying) ScreenCapture.CaptureScreenshot(Output+"/manual.png");
        }
        if (routine==null || EditorApplication.timeSinceStartup<next) return;
        try
        {
            if (EditorApplication.timeSinceStartup>deadline) throw new Exception("Short check timed out");
            if (!routine.MoveNext()) { Finish(errors==0); return; }
            next=EditorApplication.timeSinceStartup+(routine.Current is float wait?wait:.02);
        }
        catch (Exception e) { report.Add("FAIL: "+e); Finish(false); }
    }
    private static void Finish(bool success)
    {
        routine=null; report.Add(success?"PASS: short scene check; no runtime errors":"FAIL");
        File.WriteAllLines(Output+"/check.txt",report); Debug.Log("[TelekineticConstructor] "+report.Last());
        EditorApplication.ExitPlaymode();
    }
    private static void Require(bool condition,string text)
    { if (!condition) throw new Exception(text); report.Add("PASS: "+text); }
    private static IEnumerator Checks()
    {
        yield return .25f;
        var lab=Object.FindFirstObjectByType<TelekineticConstructorLab>();
        Require(lab!=null && lab.Player!=null && lab.Editing,"V2 scene initialized in paused constructor");
        var b=new TelekineticBuild();
        var kinds=(TelekineticPart[])Enum.GetValues(typeof(TelekineticPart));
        int[] sizes={1,1,2,4,2,1,4,3,2,2,2,4,1,1,2,1,4};
        for (int kind=0;kind<kinds.Length;kind++) for (int turn=0;turn<4;turn++)
        {
            b.Clear(); bool placed=false;
            for (int y=-4;y<=4 && !placed;y++) for (int x=-4;x<=4 && !placed;x++)
                if (b.CanPlace(kinds[kind],new Vector2Int(x,y),turn)) placed=b.Place(kinds[kind],new Vector2Int(x,y),turn);
            var item=b.Items.Single();
            if (!placed || item.CellCount!=sizes[kind] || item.QuarterTurns!=turn || !b.Validate()) throw new Exception("Invalid shape/orientation: "+kinds[kind]);
            for (int i=0;i<item.CellCount;i++) if (b.At(item.Cell(i))!=item) throw new Exception("Footprint duplicated logical item");
        }
        Require(true,"all seventeen item types support four orientations and one identity per footprint");
        b.Clear(); b.Place(TelekineticPart.Frame,Vector2Int.right); b.Place(TelekineticPart.Frame,new Vector2Int(2,0)); b.Place(TelekineticPart.Frame,new Vector2Int(3,0));
        Require(!b.Place(TelekineticPart.Saw,new Vector2Int(4,0)) && b.Items.Count==3,"reject footprint extending outside grid even when anchor is free and connected");
        b.Clear(); b.Place(TelekineticPart.FR,Vector2Int.right); b.Place(TelekineticPart.FR,new Vector2Int(1,1));
        Require(!b.Place(TelekineticPart.Shotgun,Vector2Int.up,1) && b.At(Vector2Int.up)==null && b.Items.Count==2,"reject overlap in non-anchor cell and preserve existing items");
        b.Clear(); b.Place(TelekineticPart.Frame,Vector2Int.right); b.Place(TelekineticPart.Shotgun,new Vector2Int(2,0)); b.Place(TelekineticPart.Pistol,new Vector2Int(3,1));
        var gun=b.At(new Vector2Int(2,1)); gun.Remaining=1.25f;
        Require(b.Rotate(gun) && gun.QuarterTurns==1 && b.At(new Vector2Int(3,0))==gun,"R rotates the whole 1x2 footprint around placement anchor");
        Require(!b.Rotate(gun) && gun.QuarterTurns==1 && gun.Remaining==1.25f,"reject disconnecting rotation and retain identity/timer/orientation");
        Require(!b.Remove(gun) && b.Items.Contains(gun),"reject removing a whole multi-cell bridge");
        b.Clear(); b.Place(TelekineticPart.RIC,Vector2Int.right);
        var ric=b.At(Vector2Int.right);
        Require(b.Rotate(ric) && b.At(new Vector2Int(1,-1))==ric && b.At(new Vector2Int(1,1))==null &&
            !b.Rotate(ric) && ric.QuarterTurns==1,"L footprint rotates; core overlap is rejected atomically");
        b.Clear(); b.Place(TelekineticPart.Frame,Vector2Int.right); b.Place(TelekineticPart.FR,new Vector2Int(2,0));
        b.Place(TelekineticPart.Shotgun,new Vector2Int(1,1)); b.Place(TelekineticPart.FR,new Vector2Int(2,1));
        gun=b.At(new Vector2Int(1,2)); var peer=b.At(new Vector2Int(2,0));
        Require(!b.Move(gun,new Vector2Int(2,0)) && gun.Position==new Vector2Int(1,1) && peer.Position==new Vector2Int(2,0),"failed whole-item swap cannot overwrite a third item");
        b.Remove(b.At(new Vector2Int(2,1)));
        Require(b.Move(gun,new Vector2Int(2,0)) && b.At(new Vector2Int(2,1))==gun && b.At(new Vector2Int(1,1))==peer,"whole-item swap succeeds when both footprints fit and stay connected");
        b.Clear(); b.Place(TelekineticPart.Shotgun,Vector2Int.right); b.Place(TelekineticPart.Pistol,new Vector2Int(2,1));
        gun=b.At(new Vector2Int(1,1)); peer=b.At(new Vector2Int(2,1));
        Require(b.CanMove(gun,new Vector2Int(2,0),0) && gun.Position==Vector2Int.right && peer.Position==new Vector2Int(2,1) &&
            b.Move(gun,new Vector2Int(2,0)) && peer.Position==Vector2Int.right && b.At(new Vector2Int(2,1))==gun,
            "non-anchor-cell drag swaps the item under the grabbed cell; preview leaves both items unchanged");
        b.Clear(); b.Place(TelekineticPart.Shotgun,Vector2Int.right); b.Place(TelekineticPart.Pistol,new Vector2Int(2,1));
        gun=b.At(new Vector2Int(1,1)); peer=b.At(new Vector2Int(2,1));
        Require(b.CanMove(gun,new Vector2Int(1,1),1) && gun.QuarterTurns==0 &&
            b.Move(gun,new Vector2Int(1,1),1) && gun.QuarterTurns==1 && peer.Position==Vector2Int.right,
            "non-anchor drag plus R exchanges whole items using the rotated grab offset");
        b.Clear(); b.Place(TelekineticPart.Shotgun,Vector2Int.right); b.Place(TelekineticPart.Pistol,new Vector2Int(2,0));
        gun=b.At(Vector2Int.right); peer=b.At(new Vector2Int(2,0));
        Require(!b.Rotate(gun) && gun.QuarterTurns==0 && peer.Position==new Vector2Int(2,0),"in-place R cannot swap an overlapping neighbor");
        b.Clear(); b.Place(TelekineticPart.DMG,Vector2Int.right); b.Place(TelekineticPart.Frame,Vector2Int.up);
        b.Place(TelekineticPart.Saw,new Vector2Int(2,0)); b.Place(TelekineticPart.Pistol,new Vector2Int(0,2));
        var module=b.At(Vector2Int.right); var saw=b.At(new Vector2Int(3,1)); var diagonal=b.At(new Vector2Int(0,2));
        Require(b.Neighbours(module).Count(i=>i.IsWeapon)==1 && b.Neighbours(module).Contains(saw) &&
            !TelekineticBuild.Adjacent(module,diagonal),"two shared sides yield one weapon recipient; diagonal yields none");
        b.Clear(); b.Place(TelekineticPart.Frame,Vector2Int.right); b.Place(TelekineticPart.RIC,new Vector2Int(2,0));
        Require(b.Place(TelekineticPart.Pistol,new Vector2Int(3,1)) && b.Neighbours(b.At(new Vector2Int(2,0))).Count(i=>i.IsWeapon)==1,"L notch remains genuinely free; its two shared sides do not duplicate effect");
        b.Clear(); b.Place(TelekineticPart.Frame,Vector2Int.right); b.Place(TelekineticPart.DMG,new Vector2Int(2,0)); b.Place(TelekineticPart.Energy,new Vector2Int(3,0));
        module=b.At(new Vector2Int(2,0));
        Require(Mathf.Abs(lab.Modules.Cooldown(module,b)-lab.Modules.DamageCooldown/(1+lab.Modules.EnergyBonus))<.001f,"one 1x2 ENERGY sharing two sides contributes exactly once");
        b.Place(TelekineticPart.Energy,new Vector2Int(2,2));
        Require(Mathf.Abs(lab.Modules.Cooldown(module,b)-lab.Modules.DamageCooldown/(1+2*lab.Modules.EnergyBonus))<.001f,"two distinct ENERGY items retain the existing additive cooldown formula");
        b.Clear(); b.Place(TelekineticPart.DMG,Vector2Int.right); b.Place(TelekineticPart.DMG,Vector2Int.left);
        var keep=b.At(Vector2Int.right); var donor=b.At(new Vector2Int(-1,1)); keep.Remaining=1.23f;
        Require(b.Merge(keep,donor) && keep.Level==2 && b.Items.Count==1 && keep.CellCount==2 && b.At(new Vector2Int(-1,1))==null,"Merge I+I consumes one whole 1x2 item and frees both cells");
        b.Place(TelekineticPart.DMG,Vector2Int.left); b.Place(TelekineticPart.DMG,Vector2Int.down,1);
        donor=b.At(Vector2Int.left); b.Merge(donor,b.At(Vector2Int.down));
        Require(b.Merge(keep,donor) && keep.Level==3 && b.Items.Count==1 && keep.Remaining==1.23f,"Merge II+II retains surviving item and timer, without scaling its footprint");
        b.Clear(); b.Place(TelekineticPart.Frame,Vector2Int.right); b.Place(TelekineticPart.DMG,new Vector2Int(2,0));
        b.Place(TelekineticPart.Pistol,new Vector2Int(3,1)); b.Place(TelekineticPart.DMG,Vector2Int.up);
        Require(!b.Merge(b.At(Vector2Int.up),b.At(new Vector2Int(2,1))) && b.Items.Count==4,"Merge cannot silently detach a branch");

        lab.ResetBuild(); lab.EnemyDensity=0;
        lab.Place(TelekineticPart.DMG,Vector2Int.right); lab.Place(TelekineticPart.Frame,Vector2Int.up);
        lab.Place(TelekineticPart.Saw,new Vector2Int(2,0)); lab.Place(TelekineticPart.Pistol,new Vector2Int(0,2));
        lab.Place(TelekineticPart.Energy,Vector2Int.down,2);
        module=lab.Build.At(Vector2Int.right); saw=lab.Build.At(new Vector2Int(3,1)); diagonal=lab.Build.At(new Vector2Int(0,2));
        Require(Mathf.Abs(lab.Modules.Cooldown(module,lab.Build)-lab.Modules.DamageCooldown)<.001f,"diagonal ENERGY cannot reduce module cooldown");
        lab.NewFight(); foreach (var item in lab.Build.Items) if (item.IsWeapon) item.ShotRemaining=100;
        module.Remaining=0; yield return .12f;
        Require(module.Activations==1 && Mathf.Abs(lab.DamageFactor(saw)-lab.Modules.DamageMultiplier)<.001f && lab.DamageFactor(diagonal)==1,
            "real module activation applies DMG once across two sides and never across diagonal");
        lab.SetEditing(true); lab.ResetBuild();
        lab.Place(TelekineticPart.Frame,Vector2Int.right); lab.Place(TelekineticPart.Shotgun,new Vector2Int(2,0)); lab.Place(TelekineticPart.Pistol,new Vector2Int(3,1));
        gun=lab.Build.At(new Vector2Int(2,1)); int version=lab.StructureView.RebuildVersion;
        Require(lab.Rotate(gun) && lab.Move(gun,new Vector2Int(1,1)) && lab.StructureView.RebuildVersion==version+2,
            "lab rotation and drag transaction rebuild the current visual rig");
        var gunView=lab.Construction.Find(TelekineticBuild.Label(gun.Kind)+" #"+gun.Id);
        Require(Vector2.Distance(gunView.localPosition,gun.Center*lab.CellSpacing)<.001f && lab.StructureView.SocketCount==lab.Build.Items.Count,
            "one visual/weapon center and one socket per logical item");
        Directory.CreateDirectory(Output+"/V2"); lab.EnemyDensity=8;
        for (int preset=0;preset<3;preset++)
        {
            lab.LoadPreset(preset);
            Require(lab.Build.Items.Count==11 && lab.Build.Validate() && lab.StructureView.SocketCount==11,"V2 preset "+preset+": valid multi-cell layout and eleven logical sockets");
            if (preset==0) { ScreenCapture.CaptureScreenshot(Output+"/V2/constructor.png"); yield return .15f; }
            lab.NewFight(); yield return .2f;
            Require(lab.StructureView.CombatVisible && lab.Construction.GetComponentsInChildren<Rigidbody2D>(true).Length==0 &&
                lab.Construction.GetComponentsInChildren<Joint2D>(true).Length==0,"V2 preset "+preset+": unified combat chassis has no per-cell physics");
            ScreenCapture.CaptureScreenshot(Output+"/V2/preset-"+preset+".png"); yield return .15f;
            if (preset==0) { yield return .75f; ScreenCapture.CaptureScreenshot(Output+"/V2/preset-0-motion.png"); yield return .15f; }
            lab.SetEditing(true); Require(!lab.StructureView.CombatVisible,"V2 preset "+preset+": Tab hides the combat chassis");
        }
        var effects=EffectChecks(lab); while (effects.MoveNext()) yield return effects.Current;
        lab.LoadPreset(3);
    }
    private static IEnumerator EffectChecks(TelekineticConstructorLab lab)
    {
        Directory.CreateDirectory(Output+"/Effects");
        Require(Enum.GetValues(typeof(TelekineticPart)).Cast<TelekineticPart>().All(kind=>lab.Icon(kind)!=null),"all weapons and modules have real icon sprites");
        float originalHealth=lab.EnemyMaxHealth;
        foreach (var kind in new[]{TelekineticPart.Pierce,TelekineticPart.Splash,TelekineticPart.Chain,TelekineticPart.Crit,
            TelekineticPart.Slow,TelekineticPart.Shred,TelekineticPart.Knock,TelekineticPart.Echo})
        {
            lab.SetEditing(true); lab.ResetBuild(); lab.EnemyDensity=0; lab.EnemyMaxHealth=1000;
            lab.Place(TelekineticPart.Frame,Vector2Int.right); lab.Place(TelekineticPart.Pistol,new Vector2Int(2,0));
            Require(lab.Place(kind,new Vector2Int(3,0)),kind+": place requested footprint");
            var source=lab.Build.At(new Vector2Int(3,0)); var weapon=lab.Build.At(new Vector2Int(2,0));
            int diagonalY=source.CellCount==1?1:2;
            lab.Place(TelekineticPart.Frame,new Vector2Int(1,1));
            if (diagonalY==2) lab.Place(TelekineticPart.Frame,new Vector2Int(1,2));
            lab.Place(TelekineticPart.Pistol,new Vector2Int(2,diagonalY));
            var diagonal=lab.Build.At(new Vector2Int(2,diagonalY));
            lab.NewFight(); lab.EnemyDensity=3;
            var targets=new[]{lab.SpawnTarget(new Vector2(4,0),true,kind==TelekineticPart.Shred),
                lab.SpawnTarget(new Vector2(5.3f,0),true),lab.SpawnTarget(new Vector2(4,1),true)};
            Physics2D.SyncTransforms();
            for (int i=0;i<targets.Length;i++)
            {
                Collider2D body=targets[i].GetComponent<CapsuleCollider2D>() ?? targets[i].GetComponent<Collider2D>();
                Vector2 center=i==0?new Vector2(4,0):i==1?new Vector2(5.3f,0):new Vector2(4,1);
                targets[i].GetComponent<LabEnemyDummy>().MoveRoot(center-(Vector2)(body.bounds.center-targets[i].transform.position));
            }
            Physics2D.SyncTransforms();
            weapon.ShotRemaining=diagonal.ShotRemaining=100; source.Remaining=100;
            yield return .08f;
            if (kind==TelekineticPart.Shred)
            {
                weapon.ShotRemaining=0; yield return .05f; weapon.ShotRemaining=100; yield return .2f;
                Require(targets[0].CurrentHealth==1000 && targets[0].GetComponent<EnemyDirectionalShield>().Blocks>0,"ordinary shot is blocked by an authored shield");
            }
            source.Remaining=0; yield return .08f;
            Require(lab.PendingEffects(weapon).Contains(kind) && !lab.PendingEffects(diagonal).Any(),kind+": one side-adjacent charge, no diagonal/weapon relay");
            if (kind==TelekineticPart.Knock)
            {
                targets[0].GetComponent<LabEnemyDummy>().ApplyMode(LabEnemyDummyMode.NormalAi);
                var movement=targets[0].GetComponent<EnemyMovement>(); movement.SetSpeedMultiplier(.1f); movement.SetAnomalySpeedMultiplier(.1f);
            }
            float startX=targets[0].transform.position.x;
            weapon.ShotRemaining=0; yield return .05f; weapon.ShotRemaining=100; yield return .4f;
            bool applied=kind switch
            {
                TelekineticPart.Pierce=>targets[1].CurrentHealth<1000,
                TelekineticPart.Splash=>targets[2].CurrentHealth<1000 && lab.SplashBursts==1,
                TelekineticPart.Chain=>targets[2].CurrentHealth<1000 && lab.ChainJumps==1 && targets[1].CurrentHealth==1000,
                TelekineticPart.Crit=>Mathf.Abs((1000-targets[0].CurrentHealth)-lab.Pistol.Damage*lab.Modules.CritMultiplier)<.02f,
                TelekineticPart.Slow=>lab.SlowFactor(targets[0])<1 && lab.ActiveSlows==1,
                TelekineticPart.Shred=>targets[0].CurrentHealth<1000 && !targets[0].GetComponent<EnemyDirectionalShield>().IsBroken,
                TelekineticPart.Knock=>targets[0].transform.position.x>startX+.02f,
                TelekineticPart.Echo=>lab.EchoShots==1 && weapon.Shots==2 && targets[0].CurrentHealth<1000-lab.Pistol.Damage,
                _=>false
            };
            Require(applied,kind+": effect confirmed by real projectile hit / target state");
            if (kind==TelekineticPart.Slow)
            {
                ScreenCapture.CaptureScreenshot(Output+"/Effects/slow-hit.png"); yield return .1f;
                yield return lab.Modules.SlowSeconds+.05f;
                Require(lab.ActiveSlows==0 && lab.SlowFactor(targets[0])==1,"slow expires and restores ordinary movement multiplier");
            }
            if (kind==TelekineticPart.Echo) { yield return .35f; Require(lab.EchoShots==1,"delayed echo cannot schedule another echo"); }
            lab.SetEditing(true); lab.ClearEnemies();
            Require(lab.ActiveSlows==0,"fixture reset releases temporary enemy status");
        }
        var contacts=VolleyAndContactChecks(lab); while (contacts.MoveNext()) yield return contacts.Current;
        lab.EnemyMaxHealth=originalHealth; lab.EnemyDensity=8;
        foreach (int preset in new[]{3,4})
        {
            lab.LoadPreset(preset); Require(lab.Build.Validate(),"effect preset "+preset+": connected small/large layout");
            ScreenCapture.CaptureScreenshot(Output+"/Effects/constructor-"+preset+".png"); yield return .15f;
            lab.NewFight(); yield return .25f;
            foreach (var item in lab.Build.Items.Where(i=>!i.IsWeapon))
            {
                var view=lab.Construction.Find(TelekineticBuild.Label(item.Kind)+" #"+item.Id);
                var node=view.Find("Pixel node").GetComponent<SpriteRenderer>();
                Require(node.sprite==lab.Icon(item.Kind),$"combat {item.Kind}: renderer uses its own icon ({node.sprite.name})");
            }
            ScreenCapture.CaptureScreenshot(Output+"/Effects/combat-"+preset+"-a.png"); yield return .15f;
            yield return .65f; ScreenCapture.CaptureScreenshot(Output+"/Effects/combat-"+preset+"-b.png"); yield return .15f;
            lab.SetEditing(true);
        }
    }
    private static void AlignTarget(EnemyHealth target,Vector2 center)
    {
        Physics2D.SyncTransforms();
        Collider2D body=target.GetComponent<CapsuleCollider2D>() ?? target.GetComponent<Collider2D>();
        target.GetComponent<LabEnemyDummy>().MoveRoot(center-(Vector2)(body.bounds.center-target.transform.position));
        Physics2D.SyncTransforms();
    }
    private static IEnumerator VolleyAndContactChecks(TelekineticConstructorLab lab)
    {
        lab.SetEditing(true); lab.ResetBuild(); lab.EnemyDensity=0;
        lab.Place(TelekineticPart.Frame,Vector2Int.right); lab.Place(TelekineticPart.Shotgun,new Vector2Int(2,0));
        lab.Place(TelekineticPart.Splash,new Vector2Int(3,0)); lab.Place(TelekineticPart.Chain,new Vector2Int(0,1));
        var weapon=lab.Build.At(new Vector2Int(2,0));
        lab.NewFight(); lab.EnemyDensity=3;
        var primary=lab.SpawnTarget(new Vector2(4,1),true); AlignTarget(primary,new Vector2(4,1));
        var second=lab.SpawnTarget(new Vector2(5.3f,1.3f),true); AlignTarget(second,new Vector2(5.3f,1.3f));
        var side=lab.SpawnTarget(new Vector2(4,2),true); AlignTarget(side,new Vector2(4,2));
        weapon.ShotRemaining=100; foreach (var item in lab.Build.Items) if (item.IsModule) item.Remaining=0;
        yield return .08f;
        Require(lab.PendingEffects(weapon).Count()==2,"one shotgun receives both side-adjacent splash and chain");
        weapon.ShotRemaining=0; yield return .05f; weapon.ShotRemaining=100; yield return .5f;
        Require(primary.CurrentHealth<1000-lab.Shotgun.Damage && lab.SplashBursts==1 && lab.ChainJumps==1,
            "several pellets hit; splash and chain trigger once per volley");

        lab.SetEditing(true); lab.ResetBuild(); lab.EnemyDensity=0;
        lab.Place(TelekineticPart.Saw,Vector2Int.right); lab.Place(TelekineticPart.Crit,new Vector2Int(3,0));
        lab.Place(TelekineticPart.Slow,new Vector2Int(3,1)); lab.Place(TelekineticPart.Splash,new Vector2Int(1,2));
        lab.Place(TelekineticPart.Echo,new Vector2Int(-1,1));
        weapon=lab.Build.At(Vector2Int.right);
        lab.NewFight(); lab.EnemyDensity=2;
        Vector2 point=weapon.Center*lab.CellSpacing;
        primary=lab.SpawnTarget(point+Vector2.right*.25f,true); AlignTarget(primary,point+Vector2.right*.25f);
        second=lab.SpawnTarget(point+Vector2.right*1.25f,true); AlignTarget(second,point+Vector2.right*1.25f);
        weapon.ShotRemaining=100; foreach (var item in lab.Build.Items) if (item.IsModule) item.Remaining=0;
        yield return .08f;
        Require(lab.PendingEffects(weapon).Count()==4,"saw receives crit, slow, splash and echo as one logical weapon");
        weapon.ShotRemaining=0; yield return .05f; weapon.ShotRemaining=100; yield return .45f;
        Require(Mathf.Abs(1000-primary.CurrentHealth-2*lab.Saw.Damage*lab.Modules.CritMultiplier)<.02f &&
            lab.EchoShots==1 && lab.SplashBursts==2 && lab.ActiveSlows==1 && second.CurrentHealth<1000,
            "saw contact and its delayed echo apply captured damage, slow and one splash each");

        lab.SetEditing(true); lab.ResetBuild(); lab.EnemyDensity=0;
        lab.Place(TelekineticPart.Frame,Vector2Int.right); lab.Place(TelekineticPart.Pistol,new Vector2Int(2,0));
        lab.Place(TelekineticPart.Slow,new Vector2Int(3,0)); weapon=lab.Build.At(new Vector2Int(2,0));
        var source=lab.Build.At(new Vector2Int(3,0));
        lab.NewFight(); lab.EnemyDensity=1;
        primary=lab.SpawnTarget(new Vector2(4,0),true); AlignTarget(primary,new Vector2(4,0));
        primary.GetComponent<LabEnemyDummy>().ApplyMode(LabEnemyDummyMode.NormalAi);
        primary.GetComponent<EnemyMovement>().SetSpeedMultiplier(.25f);
        weapon.ShotRemaining=source.Remaining=100; yield return .1f;
        Vector2 from=primary.transform.position; float start=lab.CombatSeconds;
        yield return .2f;
        float before=Vector2.Distance(from,primary.transform.position)/(lab.CombatSeconds-start);
        source.Remaining=0; yield return .05f; weapon.ShotRemaining=0; yield return .05f; weapon.ShotRemaining=100;
        yield return .12f;
        Require(lab.ActiveSlows==1,"moving enemy receives slow from a real projectile");
        from=primary.transform.position; start=lab.CombatSeconds; yield return .25f;
        float during=Vector2.Distance(from,primary.transform.position)/(lab.CombatSeconds-start);
        yield return lab.Modules.SlowSeconds+.05f;
        from=primary.transform.position; start=lab.CombatSeconds; yield return .2f;
        float after=Vector2.Distance(from,primary.transform.position)/(lab.CombatSeconds-start);
        Require(before>.05f && during>before*.3f && during<before*.9f && after>before*.9f && lab.ActiveSlows==0,
            $"real movement slows and recovers (before {before:F2}, during {during:F2}, after {after:F2})");
        lab.SetEditing(true); lab.ClearEnemies();
    }
    private static void SetGameView()
    {
        var assembly=typeof(Editor).Assembly; var type=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        var group=type.GetMethod("GetGroup").Invoke(sizes,new object[]{0}); var gt=group.GetType();
        int count=(int)gt.GetMethod("GetTotalCount").Invoke(group,null),index=-1;
        for (int i=0;i<count;i++)
        {
            var size=gt.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
            if ((int)size.GetType().GetProperty("width").GetValue(size)==1600 && (int)size.GetType().GetProperty("height").GetValue(size)==900) { index=i; break; }
        }
        if (index<0)
        {
            var mode=Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"),1);
            var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{mode,1600,900,"Subject42 Lab 1600x900"});
            gt.GetMethod("AddCustomSize").Invoke(group,new[]{size}); index=count;
        }
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex").SetValue(view,index); view.Show(); view.Focus();
    }
}
#endif
