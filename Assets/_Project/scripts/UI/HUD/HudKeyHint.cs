using TMPro;
using UnityEngine;

public sealed class HudKeyHint : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string localizationKey;
    private void OnEnable()
    {
        LocalizationService.EnsureExists().LanguageChanged += Refresh;
        Refresh(default);
    }
    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= Refresh;
    }
    private void Refresh(GameLanguage _) => label.text = LocalizationService.Instance.Get(localizationKey);
}
