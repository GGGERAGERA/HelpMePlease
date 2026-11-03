#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using Subject42.Combat.Telekinetic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

internal static class RigCatalogueFxCheck
{
    internal const string Output="Artifacts/GeneratedQA/RestoredMvpF1";
    internal static IEnumerator Run(OrbitalStationRuntime station,Func<string,IEnumerator> capture)
    {
        Directory.CreateDirectory(Output);
        var rig=station.Rig; var rewards=UpgradeManager.Instance;
        var report=new List<string> {Application.dataPath,Application.unityVersion};
        const BindingFlags methods=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        foreach(var c in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(c.GetType().Name.Contains("Debug") || c.GetType().GetMethod("OnGUI",methods)!=null)
                report.Add("LIVE "+c.GetType().FullName+" | "+AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(c)));
        foreach(var w in Resources.FindObjectsOfTypeAll<EditorWindow>()) report.Add("WINDOW "+w.GetType().FullName+" | "+w.titleContent.text);
        File.WriteAllLines(Output+"/live-debug-sources.txt",report);
        var debug=Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(debug,Is.Not.Null,"The original complete MVP F1 menu is installed");
        Assert.That(Object.FindObjectsByType<Subject42DebugMenu>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        void Toggle() => typeof(Subject42DebugMenu).GetMethod("ToggleHub",methods).Invoke(debug,null);
        void Open() { if(!Subject42DebugMenu.IsDebugMenuOpen) Toggle(); }
        void Close() { if(Subject42DebugMenu.IsDebugMenuOpen) Toggle(); }
        UnityEngine.UI.Button RewardButton(OrbitalRewardKind kind) =>
            Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>().Single(b=>b.gameObject.activeInHierarchy && b.name=="Rig reward · "+kind);
        void Add(OrbitalRewardKind kind)
        {
            var button=RewardButton(kind);
            Assert.That(button.interactable,Is.True,kind+": "+rewards.DebugRewardUnavailableReason(kind));
            button.onClick.Invoke();
        }
        Open(); yield return CoreTestSupport.Await(()=>Subject42DebugMenu.IsDebugMenuOpen);
        Assert.That(Time.timeScale,Is.Zero,"The original F1 sandbox pauses combat");
        var all=Enum.GetValues(typeof(TelekineticPart)).Cast<TelekineticPart>().ToArray();
        void AssertCompleteCatalogue()
        {
            foreach(var part in all)
            {
                var kind=rewards.DebugRewardCatalogue.Single(c=>OrbitalRewardProvider.GetRigPart(c.RewardKind)==part).RewardKind;
                Assert.That(RewardButton(kind),Is.Not.Null,"Existing F1 catalogue: "+part);
            }
        }
        AssertCompleteCatalogue();
        yield return capture("f1-catalogue");
        var tabs=(UnityEngine.UI.Image[])typeof(Subject42DebugMenu).GetField("tabButtonImages",methods).GetValue(debug);
        Assert.That(tabs.Length,Is.EqualTo(6),"All original F1 sections are preserved");
        tabs[1].GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
        AssertCompleteCatalogue();
        foreach(int section in new[]{2,3,5})
        {
            tabs[section].GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
            Assert.That(Subject42DebugMenu.IsDebugMenuOpen,Is.True);
            Assert.That(Time.timeScale,Is.Zero);
        }
        tabs[4].GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
        Assert.That(Subject42DebugMenu.IsDebugMenuOpen,Is.True,"Original search/favorites/visual hub exists");
        Close();
        yield return null; Assert.That(Time.timeScale,Is.GreaterThan(0),"F1 close restores combat time");
        Assert.That(station.InputOwner.IsIdle,Is.True);
        Assert.That(station.Interaction.OpenConstructor(),Is.True);
        Open(); yield return CoreTestSupport.Await(()=>Subject42DebugMenu.IsDebugMenuOpen);
        for(int i=0;i<4;i++) yield return null;
        Close(); yield return null;
        Assert.That(station.InputOwner.IsIdle,Is.True,"Original F1 releases the constructor before taking its pause");
        Assert.That(Time.timeScale,Is.GreaterThan(0),"F1 close releases its own pause");
        Assert.That(station.Interaction.OpenConstructor(),Is.True);
        while(rig.Build.Items.Count>0)
        {
            int before=rig.Build.Items.Count;
            foreach(var item in rig.Build.Items.Reverse().ToArray()) rig.Remove(item);
            Assert.That(rig.Build.Items.Count,Is.LessThan(before));
        }
        station.Interaction.CloseConstructor();
        var catalogue=rewards.DebugRewardCatalogue;
        foreach(var part in all)
        {
            var card=catalogue.SingleOrDefault(c=>OrbitalRewardProvider.GetRigPart(c.RewardKind)==part);
            Assert.That(card,Is.Not.Null,"Debug catalogue missing "+part);
            while(rig.Build.Reserve.Count(c=>c.Kind==part)<2)
            {
                yield return CoreTestSupport.Await(()=>rewards.IsRewardQueueIdle);
                int previous=rig.Build.Reserve.Count(c=>c.Kind==part);
                Open();
                yield return CoreTestSupport.Await(()=>Subject42DebugMenu.IsDebugMenuOpen);
                Add(card.RewardKind);
                yield return CoreTestSupport.Await(()=>station.InputOwner.IsConstructing && rig.Build.Reserve.Count(c=>c.Kind==part)==previous+1);
                station.Interaction.CloseConstructor();
                yield return CoreTestSupport.Await(()=>rewards.IsRewardQueueIdle && Time.timeScale>0);
            }
            report.Add("GRANTED "+part);
        }
        while(rig.Build.AvailableRadius<4)
        {
            int previous=rig.Build.AvailableRadius;
            Open(); yield return CoreTestSupport.Await(()=>Subject42DebugMenu.IsDebugMenuOpen);
            Add(OrbitalRewardKind.RigExpansion);
            yield return CoreTestSupport.Await(()=>station.InputOwner.IsConstructing && rig.Build.AvailableRadius==previous+1);
            station.Interaction.CloseConstructor();
            yield return CoreTestSupport.Await(()=>rewards.IsRewardQueueIdle);
        }
        Assert.That(station.Interaction.OpenConstructor(),Is.True);
        var points=(from y in Enumerable.Range(-4,9) from x in Enumerable.Range(-4,9) select new Vector2Int(x,y))
            .OrderBy(p=>Mathf.Abs(p.x)+Mathf.Abs(p.y)).ToArray();
        foreach(var part in all.OrderByDescending(p=>TelekineticItem.Shape(p).Count))
        {
            bool placed=false;
            foreach(var p in points)
            {
                for(int turn=0;turn<4 && !placed;turn++)
                    if(rig.Build.CanPlace(part,p,turn)) placed=rig.Place(part,p,turn);
                if(placed) break;
            }
            Assert.That(placed,Is.True,"Place whole footprint "+part);
        }
        var expected=new Dictionary<TelekineticPart,int>
        {
            [TelekineticPart.Frame]=1,[TelekineticPart.Pistol]=1,[TelekineticPart.Sniper]=2,[TelekineticPart.FR]=1,
            [TelekineticPart.Crit]=1,[TelekineticPart.Slow]=1,[TelekineticPart.Knock]=1,
            [TelekineticPart.Shotgun]=2,[TelekineticPart.DMG]=2,[TelekineticPart.Energy]=2,[TelekineticPart.Pierce]=2,[TelekineticPart.Splash]=2,[TelekineticPart.Shred]=2,
            [TelekineticPart.RIC]=3,[TelekineticPart.Saw]=4,[TelekineticPart.Double]=4,[TelekineticPart.Chain]=4,[TelekineticPart.Echo]=4
        };
        foreach(var item in rig.Build.Items)
        {
            Assert.That(item.CellCount,Is.EqualTo(expected[item.Kind]),"Preserve approved size: "+item.Kind);
            report.Add("FOOTPRINT "+item.Kind+" cells="+item.CellCount+" at="+item.Position+" turns="+item.QuarterTurns);
        }
        Assert.That(rig.Build.ValidateInventory(),Is.True);
        var keys=typeof(TelekineticRig).GetMethod("HandleConstructorInput",methods);
        bool auto=rig.AutoRotate;
        keys.Invoke(rig,new object[]{new Event {type=EventType.KeyDown,keyCode=KeyCode.F},Vector2Int.zero,false});
        Assert.That(rig.AutoRotate,Is.EqualTo(!auto));
        keys.Invoke(rig,new object[]{new Event {type=EventType.KeyDown,keyCode=KeyCode.F},Vector2Int.zero,false});
        typeof(TelekineticRig).GetField("selected",methods).SetValue(rig,rig.Build.Items.First(c=>c.Kind==TelekineticPart.Crit));
        keys.Invoke(rig,new object[]{new Event {type=EventType.KeyDown,keyCode=KeyCode.M},Vector2Int.zero,false});
        Assert.That((bool)typeof(TelekineticRig).GetField("merging",methods).GetValue(rig),Is.True);
        rig.CancelSelection();
        typeof(TelekineticRig).GetField("selected",methods).SetValue(rig,rig.Build.Items.First(c=>c.Kind==TelekineticPart.Crit));
        yield return capture("all-items");
        foreach(var item in rig.Build.Items.Where(c=>c.IsWeapon)) item.ShotRemaining=10;
        station.Interaction.CloseConstructor();
        float idleUntil=Time.time+.35f;
        yield return CoreTestSupport.Await(()=>Time.time>=idleUntil);
        foreach(var view in rig.Construction.GetComponentsInChildren<OrbitalModuleView>())
        {
            report.Add("WEAPON "+view.name+" animator="+(view.Animator!=null));
            foreach(var particle in view.Particles)
                report.Add("FX "+particle.name+" loop="+particle.main.loop+" awake="+particle.main.playOnAwake+" playing="+particle.isPlaying);
        }
        File.WriteAllLines(Output+"/catalogue-footprints-fx.txt",report);
        yield return capture("idle-weapons");
        Assert.That(rig.Construction.GetComponentsInChildren<OrbitalModuleView>().SelectMany(v=>v.Particles).All(p=>!p.isPlaying),Is.True,
            "Shot effects must be quiet between real attacks, not loop/playOnAwake");
        var pistol=rig.Build.Items.Single(c=>c.Kind==TelekineticPart.Pistol);
        int shots=pistol.Shots; pistol.ShotRemaining=0;
        yield return null;
        Assert.That(pistol.Shots,Is.GreaterThan(shots));
        var pistolView=rig.Construction.GetComponentsInChildren<OrbitalModuleView>().Single(v=>v.transform.parent.name.StartsWith("PISTOL #"));
        Assert.That(pistolView.Particles.Any(p=>p.isPlaying),Is.True,"An actual shot emits its existing FX");
        pistol.ShotRemaining=10;
        yield return capture("shot-pulse");
        float quietUntil=Time.time+.3f;
        yield return CoreTestSupport.Await(()=>Time.time>=quietUntil);
        yield return null;
        File.AppendAllText(Output+"/catalogue-footprints-fx.txt","\nAfter shot: time="+Time.time+" shots="+pistol.Shots+" playing="+pistolView.Particles.Count(p=>p.isPlaying));
        Assert.That(pistolView.Particles.All(p=>!p.isPlaying),Is.True,"One shot returns to idle");
        // Closely spaced real attacks must each restart the burst, without enabling a loop.
        for(int i=0;i<2;i++)
        {
            int before=pistol.Shots; pistol.ShotRemaining=0; yield return null;
            Assert.That(pistol.Shots,Is.GreaterThan(before)); pistol.ShotRemaining=10;
            Assert.That(pistolView.Particles.Any(p=>p.isPlaying),Is.True);
        }
        quietUntil=Time.time+.3f; yield return CoreTestSupport.Await(()=>Time.time>=quietUntil); yield return null;
        Assert.That(pistolView.Particles.All(p=>!p.isPlaying),Is.True,"Rapid repeated shots also return to idle");
        File.WriteAllText(Output+"/completed.txt","All 18 parts granted through the F1 UI add handler; distinct complete footprints installed; M/F controls and quiet/single-shot FX validated.");
    }
}
#endif
