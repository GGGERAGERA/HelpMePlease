using TMPro;
using UnityEngine;

public sealed class MagnetHudView : MonoBehaviour
{
    [SerializeField] private HUDManager hud;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private TextMeshProUGUI remainingTime;
    private int displayedSeconds = -1;

    private void LateUpdate()
    {
        var experience = ExperienceManager.Instance;
        bool active = experience != null && experience.IsGlobalMagnetActive;
        visibility.alpha = active && hud.IsInformationVisible ? 1f : 0f;
        if (!active) return;
        int seconds = Mathf.CeilToInt(experience.MagnetSecondsRemaining);
        if (seconds == displayedSeconds) return;
        displayedSeconds = seconds;
        remainingTime.SetText("{0}:{1:00}", seconds / 60, seconds % 60);
    }
}
