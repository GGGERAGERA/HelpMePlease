using TMPro;
using UnityEngine;

public class MetaUpgradeShopUI : MonoBehaviour
{
    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("Cards")]
    [SerializeField] private MetaUpgradeCardUI[] upgradeCards;

    [Header("Summary")]
    [SerializeField] private MetaUpgradeSummaryUI summaryUI;

    private void OnEnable()
    {
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        int gold = CurrencyManager.Instance != null
            ? CurrencyManager.Instance.TotalGold
            : 0;

        if (goldText != null)
            goldText.text = $"GOLD: {gold}";

        if (upgradeCards != null)
        {
            foreach (MetaUpgradeCardUI card in upgradeCards)
            {
                if (card != null)
                    card.Refresh(gold);
            }
        }

        if (summaryUI != null)
            summaryUI.Refresh();
    }
}