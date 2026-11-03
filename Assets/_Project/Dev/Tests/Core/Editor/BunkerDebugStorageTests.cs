#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The native keys below are disposable fixture keys, never player balances.
public sealed class BunkerDebugStorageTests
{
    private const string Key = "Subject42.BunkerDebug.StorageFixture";
    private Type facade;
    private bool ownsFixture;
    private void Call(string method, params object[] args) => facade.GetMethod(method).Invoke(null, args);

    [SetUp]
    public void Setup()
    {
        ownsFixture = false;
        facade = typeof(CurrencyManager).Assembly.GetType("PlayerPrefs");
        Assert.That(facade, Is.Not.Null, "Developer storage boundary must exist before debug can mutate progression.");
        Call("EndSandbox");
        Assert.That(UnityEngine.PlayerPrefs.HasKey(Key), Is.False, "Fixture must not overwrite existing data.");
        ownsFixture = true;
    }

    [TearDown]
    public void Cleanup()
    {
        if (facade != null) Call("EndSandbox");
        if (ownsFixture) UnityEngine.PlayerPrefs.DeleteKey(Key);
        UnityEngine.PlayerPrefs.Save();
    }

    [Test]
    public void CopySupportsWritesAndDeletionWithoutTouchingNativeProgress()
    {
        UnityEngine.PlayerPrefs.SetInt(Key, 137);
        Call("BeginSandbox", false);
        Assert.That(PlayerPrefs.GetInt(Key), Is.EqualTo(137));
        PlayerPrefs.SetInt(Key, 25);
        PlayerPrefs.SetString(Key + ".receipt", "paid");
        PlayerPrefs.Save();
        Assert.That(PlayerPrefs.GetInt(Key), Is.EqualTo(25));
        Assert.That(UnityEngine.PlayerPrefs.GetInt(Key), Is.EqualTo(137));
        Assert.That(UnityEngine.PlayerPrefs.HasKey(Key + ".receipt"), Is.False);
        PlayerPrefs.DeleteKey(Key);
        Assert.That(PlayerPrefs.HasKey(Key), Is.False);
        Assert.That(PlayerPrefs.GetInt(Key, 9), Is.EqualTo(9));
        Call("EndSandbox");
        Assert.That(PlayerPrefs.GetInt(Key), Is.EqualTo(137));
        Assert.That(PlayerPrefs.HasKey(Key + ".receipt"), Is.False);
    }

    [Test]
    public void ResetRestoresCopyAndFreshProfileCannotDeleteNativeData()
    {
        UnityEngine.PlayerPrefs.SetInt(Key, 137);
        Call("BeginSandbox", false);
        PlayerPrefs.SetFloat(Key, 2.5f);
        Assert.That(PlayerPrefs.GetFloat(Key), Is.EqualTo(2.5f));
        Call("ResetSandbox");
        Assert.That(PlayerPrefs.GetInt(Key), Is.EqualTo(137));
        PlayerPrefs.DeleteAll();
        Assert.That(PlayerPrefs.HasKey(Key), Is.False);
        PlayerPrefs.Save();
        Assert.That(UnityEngine.PlayerPrefs.GetInt(Key), Is.EqualTo(137));
        Call("EndSandbox");
        Call("BeginSandbox", true);
        Assert.That(PlayerPrefs.GetInt(Key, 4), Is.EqualTo(4));
        Assert.That(UnityEngine.PlayerPrefs.GetInt(Key), Is.EqualTo(137));
    }
}
#endif
