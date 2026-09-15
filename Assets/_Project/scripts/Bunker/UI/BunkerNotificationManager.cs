using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerNotificationManager : MonoBehaviour
{
    public static BunkerNotificationManager Instance { get; private set; }

    [Header("View")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panel;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Timing")]
    [SerializeField] private float showDuration = 2f;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float slideOffsetY = 40f;

    [Header("Colors")]
    [SerializeField] private Color infoColor = new(0.15f, 0.2f, 0.28f, 0.95f);
    [SerializeField] private Color successColor = new(0.1f, 0.35f, 0.18f, 0.95f);
    [SerializeField] private Color warningColor = new(0.55f, 0.38f, 0.08f, 0.95f);
    [SerializeField] private Color errorColor = new(0.45f, 0.08f, 0.08f, 0.95f);

    private string displayedMessageKey;
    private void OnEnable() => LocalizationService.Instance.LanguageChanged += HandleLanguageChanged;
    private void OnDisable()
    {
        if (LocalizationService.Instance != null)
            LocalizationService.Instance.LanguageChanged -= HandleLanguageChanged;
    }
    private void HandleLanguageChanged(GameLanguage language)
    {
        if (!string.IsNullOrEmpty(displayedMessageKey))
            messageText.text = LocalizationService.Instance.Get(displayedMessageKey);
    }

    private Coroutine routine;
    private Vector2 basePosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panel != null)
            basePosition = panel.anchoredPosition;

        HideImmediate();
    }

    public void ShowInfo(string messageKey) => Show(messageKey, BunkerNotificationType.Info);
    public void ShowSuccess(string messageKey) => Show(messageKey, BunkerNotificationType.Success);
    public void ShowWarning(string messageKey) => Show(messageKey, BunkerNotificationType.Warning);
    public void ShowError(string messageKey) => Show(messageKey, BunkerNotificationType.Error);

    public void Show(string messageKey, BunkerNotificationType type = BunkerNotificationType.Info)
    {
        if (string.IsNullOrWhiteSpace(messageKey))
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(ShowRoutine(messageKey, type));
    }

    private IEnumerator ShowRoutine(string messageKey, BunkerNotificationType type)
    {
        if (root == null || canvasGroup == null || panel == null || messageText == null)
            yield break;

        root.SetActive(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        displayedMessageKey = messageKey;
        messageText.text = LocalizationService.Instance.Get(messageKey);

        if (backgroundImage != null)
            backgroundImage.color = GetColor(type);

        canvasGroup.alpha = 0f;
        panel.anchoredPosition = basePosition + Vector2.up * slideOffsetY;

        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / fadeDuration);
            t = Smooth(t);

            canvasGroup.alpha = t;
            panel.anchoredPosition = Vector2.Lerp(
                basePosition + Vector2.up * slideOffsetY,
                basePosition,
                t
            );

            yield return null;
        }

        canvasGroup.alpha = 1f;
        panel.anchoredPosition = basePosition;

        yield return new WaitForSecondsRealtime(showDuration);

        time = 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / fadeDuration);
            t = Smooth(t);

            canvasGroup.alpha = 1f - t;
            panel.anchoredPosition = Vector2.Lerp(
                basePosition,
                basePosition + Vector2.up * slideOffsetY,
                t
            );

            yield return null;
        }

        HideImmediate();
        routine = null;
    }

    private void HideImmediate()
    {
        displayedMessageKey = null;
        if (root != null)
            root.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (panel != null)
            panel.anchoredPosition = basePosition + Vector2.up * slideOffsetY;
    }

    private Color GetColor(BunkerNotificationType type)
    {
        return type switch
        {
            BunkerNotificationType.Success => successColor,
            BunkerNotificationType.Warning => warningColor,
            BunkerNotificationType.Error => errorColor,
            _ => infoColor
        };
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
