using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DashCooldownView : MonoBehaviour
{
    [SerializeField] private Image cooldownFillImage;
    [SerializeField] private TextMeshProUGUI keyText;

    private CharacterMovement2D movement;

    public void Bind(CharacterMovement2D source)
    {
        movement = source;
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        float progress = movement != null
            ? movement.DashCooldownProgress
            : 1f;

        if (cooldownFillImage != null)
            cooldownFillImage.fillAmount = progress;

        if (keyText != null)
        {
            KeyCode key = movement != null
                ? movement.DashKey
                : KeyCode.Space;
            keyText.text = key.ToString().ToUpperInvariant();
        }
    }
}
