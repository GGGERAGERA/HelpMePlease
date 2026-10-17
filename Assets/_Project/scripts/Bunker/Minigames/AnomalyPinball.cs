using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class AnomalyPinball : ArcadeMinigame
{
    private const float Step = 1f / 240f, Radius = 5f;
    private const int Target = 5000;
    public const int SessionCost = 30, CoinReward = 10;
    private const float ZoneRadius = 78, PortalRadius = 17, FlipperLength = 100;
    protected override string RecordKey => "BunkerAnomalyPinballBest";
    protected override string Title => "";
    protected override Color ScreenBackdrop => new Color(.025f, .035f, .04f, .9f);
    private readonly Vector2[] bumpers = { new(194, 350), new(474, 350), new(340, 260), new(160, 180), new(525, 175) };
    private readonly Vector2[] zones = { new(235, 260), new(490, 245) };
    private readonly Vector2[] portals = { new(85, 332), new(590, 260) };
    private readonly Vector2[] portalOut = { new(.8f, -.6f), new(-.8f, .6f) };
    private readonly Vector2[] coinSpots = { new(338, 350), new(135, 286), new(576, 350), new(340, 167), new(280, 205), new(415, 200) };
    private readonly float[] cooldown = new float[5];
    private readonly Image[] bumperArt = new Image[5];
    private readonly Image[] zoneArt = new Image[2];
    private readonly RectTransform[,] zoneMotes = new RectTransform[2, 8];
    private readonly Image[] portalArt = new Image[2], ballIcons = new Image[3];
    private readonly RectTransform[] trail = new RectTransform[6];
    private readonly Vector2[] trailPoints = new Vector2[6];
    private RectTransform board, ballArt, leftArt, rightArt, coinArt, coinTimer, spring;
    private TMP_Text scoreText, priceText, launchText, rewardText;
    private Button launch;
    private Vector2 ball, velocity, coinPosition, rewardPosition;
    private float accumulator, elapsed, comboTime, charge, stuckTime, leftAngle, rightAngle, coinTime, nextCoin, portalCooldown, portalFlash, rewardFlash;
    private int lives, hits;
    private bool waiting, charging, mouseLeft, mouseRight, paid, coinActive;
    private AudioSource sounds;
    private AudioClip rewardClip, clickClip;
    public Vector2 BallPosition => ball;
    public Vector2 BallVelocity => velocity;
    public int BallsRemaining => lives;
    public bool WaitingForLaunch => waiting;
    private int Level => Mathf.Min(6, 1 + Score / 1500 + (int)(elapsed / 45));
    private int Multiplier => Mathf.Min(5, 1 + hits / 2);

    protected override void BuildChrome()
    {
        Box(Content, new Vector2(0, -1), new Vector2(1040, 652), new Color32(96, 74, 49, 255));
        Box(Content, new Vector2(0, -1), new Vector2(1028, 640), new Color32(39, 43, 39, 255));
        Status = Label(Content, "", new Vector2(-420, 344), new Vector2(260, 36), 22, Gold);
        priceText = Label(Content, L("Партия 30", "Play 30"), new Vector2(-150, 344), new Vector2(190, 36), 20, Gold);
        scoreText = Label(Content, "", new Vector2(100, 344), new Vector2(280, 36), 24, Color.white);
        for (int i = 0; i < ballIcons.Length; i++)
            ballIcons[i] = Box(Content, new Vector2(290 + i * 24, 344), Vector2.one * 15, Color.white, true);
        Button(Content, L("Выйти", "Exit"), new Vector2(471, 344), new Vector2(100, 34), Close);
    }

    protected override void BuildGame()
    {
        board = Rect(Content, "Pinball field", new Vector2(0, -1), new Vector2(720, 448));
        board.localScale = Vector3.one * 1.4f;
        Box(board, Vector2.zero, new Vector2(720, 448), new Color32(25, 38, 38, 255));
        // Faint screws and grooves belong to the table, rather than an instruction panel.
        foreach (float x in new[] { 20f, 652f }) foreach (float y in new[] { 146f, 400f })
            Box(board, Local(new Vector2(x, y)), Vector2.one * 4, new Color32(102, 109, 95, 255));
        // All geometry uses the same board coordinates for drawing and collision.
        WallArt(new(8, 0), new(8, 410)); WallArt(new(712, 0), new(712, 410));
        WallArt(new(8, 410), new(44, 440)); WallArt(new(44, 440), new(676, 440));
        WallArt(new(676, 440), new(712, 410));
        WallArt(new(672, 0), new(672, 324));
        WallArt(new(8, 130), new(220, 68)); WallArt(new(672, 130), new(444, 68));
        Box(board, Local(new(332, 7)), new Vector2(180, 3), new Color32(146, 74, 60, 255));
        for (int i = 0; i < 2; i++)
        {
            Color color = i == 0 ? Purple : Mint;
            zoneArt[i] = Box(board, Local(zones[i]), Vector2.one * ZoneRadius * 2, color * new Color(1, 1, 1, .1f), true);
            Box(board, Local(zones[i]), Vector2.one * 14, color, true);
            Box(board, Local(zones[i]), Vector2.one * 7, Ink, true);
            for (int j = 0; j < 8; j++)
            {
                var mote = Rect(board, "Field flow", Vector2.zero, Vector2.one);
                // V-shaped particles travel in the direction of the actual radial force.
                var a = Box(mote, new Vector2(-2, 2), new Vector2(6, 2), color).rectTransform;
                var b = Box(mote, new Vector2(-2, -2), new Vector2(6, 2), color).rectTransform;
                a.localEulerAngles = new Vector3(0, 0, -45); b.localEulerAngles = new Vector3(0, 0, 45);
                zoneMotes[i, j] = mote;
            }
        }
        for (int i = 0; i < bumpers.Length; i++)
        {
            Box(board, Local(bumpers[i]), Vector2.one * 48, Ink, true);
            bumperArt[i] = Box(board, Local(bumpers[i]), Vector2.one * 42, Gold, true);
            Box(board, Local(bumpers[i]), Vector2.one * 14, Ink, true);
        }
        for (int i = 0; i < portals.Length; i++)
        {
            portalArt[i] = Box(board, Local(portals[i]), Vector2.one * PortalRadius * 2, new Color32(93, 178, 237, 255), true);
            Box(board, Local(portals[i]), Vector2.one * (PortalRadius * 2 - 6), Ink, true);
            for (int j = 0; j < 3; j++)
                Box(board, Local(portals[i]) + new Vector2(-5 + j * 5, 0), Vector2.one * 2, new Color32(93, 178, 237, 255));
        }
        coinArt = Rect(board, "Gold coin", Vector2.zero, Vector2.one * 26);
        Box(coinArt, Vector2.zero, Vector2.one * 26, Gold, true);
        Box(coinArt, Vector2.zero, Vector2.one * 20, new Color32(191, 123, 45, 255), true);
        Box(coinArt, new Vector2(-3, 3), new Vector2(3, 10), new Color32(255, 238, 166, 255));
        coinTimer = Box(coinArt, new Vector2(0, -18), new Vector2(24, 2), Gold).rectTransform;
        leftArt = Box(board, Vector2.zero, new Vector2(FlipperLength, 10), Mint).rectTransform;
        rightArt = Box(board, Vector2.zero, new Vector2(FlipperLength, 10), Mint).rectTransform;
        spring = Box(board, Local(new(692, 17)), new Vector2(25, 7), Gold).rectTransform;
        for (int i = trail.Length - 1; i >= 0; i--)
            trail[i] = Box(board, Vector2.zero, Vector2.one * (Radius * 2 - i), new Color(1, 1, 1, .19f - i * .025f), true).rectTransform;
        ballArt = Box(board, Vector2.zero, Vector2.one * Radius * 2, Color.white, true).rectTransform;
        rewardText = Label(board, "+10", Vector2.zero, new Vector2(75, 28), 21, Gold);
        launch = Button(Content, "", new Vector2(0, -348), new Vector2(360, 42), () => Launch(.65f), false);
        launchText = launch.GetComponentInChildren<TMP_Text>();
        var left = Button(Content, "A", new Vector2(-425, -348), new Vector2(206, 38), () => { }, false);
        var right = Button(Content, "D", new Vector2(425, -348), new Vector2(206, 38), () => { }, false);
        Hold(left, v => mouseLeft = v); Hold(right, v => mouseRight = v);
        sounds = Content.gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false; sounds.spatialBlend = 0; sounds.ignoreListenerPause = true;
        AudioService.Instance?.RouteExternalSource(sounds, AudioCategory.UI);
        if (rewardClip == null) rewardClip = MakeSound(true);
        if (clickClip == null) clickClip = MakeSound(false);
    }

    private void Hold(Button button, System.Action<bool> set)
    {
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        foreach (var type in new[] { EventTriggerType.PointerDown, EventTriggerType.PointerUp, EventTriggerType.PointerExit })
        {
            var entry = new EventTrigger.Entry { eventID = type };
            bool pressed = type == EventTriggerType.PointerDown;
            entry.callback.AddListener(_ => set(pressed));
            trigger.triggers.Add(entry);
        }
    }

    protected override void BeginRound()
    {
        lives = 3; hits = 0;
        accumulator = elapsed = comboTime = charge = stuckTime = coinTime = nextCoin = portalCooldown = portalFlash = rewardFlash = 0;
        mouseLeft = mouseRight = paid = coinActive = false;
        leftAngle = rightAngle = -18;
        System.Array.Clear(cooldown, 0, cooldown.Length);
        PrepareBall();
        Draw();
    }

    private void PrepareBall()
    {
        ball = new Vector2(692, 35); velocity = Vector2.zero;
        waiting = true; charging = false; charge = 0; hits = 0; comboTime = 0; stuckTime = 0;
        portalCooldown = 0;
        ResetTrail();
    }

    public void Launch(float power)
    {
        if (!IsOpen) return;
        if (!IsRunning) Replay();
        if (!IsRunning || !waiting) return;
        if (!paid)
        {
            var wallet = CurrencyManager.Instance;
            if (wallet == null || !wallet.TryChangeGoldExact(-SessionCost, () => { paid = true; waiting = false; }))
            { charging = false; charge = 0; return; }
            SpawnCoin();
        }
        waiting = charging = false;
        velocity = new Vector2(-16, Mathf.Lerp(425, 525, Mathf.Clamp01(power)));
        charge = 0;
        Play(clickClip, .3f);
        RefreshStatus();
    }

    protected override void Tick(float dt)
    {
        if (waiting)
        {
            if (Input.GetKeyDown(KeyCode.Space)) { charging = true; charge = 0; }
            if (charging) charge = Mathf.Min(1, charge + dt);
            if (charging && Input.GetKeyUp(KeyCode.Space)) Launch(charge);
        }
        bool left = mouseLeft || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = mouseRight || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        accumulator += dt;
        while (accumulator >= Step && IsRunning)
        {
            Simulate(Step, left, right);
            accumulator -= Step;
        }
        Draw();
    }

    protected override void ShowRoundResult(bool won, string details, bool record)
    {
        coinActive = false;
        coinArt.gameObject.SetActive(false);
        Draw();
    }

    // Small fixed substeps avoid tunnelling and make the simulation independent of
    // frame rate and the suspended bunker's Physics2D settings.
    public void Simulate(float dt, bool left, bool right)
    {
        if (!IsRunning) return;
        float oldLeft = leftAngle, oldRight = rightAngle;
        leftAngle = Mathf.MoveTowards(leftAngle, left ? 28 : -18, 650 * dt);
        rightAngle = Mathf.MoveTowards(rightAngle, right ? 28 : -18, 650 * dt);
        if (!paid) return;
        elapsed += dt;
        portalCooldown = Mathf.Max(0, portalCooldown - dt);
        portalFlash = Mathf.Max(0, portalFlash - dt);
        rewardFlash = Mathf.Max(0, rewardFlash - dt);
        if (coinActive)
        {
            coinTime -= dt;
            if (coinTime <= 0) { coinActive = false; nextCoin = 5; }
        }
        else
        {
            nextCoin -= dt;
            if (nextCoin <= 0) SpawnCoin();
        }
        if (waiting) return;
        comboTime -= dt;
        if (comboTime <= 0) hits = 0;
        for (int i = 0; i < cooldown.Length; i++) cooldown[i] = Mathf.Max(0, cooldown[i] - dt);
        velocity += Vector2.down * (205 + Level * 16) * dt;
        for (int i = 0; i < zones.Length; i++)
        {
            Vector2 delta = zones[i] - ball;
            float distance = delta.magnitude;
            if (distance < ZoneRadius && distance > .01f)
                velocity += delta / distance * ((i == 0 ? 1 : -1) * (1150 + Level * 55) * (1 - distance / ZoneRadius)) * dt;
        }
        velocity = Vector2.ClampMagnitude(velocity, 500 + Level * 28);
        ball += velocity * dt;
        Bounce(new(8, 0), new(8, 410)); Bounce(new(712, 0), new(712, 410));
        Bounce(new(8, 410), new(44, 440)); Bounce(new(44, 440), new(676, 440));
        Bounce(new(676, 440), new(712, 410)); Bounce(new(672, 0), new(672, 324));
        Bounce(new(8, 130), new(220, 68)); Bounce(new(672, 130), new(444, 68));
        Flipper(true, leftAngle, oldLeft, dt); Flipper(false, rightAngle, oldRight, dt);
        for (int i = 0; i < bumpers.Length; i++)
        {
            Vector2 delta = ball - bumpers[i];
            if (delta.sqrMagnitude >= 26 * 26) continue;
            Vector2 normal = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.up;
            ball = bumpers[i] + normal * 26;
            float approach = Vector2.Dot(velocity, normal);
            if (approach < 0) velocity -= normal * (1.9f * approach);
            if (cooldown[i] > 0) continue;
            velocity += normal * (100 + Level * 12);
            cooldown[i] = .16f;
            hits++; comboTime = 2;
            Score += 100 * Multiplier;
        }
        TryPortal();
        if (coinActive && (ball - coinPosition).sqrMagnitude <= 18 * 18)
        {
            // Consume the target before wallet observers run: one hit can only pay once.
            var wallet = CurrencyManager.Instance;
            if (wallet != null && wallet.TryChangeGoldExact(CoinReward, () =>
                { coinActive = false; nextCoin = 5; rewardPosition = coinPosition; rewardFlash = .8f; }))
            {
                Score += 250;
                Play(rewardClip, .35f);
            }
        }
        if (velocity.sqrMagnitude < 100) stuckTime += dt; else stuckTime = 0;
        if (stuckTime > 2.5f) { velocity += new Vector2(45, 125); stuckTime = 0; }
        // Weak launches falling back into the separated plunger lane can be retried.
        if (ball.x > 677 && ball.y < 22) { PrepareBall(); return; }
        if (ball.y < -12)
        {
            lives--;
            if (lives == 0) Finish(Score >= Target, "");
            else PrepareBall();
        }
    }

    private void SpawnCoin()
    {
        // Authored clear spots keep coins away from solids, portals and the plunger lane.
        coinPosition = coinSpots[Random.Range(0, coinSpots.Length)];
        coinTime = 10; coinActive = true;
    }

    private void TryPortal()
    {
        if (portalCooldown > 0) return;
        for (int i = 0; i < portals.Length; i++)
        {
            if ((ball - portals[i]).sqrMagnitude > (PortalRadius + Radius) * (PortalRadius + Radius)) continue;
            int exit = 1 - i;
            float speed = Mathf.Clamp(velocity.magnitude, 250, 500 + Level * 28);
            ball = portals[exit] + portalOut[exit] * (PortalRadius + Radius + 7);
            velocity = portalOut[exit] * speed;
            portalCooldown = .5f; portalFlash = .4f;
            ResetTrail();
            Play(clickClip, .2f);
            return;
        }
    }

    private void Bounce(Vector2 a, Vector2 b, float thickness = 3)
    {
        Vector2 edge = b - a;
        Vector2 nearest = a + edge * Mathf.Clamp01(Vector2.Dot(ball - a, edge) / edge.sqrMagnitude);
        Vector2 delta = ball - nearest;
        float distance = delta.magnitude;
        if (distance >= Radius + thickness) return;
        Vector2 normal = distance > .001f ? delta / distance : new Vector2(-edge.y, edge.x).normalized;
        ball = nearest + normal * (Radius + thickness);
        float speed = Vector2.Dot(velocity, normal);
        if (speed < 0) velocity -= normal * (1.85f * speed);
    }

    private Vector2 Tip(bool left, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(left ? 220 : 444, 68) + new Vector2((left ? 1 : -1) * Mathf.Cos(radians), Mathf.Sin(radians)) * FlipperLength;
    }

    private void Flipper(bool left, float angle, float oldAngle, float dt)
    {
        Vector2 pivot = new(left ? 220 : 444, 68), tip = Tip(left, angle), edge = tip - pivot;
        float t = Mathf.Clamp01(Vector2.Dot(ball - pivot, edge) / edge.sqrMagnitude);
        Vector2 nearest = pivot + edge * t, delta = ball - nearest;
        if (delta.sqrMagnitude >= 10 * 10) return;
        Vector2 normal = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.up;
        Vector2 surface = (tip - Tip(left, oldAngle)) * (t / dt);
        ball = nearest + normal * 10;
        float approach = Vector2.Dot(velocity - surface, normal);
        if (approach < 0) velocity -= normal * (1.8f * approach);
    }

    private static Vector2 Local(Vector2 point) => point - new Vector2(360, 224);
    private void WallArt(Vector2 a, Vector2 b) => Segment(Box(board, Vector2.zero, Vector2.one, Mint).rectTransform, a, b, 5);
    private void Segment(RectTransform graphic, Vector2 a, Vector2 b, float width)
    {
        graphic.anchoredPosition = Local((a + b) * .5f);
        graphic.sizeDelta = new Vector2(Vector2.Distance(a, b), width);
        graphic.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
    }
    private void Draw()
    {
        ballArt.anchoredPosition = Local(new Vector2(Mathf.Round(ball.x), Mathf.Round(ball.y)));
        ballArt.gameObject.SetActive(IsRunning);
        Segment(leftArt, new(220, 68), Tip(true, leftAngle), 10);
        Segment(rightArt, new(444, 68), Tip(false, rightAngle), 10);
        for (int i = trail.Length - 1; i >= 0; i--)
        {
            trail[i].gameObject.SetActive(paid && !waiting && IsRunning);
            trail[i].anchoredPosition = Local(trailPoints[i]);
            trailPoints[i] = i == 0 ? ball : trailPoints[i - 1];
        }
        for (int i = 0; i < bumperArt.Length; i++) bumperArt[i].color = cooldown[i] > .08f ? Color.white : Gold;
        for (int i = 0; i < zoneArt.Length; i++)
        {
            zoneArt[i].rectTransform.localScale = Vector3.one * (1 + .04f * Mathf.Sin(elapsed * 3 + i));
            for (int j = 0; j < 8; j++)
            {
                float angle = (j * 45 + elapsed * (i == 0 ? 10 : -10)) * Mathf.Deg2Rad;
                float phase = Mathf.Repeat(elapsed * .7f + j * .13f, 1);
                float radius = Mathf.Lerp(12, ZoneRadius - 3, i == 0 ? 1 - phase : phase);
                zoneMotes[i, j].anchoredPosition = Local(zones[i]) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                zoneMotes[i, j].localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg + (i == 0 ? 180 : 0));
            }
        }
        for (int i = 0; i < portalArt.Length; i++)
        {
            portalArt[i].color = portalFlash > 0 ? Color.white : new Color32(93, 178, 237, 255);
            portalArt[i].rectTransform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(elapsed * 5));
        }
        coinArt.gameObject.SetActive(coinActive);
        coinArt.anchoredPosition = Local(coinPosition);
        coinArt.localScale = Vector3.one * (1 + .08f * Mathf.Sin(elapsed * 7));
        coinTimer.sizeDelta = new Vector2(24 * Mathf.Clamp01(coinTime / 10), 2);
        spring.anchoredPosition = Local(new Vector2(692, 17 - charge * 8));
        rewardText.gameObject.SetActive(rewardFlash > 0);
        rewardText.rectTransform.anchoredPosition = Local(rewardPosition) + Vector2.up * (20 + (1 - rewardFlash / .8f) * 24);
        rewardText.color = new Color(1, .82f, .4f, Mathf.Clamp01(rewardFlash * 3));
    }
    protected override void RefreshStatus()
    {
        var wallet = CurrencyManager.Instance;
        int balance = wallet != null ? wallet.TotalGold : 0;
        Status.text = L($"Золото {balance:N0}", $"Gold {balance:N0}");
        Status.color = rewardFlash > 0 ? Color.white : Gold;
        priceText.color = paid && IsRunning || balance >= SessionCost ? Gold : new Color32(222, 132, 112, 255);
        scoreText.text = L($"Очки {Score:N0}", $"Score {Score:N0}");
        for (int i = 0; i < ballIcons.Length; i++) ballIcons[i].color = i < lives ? Color.white : new Color32(60, 72, 73, 255);
        launch.gameObject.SetActive(waiting || !IsRunning);
        launch.interactable = (paid && IsRunning || balance >= SessionCost) && (waiting || !IsRunning);
        launchText.text = !IsRunning ? L("Ещё партия · 30", "Play again · 30") : paid
            ? L("Пуск · Пробел", "Launch · Space") : L("Старт · 30 · Пробел", "Start · 30 · Space");
    }

    private void ResetTrail() { for (int i = 0; i < trailPoints.Length; i++) trailPoints[i] = ball; }
    private void Play(AudioClip clip, float volume)
    {
        if (sounds == null || clip == null) return;
        AudioService.Instance?.RouteExternalSource(sounds, AudioCategory.UI);
        sounds.PlayOneShot(clip, volume);
    }
    private static AudioClip MakeSound(bool reward)
    {
        const int rate = 22050;
        var samples = new float[Mathf.CeilToInt(rate * (reward ? .22f : .045f))];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float wave = Mathf.Sin(2 * Mathf.PI * (reward ? 780 : 170) * t);
            wave += reward ? .45f * Mathf.Sin(2 * Mathf.PI * 1170 * t) : .6f * (Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.54f, 2) - 1);
            samples[i] = wave * Mathf.Exp(-t * (reward ? 17 : 90)) * Mathf.Min(1, t * 1000) * .35f;
        }
        var clip = AudioClip.Create(reward ? "Pinball coin" : "Pinball mechanism", samples.Length, 1, rate, false);
        clip.SetData(samples, 0); return clip;
    }
    private void OnDestroy()
    {
        if (rewardClip != null) Destroy(rewardClip);
        if (clickClip != null) Destroy(clickClip);
    }
}
