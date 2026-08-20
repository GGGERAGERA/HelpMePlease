using System.Text;
using TMPro;
using UnityEngine;

public sealed class LevelMechanicsPanel : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TextMeshProUGUI contentText;

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
    }

    private void Update()
    {
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

        if (panelRect != null)
        {
            float height = Mathf.Clamp(contentText.preferredHeight + 24f, 48f, 400f);
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
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

        AppendHeader(ref hasSection, "WORLD RULES");
        textBuilder.Append("- World Acceleration - ")
            .Append(Mathf.CeilToInt(worldAccelerationRule.TimeRemaining))
            .AppendLine("s");
    }

    private void AppendChallenges(ref bool hasSection)
    {
        if (noDamageChallenge == null ||
            noDamageChallenge.State == NoDamageChallengeState.Inactive)
        {
            return;
        }

        AppendHeader(ref hasSection, "CHALLENGES");
        textBuilder.Append("- No Damage - ")
            .Append(noDamageChallenge.State);

        if (noDamageChallenge.State == NoDamageChallengeState.Active)
        {
            textBuilder.Append(" - ")
                .Append(Mathf.CeilToInt(noDamageChallenge.TimeRemaining))
                .Append('s');
        }

        textBuilder.AppendLine();
    }

    private void AppendDoubleOrLeave(ref bool hasSection)
    {
        if (doubleOrLeave == null || doubleOrLeave.State == DoubleOrLeaveState.Inactive)
            return;

        AppendHeader(ref hasSection, "TAKE OR RISK");
        textBuilder.Append("- ").Append(GetDoubleOrLeaveStateLabel());

        textBuilder.AppendLine();
    }

    private string GetDoubleOrLeaveStateLabel()
    {
        return doubleOrLeave.State switch
        {
            DoubleOrLeaveState.WaitingForChoice => "Waiting for Choice",
            DoubleOrLeaveState.WaitingForChallenge => "Risk Event Pending",
            DoubleOrLeaveState.RewardGranted => "Reward Ready",
            DoubleOrLeaveState.Failed => "Reward Lost",
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
