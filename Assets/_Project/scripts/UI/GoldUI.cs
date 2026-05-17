using TMPro;
using UnityEngine;

public class GoldUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;

    private void Start()
    {
        UpdateGold();
    }

    public void UpdateGold()
    {
        if (goldText == null)
            return;

        int gold = CurrencyManager.Instance != null
            ? CurrencyManager.Instance.TotalGold
            : 0;

        goldText.text = $"GOLD: {gold}";
    }
}