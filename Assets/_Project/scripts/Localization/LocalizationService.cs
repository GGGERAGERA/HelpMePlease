using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalizationService : MonoBehaviour
{
    public const string LanguagePreferenceKey = "localization.language";

    private const string TableResourcePath =
        "Localization/LocalizationTable";

    public static LocalizationService Instance { get; private set; }

    public GameLanguage CurrentLanguage { get; private set; }

    public event Action<GameLanguage> LanguageChanged;

    private LocalizationTable table;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad
    )]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static LocalizationService EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject serviceObject =
            new GameObject(nameof(LocalizationService));
        return serviceObject.AddComponent<LocalizationService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        table = Resources.Load<LocalizationTable>(TableResourcePath);
        CurrentLanguage = LoadLanguage();

        if (table == null)
        {
            Debug.LogWarning(
                $"[LocalizationService] Missing Resources/" +
                $"{TableResourcePath}."
            );
        }
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
