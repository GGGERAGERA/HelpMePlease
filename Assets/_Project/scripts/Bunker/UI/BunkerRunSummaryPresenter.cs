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

        string reasonText = summary.EndReason switch
        {
            RunEndReason.PlayerDied => "Забег завершён",
            RunEndReason.ReturnedToBunker => "Возвращение в бункер",
            _ => "Итоги забега"
        };

        string message =
            $"{reasonText}\n" +
            $"Уровней пройдено: {summary.CompletedLevels}\n" +
            $"Убийств: {summary.Kills}\n" +
            $"Получено золота: +{summary.GoldEarned}";

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
}