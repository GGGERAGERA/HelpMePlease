using TMPro;
using UnityEngine;

public class PortalLabel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI labelText;

    public void SetText(string text)
    {
        if (labelText != null)
            labelText.text = text;
    }
}
