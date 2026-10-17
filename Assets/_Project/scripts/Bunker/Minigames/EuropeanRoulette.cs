using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EuropeanRoulette : ArcadeMinigame
{
    protected override string RecordKey => null;
    protected override string Title => "";
    protected override Color ScreenBackdrop => new Color(.025f, .035f, .04f, .88f);
    public RouletteSession Session { get; private set; }
    public int SelectedChip { get; private set; } = 25;
    public int DisplayedBallNumber => RouletteWheelGraphic.NumberAt(wheelAngle, ballAngle);
    private readonly Button[] betButtons = new Button[RouletteSession.BetCount];
    private readonly Image[] frames = new Image[RouletteSession.BetCount], markers = new Image[RouletteSession.BetCount];
    private readonly TMP_Text[] markerLabels = new TMP_Text[RouletteSession.BetCount];
    private readonly Button[] chipButtons = new Button[4];
    private readonly Image[] chipRims = new Image[4];
    private readonly Color[] chipColors = { new Color32(204, 71, 75, 255), new Color32(68, 133, 210, 255), new Color32(57, 174, 123, 255), new Color32(33, 40, 51, 255) };
    private Button spinButton, clearButton, repeatButton;
    private TMP_Text stakeText, wheelResult, spinText;
    private RectTransform rotor, ballGraphic;
    private RouletteWheelGraphic wheel;
    private System.Random random;
    private AudioSource sounds;
    private AudioClip placeClip, tickClip, winClip;
    private float wheelAngle, ballAngle, wheelStart, ballStart, wheelEnd, ballEnd, elapsed, speed = 1, nextTick, noticeUntil, resultPulse;
    private int outcome;
    private string notice;

    protected override void BuildChrome()
    {
        Box(Content, new Vector2(-5, 20), new Vector2(1054, 614), new Color32(99, 79, 55, 255));
        Box(Content, new Vector2(-5, 20), new Vector2(1042, 602), new Color32(29, 33, 32, 255));
        foreach (float x in new[] { -519f, 509f }) foreach (float y in new[] { -274f, 314f })
            Box(Content, new Vector2(x, y), Vector2.one * 6, new Color32(164, 143, 109, 255));
        Status = Label(Content, "", new Vector2(-340, 280), new Vector2(350, 40), 24, Gold);
        stakeText = Label(Content, "", new Vector2(210, 280), new Vector2(320, 40), 20, Color.white);
        Button(Content, L("Выйти", "Exit"), new Vector2(457, 280), new Vector2(100, 36), Close);
        Result = Label(Content, "", new Vector2(-340, -114), new Vector2(340, 48), 20, Gold);
    }
    protected override void BuildGame()
    {
        // Numbers stay available, while the four familiar outside bets take priority.
        for (int n = 1; n <= 36; n++)
        {
            int column = (n - 1) / 3, row = (n - 1) % 3;
            BetCell(n, n.ToString(), new Vector2(-45 + column * 42, 71 + row * 44), new Vector2(40, 42), RouletteWheelGraphic.NumberColor(n), 21);
        }
        BetCell(0, "0", new Vector2(-89, 115), new Vector2(42, 130), RouletteWheelGraphic.NumberColor(0), 24);
        int[] outside = { RouletteSession.Red, RouletteSession.Black, RouletteSession.Even, RouletteSession.Odd };
        for (int i = 0; i < outside.Length; i++)
        {
            int id = outside[i];
            Color c = id == RouletteSession.Red ? RouletteWheelGraphic.NumberColor(1) : id == RouletteSession.Black ? RouletteWheelGraphic.NumberColor(2) : Panel;
            BetCell(id, BetName(id), new Vector2(34 + i % 2 * 263, 13 - i / 2 * 61), new Vector2(257, 52), c, 21);
        }
        int[] secondary = { RouletteSession.Low, RouletteSession.High, RouletteSession.FirstDozen, RouletteSession.SecondDozen, RouletteSession.ThirdDozen };
        for (int i = 0; i < secondary.Length; i++)
            BetCell(secondary[i], BetName(secondary[i]), new Vector2(-43 + i * 105, -104), new Vector2(101, 32), Panel, 15);
        BuildWheel();
        for (int i = 0; i < 4; i++)
        {
            int amount = RouletteSession.Chips[i];
            Vector2 position = new Vector2(-459 + i * 89, -184);
            chipRims[i] = Box(Content, position, Vector2.one * 60, Gold, true);
            chipButtons[i] = Button(Content, "", position, Vector2.one * 50, () => SelectChip(amount), false);
            var body = chipButtons[i].GetComponent<Image>();
            // Reuse the point-filtered disc sprite from the rim.
            body.sprite = chipRims[i].sprite; body.color = chipColors[i];
            chipButtons[i].name = "Chip " + amount;
            chipButtons[i].transition = Selectable.Transition.None;
            for (int j = 0; j < 4; j++)
            {
                var stripe = Box(chipButtons[i].transform, RouletteWheelGraphic.Polar(j * 90, 20), new Vector2(8, 4), Color.white);
                stripe.rectTransform.localEulerAngles = new Vector3(0, 0, j * 90 - 90);
            }
            Label(chipButtons[i].transform, amount.ToString(), Vector2.zero, new Vector2(46, 28), 18, Color.white);
        }
        spinButton = Button(Content, "", new Vector2(330, -220), new Vector2(264, 58), () =>
        { if (Session.Spinning) SkipSpin(); else Spin(); });
        spinText = spinButton.GetComponentInChildren<TMP_Text>(); spinText.fontSize = 24;
        repeatButton = Button(Content, L("Ещё раз", "Again"), new Vector2(81, -220), new Vector2(216, 48), () =>
        {
            if (Session.CanRepeat) { RepeatBets(); Spin(); }
        });
        clearButton = Button(Content, L("Снять", "Clear"), new Vector2(-104, -220), new Vector2(128, 40), ClearBets);
        sounds = Content.gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false; sounds.spatialBlend = 0; sounds.ignoreListenerPause = true;
        AudioService.Instance?.RouteExternalSource(sounds, AudioCategory.UI);
        if (placeClip == null)
        {
            placeClip = MakeSound("Roulette chip", .055f, 650, false);
            tickClip = MakeSound("Roulette ball", .025f, 1300, false);
            winClip = MakeSound("Roulette win", .26f, 660, true);
        }
    }
    private void BetCell(int id, string text, Vector2 position, Vector2 size, Color color, int fontSize)
    {
        frames[id] = Box(Content, position, size + Vector2.one * 2, new Color32(128, 155, 133, 255));
        var button = Button(Content, text, position, size - Vector2.one * 2, () => PlaceBet(id), false);
        button.name = "Bet " + id;
        button.GetComponent<Image>().color = color;
        button.GetComponentInChildren<TMP_Text>().fontSize = fontSize;
        betButtons[id] = button;
        markers[id] = Box(button.transform, new Vector2(size.x * .22f, -size.y * .22f), new Vector2(25, 22), Gold, true);
        markerLabels[id] = Label(markers[id].transform, "", Vector2.zero, new Vector2(34, 22), 11, Ink);
    }
    private void BuildWheel()
    {
        var assembly = Rect(Content, "Wheel", new Vector2(-345, 83), Vector2.one * 414);
        assembly.localScale = Vector3.one * .8f;
        Vector2 center = Vector2.zero;
        Box(assembly, center, Vector2.one * 414, new Color32(93, 65, 45, 255), true);
        Box(assembly, center, Vector2.one * 400, new Color32(169, 136, 79, 255), true);
        Box(assembly, center, Vector2.one * 389, Ink, true);
        rotor = Rect(assembly, "Wheel rotor", center, Vector2.one * 376);
        wheel = rotor.gameObject.AddComponent<RouletteWheelGraphic>(); wheel.raycastTarget = false;
        for (int i = 0; i < RouletteSession.Wheel.Length; i++)
        {
            float angle = RouletteWheelGraphic.PocketAngle(i);
            var text = Label(rotor, RouletteSession.Wheel[i].ToString(), RouletteWheelGraphic.Polar(angle, 166), new Vector2(30, 25), 14, Color.white);
            text.rectTransform.localEulerAngles = new Vector3(0, 0, angle - 90);
        }
        Box(assembly, center, Vector2.one * 242, new Color32(52, 46, 39, 255), true);
        Box(assembly, center, Vector2.one * 195, new Color32(102, 74, 49, 255), true);
        Box(assembly, center, new Vector2(112, 10), Gold);
        Box(assembly, center, new Vector2(10, 112), Gold);
        Box(assembly, center, Vector2.one * 82, Ink, true);
        wheelResult = Label(assembly, "", center, Vector2.one * 74, 34, Gold);
        ballGraphic = Box(assembly, center, Vector2.one * 11, Color.white, true).rectTransform;
    }
    protected override void BeginRound()
    {
        Session = new RouletteSession(CurrencyManager.Instance); random = new System.Random(); SelectedChip = 25;
        wheelAngle = 0; ballAngle = Session.LastNumber < 0 ? 90 : RouletteWheelGraphic.PocketAngle(Array.IndexOf(RouletteSession.Wheel, Session.LastNumber));
        noticeUntil = resultPulse = 0;
        wheel.Highlight(Session.LastNumber);
        DrawWheel(Session.LastNumber < 0 ? 201 : 143);
    }
    public void SelectChip(int amount)
    {
        if (!IsRunning || Session.Spinning || Array.IndexOf(RouletteSession.Chips, amount) < 0) return;
        SelectedChip = amount; Play(placeClip, .2f); RefreshStatus();
    }
    public void PlaceBet(int id)
    {
        if (!IsOpen || !IsRunning || Session.Spinning) return;
        if (!Session.Place(id, SelectedChip))
        {
            notice = L("Недостаточно денег", "Not enough money");
            noticeUntil = Time.unscaledTime + 2;
        }
        else { noticeUntil = 0; Play(placeClip, .4f); }
        RefreshStatus();
    }
    public void ClearBets()
    {
        if (!IsRunning || !Session.Clear()) return;
        RefreshStatus();
    }
    public void RepeatBets()
    {
        if (!IsRunning || !Session.Repeat()) return;
        Play(placeClip, .4f); RefreshStatus();
    }
    public void Spin()
    {
        if (!IsRunning || Session.Spinning || Session.Stake == 0) return;
        // One uniform outcome per spin; skipping and animation speed cannot reroll it.
        outcome = random.Next(37);
        if (!Session.StartSpin(outcome)) return;
        wheelStart = wheelAngle; ballStart = ballAngle;
        wheelEnd = wheelStart - 720 - (float)random.NextDouble() * 360;
        float pocket = RouletteWheelGraphic.PocketAngle(Array.IndexOf(RouletteSession.Wheel, outcome));
        ballEnd = ballStart + 1080 + Mathf.Repeat(wheelEnd + pocket - ballStart, 360);
        elapsed = 0; speed = 1; nextTick = 0;
        wheel.Highlight(-1);
        RefreshStatus();
    }
    public void Accelerate() { if (IsRunning && Session.Spinning) speed = 3; }
    public void SkipSpin()
    {
        if (!IsRunning || !Session.Spinning) return;
        wheelAngle = wheelEnd; ballAngle = ballEnd;
        DrawWheel(143);
        if (!Session.Resolve(outcome)) return;
        resultPulse = .65f; wheel.Highlight(outcome);
        if (Session.LastDelta > 0) Play(winClip, .3f); else Play(placeClip, .2f);
        RefreshStatus();
    }
    protected override void OnScreenClosing() => Session?.Close();
    private void OnApplicationQuit() => Session?.Close();
    protected override void Tick(float dt)
    {
        resultPulse = Mathf.Max(0, resultPulse - dt);
        if (Input.GetKeyDown(KeyCode.Space)) { if (Session.Spinning) SkipSpin(); else Spin(); }
        if (!Session.Spinning) return;
        elapsed += dt * speed;
        float t = Mathf.Clamp01(elapsed / 2.2f);
        float eased = 1 - Mathf.Pow(1 - t, 3);
        wheelAngle = Mathf.Lerp(wheelStart, wheelEnd, eased);
        ballAngle = Mathf.Lerp(ballStart, ballEnd, eased);
        DrawWheel(Mathf.Lerp(201, 143, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, .95f, t))));
        nextTick -= dt;
        if (nextTick <= 0) { Play(tickClip, .16f); nextTick = Mathf.Lerp(.045f, .28f, t); }
        if (t >= 1) SkipSpin();
    }
    private void DrawWheel(float radius)
    {
        rotor.localEulerAngles = new Vector3(0, 0, wheelAngle);
        ballGraphic.anchoredPosition = RouletteWheelGraphic.Polar(ballAngle, radius);
    }
    private string BetName(int id) => id <= 36 ? id.ToString() : id switch
    {
        RouletteSession.Red => L("КРАСНОЕ", "RED"), RouletteSession.Black => L("ЧЁРНОЕ", "BLACK"),
        RouletteSession.Even => L("ЧЁТНОЕ", "EVEN"), RouletteSession.Odd => L("НЕЧЁТНОЕ", "ODD"),
        RouletteSession.Low => "1-18", RouletteSession.High => "19-36",
        RouletteSession.FirstDozen => "1-12", RouletteSession.SecondDozen => "13-24", _ => "25-36"
    };
    protected override void RefreshStatus()
    {
        if (Session == null) return;
        bool spinning = Session.Spinning;
        Status.text = L($"Деньги {Session.Balance:N0}", $"Money {Session.Balance:N0}");
        stakeText.text = L($"Ставка {Session.Stake:N0}", $"Bet {Session.Stake:N0}");
        spinButton.interactable = spinning || Session.Stake > 0;
        spinText.text = spinning ? L("Пропустить", "Skip") : L("Крутить", "Spin");
        repeatButton.interactable = Session.CanRepeat && Session.Stake == 0;
        clearButton.interactable = !spinning && Session.Stake > 0;
        wheelResult.text = spinning ? "" : Session.LastNumber < 0 ? "" : Session.LastNumber.ToString();
        wheelResult.color = Color.Lerp(Gold, Color.white, resultPulse * Mathf.Abs(Mathf.Sin(resultPulse * 20)));
        for (int i = 0; i < betButtons.Length; i++)
        {
            betButtons[i].interactable = !spinning;
            bool won = !spinning && Session.LastNumber >= 0 && RouletteSession.ReturnMultiplier(i, Session.LastNumber) > 0;
            frames[i].color = won ? Gold : new Color32(128, 155, 133, 255);
            int amount = Session.Bet(i);
            markers[i].gameObject.SetActive(amount > 0);
            markerLabels[i].text = amount.ToString();
        }
        for (int i = 0; i < chipButtons.Length; i++)
        {
            chipButtons[i].interactable = !spinning;
            chipRims[i].color = RouletteSession.Chips[i] == SelectedChip ? Gold : new Color32(86, 103, 107, 255);
        }
        Result.color = Session.LastDelta < 0 ? new Color32(222, 132, 112, 255) : Gold;
        Result.text = Time.unscaledTime < noticeUntil ? notice : spinning ? "" : Session.LastNumber >= 0 ? LastResult() : Session.IsBroke ? L("Недостаточно денег", "Not enough money") : "";
    }
    private string LastResult()
    {
        if (Session.LastNumber < 0) return "";
        if (Session.LastDelta > 0) return L($"Выигрыш +{Session.LastDelta}", $"Won +{Session.LastDelta}");
        if (Session.LastDelta < 0) return L($"Потеря {-Session.LastDelta}", $"Lost {-Session.LastDelta}");
        return L("Ставка вернулась", "Bet returned");
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
