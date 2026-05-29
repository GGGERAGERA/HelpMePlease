using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHoverSound : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private float volume = 0.25f;

    private static float lastHoverTime;
    private const float MinInterval = 0.025f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound == null)
            return;

        if (Time.unscaledTime < lastHoverTime + MinInterval)
            return;

        UISoundPlayer.Instance?.Play(hoverSound, volume);
        lastHoverTime = Time.unscaledTime;
    }
}