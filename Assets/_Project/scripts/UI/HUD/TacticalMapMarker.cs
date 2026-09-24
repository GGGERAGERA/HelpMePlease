using UnityEngine;
using UnityEngine.UI;

public sealed class TacticalMapMarker : MonoBehaviour
{
    [SerializeField] private RectTransform rect;
    [SerializeField] private Image fill;
    [SerializeField] private Outline border;
    public RectTransform Rect => rect;
    public Image Fill => fill;
    public Outline Border => border;
}
