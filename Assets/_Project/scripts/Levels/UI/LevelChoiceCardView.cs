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

        if (data.weatherType != LevelWeatherType.None)
            result += $"Х {FormatWeather(data.weatherType)}\n";

        if (data.enemyHealthMultiplier != 1f)
            result += $"Х HP врагов x{data.enemyHealthMultiplier:0.##}\n";

        if (data.enemySpeedMultiplier != 1f)
            result += $"Х —корость врагов x{data.enemySpeedMultiplier:0.##}\n";

        if (data.spawnRateMultiplier != 1f)
            result += $"Х „астота спавна x{data.spawnRateMultiplier:0.##}\n";

        if (data.hasEliteEnemies)
            result += "Х Ёлитные враги\n";

        if (data.hasExplosiveEnemies)
            result += "Х ¬зрывные враги\n";

        if (data.hasHoldZoneEvent)
            result += "Х —обытие удержани€ зоны\n";

        if (data.hasExtraChest)
            result += "Х ƒополнительный сундук\n";

        return string.IsNullOrWhiteSpace(result) ? "Х Ѕез особых условий" : result.TrimEnd();
    }

    private string BuildRewardText(LevelNodeData data)
    {
        if (data.bonusRareChance > 0f)
            return $"+{Mathf.RoundToInt(data.bonusRareChance * 100f)}% шанс редкой награды";

        return "ќбычна€ награда";
    }

    private string FormatWeather(LevelWeatherType weather)
    {
        return weather switch
        {
            LevelWeatherType.Darkness => "“емнота",
            LevelWeatherType.Rain => "ƒождь",
            LevelWeatherType.Snow => "—нег",
            _ => "ќбычна€ погода"
        };
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}