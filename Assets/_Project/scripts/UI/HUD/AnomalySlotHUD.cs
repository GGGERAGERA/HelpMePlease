using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnomalySlotHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI valueText;
    private AnomalyInventory inventory;
    private RunStateManager runState;

    private void Awake()
    {
        if (titleText == null || valueText == null)
        {
            Debug.LogError("[AnomalySlotHUD] Authored title/value references are missing.", this);
            enabled = false;
            gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        runState = RunStateManager.Instance != null
            ? RunStateManager.Instance
            : RunStateManager.EnsureExists();
        inventory = runState.AnomalyInventory;
        inventory.Changed += Refresh;
        runState.EvolutionChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.Changed -= Refresh;

        if (runState != null)
            runState.EvolutionChanged -= Refresh;

        inventory = null;
        runState = null;
    }

    private void Refresh()
    {
        if (valueText == null)
            return;

        if (inventory == null || inventory.IsEmpty)
        {
            titleText.text = "ANOMALY";
            valueText.text = "[ EMPTY ]";
            return;
        }

        if (runState != null && runState.HasEvolution)
        {
            string mode = inventory.Level >= 3
                ? "OVERDRIVE"
                : "HYBRID";
            titleText.text = "ANOMALY";
            valueText.text = $"[ {inventory.CurrentItem.DisplayName} " +
                $"{ToRoman(inventory.Level)} • {mode} ]";
            return;
        }

        titleText.text = "ANOMALY";
        valueText.text = $"[ {inventory.CurrentItem.DisplayName} " +
            $"{ToRoman(inventory.Level)} ]";
    }

    private static string ToRoman(int level) => level switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        _ => "—"
    };
}
