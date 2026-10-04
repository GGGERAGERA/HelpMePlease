#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
public sealed class BunkerInteractionFixTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    static T Read<T>(Object o,string f) => (T)o.GetType().GetField(f,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    [UnityTest] public IEnumerator SlotPanelHubSpeedAndNearDoors()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode(); yield return Exercise();
    }
    static IEnumerator Exercise()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName && !SceneTransitionOverlay.IsTransitioning && Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady:true });
        var station = Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None).Single(s => Read<BunkerStationType>(s,"stationType") == BunkerStationType.OrbitalSlot);
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>(); var slot = Read<BunkerOrbitalSlotPanel>(panels,"orbitalSlotPanel");
        station.Interact(); yield return null;
        Assert.That(slot.IsOpen,Is.True); Assert.That(panels.IsAnyPanelOpen,Is.True);
        Read<Button>(slot,"closeButton").onClick.Invoke(); Assert.That(slot.IsOpen,Is.False);
        var loadout = Object.FindFirstObjectByType<BunkerPlayerLoadoutController>();
        var player = Read<GameObject>(loadout,"player"); var movement = player.GetComponent<CharacterMovement2D>();
        float authored = (float)typeof(CharacterMovement2D).GetProperty("AuthoredMoveSpeed",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(movement);
        Assert.That(movement.speed,Is.EqualTo(PlayerLoadoutFactory.CalculateFinalMoveSpeed(null,authored)).Within(.001f));
        Assert.That(authored,Is.GreaterThan(7f));
        var character = RunSelectionManager.Instance.SelectedCharacter;
        PlayerLoadoutFactory.ApplyCharacterStats(player,character);
        Assert.That(movement.speed,Is.EqualTo(PlayerLoadoutFactory.CalculateFinalMoveSpeed(character)).Within(.001f),"Run factory must retain character speed");
        typeof(BunkerPlayerLoadoutController).GetField("activeCharacter",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(loadout,null);
        typeof(BunkerPlayerLoadoutController).GetMethod("ApplyCharacter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(loadout,new object[]{character});
        Assert.That(movement.speed,Is.EqualTo(PlayerLoadoutFactory.CalculateFinalMoveSpeed(null,authored)).Within(.001f));
        foreach (var orientation in new[]{"H","V"})
        {
            var root = new GameObject("Door verification"); root.SetActive(false); root.transform.position = new Vector3(1000,1000);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Bunker/PF_BunkerDoor_"+orientation+".prefab");
            var door = Object.Instantiate(prefab,root.transform).GetComponent<BunkerGateVisual>(); root.SetActive(true); yield return new WaitForFixedUpdate();
            Assert.That(Read<bool>(door,"hasDoorBounds"),Is.True);
            var bounds = Read<Bounds>(door,"closedDoorBounds");
            var dummy = new GameObject("Door test player"); dummy.tag="Player"; dummy.layer=player.layer;
            var rb = dummy.AddComponent<Rigidbody2D>(); rb.gravityScale=0; rb.constraints=RigidbodyConstraints2D.FreezeAll;
            dummy.AddComponent<BoxCollider2D>().size=Vector2.one*.6f;
            System.Action<float> place = gap => { dummy.transform.position = orientation=="H" ? new Vector3(bounds.center.x,bounds.max.y+.3f+gap) : new Vector3(bounds.max.x+.3f+gap,bounds.center.y); Physics2D.SyncTransforms(); };
            place(1f); for(int i=0;i<3;i++) yield return new WaitForFixedUpdate(); Assert.That(door.IsOpen,Is.False,orientation+" distant");
            place(.2f); for(int i=0;i<3;i++) yield return new WaitForFixedUpdate(); Assert.That(door.IsOpen,Is.True,orientation+" near contacts="+Read<System.Collections.Generic.HashSet<Collider2D>>(door,"playerContacts").Count+" door="+bounds+" body="+dummy.GetComponent<Collider2D>().bounds);
            place(1f); for(int i=0;i<3;i++) yield return new WaitForFixedUpdate(); Assert.That(door.IsOpen,Is.False,orientation+" leave");
            Object.Destroy(dummy); Object.Destroy(root);
        }
    }
}
#endif


