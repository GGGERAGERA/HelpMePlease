using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelChoiceCardView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI modifiersText;
    [SerializeField] private TextMeshProUGUI rewardText;

    private LevelNodeData nodeData;
    private Action<LevelNodeData> onClicked;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Setup(LevelNodeData data, Action<LevelNodeData> clickCallback)
    {
        nodeData = data;
        onClicked = clickCallback;

        gameObject.SetActive(data != null);

        if (data == null)
            return;

        SetText(nameText, data.nodeName);
        SetText(descriptionText, data.description);
        SetText(modifiersText, BuildModifiersText(data));
        SetText(rewardText, BuildRewardText(data));

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
        }
    }

    private void HandleClick()
    {
        if (nodeData == null)
            return;

        onClicked?.Invoke(nodeData);
    }

    private string BuildModifiersText(LevelNodeData data)
    {
        string result = "";
        int nextLevel = RunStateManager.Instance != null
            ? RunStateManager.Instance.CurrentLevel + 1
            : 2;

        if (nextLevel > 2)
            result += $"• Давление уровня +{(nextLevel - 2) * 12}%\n";

        if (data.weatherType != LevelWeatherType.None)
            result += $"• {FormatWeather(data.weatherType)}\n";

        if (data.enemyHealthMultiplier != 1f)
            result += $"• HP врагов x{data.enemyHealthMultiplier:0.##}\n";

        if (data.enemySpeedMultiplier != 1f)
            result += $"• Скорость врагов x{data.enemySpeedMultiplier:0.##}\n";

        if (data.spawnRateMultiplier != 1f)
            result += $"• Частота спавна x{data.spawnRateMultiplier:0.##}\n";

        if (data.hasEliteEnemies)
            result += "• Элитные враги\n";

        if (data.hasExplosiveEnemies)
            result += "• Взрывные враги\n";

        if (data.hasHoldZoneEvent)
            result += "• Событие удержания зоны\n";

        if (data.hasExtraChest)
            result += "• Дополнительный сундук\n";

        return string.IsNullOrWhiteSpace(result)
            ? "• Без особых условий"
            : result.TrimEnd();
    }

    private string BuildRewardText(LevelNodeData data)
    {
        if (data.bonusRareChance > 0f)
            return $"+{Mathf.RoundToInt(data.bonusRareChance * 100f)}% шанс редкой награды";

        return "Обычная награда";
    }

    private string FormatWeather(LevelWeatherType weather)
    {
        return weather switch
        {
            LevelWeatherType.Darkness => "Темнота",
            LevelWeatherType.Rain => "Дождь",
            LevelWeatherType.Snow => "Снег",
            _ => "Обычная погода"
        };
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
