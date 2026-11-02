using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
#endif

/// <summary>
/// Project-wide storage boundary. The global name intentionally covers existing
/// PlayerPrefs consumers, including dynamically named reward receipts. Release
/// builds forward directly to Unity. Never bypass this boundary in gameplay code.
/// </summary>
public static class PlayerPrefs
{
    public static int GetInt(string key, int defaultValue = 0)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) return Read(key, 1, out var entry) ? entry.integer :
            IsShadowed(key) ? defaultValue : UnityEngine.PlayerPrefs.GetInt(key, defaultValue);
#endif
        return UnityEngine.PlayerPrefs.GetInt(key, defaultValue);
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) return Read(key, 2, out var entry) ? entry.number :
            IsShadowed(key) ? defaultValue : UnityEngine.PlayerPrefs.GetFloat(key, defaultValue);
#endif
        return UnityEngine.PlayerPrefs.GetFloat(key, defaultValue);
    }

    public static string GetString(string key, string defaultValue = "")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) return Read(key, 3, out var entry) ? entry.text :
            IsShadowed(key) ? defaultValue : UnityEngine.PlayerPrefs.GetString(key, defaultValue);
#endif
        return UnityEngine.PlayerPrefs.GetString(key, defaultValue);
    }

    public static bool HasKey(string key)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null)
        {
            var value = Find(key);
            return value != null ? value.kind != 0 : !Sandbox.cleared && UnityEngine.PlayerPrefs.HasKey(key);
        }
#endif
        return UnityEngine.PlayerPrefs.HasKey(key);
    }

    public static void SetInt(string key, int value)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) { Put(new Value { key = key, kind = 1, integer = value }); return; }
#endif
        UnityEngine.PlayerPrefs.SetInt(key, value);
    }
    public static void SetFloat(string key, float value)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) { Put(new Value { key = key, kind = 2, number = value }); return; }
#endif
        UnityEngine.PlayerPrefs.SetFloat(key, value);
    }
    public static void SetString(string key, string value)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) { Put(new Value { key = key, kind = 3, text = value }); return; }
#endif
        UnityEngine.PlayerPrefs.SetString(key, value);
    }
    public static void DeleteKey(string key)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) { Put(new Value { key = key }); return; }
#endif
        UnityEngine.PlayerPrefs.DeleteKey(key);
    }
    public static void DeleteAll()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) { Sandbox.values.Clear(); Sandbox.cleared = true; Persist(); return; }
#endif
        UnityEngine.PlayerPrefs.DeleteAll();
    }
    public static void Save()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Sandbox != null) { Persist(); return; }
#endif
        UnityEngine.PlayerPrefs.Save();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Serializable] private sealed class Value
    { public string key, text; public int kind, integer; public float number; }
    [Serializable] private sealed class Profile
    { public bool fresh, cleared; public List<Value> values = new(); }
    private static Profile sandbox;
    private const string SessionKey = "Subject42.BunkerDebug.IsolatedPreferences";
    private static Profile Sandbox
    {
        get
        {
#if UNITY_EDITOR
            // SessionState survives script/domain reloads, but never writes the player profile.
            // A malformed journal throws before a read/write can fall back to native storage.
            if (sandbox == null)
            {
                string json = UnityEditor.SessionState.GetString(SessionKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    sandbox = JsonUtility.FromJson<Profile>(json);
                    if (sandbox?.values == null) throw new InvalidOperationException("Invalid developer save journal. Stop Play Mode to discard it safely.");
                }
            }
#endif
            return sandbox;
        }
    }
    public static bool IsSandboxActive => Sandbox != null;
    public static bool IsFreshSandbox => Sandbox != null && Sandbox.fresh;
    public static int SandboxChangedKeys => Sandbox?.values.Count ?? 0;

    public static void BeginSandbox(bool fresh)
    {
        if (IsSandboxActive) throw new InvalidOperationException("A test profile is already active.");
        UnityEngine.PlayerPrefs.Save();
        sandbox = new Profile { fresh = fresh, cleared = fresh };
        Persist();
    }
    public static void ResetSandbox()
    {
        if (!IsSandboxActive) throw new InvalidOperationException("No test profile is active.");
        sandbox = new Profile { fresh = Sandbox.fresh, cleared = Sandbox.fresh };
        Persist();
    }
    public static void EndSandbox()
    {
        sandbox = null;
#if UNITY_EDITOR
        UnityEditor.SessionState.EraseString(SessionKey);
#endif
    }
    private static Value Find(string key) => Sandbox.values.Find(v => v.key == key);
    private static bool Read(string key, int kind, out Value value)
    { value = Find(key); return value != null && value.kind == kind; }
    private static bool IsShadowed(string key) => Sandbox.cleared || Find(key) != null;
    private static void Put(Value value)
    {
        Sandbox.values.RemoveAll(v => v.key == value.key);
        Sandbox.values.Add(value);
        Persist();
    }
    private static void Persist()
    {
#if UNITY_EDITOR
        UnityEditor.SessionState.SetString(SessionKey, JsonUtility.ToJson(sandbox));
#endif
    }
#if !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void StartApplication() => sandbox = null;
#endif
#endif
}
