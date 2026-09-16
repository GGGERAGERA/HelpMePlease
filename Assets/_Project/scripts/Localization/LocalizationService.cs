using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalizationService : MonoBehaviour
{
    public const string LanguagePreferenceKey = "localization.language";

    public static LocalizationService Instance { get; private set; }

    public GameLanguage CurrentLanguage { get; private set; }

    public event Action<GameLanguage> LanguageChanged;

    [SerializeField] private LocalizationTable table;

    public static LocalizationService EnsureExists()
    {
        if (Instance != null)
            return Instance;

        throw new InvalidOperationException("LocalizationService is missing. Add the configured service to the scene composition before it is used.");
    }

    private void Awake() => InitializeAuthored();

    public void InitializeAuthored()
    {
        if (Instance == this)
            return;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (table == null)
            throw new InvalidOperationException("LocalizationService requires an assigned table in the scene composition.");

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentLanguage = LoadLanguage();
    }

    public string Get(string key)
    {
        if (table != null &&
            table.TryGet(key, CurrentLanguage, out string value))
        {
            return value;
        }

        return key;
    }

    public bool HasKey(string key)
    {
        return table != null && table.ContainsKey(key);
    }

    public void SetLanguage(GameLanguage language)
    {
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        PlayerPrefs.SetInt(LanguagePreferenceKey, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke(CurrentLanguage);
    }

    private static GameLanguage LoadLanguage()
    {
        if (PlayerPrefs.HasKey(LanguagePreferenceKey))
        {
            int storedValue = PlayerPrefs.GetInt(
                LanguagePreferenceKey,
                (int)GameLanguage.English
            );

            if (Enum.IsDefined(typeof(GameLanguage), storedValue))
                return (GameLanguage)storedValue;
        }

        return Application.systemLanguage == SystemLanguage.Russian
            ? GameLanguage.Russian
            : GameLanguage.English;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
