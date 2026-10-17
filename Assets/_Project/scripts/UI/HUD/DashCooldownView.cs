using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DashCooldownView : MonoBehaviour
{
    [SerializeField] private Image cooldownFillImage;

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

    }
}
