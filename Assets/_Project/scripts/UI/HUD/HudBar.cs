using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HudBar : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;
    public void SetValue(float value, float maximum)
    {
        slider.maxValue = Mathf.Max(1f, maximum);
        slider.SetValueWithoutNotify(Mathf.Clamp(value, 0f, slider.maxValue));
        if (valueText != null) valueText.SetText("{0}/{1}", Mathf.Ceil(value), Mathf.Ceil(maximum));
    }
}
