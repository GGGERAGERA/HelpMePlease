using System.Text;
using TMPro;
using UnityEngine;

public sealed class LevelMechanicsPanel : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private HudIconNumber accelerationView;
    [SerializeField] private HudIconNumber challengeView;
    [SerializeField] private float noticeDuration = 4f;
    private float noticeUntil;
    private bool hasContent;

    [Header("Mechanics")]
    [SerializeField] private WorldAccelerationRule worldAccelerationRule;
    [SerializeField] private NoDamageChallenge noDamageChallenge;

    private readonly StringBuilder textBuilder = new();
    private bool displayStateCaptured;
    private int previousWorldSeconds;
    private int previousChallengeState;
    private int previousChallengeSeconds;

    private void OnEnable()
    {
        displayStateCaptured = false;
        LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
    }

    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
    }
    private void RefreshLanguage(GameLanguage language) => RefreshContent();

    private void Update()
    {
        if (CaptureDisplayState())
            RefreshContent();

        bool visible = hasContent && Time.unscaledTime < noticeUntil;
        if (panelRoot.activeSelf != visible)
            panelRoot.SetActive(visible);
    }

    private void RefreshContent()
    {
        hasContent = BuildContent();

        if (!hasContent || contentText == null)
            return;

        string content = textBuilder.ToString();

        if (contentText.text != content)
            contentText.text = content;

        accelerationView.gameObject.SetActive(previousWorldSeconds >= 0);
        accelerationView.SetValue(Mathf.Max(0,previousWorldSeconds));
        challengeView.gameObject.SetActive(previousChallengeState >= 0);
        challengeView.SetValue(Mathf.Max(0,previousChallengeSeconds));
    }

    private bool CaptureDisplayState()
    {
        int worldSeconds =
            worldAccelerationRule != null && worldAccelerationRule.IsRunning
                ? Mathf.CeilToInt(worldAccelerationRule.TimeRemaining)
                : -1;
        int challengeState =
            noDamageChallenge != null &&
            noDamageChallenge.State != NoDamageChallengeState.Inactive
                ? (int)noDamageChallenge.State
                : -1;
        int challengeSeconds =
            noDamageChallenge != null &&
            noDamageChallenge.State == NoDamageChallengeState.Active
                ? Mathf.CeilToInt(noDamageChallenge.TimeRemaining)
                : -1;
        bool changed = !displayStateCaptured ||
            previousWorldSeconds != worldSeconds ||
            previousChallengeState != challengeState ||
            previousChallengeSeconds != challengeSeconds;

        if (!displayStateCaptured || previousChallengeState != challengeState ||
            (previousWorldSeconds < 0) != (worldSeconds < 0))
            noticeUntil = Time.unscaledTime + noticeDuration;
        displayStateCaptured = true;
        previousWorldSeconds = worldSeconds;
        previousChallengeState = challengeState;
        previousChallengeSeconds = challengeSeconds;
        return changed;
    }

    private bool BuildContent()
    {
        textBuilder.Clear();
        bool hasSection = false;

        AppendWorldRules(ref hasSection);
        AppendChallenges(ref hasSection);

        return hasSection;
    }

    private void AppendWorldRules(ref bool hasSection)
    {
        if (worldAccelerationRule == null || !worldAccelerationRule.IsRunning)
            return;

        AppendHeader(ref hasSection, LocalizationService.EnsureExists().Get("mechanic.rules"));
        textBuilder.Append(LocalizationService.EnsureExists().Get("mechanic.acceleration"))
            .Append(Mathf.CeilToInt(worldAccelerationRule.TimeRemaining))
            .AppendLine(LocalizationService.EnsureExists().Get("mechanic.seconds"));
    }

    private void AppendChallenges(ref bool hasSection)
    {
        if (noDamageChallenge == null ||
            noDamageChallenge.State == NoDamageChallengeState.Inactive)
        {
            return;
        }

        AppendHeader(ref hasSection, LocalizationService.EnsureExists().Get("mechanic.challenges"));
        textBuilder.Append(LocalizationService.EnsureExists().Get("mechanic.noDamage"))
            .Append(LocalizationService.EnsureExists().Get("mechanic.challenge." + noDamageChallenge.State));

        if (noDamageChallenge.State == NoDamageChallengeState.Active)
        {
            textBuilder.Append(" - ")
                .Append(Mathf.CeilToInt(noDamageChallenge.TimeRemaining))
                .Append(LocalizationService.EnsureExists().Get("mechanic.seconds"));
        }

        textBuilder.AppendLine();
    }

    private void AppendHeader(ref bool hasSection, string title)
    {
        if (hasSection)
            textBuilder.AppendLine();

        textBuilder.Append("<b>").Append(title).AppendLine("</b>");
        hasSection = true;
    }

}
