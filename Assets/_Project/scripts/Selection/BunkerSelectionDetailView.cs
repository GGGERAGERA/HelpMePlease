using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BunkerSelectionDetailView : MonoBehaviour
{
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private TextMeshProUGUI emptyText;
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private GameObject featureBlock;
    [SerializeField] private TextMeshProUGUI featureText;
    [SerializeField] private GameObject statsBlock;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private GameObject descriptionBlock;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private GameObject lockBlock;
    [SerializeField] private TextMeshProUGUI lockText;

    public void ShowEmpty(string message)
    {
        contentRoot?.SetActive(false);
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
            ShowEmpty("ВЫБЕРИТЕ ЭЛЕМЕНТ");
            return;
        }

        contentRoot?.SetActive(true);
        emptyText?.gameObject.SetActive(false);

        SetText(nameText, entry.DisplayName);
        SetOptional(categoryText, entry.Category);
        SetBlock(featureBlock, featureText, entry.Feature);
        SetBlock(descriptionBlock, descriptionText, entry.Description);
        SetBlock(lockBlock, lockText, entry.Locked ? entry.LockReason : null);

        if (portrait != null)
        {
            portrait.sprite = entry.Icon;
            portrait.enabled = entry.Icon != null;
            portrait.preserveAspect = true;
            portrait.color = entry.Locked
                ? StationPixelVisuals.Disabled
                : entry.IconColor;
        }

        if (entry.Stats.Count == 0)
        {
            statsBlock?.SetActive(false);
        }
        else
        {
            statsBlock?.SetActive(true);
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
