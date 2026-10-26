#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct DebugAudioPlayback
{
    public AudioCueId CueId { get; }
    public string ClipName { get; }
    public AudioCategory Category { get; }
    public Vector3? Position { get; }
    public string CallerMember { get; }
    public string CallerFile { get; }
    public int CallerLine { get; }
    public string SourceObject { get; }
    public float UnscaledTime { get; }

    internal DebugAudioPlayback(AudioCueId cue, AudioClip clip, AudioCategory category, Vector3? position,
        string caller, string file, int line, string sourceObject)
    {
        CueId = cue; ClipName = clip.name; Category = category; Position = position;
        CallerMember = caller; CallerFile = file; CallerLine = line; SourceObject = sourceObject;
        UnscaledTime = Time.unscaledTime;
    }
}

public sealed partial class AudioService
{
    private const int DebugHistoryCapacity = 64;
    private const int DebugVisibleRows = 7;
    private readonly DebugAudioPlayback[] debugAudioHistory = new DebugAudioPlayback[DebugHistoryCapacity];
    private readonly string[] debugAudioRows = new string[DebugHistoryCapacity];
    private readonly TextMeshProUGUI[] debugAudioLabels = new TextMeshProUGUI[DebugVisibleRows];
    private int debugAudioNext;
    private GameObject debugAudioCanvas;
    private bool debugAudioDirty;

    public bool DebugAudioPanelVisible { get; private set; }
    public int DebugAudioHistoryCount { get; private set; }

    // These methods never acquire time, cursor, EventSystem or player-input ownership.
    public void ToggleDebugAudioPanel()
    {
        DebugAudioPanelVisible = !DebugAudioPanelVisible;
        if (DebugAudioPanelVisible)
        {
            EnsureDebugAudioPanel();
            RefreshDebugAudioLabels();
        }
        if (debugAudioCanvas != null) debugAudioCanvas.SetActive(DebugAudioPanelVisible);
    }

    public void ClearDebugAudioHistory()
    {
        Array.Clear(debugAudioHistory, 0, debugAudioHistory.Length);
        Array.Clear(debugAudioRows, 0, debugAudioRows.Length);
        debugAudioNext = DebugAudioHistoryCount = 0;
        debugAudioDirty = true;
    }

    public DebugAudioPlayback GetDebugAudioPlayback(int newestIndex)
    {
        if (newestIndex < 0 || newestIndex >= DebugAudioHistoryCount)
            throw new ArgumentOutOfRangeException(nameof(newestIndex));
        return debugAudioHistory[DebugAudioIndex(newestIndex)];
    }

    private int DebugAudioIndex(int newestIndex) =>
        (debugAudioNext - 1 - newestIndex + DebugHistoryCapacity) % DebugHistoryCapacity;

    partial void UpdateDebugAudioPanel()
    {
        if (Input.GetKeyDown(KeyCode.F10)) ToggleDebugAudioPanel();
        if (DebugAudioPanelVisible && debugAudioDirty) RefreshDebugAudioLabels();
    }

    partial void DisableDebugAudioPanel()
    {
        DebugAudioPanelVisible = false;
        if (debugAudioCanvas != null) debugAudioCanvas.SetActive(false);
    }

    partial void RecordDebugPlayback(AudioCueId cueId, AudioClip clip, AudioCategory category,
        Vector3? position, string caller, string callerFile, int callerLine, string sourceObject)
    {
        var entry = new DebugAudioPlayback(cueId, clip, category, position, caller, callerFile, callerLine, sourceObject);
        debugAudioHistory[debugAudioNext] = entry;
        string cue = cueId == AudioCueId.None ? "External clip" : cueId.ToString();
        string point = position.HasValue ? $"  @ {position.Value.x:0.0}, {position.Value.y:0.0}" : "";
        string owner = string.IsNullOrEmpty(sourceObject) ? "" : $"  · {sourceObject}";
        debugAudioRows[debugAudioNext] = $"{entry.UnscaledTime:0000.000}s  {cue}  [{category}]\n" +
            $"{clip.name}{owner}\n{Path.GetFileNameWithoutExtension(callerFile)}.{caller}:{callerLine}{point}";
        debugAudioNext = (debugAudioNext + 1) % DebugHistoryCapacity;
        DebugAudioHistoryCount = Mathf.Min(DebugAudioHistoryCount + 1, DebugHistoryCapacity);
        debugAudioDirty = true;
    }

    private void EnsureDebugAudioPanel()
    {
        if (debugAudioCanvas != null) return;
        // Development UI follows the existing F1 menu pattern. No GraphicRaycaster or Selectables are created.
        debugAudioCanvas = new GameObject("[Dev] Audio monitor", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        debugAudioCanvas.transform.SetParent(transform, false);
        var canvas = debugAudioCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = debugAudioCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 1f;
        var panel = new GameObject("Sound history", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(debugAudioCanvas.transform, false);
        panel.anchorMin = new Vector2(1, 0); panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(1, .5f);
        panel.sizeDelta = new Vector2(510, -290); panel.anchoredPosition = new Vector2(-20, -75);
        panel.GetComponent<Image>().color = new Color(.015f, .035f, .045f, .94f);
        panel.GetComponent<Image>().raycastTarget = false;
        AddDebugAudioLabel(panel, "Title", "ЗВУКИ В ЗАБЕГЕ  ·  F10 скрыть", 10, 26, 17,
            new Color(.35f, .95f, 1f)).fontStyle = FontStyles.Bold;
        AddDebugAudioLabel(panel, "Hint", "Последние запуски · новые сверху\nРан продолжается; мышь проходит через панель",
            42, 38, 13, new Color(.64f, .78f, .81f));
        for (int i = 0; i < DebugVisibleRows; i++)
            debugAudioLabels[i] = AddDebugAudioLabel(panel, $"Playback {i + 1}", "", 86 + i * 68, 64, 14,
                new Color(.88f, .94f, .96f));
    }

    private static TextMeshProUGUI AddDebugAudioLabel(RectTransform parent, string name, string value,
        float top, float height, float size, Color color)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.rectTransform.anchorMin = new Vector2(0, 1); text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(.5f, 1);
        text.rectTransform.sizeDelta = new Vector2(-28, height);
        text.rectTransform.anchoredPosition = new Vector2(0, -top);
        text.fontSize = size; text.color = color; text.text = value;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.richText = false; text.raycastTarget = false;
        return text;
    }

    private void RefreshDebugAudioLabels()
    {
        for (int i = 0; i < DebugVisibleRows; i++)
            debugAudioLabels[i].text = i < DebugAudioHistoryCount ? debugAudioRows[DebugAudioIndex(i)] :
                i == 0 ? "Пока нет запусков звука" : "";
        debugAudioDirty = false;
    }
}
#endif
