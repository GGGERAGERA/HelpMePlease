using UnityEngine;
using UnityEngine.UI;

public sealed class HudSegments : MonoBehaviour
{
    [SerializeField] private Image[] segments;
    [SerializeField] private Color activeColor;
    [SerializeField] private Color inactiveColor;
    public void SetValue(int count)
    {
        for (int i = 0; i < segments.Length; i++)
            segments[i].color = i < count ? activeColor : inactiveColor;
    }
}
