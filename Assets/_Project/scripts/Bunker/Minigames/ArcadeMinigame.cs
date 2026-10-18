using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Screen-space presentation for the two cabinets. BunkerPanelManager owns navigation;
// the existing scene and physics stay suspended while this panel is open.
public abstract class ArcadeMinigame : BunkerMinigame
{
    [SerializeField] private BunkerPanelManager panels;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private TMP_FontAsset font;
    private GameObject screen, previousSelection;
    private float previousTimeScale;
    private bool cameraWasSuspended, cursorWasVisible;
    private CursorLockMode previousCursorLock;
    private Sprite disc;
    private Texture2D discTexture;
    protected RectTransform Content;
    protected CameraFollow PresentationCamera => cameraFollow;
    protected TMP_Text Status, Result;
    private Button replay;
    public bool IsOpen { get; private set; }
    public int Score { get; protected set; }
    public int BestScore { get; private set; }
    protected abstract string RecordKey { get; }
    protected abstract string Title { get; }
    protected virtual Color ScreenBackdrop => Ink;
    protected bool Russian => LocalizationService.Instance == null || LocalizationService.Instance.CurrentLanguage == GameLanguage.Russian;
    protected string L(string ru, string en) => Russian ? ru : en;
    protected static readonly Color Ink = new Color32(12, 19, 25, 255);
    protected static readonly Color Panel = new Color32(24, 38, 43, 255);
    protected static readonly Color Mint = new Color32(94, 234, 179, 255);
    protected static readonly Color Gold = new Color32(255, 203, 101, 255);
    protected static readonly Color Purple = new Color32(183, 131, 247, 255);

    public void Open()
    {
        if (!CanStart || IsOpen || panels == null || Time.timeScale <= 0f || SceneTransitionOverlay.IsTransitioning) return;
        if (!panels.TryOpenArcade(this)) return;
        StartGame();
    }

    protected sealed override void OnGameStarted()
    {
        if (!IsOpen)
        {
            previousTimeScale = Time.timeScale;
            cameraWasSuspended = cameraFollow != null && cameraFollow.IsPresentationSuspended;
            if (cameraFollow != null) cameraFollow.IsPresentationSuspended = true;
            cursorWasVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            EventSystem.current?.SetSelectedGameObject(null);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
            IsOpen = true;
            BestScore = string.IsNullOrEmpty(RecordKey) ? 0 : PlayerPrefs.GetInt(RecordKey, 0);
            BuildScreen();
        }
        Score = 0;
        if (Result != null) Result.text = "";
        if (replay != null) replay.gameObject.SetActive(false);
        BeginRound();
        RefreshStatus();
    }

    protected abstract void BuildGame();
    protected abstract void BeginRound();
    protected abstract void Tick(float dt);
    protected abstract void RefreshStatus();
    private void Update()
    {
        if (!IsOpen) return;
        if (SceneTransitionOverlay.IsTransitioning) { Close(); return; }
        if (IsRunning) Tick(Mathf.Min(Time.unscaledDeltaTime, .05f));
        else if (Input.GetKeyDown(KeyCode.R)) Replay();
        RefreshStatus();
    }

