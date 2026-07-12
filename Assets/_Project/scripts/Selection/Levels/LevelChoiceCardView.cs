using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelChoiceCardView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;

    [Header("Selection")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image glowImage;
    [SerializeField] private Color normalFrameColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color selectedFrameColor = new Color(0.2f, 0.72f, 0.82f, 1f);

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI tagText;

    private LevelNodeData nodeData;
    private Action<LevelNodeData> onClicked;

    public LevelNodeData Data => nodeData;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);

        ConfigureText(titleText, 30f, 44f);
        ConfigureText(descriptionText, 18f, 24f);
        ConfigureText(tagText, 16f, 20f);
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

        SetSelected(false);
        SetText(titleText, FormatWeather(data.weatherType));
        SetText(descriptionText, FormatWeatherEffect(data.weatherType));
        SetText(tagText, data.MainThreat);

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.gameObject.SetActive(false);
        }
    }

    private void HandleClick()
    {
        if (nodeData != null)
            onClicked?.Invoke(nodeData);
    }

    public void SetSelected(bool selected)
    {
        transform.localScale = selected ? Vector3.one * 1.035f : Vector3.one;

        Color frameColor = selected ? selectedFrameColor : normalFrameColor;

        if (frameImage != null)
            frameImage.color = frameColor;

        if (glowImage != null)
            glowImage.color = new Color(
                frameColor.r,
                frameColor.g,
                frameColor.b,
                selected ? 0.42f : 0.12f);
    }

    private string FormatWeather(LevelWeatherType weather)
    {
        return weather switch
        {
            LevelWeatherType.Darkness => "ТЕМНОТА",
            LevelWeatherType.Rain => "ДОЖДЬ",
            LevelWeatherType.Snow => "СНЕГ",
            _ => "БЕЗ ПОГОДЫ"
        };
    }

    private string FormatWeatherEffect(LevelWeatherType weather)
    {
        return weather switch
        {
            LevelWeatherType.Darkness => "Враги крепче",
            LevelWeatherType.Rain => "Враги быстрее",
            LevelWeatherType.Snow => "Врагов больше",
            _ => "Обычные условия"
        };
    }

    private void ConfigureText(TextMeshProUGUI text, float minimumSize, float maximumSize)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
