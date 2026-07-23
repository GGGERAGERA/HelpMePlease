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
    private UICardHoverAnimation hoverAnimation;

    public LevelNodeData Data => nodeData;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        hoverAnimation = GetComponent<UICardHoverAnimation>();

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
        SetText(titleText, data.nodeName);
        SetText(descriptionText, data.description);
        SetText(tagText, data.MainThreat);

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.preserveAspect = true;
            iconImage.gameObject.SetActive(data.icon != null);
        }
    }

    private void HandleClick()
    {
        if (nodeData != null)
        {
            onClicked?.Invoke(nodeData);
        }
    }

    public void SetSelected(bool selected)
    {
        Vector3 restingScale = selected ? Vector3.one * 1.035f : Vector3.one;

        if (hoverAnimation != null)
            hoverAnimation.SetRestingScale(restingScale);
        else
            transform.localScale = restingScale;

        Color frameColor = selected ? selectedFrameColor : normalFrameColor;

        if (frameImage != null)
            frameImage.color = frameColor;

        if (glowImage != null)
        {
            Color glowColor = new Color(
                frameColor.r,
                frameColor.g,
                frameColor.b,
                selected ? 0.42f : 0.12f);

            glowImage.color = glowColor;
            hoverAnimation?.SetRestingAccentColor(glowColor);
        }
    }

    private void ConfigureText(TextMeshProUGUI text, float minimumSize, float maximumSize)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
