using System.Collections.Generic;
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
    [SerializeField] private WorldEventSpawner worldEventSpawner;
    [SerializeField] private WorldAccelerationRule worldAccelerationRule;
    [SerializeField] private NoDamageChallenge noDamageChallenge;
    [SerializeField] private DoubleOrLeave doubleOrLeave;

    private readonly StringBuilder textBuilder = new();

    private void Update()
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

    private bool BuildContent()
    {
        textBuilder.Clear();
        bool hasSection = false;

        AppendWorldEvents(ref hasSection);
        AppendWorldRules(ref hasSection);
        AppendChallenges(ref hasSection);
        AppendDoubleOrLeave(ref hasSection);

        return hasSection;
    }

    private void AppendWorldEvents(ref bool hasSection)
    {
        if (worldEventSpawner == null)
            return;

        IReadOnlyList<WorldEvent> activeEvents = worldEventSpawner.ActiveEvents;
        bool headerAdded = false;

        for (int i = 0; i < activeEvents.Count; i++)
        {
            WorldEvent worldEvent = activeEvents[i];

            if (worldEvent == null)
                continue;

            if (!headerAdded)
            {
                AppendHeader(ref hasSection, "WORLD EVENTS");
                headerAdded = true;
            }

            if (worldEvent is CaptureZoneEvent capture)
            {
                string state = capture.IsPlayerInside ? "Active" : "Waiting";
                AppendTimedLine(
                    "Hold Point",
                    state,
                    capture.TimeRemaining,
                    capture.Progress
                );
            }
            else if (worldEvent is RescueCapsuleEvent rescue)
            {
                string state = rescue.IsActivated ? "Active" : "Waiting";
                AppendTimedLine(
                    "Rescue Capsule",
                    state,
                    rescue.TimeRemaining,
                    rescue.Progress
                );
            }
            else
            {
                textBuilder.Append("- ")
                    .Append(worldEvent.GetType().Name)
                    .AppendLine(" - Active");
            }
        }
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

        AppendHeader(ref hasSection, "DOUBLE OR LEAVE");
        textBuilder.Append("- ").Append(GetDoubleOrLeaveStateLabel());

        if (doubleOrLeave.State == DoubleOrLeaveState.RewardGranted)
        {
            textBuilder.Append(" - ")
                .Append(doubleOrLeave.LastGrantedRewardAmount);
        }

        textBuilder.AppendLine();
    }

    private string GetDoubleOrLeaveStateLabel()
    {
        return doubleOrLeave.State switch
        {
            DoubleOrLeaveState.WaitingForChoice => "Waiting for Choice",
            DoubleOrLeaveState.WaitingForChallenge => "Waiting for Challenge",
            DoubleOrLeaveState.RewardGranted => "Reward Granted",
            DoubleOrLeaveState.Failed => "Failed",
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

    private void AppendTimedLine(
        string label,
        string state,
        float timeRemaining,
        float progress)
    {
        textBuilder.Append("- ")
            .Append(label)
            .Append(" - ")
            .Append(state)
            .Append(" - ")
            .Append(Mathf.CeilToInt(timeRemaining))
            .Append("s / ")
            .Append(Mathf.RoundToInt(progress * 100f))
            .AppendLine("%");
    }
}
