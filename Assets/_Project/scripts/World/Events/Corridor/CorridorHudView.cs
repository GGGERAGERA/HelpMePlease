using TMPro;
using UnityEngine;

public sealed class CorridorHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text title, progress;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Color normalColor = new Color(.36f, .94f, 1f), pressureColor = new Color(1f, .35f, .48f);
    public void Set(int completed, int count, bool final, bool open, bool pressure)
    {
        if (canvas != null) canvas.enabled = true;
        if (title != null) { title.text = Text(final ? "event.corridor.final" : "event.evacuation.name"); title.color = pressure ? pressureColor : normalColor; }
        if (progress != null) progress.text = final ? Text(open ? "event.corridor.exitOpen" : "event.corridor.exitLocked") : $"{Text("event.corridor.gates")} {completed} / {count}";
    }
    private static string Text(string key) => LocalizationService.Instance != null ? LocalizationService.Instance.Get(key) : key;
    public void Hide() { if (canvas != null) canvas.enabled = false; }
}
