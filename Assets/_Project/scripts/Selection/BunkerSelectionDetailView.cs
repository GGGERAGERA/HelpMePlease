using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerSelectionDetailView : MonoBehaviour
{
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private TextMeshProUGUI emptyText;
    [SerializeField] private Image portrait;
    [SerializeField] private Color lockedPortraitTint = new(0.24f, 0.29f, 0.3f, 1f);
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private bool showCharacterCategory;
    [SerializeField] private GameObject featureBlock;
    [SerializeField] private TextMeshProUGUI featureText;
    [SerializeField] private GameObject statsBlock;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI[] statValues;
    [SerializeField] private GameObject concealedSubject;
    [SerializeField] private Image[] subjectStatusLights;
    [SerializeField] private Image subjectDisplayArt;
    [SerializeField] private Image subjectGlow;
    [SerializeField] private GameObject descriptionBlock;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private GameObject lockBlock;
    [SerializeField] private TextMeshProUGUI lockText;
    [SerializeField] private BunkerStationItemDetail itemDetail;

    private string displayedSubjectId;
    private bool displayedSubjectLocked;
    private float selectionReaction;
    private float unlockReaction;

    private void OnDisable()
    {
        displayedSubjectId = null;
        selectionReaction = unlockReaction = 0f;
    }

    private void Update()
    {
        selectionReaction = Mathf.MoveTowards(selectionReaction, 0f, Time.unscaledDeltaTime * 5f);
        unlockReaction = Mathf.MoveTowards(unlockReaction, 0f, Time.unscaledDeltaTime * 2f);
        if (subjectGlow != null)
        {
            float breathing = 0.07f + 0.025f * Mathf.Sin(Time.unscaledTime * 1.5f);
            subjectGlow.color = displayedSubjectLocked
                ? new Color(0.3f, 0.12f, 0.08f, 0.025f)
                : new Color(0.1f, 0.72f, 0.65f, breathing + unlockReaction * 0.08f);
        }
        if (subjectDisplayArt != null && nameText != null)
            nameText.color = Color.Lerp(StationPixelVisuals.Text, StationPixelVisuals.Cyan,
                Mathf.Max(selectionReaction * 0.25f, unlockReaction * 0.45f));
    }

    public void ShowEmpty(string message)
    {
        contentRoot?.SetActive(false);
        itemDetail?.gameObject.SetActive(false);
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(true);
            emptyText.text = message;
        }
    }

    public void Bind(BunkerSelectionEntryModel entry)
    {
        if (entry == null)
        {
            ShowEmpty(LocalizationService.Instance.Get("bunker.select_item"));
            return;
        }

        itemDetail?.gameObject.SetActive(!entry.IsCharacter);
        if (!entry.IsCharacter && itemDetail != null)
        {
            contentRoot?.SetActive(false);
            emptyText?.gameObject.SetActive(false);
            itemDetail.Bind(entry);
            return;
        }
        contentRoot?.SetActive(true);
        emptyText?.gameObject.SetActive(false);
        if (entry.Id != displayedSubjectId)
            selectionReaction = 1f;
        else if (displayedSubjectLocked && !entry.Locked)
            unlockReaction = 1f;
        displayedSubjectId = entry.Id;
        displayedSubjectLocked = entry.Locked;
        if (subjectDisplayArt != null)
            subjectDisplayArt.color = entry.Locked ? new Color(0.48f, 0.57f, 0.57f, 1f) : Color.white;
        SetText(nameText, entry.DisplayName);
        SetOptional(categoryText, entry.IsCharacter && !showCharacterCategory ? null : entry.Category);
        if (entry.IsCharacter && showCharacterCategory && categoryText != null)
            categoryText.color = entry.Locked ? new Color(0.62f, 0.68f, 0.69f, 1f) : StationPixelVisuals.Cyan;
        SetBlock(featureBlock, featureText, entry.Feature);
        SetBlock(descriptionBlock, descriptionText, entry.IsCharacter ? null : entry.Description);
        SetBlock(lockBlock, lockText, entry.Locked && entry.Progression == null ? entry.LockReason : null);
        if (concealedSubject != null)
            concealedSubject.SetActive(entry.IsCharacter && entry.Locked);
        if (subjectStatusLights != null)
        {
            Color state = entry.Locked ? new Color(0.85f, 0.31f, 0.25f, 1f) : StationPixelVisuals.Cyan;
            foreach (Image light in subjectStatusLights)
                light.color = state;
        }

        if (portrait != null)
        {
            portrait.sprite = entry.IsCharacter ? entry.CharacterVisual : entry.PreviewIcon != null ? entry.PreviewIcon : entry.Icon;
            portrait.gameObject.SetActive(portrait.sprite != null);
            portrait.enabled = portrait.sprite != null;
            portrait.preserveAspect = true;
            portrait.color = entry.Locked
                ? lockedPortraitTint
                : entry.IconColor;
        }

        if (entry.Stats.Count == 0)
        {
            statsBlock?.SetActive(false);
        }
        else
        {
            statsBlock?.SetActive(true);
            if (statValues != null && statValues.Length > 0)
            {
                for (int i = 0; i < statValues.Length; i++)
                    SetOptional(statValues[i], i < entry.Stats.Count ? entry.Stats[i].Value : null);
                return;
            }
            var builder = new StringBuilder();
            for (int i = 0; i < entry.Stats.Count; i++)
            {
                if (i > 0)
                    builder.Append('\n');
                builder.Append(entry.Stats[i].Label)
                    .Append("   ")
                    .Append(entry.Stats[i].Value);
            }
            SetText(statsText, builder.ToString());
        }
    }

    private static void SetBlock(GameObject root, TextMeshProUGUI text, string value)
    {
        bool visible = !string.IsNullOrWhiteSpace(value);
        root?.SetActive(visible);
        if (visible)
            SetText(text, value);
    }

    private static void SetOptional(TextMeshProUGUI text, string value)
    {
        if (text == null)
            return;
        text.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        text.text = value ?? string.Empty;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }
}
