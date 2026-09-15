using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalizedText : MonoBehaviour
{
    private static readonly HashSet<string> WarnedKeys = new();

    [SerializeField] private string localizationKey;

    private TMP_Text targetText;
    private LocalizationService service;

    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        service = LocalizationService.EnsureExists();
        service.LanguageChanged += HandleLanguageChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (service != null)
            service.LanguageChanged -= HandleLanguageChanged;

        service = null;
    }

    public void SetKey(string key)
    {
        localizationKey = key;
        Refresh();
    }

    public void Refresh()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (service == null)
            service = LocalizationService.EnsureExists();

        if (targetText == null || service == null)
            return;

        targetText.text = service.Get(localizationKey);

        if (!service.HasKey(localizationKey) &&
            WarnedKeys.Add(localizationKey))
        {
            Debug.LogWarning(
                $"[LocalizedText] Unknown key '{localizationKey}'.",
                this
            );
        }
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        Refresh();
    }
}
