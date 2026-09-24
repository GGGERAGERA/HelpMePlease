using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class LevelAnomalyView : MonoBehaviour
{
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private CanvasGroup cardGroup;
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private float descriptionDuration = 4f;
    private Coroutine cardRoutine;
    private bool built;
    [SerializeField] private TextMeshProUGUI header;
    private LevelMechanicPresentationData currentPresentation;
    private void OnEnable() => LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
    private void RefreshLanguage(GameLanguage language)
    {
        if (built) SetData(currentPresentation);
    }

    private void Awake()
    {
        rootGroup = GetComponent<CanvasGroup>();
        ConfigureRootGroup();
    }

    public void Prepare()
    {
        if (built)
            return;

        built = true;
        rootGroup ??= GetComponent<CanvasGroup>();
        ConfigureRootGroup();

    }

    public void ShowLocalAnomaly(
        LevelMechanicPresentationData presentation)
    {
        Prepare();
        StopCardRoutine();
        cardRoutine = StartCoroutine(ShowCardRoutine(presentation));
    }

    public void HideLocalAnomaly()
    {
        if (!built)
            return;

        StopCardRoutine();
        cardGroup.alpha = 0f;
        cardGroup.gameObject.SetActive(false);
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
    }

    private IEnumerator ShowCardRoutine(
        LevelMechanicPresentationData presentation)
    {
        SetData(presentation);
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = false;

        cardGroup.gameObject.SetActive(true);
        yield return Fade(cardGroup, 0f, 1f, 0.15f);
        yield return new WaitForSecondsRealtime(descriptionDuration);
        cardGroup.alpha = 0f;
        cardRoutine = null;
    }

    private void SetData(LevelMechanicPresentationData presentation)
    {
        currentPresentation = presentation;
        header.text = LocalizationService.EnsureExists().Get("hud.anomaly");
        nameText.text = GetCompactName(presentation.Title);
        descriptionText.text = string.IsNullOrWhiteSpace(
                presentation.PinnedDescription)
            ? presentation.Description
            : presentation.PinnedDescription;
    }

    private static string GetCompactName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        int separatorIndex = title.IndexOf(':');
        return separatorIndex >= 0 && separatorIndex < title.Length - 1
            ? title.Substring(separatorIndex + 1).Trim()
            : title.Trim();
    }

    private void ConfigureRootGroup()
    {
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
    }

    private void StopCardRoutine()
    {
        if (cardRoutine == null)
            return;

        StopCoroutine(cardRoutine);
        cardRoutine = null;
    }

    private static IEnumerator Fade(
        CanvasGroup group,
        float start,
        float target,
        float duration)
    {
        group.alpha = start;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(start, target, 1f - (1f - t) * (1f - t));
            yield return null;
        }

        group.alpha = target;
    }

    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
        HideLocalAnomaly();
    }
}
