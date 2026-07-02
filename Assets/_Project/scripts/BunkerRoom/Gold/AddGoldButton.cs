using UnityEngine;
using UnityEngine.UI;

public class AddGoldButton : MonoBehaviour
{
    [SerializeField] private int addAmount = 1000;
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddGold(addAmount);
        }
        else
        {
            // Консольная команда (отладка) без краша игры. 
            // Нажатие на кнопку не добавит золота, но сообщит об ошибке в лог.
            Debug.Log($"[AddGoldButton] Не удалось добавить {addAmount} золота, так как CurrencyManager отсутствует в сцене!");
        }
    }
}