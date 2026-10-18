using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EuropeanRoulette : ArcadeMinigame
{
    protected override string RecordKey => null;
    protected override string Title => "";
    public RouletteSession Session { get; private set; }
    public int SelectedColour { get; private set; } = RouletteSession.Red;
    public int SelectedAmount { get; private set; } = 25;
    public int DisplayedBallNumber => RouletteWheelGraphic.NumberAt(wheelAngle, ballAngle);
    private readonly Button[] colourButtons = new Button[3];
    private readonly Image[] colourFrames = new Image[3];
    private readonly Button[] amountButtons = new Button[5];
    private Button spinButton;
    private TMP_Text spinText;
    private TMP_InputField amountInput;
    private RectTransform rotor, ballGraphic;
    private RouletteWheelGraphic wheel;
    private CanvasGroup tableFade;
    private GameObject stationLabel;
    private bool labelWasActive;
    private System.Random random;
    private AudioSource sounds;
    private AudioClip placeClip, tickClip, winClip;
    private Vector2 cameraStart, tableCenter;
    private float cameraSize, focusSize, focusTime;
    private bool focused;
    private float wheelAngle, ballAngle, wheelStart, ballStart, wheelEnd, ballEnd, elapsed, nextTick, resultPulse;
    private int outcome;

    protected override void ConfigureScreen(Canvas canvas, CanvasScaler scaler, Image background)
    {
        stationLabel = transform.Find("ArcadeLabel")?.gameObject;
        labelWasActive = stationLabel != null && stationLabel.activeSelf;
        if (stationLabel != null) stationLabel.SetActive(false);
        // The existing modal still owns input/pause. Only this table uses world
        // presentation; cabinets retain their normal screen-space presentation.
        var table = transform.Find("graphic/Table")?.GetComponent<SpriteRenderer>();
        Bounds bounds = table != null ? table.bounds : new Bounds(transform.position + Vector3.up * 2.5f, new Vector3(6, 5));
        tableCenter = (Vector2)bounds.center + Vector2.up * bounds.size.y * .04f;
        float scale = bounds.size.x / 960;
        scaler.enabled = false;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = PresentationCamera.ControlledCamera;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "UI";
        var root = (RectTransform)canvas.transform;
        root.sizeDelta = new Vector2(960, 600);
        root.position = new Vector3(tableCenter.x, tableCenter.y, -1);
        root.localScale = Vector3.one * scale;
        Content.sizeDelta = root.sizeDelta;
        background.color = Color.clear;
        tableFade = canvas.gameObject.AddComponent<CanvasGroup>(); tableFade.alpha = 0;
        var cam = canvas.worldCamera;
        cameraStart = cam.transform.position; cameraSize = cam.orthographicSize;
        focusSize = Mathf.Max(760 * scale / 2, 1180 * scale / (2 * cam.aspect));
        focusTime = 0; focused = true;
        PresentationCamera.BeginWorldBoundsFocus(this, cameraStart, cameraSize);
    }
    protected override void BuildChrome()
    {
        // Cover only the old betting cloth, leaving the authored red table,
        // chairs and surrounding bunker visible around the playable surface.
        Box(Content, Vector2.zero, new Vector2(920, 450), new Color32(78, 31, 33, 255), true);
        Box(Content, Vector2.zero, new Vector2(900, 430), new Color32(108, 42, 44, 255), true);
        Status = Label(Content, "", new Vector2(225, 182), new Vector2(380, 38), 24, Gold);
        Button(Content, "Esc", new Vector2(440, 242), new Vector2(64, 32), Close, false);
    }
    protected override void BuildGame()
    {
        BuildWheel();
        string[] names = { L("Красное", "Red"), L("Чёрное", "Black"), L("Зелёное", "Green") };
        for (int i = 0; i < 3; i++)
        {
            int colour = i;
            Vector2 pos = new Vector2(93 + i * 132, 100);
            colourFrames[i] = Box(Content, pos, new Vector2(126, 86), Gold);
            var button = Button(Content, names[i] + "\n×" + RouletteSession.Multiplier(i), pos, new Vector2(118, 78), () => SelectColour(colour), false);
            button.name = "Colour " + i;
            button.GetComponent<Image>().color = Colour(i);
            button.GetComponentInChildren<TMP_Text>().fontSize = 22;
            button.transition = Selectable.Transition.None;
            colourButtons[i] = button;
        }
        amountButtons[0] = Button(Content, "-", new Vector2(68, -12), new Vector2(60, 52), () => SetAmount(SelectedAmount - 5), false);
        amountButtons[1] = Button(Content, "+", new Vector2(382, -12), new Vector2(60, 52), () => SetAmount((int)Math.Min(int.MaxValue, (long)SelectedAmount + 5)), false);
        var inputBody = Box(Content, new Vector2(225, -12), new Vector2(238, 52), Ink);
        inputBody.raycastTarget = true;
        amountInput = inputBody.gameObject.AddComponent<TMP_InputField>();
        amountInput.targetGraphic = inputBody;
        amountInput.textViewport = inputBody.rectTransform;
        amountInput.textComponent = Label(inputBody.transform, "25", Vector2.zero, new Vector2(220, 46), 27, Gold);
        amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        amountInput.characterLimit = 10;
        amountInput.onEndEdit.AddListener(value => { if (long.TryParse(value, out long amount)) SetAmount((int)Math.Max(1, Math.Min(int.MaxValue, amount))); else RefreshStatus(); });
        amountButtons[2] = Button(Content, "25", new Vector2(119, -80), new Vector2(94, 36), () => SetAmount(25), false);
        amountButtons[3] = Button(Content, "100", new Vector2(225, -80), new Vector2(94, 36), () => SetAmount(100), false);
        amountButtons[4] = Button(Content, L("Всё", "All"), new Vector2(331, -80), new Vector2(94, 36), () => SetAmount(MaximumStake()), false);
        spinButton = Button(Content, "", new Vector2(225, -155), new Vector2(368, 58), () => { if (Session.Spinning) SkipSpin(); else Spin(); }, false);
        spinText = spinButton.GetComponentInChildren<TMP_Text>(); spinText.fontSize = 25;
        sounds = Content.gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false; sounds.spatialBlend = 0; sounds.ignoreListenerPause = true;
        AudioService.Instance?.RouteExternalSource(sounds, AudioCategory.UI);
        if (placeClip == null)
        {
            placeClip = MakeSound("Roulette select", .055f, 650, false);
            tickClip = MakeSound("Roulette ball", .025f, 1300, false);
            winClip = MakeSound("Roulette win", .26f, 660, true);
        }
    }
    private void BuildWheel()
    {
        var assembly = Rect(Content, "Wheel", new Vector2(-225, 0), Vector2.one * 414);
        Box(assembly, Vector2.zero, Vector2.one * 414, new Color32(93, 65, 45, 255), true);
        Box(assembly, Vector2.zero, Vector2.one * 400, new Color32(169, 136, 79, 255), true);
        Box(assembly, Vector2.zero, Vector2.one * 389, Ink, true);
        rotor = Rect(assembly, "Wheel rotor", Vector2.zero, Vector2.one * 376);
        wheel = rotor.gameObject.AddComponent<RouletteWheelGraphic>(); wheel.raycastTarget = false;
        Box(assembly, Vector2.zero, Vector2.one * 242, new Color32(52, 46, 39, 255), true);
        Box(rotor, Vector2.zero, new Vector2(180, 9), new Color32(169, 136, 79, 255));
        Box(rotor, Vector2.zero, new Vector2(9, 180), new Color32(169, 136, 79, 255));
        Box(assembly, Vector2.zero, Vector2.one * 128, new Color32(102, 74, 49, 255), true);
        Box(assembly, Vector2.zero, Vector2.one * 110, Ink, true);
        Result = Label(assembly, "", Vector2.zero, new Vector2(100, 64), 30, Gold);
        Result.enableAutoSizing = true; Result.fontSizeMin = 14; Result.fontSizeMax = 30;
        ballGraphic = Box(assembly, Vector2.zero, Vector2.one * 11, Color.white, true).rectTransform;
    }
    protected override void BeginRound()
    {
        Session = new RouletteSession(CurrencyManager.Instance); random = new System.Random();
        SelectedAmount = Mathf.Clamp(SelectedAmount, 1, Mathf.Max(1, MaximumStake()));
        wheelAngle = 0; ballAngle = Session.LastNumber < 0 ? 90 : RouletteWheelGraphic.PocketAngle(Array.IndexOf(RouletteSession.Wheel, Session.LastNumber));
        resultPulse = 0; wheel.Highlight(Session.LastNumber);
        DrawWheel(Session.LastNumber < 0 ? 201 : 143);
    }
    private int MaximumStake() => (int)Math.Min(Session.Balance, (int.MaxValue - (long)Session.Balance) / (RouletteSession.Multiplier(SelectedColour) - 1));
    public void SelectColour(int colour)
    {
        if (!IsOpen || !IsRunning || Session.Spinning || colour < RouletteSession.Red || colour > RouletteSession.Green) return;
        SelectedColour = colour;
        SelectedAmount = Mathf.Clamp(SelectedAmount, 1, Mathf.Max(1, MaximumStake()));
        Play(placeClip, .2f); RefreshStatus();
    }
    public void SetAmount(int amount)
    {
        if (!IsOpen || !IsRunning || Session.Spinning) return;
        SelectedAmount = Mathf.Clamp(amount, 1, Mathf.Max(1, MaximumStake()));
        Play(placeClip, .2f); RefreshStatus();
    }
    public void Spin()
    {
        if (!IsRunning || Session.Spinning) return;
        if (amountInput.isFocused && long.TryParse(amountInput.text, out long entered))
            SetAmount((int)Math.Max(1, Math.Min(int.MaxValue, entered)));
        // Every sector is equally likely. Animation, skipping and exit never reroll.
        outcome = RouletteSession.Wheel[random.Next(RouletteSession.Wheel.Length)];
        if (!Session.StartSpin(SelectedColour, SelectedAmount, outcome)) return;
        amountInput.DeactivateInputField();
        wheelStart = Mathf.Repeat(wheelAngle, 360); ballStart = Mathf.Repeat(ballAngle, 360);
        wheelEnd = wheelStart - 720 - (float)random.NextDouble() * 360;
        float pocket = RouletteWheelGraphic.PocketAngle(Array.IndexOf(RouletteSession.Wheel, outcome));
        ballEnd = ballStart + 1440 + Mathf.Repeat(wheelEnd + pocket - ballStart, 360);
        elapsed = 0; nextTick = 0; resultPulse = 0;
        wheel.Highlight(-1); RefreshStatus();
    }
    public void SkipSpin()
    {
        if (!IsRunning || !Session.Spinning) return;
        wheelAngle = wheelEnd; ballAngle = ballEnd; DrawWheel(143);
        if (!Session.Resolve(outcome)) return;
        resultPulse = .8f; wheel.Highlight(outcome);
        if (Session.LastDelta > 0) Play(winClip, .3f); else Play(placeClip, .2f);
        RefreshStatus();
    }
    protected override void OnScreenClosing()
    {
        Session?.Close();
        if (focused && PresentationCamera != null) PresentationCamera.EndWorldBoundsFocus(this);
        if (stationLabel != null) stationLabel.SetActive(labelWasActive);
        focused = false;
    }
    private void OnApplicationQuit() => Session?.Close();
    protected override void Tick(float dt)
    {
        focusTime = Mathf.Min(1, focusTime + dt / .38f);
        float focus = Mathf.SmoothStep(0, 1, focusTime);
        PresentationCamera.BeginWorldBoundsFocus(this, Vector2.Lerp(cameraStart, tableCenter, focus), Mathf.Lerp(cameraSize, focusSize, focus));
        tableFade.alpha = focus;
        resultPulse = Mathf.Max(0, resultPulse - dt);
        if (Input.GetKeyDown(KeyCode.Space) && !amountInput.isFocused) { if (Session.Spinning) SkipSpin(); else Spin(); }
        if (!Session.Spinning) return;
        elapsed += dt;
        float t = Mathf.Clamp01(elapsed / 3.2f);
        // Integral of a smooth speed hump: start at rest, accelerate, then coast
        // down to the exact preselected pocket without a final corrective jump.
        float eased = t * t * t * (t * (t * 6 - 15) + 10);
        wheelAngle = Mathf.Lerp(wheelStart, wheelEnd, eased);
        ballAngle = Mathf.Lerp(ballStart, ballEnd, eased);
        DrawWheel(Mathf.Lerp(201, 143, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, .95f, t))));
        nextTick -= dt;
        if (nextTick <= 0) { Play(tickClip, .13f); nextTick = Mathf.Lerp(.025f, .24f, Mathf.Pow(Mathf.Abs(t * 2 - 1), 2)); }
        if (t >= 1) SkipSpin();
    }
    private void DrawWheel(float radius)
    {
        rotor.localEulerAngles = new Vector3(0, 0, wheelAngle);
        ballGraphic.anchoredPosition = RouletteWheelGraphic.Polar(ballAngle, radius);
    }
    private static Color Colour(int colour) => RouletteWheelGraphic.NumberColor(colour == RouletteSession.Green ? 0 : colour == RouletteSession.Red ? 1 : 2);
    protected override void RefreshStatus()
    {
        if (Session == null) return;
        bool spinning = Session.Spinning;
        Status.text = L($"Золото {Session.Balance:N0}", $"Gold {Session.Balance:N0}");
        spinButton.interactable = spinning || Session.CanStake(SelectedColour, SelectedAmount);
        spinText.text = spinning ? "»" : L("Крутить", "Spin");
        amountInput.interactable = !spinning;
        if (!amountInput.isFocused) amountInput.SetTextWithoutNotify(SelectedAmount.ToString());
        foreach (var button in amountButtons) button.interactable = !spinning;
        for (int i = 0; i < colourButtons.Length; i++)
        {
            colourButtons[i].interactable = !spinning;
            colourFrames[i].color = i == SelectedColour ? Gold : new Color32(65, 29, 31, 255);
        }
        Result.text = spinning || Session.LastNumber < 0 ? "" : Session.LastDelta.ToString("+0;-0;0");
        Result.color = Session.LastDelta > 0 ? Gold : new Color32(224, 156, 140, 255);
        float pulse = resultPulse * Mathf.Abs(Mathf.Sin(resultPulse * 20));
        Result.rectTransform.localScale = Vector3.one * (1 + pulse * .18f);
    }
    private void Play(AudioClip clip, float volume)
    {
        if (sounds == null || clip == null) return;
        AudioService.Instance?.RouteExternalSource(sounds, AudioCategory.UI);
        // Existing UI mixer owns user volume and transition fades.
        sounds.PlayOneShot(clip, volume);
    }
    private static AudioClip MakeSound(string name, float duration, float frequency, bool chord)
    {
        const int rate = 22050;
        var data = new float[Mathf.CeilToInt(rate * duration)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate, envelope = Mathf.Exp(-t * (chord ? 13 : 75)) * Mathf.Min(1, t * 800);
            float wave = Mathf.Sin(2 * Mathf.PI * frequency * t) + .35f * Mathf.Sin(2 * Mathf.PI * frequency * (chord ? 1.33333f : 2.47f) * t);
            if (!chord) wave = .45f * wave + .55f * (Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.54f, 2) - 1);
            if (chord) wave += .3f * Mathf.Sin(2 * Mathf.PI * frequency * 1.66667f * t);
            data[i] = wave * envelope * .35f;
        }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
    }
    private void OnDestroy()
    {
        if (placeClip != null) Destroy(placeClip);
        if (tickClip != null) Destroy(tickClip);
        if (winClip != null) Destroy(winClip);
    }
}
