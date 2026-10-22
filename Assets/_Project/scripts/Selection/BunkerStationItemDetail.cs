using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Item-specific content slot inside the approved shared station shell.</summary>
public sealed class BunkerStationItemDetail : MonoBehaviour
{
    [SerializeField] private Image preview;
    [SerializeField] private Image accent;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private TextMeshProUGUI stats;

    public void Bind(BunkerSelectionEntryModel entry)
    {
        title.text = entry.DisplayName;
        title.color = StationPixelVisuals.Text;
        description.text = entry.Description ?? string.Empty;
        preview.sprite = entry.PreviewIcon != null ? entry.PreviewIcon : entry.Icon;
        preview.enabled = preview.sprite != null;
        preview.preserveAspect = true;
        preview.color = entry.IconColor;
        accent.color = entry.AccentColor;
        var text = new StringBuilder();
        foreach (var stat in entry.Stats)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append(stat.Label).Append(" <color=#42D5DC>").Append(stat.Value).Append("</color>");
        }
        stats.text = text.ToString();
    }
}
