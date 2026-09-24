using TMPro;
using UnityEngine;

public sealed class HudIconNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText;
    public void SetValue(int value) => valueText.SetText("{0}", value);
}
