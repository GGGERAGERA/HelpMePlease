using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates the vitals layout authored in GameplayHUD.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
internal sealed class CombatVitalsHud : MonoBehaviour
{
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private RectTransform healthFill, relayRow, energyStat, coreStat;
    [SerializeField] private TMP_Text energy, cores, relayCaption, relayTime;
    [SerializeField] private float relayHeight = 106f;

    private RectTransform panel;
    private float compactHeight;
    private int shownEnergy = -1, shownCapacity = -1, shownCores = -1;
    private int shownRelaySeconds = -1, shownRelayTotal = -1;
    private string relayFormat;

    internal void SetVisible(bool visible) => visibility.alpha = visible ? 1f : 0f;

    internal float Refresh(Slider health, OrbitalStationRuntime station, OrbitalRelayEvent relay)
    {
        if (panel == null)
        {
            panel = (RectTransform)transform;
            compactHeight = panel.sizeDelta.y;
        }

        float fraction = health.normalizedValue;
        if (healthFill.anchorMax.x != fraction)
            healthFill.anchorMax = new Vector2(fraction, 1f);

        var rig = station != null && station.IsInitialized ? station.Rig : null;
        energyStat.gameObject.SetActive(rig != null);
        if (rig != null)
        {
            int capacity = Mathf.CeilToInt(rig.RotationCapacity);
            int current = Mathf.Clamp(Mathf.CeilToInt(rig.RotationEnergy), 0, capacity);
            if (shownEnergy != current || shownCapacity != capacity)
            {
                shownEnergy = current;
                shownCapacity = capacity;
                energy.SetText("{0}/{1}", current, capacity);
            }
        }

        var wallet = CurrencyManager.Instance;
        coreStat.gameObject.SetActive(wallet != null);
        if (wallet != null && shownCores != wallet.TotalCores)
        {
            shownCores = wallet.TotalCores;
            cores.text = shownCores.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var snapshot = relay != null ? relay.Snapshot : default;
        bool stabilizing = snapshot.Phase == OrbitalRelayPhase.Stabilization;
        float heightChange = 0f;
        if (relayRow.gameObject.activeSelf != stabilizing)
        {
            relayRow.gameObject.SetActive(stabilizing);
            float newHeight = stabilizing ? relayHeight : compactHeight;
            heightChange = newHeight - panel.sizeDelta.y;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, newHeight);
        }
        if (!stabilizing) return heightChange;

        var localization = LocalizationService.EnsureExists();
        relayCaption.text = localization.Get("hud.relay.stabilization");
        string format = localization.Get("hud.relay.time");
        int remaining = Mathf.CeilToInt(snapshot.RemainingTime);
        int total = Mathf.CeilToInt(relay.StabilizationDuration);
        if (remaining != shownRelaySeconds || total != shownRelayTotal || format != relayFormat)
        {
            shownRelaySeconds = remaining;
            shownRelayTotal = total;
            relayFormat = format;
            relayTime.SetText(format, remaining, total);
        }
        return heightChange * panel.localScale.y;
    }
}
