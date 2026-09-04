#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Subject42.Combat.OrbitalStation;
using Object=UnityEngine.Object;
public sealed class Subject42OrbitalLinkRendererTests {
 static object Get(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f.GetValue(o);}throw new MissingFieldException(n);}
 static void Set(object o,string n,object v){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null){f.SetValue(o,v);return;}}throw new MissingFieldException(n);}
 static void Tick(OrbitalStationRuntime s,float dt=1f/60)=>s.GetType().GetMethod("UpdateLinkNodes",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,new object[]{dt});
 static LineRenderer Line(OrbitalStationRuntime s)=>((IDictionary)Get(s,"linkLines")).Values.Cast<LineRenderer>().Single();
 static IEnumerator Frames(){int end=Time.frameCount+2;while(Time.frameCount<end)yield return null;}
 [UnityTest] public IEnumerator LinkRenderer_LifetimeAndCombatCompatibility(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);yield return new EnterPlayMode();yield return Run();yield return new ExitPlayMode();}
 static IEnumerator Run(){
 var stage=ScriptableObject.CreateInstance<StageProfileData>();var rule=ScriptableObject.CreateInstance<WorldRuleData>();var anomaly=ScriptableObject.CreateInstance<LocalAnomalyData>();var manager=RunStateManager.EnsureExists();manager.BeginNewRun(null,null,stage,rule,anomaly);
 var player=new GameObject("Link renderer regression");var station=OrbitalStationRuntime.Ensure(player);Assert.That(station.IsInitialized,Is.True);station.enabled=false;
 var enemyObject=new GameObject("Link segment target");var enemy=enemyObject.AddComponent<EnemyHealth>();enemy.SetRuntimeMaxHealth(1000);
 try{
 Assert.That(station.InstallLinkPair(1,1,1,2,out _),Is.True);var pair=station.State.ResolveLinkPairs().Single();var first=station.Modules.Single(m=>m.StableModuleId==pair.First);var second=station.Modules.Single(m=>m.StableModuleId==pair.Second);
 enemy.transform.position=(first.WorldPosition+second.WorldPosition)*.5f;Tick(station);Assert.That(enemy.CurrentHealth,Is.EqualTo(995).Within(.001f));Assert.That((float)Get(first,"Cooldown"),Is.EqualTo(.55f));
 var line=Line(station);int instance=line.GetInstanceID();Assert.That(line.widthMultiplier,Is.EqualTo(.045f));Assert.That(line.startColor.r,Is.EqualTo(.7f).Within(1f/255));Assert.That(line.startColor.g,Is.EqualTo(.2f).Within(1f/255));Assert.That(line.startColor.b,Is.EqualTo(1f).Within(1f/255));Assert.That(line.startColor.a,Is.EqualTo(.35f).Within(1f/255));Assert.That(line.endColor,Is.EqualTo(line.startColor));Assert.That(line.sortingLayerName,Is.EqualTo("Player"));Assert.That(line.sortingOrder,Is.EqualTo(13));Assert.That(line.sharedMaterial,Is.Not.Null);
 for(int i=0;i<120;i++)Tick(station);Assert.That(Line(station).GetInstanceID(),Is.EqualTo(instance));Assert.That(enemy.CurrentHealth,Is.EqualTo(995).Within(.001f));
 Assert.That(station.UpgradeLinkMatrix(),Is.True);Set(first,"Cooldown",0f);Tick(station);Assert.That(enemy.CurrentHealth,Is.EqualTo(988.75f).Within(.001f));
 var ring=station.AddRing();Assert.That(station.MoveModule(pair.First,ring.StableRingId,0,out _),Is.True);Assert.That(station.State.FindLinkPartner(pair.First),Is.EqualTo(pair.Second));Tick(station);Assert.That(Line(station).GetInstanceID(),Is.EqualTo(instance));Assert.That((Vector2)line.GetPosition(0),Is.EqualTo((Vector2)first.CurrentMount.Transform.position));Assert.That((Vector2)line.GetPosition(1),Is.EqualTo((Vector2)second.CurrentMount.Transform.position));
 float hp=enemy.CurrentHealth;Time.timeScale=0;Tick(station,0);Assert.That(Line(station).GetInstanceID(),Is.EqualTo(instance));Assert.That(enemy.CurrentHealth,Is.EqualTo(hp));Time.timeScale=1;
 station.FlashLink(Vector2.zero,Vector2.one,Color.magenta,.1f);Assert.That(((IList)Get(station,"flashes")).Count,Is.EqualTo(1));station.GetType().GetMethod("UpdateFlashes",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(station,new object[]{.2f});Assert.That(((IList)Get(station,"flashes")).Count,Is.Zero);Assert.That(Line(station),Is.SameAs(line));
 Assert.That(station.RemoveModule(pair.First),Is.True);Tick(station);Assert.That(((IDictionary)Get(station,"linkLines")).Count,Is.Zero);yield return Frames();Assert.That(line==null,Is.True);
 Assert.That(station.InstallModule(OrbitalModuleKind.LinkNode,ring.StableRingId,1,out _),Is.True);Tick(station);var repaired=Line(station);Assert.That(station.State.ResolveLinkPairs().Count(),Is.EqualTo(1));Assert.That(repaired.GetInstanceID(),Is.Not.EqualTo(instance));
 Assert.That(station.RebuildRuntimeFromState(),Is.True);station.enabled=false;Tick(station);var restored=Line(station);Assert.That(restored,Is.Not.SameAs(repaired));station.Teardown();Assert.That(((IDictionary)Get(station,"linkLines")).Count,Is.Zero);yield return Frames();Assert.That(restored==null,Is.True);
 }finally{Time.timeScale=1;Object.Destroy(enemyObject);Object.Destroy(player);Object.Destroy(manager.gameObject);Object.Destroy(stage);Object.Destroy(rule);Object.Destroy(anomaly);}
 }
}
#endif
