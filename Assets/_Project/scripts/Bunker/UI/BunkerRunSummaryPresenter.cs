using System.Collections;
using UnityEngine;

public sealed class BunkerRunSummaryPresenter : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float showDelay = 0.25f;

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(showDelay);

        if (RunStateManager.Instance == null)
            yield break;

        if (!RunStateManager.Instance.TryConsumeLastRunSummary(
                out RunSummary summary))
        {
            yield break;
        }

        if (summary.EndReason == RunEndReason.Victory)
            RunStateManager.Instance.ClearFinishedRunCompatibilityState();

        string reasonText = summary.EndReason switch
        {
            RunEndReason.Victory => "\u0417\u0410\u0411\u0415\u0413 \u0417\u0410\u0412\u0415\u0420\u0428\u0401\u041d",
            RunEndReason.PlayerDied => "\u0417\u0430\u0431\u0435\u0433 \u0437\u0430\u0432\u0435\u0440\u0448\u0451\u043d",
            RunEndReason.ReturnedToBunker => "\u0412\u043e\u0437\u0432\u0440\u0430\u0449\u0435\u043d\u0438\u0435 \u0432 \u0431\u0443\u043d\u043a\u0435\u0440",
            _ => "\u0418\u0442\u043e\u0433\u0438 \u0437\u0430\u0431\u0435\u0433\u0430"
        };

        string progressLabel = summary.EndReason == RunEndReason.Victory
            ? "\u0421\u0435\u043a\u0442\u043e\u0440\u043e\u0432 \u043f\u0440\u043e\u0439\u0434\u0435\u043d\u043e"
            : "\u0423\u0440\u043e\u0432\u043d\u0435\u0439 \u043f\u0440\u043e\u0439\u0434\u0435\u043d\u043e";
        string message =
            $"{reasonText}\n" +
            $"{progressLabel}: {summary.CompletedLevels}\n" +
            $"\u0423\u0431\u0438\u0439\u0441\u0442\u0432: {summary.Kills}\n" +
            $"\u0412\u0440\u0435\u043c\u044f: {FormatTime(summary.RunTime)}\n" +
            $"\u041f\u043e\u043b\u0443\u0447\u0435\u043d\u043e \u0437\u043e\u043b\u043e\u0442\u0430: +{summary.GoldEarned}";

        BunkerNotificationManager notifications =
            BunkerContext.Instance != null
                ? BunkerContext.Instance.Notifications
                : null;

        if (notifications != null)
        {
            notifications.ShowSuccess(message);
        }
        else
        {
            Debug.LogWarning(
                "[BunkerRunSummaryPresenter] " +
                "BunkerNotificationManager is missing."
            );

            Debug.Log(message);
        }
    }

    private static string FormatTime(float secondsTotal)
    {
        int seconds = Mathf.Max(0, Mathf.FloorToInt(secondsTotal));
        int minutes = seconds / 60;
        return $"{minutes:00}:{seconds % 60:00}";
    }
}
