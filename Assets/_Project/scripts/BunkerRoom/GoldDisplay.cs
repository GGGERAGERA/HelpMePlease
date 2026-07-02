using UnityEngine;
using TMPro;

public class GoldDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;

    private void Awake()
    {
        if (goldText == null) goldText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnGoldUpdated += UpdateDisplay;
            UpdateDisplay(CurrencyManager.Instance.TotalGold);
        }
        else
        {
            // Консольное предупреждение, если менеджер отсутствует
            Debug.LogWarning("[GoldDisplay] CurrencyManager не найден в сцене. Отображаю тестовое значение 0.");
            if (goldText != null) goldText.text = "0";
        }
    }

    private void OnDisable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnGoldUpdated -= UpdateDisplay;
        }
    }

    private void UpdateDisplay(int currentGold)
    {
        if (goldText != null) goldText.text = currentGold.ToString();
    }
}