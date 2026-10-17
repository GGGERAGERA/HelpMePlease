using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class HudIconNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText;
    public void SetValue(int value) => valueText.text = value.ToString(CultureInfo.InvariantCulture);
}