    protected void Finish(bool won, string details)
    {
        if (!IsRunning) return;
        bool record = Score > BestScore;
        if (record)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(RecordKey, BestScore);
            PlayerPrefs.Save();
        }
        if (won) CompleteGame(); else FailGame();
        AudioService.Instance?.Play(AudioCueId.UIConfirm);
        ShowRoundResult(won, details, record);
        RefreshStatus();
    }

    protected virtual void ShowRoundResult(bool won, string details, bool record)
    {
        Result.text = L(won ? "ИСПЫТАНИЕ ПРОЙДЕНО" : "ИСПЫТАНИЕ НЕ ПРОЙДЕНО", won ? "CHALLENGE COMPLETE" : "CHALLENGE FAILED") +
            "\n\n" + details + (record ? L("\n\nНОВЫЙ РЕКОРД!", "\n\nNEW RECORD!") : "");
        replay.gameObject.SetActive(true);
    }

    public void Replay()
    {
        if (!IsOpen || IsRunning) return;
        ResetGame();
        StartGame();
    }

    public void Close()
    {
        if (!IsOpen) return;
        OnScreenClosing();
        IsOpen = false;
        if (screen != null) { screen.SetActive(false); Destroy(screen); }
        screen = null;
        Content = null;
        if (disc != null) Destroy(disc);
        if (discTexture != null) Destroy(discTexture);
        // Destruction is deferred; an immediate reopen must create its own sprites.
        disc = null;
        discTexture = null;
        if (cameraFollow != null) cameraFollow.IsPresentationSuspended = cameraWasSuspended;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = cursorWasVisible;
        EventSystem.current?.SetSelectedGameObject(previousSelection);
        panels?.ReleaseArcade(this);
        ResetGame();
    }

    protected override void OnDisable() { Close(); base.OnDisable(); }
    protected virtual void OnScreenClosing() { }

    private void BuildScreen()
    {
        screen = new GameObject("Subject42 Arcade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // CanvasScaler only scales a root Canvas. The bunker's manager lives below
        // another Canvas, so keep this modal at the scene root and clean it up here.
        SceneManager.MoveGameObjectToScene(screen, gameObject.scene);
        var canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvas.pixelPerfect = true;
        var scaler = screen.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1180, 760);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var background = screen.AddComponent<Image>();
        background.color = ScreenBackdrop;
        Content = new GameObject("Play area", typeof(RectTransform)).GetComponent<RectTransform>();
        Content.SetParent(screen.transform, false);
        Content.sizeDelta = new Vector2(1180, 760);
        ConfigureScreen(canvas, scaler, background);
        BuildChrome();
        BuildGame();
    }

    protected virtual void ConfigureScreen(Canvas canvas, CanvasScaler scaler, Image background) { }

    protected virtual void BuildChrome()
    {
        Label(Content, Title, new Vector2(-280, 340), new Vector2(560, 44), 24, Mint);
        Label(Content, "SUBJECT #42 / RECREATION TERMINAL", new Vector2(285, 342), new Vector2(540, 32), 14, Gold);
        Status = Label(Content, "", new Vector2(280, 280), new Vector2(540, 60), 21, Color.white);
        Result = Label(Content, "", new Vector2(280, -130), new Vector2(510, 155), 21, Gold);
        replay = Button(Content, L("ЕЩЁ ПАРТИЯ [R]", "PLAY AGAIN [R]"), new Vector2(280, -252), new Vector2(420, 50), Replay);
        Button(Content, L("В БУНКЕР [ESC]", "EXIT TO BUNKER [ESC]"), new Vector2(280, -320), new Vector2(420, 46), Close);
    }

    protected RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f;
        r.anchoredPosition = position;
        r.sizeDelta = size;
        return r;
    }

    protected Image Box(Transform parent, Vector2 position, Vector2 size, Color color, bool round = false)
    {
        var image = Rect(parent, round ? "Pixel disc" : "Block", position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (round)
        {
            if (disc == null)
            {
                discTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    discTexture.SetPixel(x, y, new Vector2(x - 7.5f, y - 7.5f).sqrMagnitude < 60 ? Color.white : Color.clear);
                discTexture.Apply();
                disc = Sprite.Create(discTexture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16);
            }
            image.sprite = disc;
        }
        return image;
    }

    protected TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var label = Rect(parent, "Label", position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    protected Button Button(Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action, bool playSound = true)
    {
        var image = Box(parent, position, size, Panel);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Mint;
        colors.pressedColor = Gold;
        colors.disabledColor = new Color(.35f, .4f, .4f, 1);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => { if (playSound) AudioService.Instance?.Play(AudioCueId.UIConfirm); action(); });
        Label(button.transform, text, Vector2.zero, size - new Vector2(12, 4), 18, Color.white);
        return button;
    }
}
