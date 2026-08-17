using TMPro;
using UnityEngine;

public sealed class FootballStartZone : MonoBehaviour
{
    [SerializeField] private FootballMinigame minigame;
    [SerializeField] private BunkerMinigameTerminal terminal;
    [SerializeField] private SpriteRenderer[] visualRenderers;
    [SerializeField] private TMP_Text startText;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color unavailableColor = new(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private bool hideVisualWhileRunning = true;

    private Color[] baseColors;

    public void Interact()
    {
        if (minigame != null && minigame.CanStart)
            terminal?.Interact();
    }

    public void SetAvailable(bool available)
    {
        Color tint = available ? availableColor : unavailableColor;
        EnsureBaseColors();

        if (visualRenderers != null)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                SpriteRenderer renderer = visualRenderers[i];
                if (renderer != null)
                {
                    renderer.color = baseColors[i] * tint;
                    renderer.enabled = available || !hideVisualWhileRunning;
                }
            }
        }

        if (startText != null)
        {
            startText.gameObject.SetActive(available || !hideVisualWhileRunning);
            startText.text = available ? "START" : "RUNNING";
            startText.color = available
                ? new Color(0.2f, 1f, 0.45f, 1f)
                : new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }

    private void EnsureBaseColors()
    {
        if (baseColors != null && baseColors.Length == (visualRenderers?.Length ?? 0))
            return;

        int count = visualRenderers?.Length ?? 0;
        baseColors = new Color[count];
        for (int i = 0; i < count; i++)
            baseColors[i] = visualRenderers[i] != null ? visualRenderers[i].color : Color.white;
    }
}
