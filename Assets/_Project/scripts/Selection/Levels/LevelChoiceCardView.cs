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

        if (button == null)
            return;

        button.onClick.RemoveListener(HandleClick);
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
        SetText(modifiersText, BuildLevelDetails(data));
        SetText(rewardText, "Награда после победы над боссом");

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
        }
    }

    private void HandleClick()
    {
        if (nodeData != null)
            onClicked?.Invoke(nodeData);
    }

    private string BuildLevelDetails(LevelNodeData data)
    {
        string result = $"• {Mathf.RoundToInt(data.Duration)} сек.";

        if (data.weatherType != LevelWeatherType.None)
            result += $"\n• {FormatWeather(data.weatherType)}";

        if (!string.IsNullOrWhiteSpace(data.MainThreat))
            result += $"\n• Угроза: {data.MainThreat}";

        if (!Mathf.Approximately(data.enemyHealthMultiplier, 1f))
            result += $"\n• Здоровье врагов x{data.enemyHealthMultiplier:0.##}";

        if (!Mathf.Approximately(data.enemySpeedMultiplier, 1f))
            result += $"\n• Скорость врагов x{data.enemySpeedMultiplier:0.##}";

        if (!Mathf.Approximately(data.spawnRateMultiplier, 1f))
            result += $"\n• Давление спавна x{data.spawnRateMultiplier:0.##}";

        if (data.BossPrefab != null)
            result += $"\n• Босс: {data.BossPrefab.name}";

        return result;
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
