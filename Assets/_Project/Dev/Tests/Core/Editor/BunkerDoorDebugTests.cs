#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BunkerDoorDebugTests
{
    private GameObject root;
    private GameObject opened;
    private GameObject closed;
    private BunkerGateVisual gate;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Door debug test");
        root.SetActive(false);
        opened = new GameObject("Open");
        opened.transform.SetParent(root.transform);
        closed = new GameObject("Closed");
        closed.transform.SetParent(root.transform);
        closed.AddComponent<BoxCollider2D>();
        gate = root.AddComponent<BunkerGateVisual>();
        Set("openedDoor", opened);
        Set("closedDoor", closed);
        Set("openOnPlayerProximity", true);
        Call("Awake");
        root.SetActive(true);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void ForcedOpenAndClosedImmediatelySwitchVisualsAndSolidCollider()
    {
        gate.DebugSetOpen(true);
        Call("FixedUpdate");
        Assert.That(gate.IsOpen, Is.True, "Proximity must not close a forced-open door");
        Assert.That(opened.activeSelf, Is.True);
        Assert.That(closed.activeSelf, Is.False);
        Assert.That(closed.GetComponent<Collider2D>().isActiveAndEnabled, Is.False);

        var player = new GameObject("Near player");
        player.transform.SetParent(root.transform);
        var contact = player.AddComponent<BoxCollider2D>();
        Set("closedDoorBounds", new Bounds(Vector3.zero, Vector3.one));
        Set("hasDoorBounds", true);
        ((HashSet<Collider2D>)Get("playerContacts")).Add(contact);
        gate.DebugSetOpen(false);
        Call("FixedUpdate");
        Assert.That(gate.IsOpen, Is.False, "Proximity must not open a forced-closed door");
        Assert.That(closed.GetComponent<Collider2D>().isActiveAndEnabled, Is.True);
        gate.DebugResetOverride();
        Call("FixedUpdate");
        Assert.That(gate.IsOpen, Is.True, "Reset resumes normal proximity behavior");
        gate.DebugSetLocked(true);
        Call("FixedUpdate");
        Assert.That(gate.IsOpen, Is.False, "Lock must block a nearby player");
        gate.DebugSetLocked(false);
        Call("FixedUpdate");
        Assert.That(gate.IsOpen, Is.True, "Unlock resumes proximity when no manual open/close remains");
    }

    [Test]
    public void LockBlocksProximityAndResetUsesLatestExternalState()
    {
        gate.SetOpen(true);
        gate.DebugSetLocked(true);
        Assert.That(gate.DebugIsLocked, Is.True);
        Assert.That(gate.IsOpen, Is.False);
        gate.SetOpen(false);
        gate.SetOpen(true);
        Call("FixedUpdate");
        Assert.That(gate.IsOpen, Is.False);
        gate.DebugResetOverride();
        Assert.That(gate.DebugHasOverride, Is.False);
        Assert.That(gate.IsOpen, Is.True, "Restore the latest story request, not a snapshot");
        Call("FixedUpdate");
        Assert.That(gate.IsOpen, Is.False, "Normal proximity resumes after reset");
    }

    [Test]
    public void UnlockRestoresManualStateWithoutChangingNormalStoryState()
    {
        gate.SetOpen(false);
        gate.DebugSetOpen(true);
        gate.DebugSetLocked(true);
        gate.DebugSetLocked(false);
        Assert.That(gate.IsOpen, Is.True);
        gate.DebugResetOverride();
        Assert.That(gate.IsOpen, Is.False);
    }

    private object Get(string name) => typeof(BunkerGateVisual).GetField(name, Private).GetValue(gate);
    private void Set(string name, object value) => typeof(BunkerGateVisual).GetField(name, Private).SetValue(gate, value);
    private void Call(string name) => typeof(BunkerGateVisual).GetMethod(name, Private).Invoke(gate, null);
}
#endif
