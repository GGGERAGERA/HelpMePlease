using UnityEngine;
using UnityEngine.EventSystems;

public class MetaUpgradeHoverInfo : MonoBehaviour, IPointerEnterHandler
{
    [Header("Info")]
    [SerializeField] private Sprite icon;

    [TextArea]
    [SerializeField] private string description;

    [Header("Target")]
    [SerializeField] private MetaUpgradeDescriptionUI descriptionUI;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (descriptionUI == null)
            return;

        descriptionUI.Show(icon, description);
    }
}
