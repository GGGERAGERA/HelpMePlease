using System.Text;
using TMPro;
using UnityEngine;

public sealed class LevelMechanicsPanel : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private HudIconNumber accelerationView;
    [SerializeField] private HudIconNumber challengeView;
    [SerializeField] private GameObject riskIcon;
    [SerializeField] private float noticeDuration = 4f;
    private float noticeUntil;

    [Header("Mechanics")]
    [SerializeField] private WorldAccelerationRule worldAccelerationRule;
    [SerializeField] private NoDamageChallenge noDamageChallenge;
    [SerializeField] private DoubleOrLeave doubleOrLeave;

    private readonly StringBuilder textBuilder = new();
    private bool displayStateCaptured;
    private int previousWorldSeconds;
    private int previousChallengeState;
    private int previousChallengeSeconds;
    private int previousDoubleOrLeaveState;

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
        contentText.gameObject.SetActive(Time.unscaledTime < noticeUntil);
        if (!CaptureDisplayState())
            return;

        RefreshContent();
    }

    private void RefreshContent()
    {
        bool hasContent = BuildContent();

        if (panelRoot != null && panelRoot.activeSelf != hasContent)
            panelRoot.SetActive(hasContent);

        if (!hasContent || contentText == null)
            return;

        string content = textBuilder.ToString();

        if (contentText.text != content)
            contentText.text = content;

        accelerationView.gameObject.SetActive(previousWorldSeconds >= 0);
        accelerationView.SetValue(Mathf.Max(0,previousWorldSeconds));
        challengeView.gameObject.SetActive(previousChallengeState >= 0);
        challengeView.SetValue(Mathf.Max(0,previousChallengeSeconds));
        riskIcon.SetActive(previousDoubleOrLeaveState >= 0);
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
        int doubleOrLeaveState =
            doubleOrLeave != null &&
            doubleOrLeave.State != DoubleOrLeaveState.Inactive
                ? (int)doubleOrLeave.State
                : -1;

        bool changed = !displayStateCaptured ||
            previousWorldSeconds != worldSeconds ||
            previousChallengeState != challengeState ||
            previousChallengeSeconds != challengeSeconds ||
            previousDoubleOrLeaveState != doubleOrLeaveState;

        if (!displayStateCaptured || previousChallengeState != challengeState ||
            previousDoubleOrLeaveState != doubleOrLeaveState || (previousWorldSeconds < 0) != (worldSeconds < 0))
            noticeUntil = Time.unscaledTime + noticeDuration;
        displayStateCaptured = true;
        previousWorldSeconds = worldSeconds;
        previousChallengeState = challengeState;
        previousChallengeSeconds = challengeSeconds;
        previousDoubleOrLeaveState = doubleOrLeaveState;
        return changed;
    }

    private bool BuildContent()
    {
        textBuilder.Clear();
        bool hasSection = false;

        AppendWorldRules(ref hasSection);
        AppendChallenges(ref hasSection);
        AppendDoubleOrLeave(ref hasSection);

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

    private void AppendDoubleOrLeave(ref bool hasSection)
    {
        if (doubleOrLeave == null || doubleOrLeave.State == DoubleOrLeaveState.Inactive)
            return;

        AppendHeader(ref hasSection, LocalizationService.EnsureExists().Get("mechanic.risk"));
        textBuilder.Append("- ").Append(GetDoubleOrLeaveStateLabel());

        textBuilder.AppendLine();
    }

    private string GetDoubleOrLeaveStateLabel()
    {
        return doubleOrLeave.State switch
        {
            DoubleOrLeaveState.WaitingForChallenge => LocalizationService.EnsureExists().Get("mechanic.pending"),
            DoubleOrLeaveState.RewardGranted => LocalizationService.EnsureExists().Get("mechanic.ready"),
            DoubleOrLeaveState.Failed => LocalizationService.EnsureExists().Get("mechanic.lost"),
            _ => string.Empty
        };
    }

    private void AppendHeader(ref bool hasSection, string title)
    {
        if (hasSection)
            textBuilder.AppendLine();

        textBuilder.Append("<b>").Append(title).AppendLine("</b>");
        hasSection = true;
    }

}
