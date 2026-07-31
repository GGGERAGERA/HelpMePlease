using UnityEngine;

public sealed class LocalAnomalyVisual : MonoBehaviour
{
    [SerializeField] private LevelAnomalyView view;

    private LocalAnomalyData activeAnomaly;

    public void Apply(LocalAnomalyData anomaly)
    {
        Clear();
        activeAnomaly = anomaly;
    }

    public void Show(LocalAnomalyData anomaly)
    {
        if (activeAnomaly == null || anomaly == null)
            return;

        view?.ShowLocalAnomaly(anomaly.Presentation);
    }

    public void Hide()
    {
        view?.HideLocalAnomaly();
    }

    public void Clear()
    {
        activeAnomaly = null;
        Hide();
    }

    private void OnDisable()
    {
        Clear();
    }
}
