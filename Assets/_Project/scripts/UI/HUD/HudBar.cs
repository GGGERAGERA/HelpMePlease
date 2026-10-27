using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HudBar : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;
    public void SetValue(float value, float maximum)
    {
        float displayedValue = Mathf.Max(0f, value);
        slider.minValue = 0f;
        slider.maxValue = Mathf.Max(1f, maximum);
        slider.SetValueWithoutNotify(Mathf.Clamp(displayedValue, 0f, slider.maxValue));
        if (valueText != null) valueText.SetText("{0}/{1}", Mathf.Ceil(displayedValue), Mathf.Ceil(maximum));
    }
}
