using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCardView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData character;

    [Header("Visuals")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject lockedOverlay;

    [Header("Colors")]
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new(0.35f, 0.35f, 0.35f, 1f);

    [Header("Selection")]
    [SerializeField] private GameObject selectedVisual;

    private Button button;
    private TextMeshProUGUI lockRequirementText;
    private Image selectionOutlineImage;
    private Outline selectionOutlineEffect;

    public CharacterData Character => character;

    public event Action<CharacterData> Clicked;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(HandleClick);

        Refresh();
        ConfigurePixelStateVisuals();
        SetSelected(false);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (character == null || !IsUnlocked())
            return;

        Clicked?.Invoke(character);
    }

    public void Refresh()
    {
        if (character == null)
            return;

        bool isUnlocked = IsUnlocked();

        if (portraitImage != null)
        {
            portraitImage.sprite = character.portrait;
            portraitImage.color = isUnlocked ? unlockedColor : lockedColor;
        }

        if (nameText != null)
        {
            nameText.text = character.characterName;
            nameText.color = isUnlocked ? Color.white : Color.gray;
        }

        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(!isUnlocked);
            if (!isUnlocked)
                RefreshLockRequirement();
        }

        if (button != null)
            button.interactable = isUnlocked;
    }

    public void SetSelected(bool selected)
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(false);

        if (selectionOutlineImage != null)
        {
            Color color = selectionOutlineImage.color;
            color.a = selected ? 1f : 0f;
            selectionOutlineImage.color = color;
        }

        if (selectionOutlineEffect != null)
            selectionOutlineEffect.enabled = selected;
    }

    private bool IsUnlocked()
    {
        if (character == null || character.unlockData == null)
            return true;

        return UnlockProgressService.IsUnlockedNow(character.unlockData);
    }

    private void RefreshLockRequirement()
    {
        if (lockedOverlay == null || character == null || character.unlockData == null)
            return;

        string description = UnlockProgressService.GetLockedDescription(character.unlockData);
        if (string.IsNullOrWhiteSpace(description))
            return;

        if (lockRequirementText == null)
        {
            Transform existing = lockedOverlay.transform.Find("StationRequirementText");
            lockRequirementText = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        }

        if (lockRequirementText == null)
        {
            GameObject requirement = new("StationRequirementText", typeof(RectTransform), typeof(TextMeshProUGUI));
            requirement.layer = lockedOverlay.layer;
            requirement.transform.SetParent(lockedOverlay.transform, false);
            RectTransform rect = requirement.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.08f);
            rect.anchorMax = new Vector2(0.92f, 0.42f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            lockRequirementText = requirement.GetComponent<TextMeshProUGUI>();
            lockRequirementText.font = nameText != null ? nameText.font : TMP_Settings.defaultFontAsset;
            lockRequirementText.fontSize = 20f;
            lockRequirementText.fontStyle = FontStyles.Bold;
            lockRequirementText.alignment = TextAlignmentOptions.Center;
            lockRequirementText.color = new Color(0.78f, 0.95f, 0.96f, 1f);
            lockRequirementText.textWrappingMode = TextWrappingModes.Normal;
            lockRequirementText.raycastTarget = false;
        }

        Transform legacyCondition = lockedOverlay.transform.Find("ConditionText");
        if (legacyCondition != null)
            legacyCondition.gameObject.SetActive(false);

        lockRequirementText.text = description;
    }

    private void ConfigurePixelStateVisuals()
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(false);

        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;

        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.88f, 0.92f, 0.94f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
        }

        Transform existingOutline = transform.Find("Background/BackgroundOutline");
        selectionOutlineImage = existingOutline != null
            ? existingOutline.GetComponent<Image>()
            : null;

        if (selectionOutlineImage != null)
        {
            selectionOutlineImage.material = null;
            selectionOutlineImage.color = new Color(0.08f, 0.78f, 0.82f, 0f);
            selectionOutlineImage.raycastTarget = false;
            return;
        }

        Graphic target = button != null ? button.targetGraphic : GetComponent<Graphic>();
        if (target != null)
        {
            selectionOutlineEffect = target.GetComponent<Outline>();
            if (selectionOutlineEffect == null)
                selectionOutlineEffect = target.gameObject.AddComponent<Outline>();
            selectionOutlineEffect.effectColor = new Color(0.08f, 0.78f, 0.82f, 1f);
            selectionOutlineEffect.effectDistance = new Vector2(2f, -2f);
            selectionOutlineEffect.useGraphicAlpha = true;
            selectionOutlineEffect.enabled = false;
        }
    }
}
