using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class AnomalyPinball
{
    // Authored routes, rather than a random obstacle scatter. These polylines
    // are shared by rendering and the physical contact solver.
    private readonly Vector2[][] rails =
    {
        new[] { new Vector2(8, 0), new(8, 590), new(36, 632), new(490, 632), new(552, 590), new(552, 0) },
        new[] { new Vector2(508, 0), new(508, 520) },
        new[] { new Vector2(8, 160), new(80, 120), new(165, 60) },
        new[] { new Vector2(508, 160), new(438, 120), new(355, 60) },
        new[] { new Vector2(80, 200), new(82, 450), new(100, 530), new(140, 575) },
        new[] { new Vector2(440, 200), new(438, 450), new(420, 530), new(390, 575) },
        new[] { new Vector2(220, 558), new(220, 608) },
        new[] { new Vector2(300, 558), new(300, 608) }
    };
    private readonly Vector2[][] slings =
    {
        new[] { new Vector2(100, 110), new(146, 210), new(150, 112) },
        new[] { new Vector2(370, 112), new(374, 210), new(420, 110) }
    };
    private readonly Vector2[] bumpers = { new(186, 488), new(334, 488), new(260, 422) };
    private readonly Vector2[] posts = { new(135, 300), new(385, 300) };
    private readonly Vector2[] targets = { new(180, 612), new(260, 612), new(340, 612) };
    private readonly Vector2[] zones = { new(175, 355), new(355, 345) };
    private readonly Vector2[] portals = { new(145, 540), new(400, 245) };
    private readonly Vector2[] portalOut = { new(.8f, -.6f), new(-.75f, .6614378f) };
    private readonly Vector2[] coinSpots = { new(260, 556), new(115, 430), new(406, 440), new(260, 365), new(212, 245), new(310, 225) };
    private readonly Vector2 spinnerCenter = new(260, 300);

    private readonly Image[] bumperArt = new Image[3], zoneArt = new Image[2], portalArt = new Image[2], ballIcons = new Image[3], targetArt = new Image[3], targetLamps = new Image[3], rewardSlots = new Image[MaxCoinRewards];
    private readonly RectTransform[,] zoneMotes = new RectTransform[2, 8], trails = new RectTransform[2, 6];
    private readonly Vector2[,] trailPoints = new Vector2[2, 6];
    private readonly Image[,] routeLights = new Image[2, 5], slingEdges = new Image[2, 3];
    private RectTransform board, ballArt, bonusArt, leftArt, rightArt, coinArt, coinTimer, spring, spinnerArt, multiballBar;
    private TMP_Text scoreText, bestText, launchText, rewardText;
    private Button launch;
    private bool coinActive;
    private static Vector2 Local(Vector2 point) => point - new Vector2(280, 320);

    protected override void BuildChrome()
    {
        Label(Content, L("ПИНБОЛ", "PINBALL"), new Vector2(-408, 284), new Vector2(270, 40), 22, Mint);
        Label(Content, L("A / D · рычаги\nПробел · пуск", "A / D · flippers\nSpace · launch"),
            new Vector2(-422, 206), new Vector2(270, 80), 16, Color.white);
        Result = Label(Content, "", new Vector2(424, 50), new Vector2(260, 190), 18, Gold);
        Box(Content, new Vector2(0, -6), new Vector2(588, 664), new Color32(96, 74, 49, 255));
        Box(Content, new Vector2(0, -6), new Vector2(576, 652), new Color32(39, 43, 39, 255));
        Status = Label(Content, "", new Vector2(-385, 344), new Vector2(250, 36), 20, Gold);
        scoreText = Label(Content, "", new Vector2(-105, 344), new Vector2(250, 36), 22, Color.white);
        bestText = Label(Content, "", new Vector2(165, 344), new Vector2(250, 36), 19, Gold);
        for (int i = 0; i < ballIcons.Length; i++)
            ballIcons[i] = Box(Content, new Vector2(330 + i * 23, 344), Vector2.one * 14, Color.white, true);
        Button(Content, L("ВЫХОД  ESC", "EXIT  ESC"), new Vector2(471, 344), new Vector2(170, 38), Close);
    }

    protected override void BuildGame()
    {
        board = Rect(Content, "Pinball field", new Vector2(0, -6), new Vector2(560, 640));
        Box(board, Vector2.zero, new Vector2(560, 640), new Color32(25, 38, 38, 255));
        foreach (var path in rails)
            for (int i = 1; i < path.Length; i++) Segment(Box(board, Vector2.zero, Vector2.one, new Color32(129, 164, 151, 255)).rectTransform, path[i - 1], path[i], 5);
        Box(board, Local(new(260, 7)), new Vector2(108, 3), new Color32(146, 74, 60, 255));
        for (int i = 0; i < 2; i++)
        {
            Color color = i == 0 ? Purple : Mint;
            zoneArt[i] = Box(board, Local(zones[i]), Vector2.one * ZoneRadius * 2, color * new Color(1, 1, 1, .1f), true);
            Box(board, Local(zones[i]), Vector2.one * 14, color, true);
            Box(board, Local(zones[i]), Vector2.one * 7, Ink, true);
            for (int j = 0; j < 8; j++)
            {
                var mote = Rect(board, "Field flow", Vector2.zero, Vector2.one);
                var a = Box(mote, new Vector2(-2, 2), new Vector2(6, 2), color).rectTransform;
                var b = Box(mote, new Vector2(-2, -2), new Vector2(6, 2), color).rectTransform;
                a.localEulerAngles = new Vector3(0, 0, -45); b.localEulerAngles = new Vector3(0, 0, 45);
                zoneMotes[i, j] = mote;
            }
            var triangle = slings[i];
            FillTriangle(triangle, new Color32(96, 62, 45, 255));
            for (int edge = 0; edge < 3; edge++)
            {
                slingEdges[i, edge] = Box(board, Vector2.zero, Vector2.one, Gold);
                Segment(slingEdges[i, edge].rectTransform, triangle[edge], triangle[(edge + 1) % 3], 4);
            }
            for (int j = 0; j < 5; j++)
                routeLights[i, j] = Box(board, Local(new Vector2(i == 0 ? 46 : 470, 212 + j * 67)), new Vector2(12, 4), new Color32(64, 102, 102, 255));
        }
        for (int i = 0; i < bumpers.Length; i++)
        {
            Box(board, Local(bumpers[i]), Vector2.one * 49, Ink, true);
            bumperArt[i] = Box(board, Local(bumpers[i]), Vector2.one * 42, Gold, true);
            Box(board, Local(bumpers[i]), Vector2.one * 14, Ink, true);
        }
        foreach (var point in posts)
        {
            Box(board, Local(point), Vector2.one * 21, Mint, true);
            Box(board, Local(point), Vector2.one * 9, Ink, true);
        }
        for (int i = 0; i < targets.Length; i++)
        {
            Box(board, Local(targets[i]), new Vector2(30, 17), Ink);
            targetArt[i] = Box(board, Local(targets[i]), new Vector2(25, 10), Mint);
            targetLamps[i] = Box(board, Local(targets[i] + Vector2.down * 15), Vector2.one * 7, Gold, true);
        }
        multiballBar = Box(board, Local(new Vector2(260, 545)), new Vector2(74, 3), Mint).rectTransform;
        for (int i = 0; i < portals.Length; i++)
        {
            portalArt[i] = Box(board, Local(portals[i]), Vector2.one * PortalRadius * 2, new Color32(93, 178, 237, 255), true);
            Box(board, Local(portals[i]), Vector2.one * (PortalRadius * 2 - 6), Ink, true);
            for (int j = 0; j < 3; j++)
                Box(board, Local(portals[i]) + new Vector2(-5 + j * 5, 0), Vector2.one * 2, new Color32(93, 178, 237, 255));
        }
        spinnerArt = Rect(board, "Rotating reflector", Local(spinnerCenter), Vector2.one * 68);
        Box(spinnerArt, Vector2.zero, new Vector2(68, 7), new Color32(214, 151, 99, 255));
        Box(spinnerArt, new Vector2(-31, 0), Vector2.one * 9, Gold, true);
        Box(spinnerArt, new Vector2(31, 0), Vector2.one * 9, Gold, true);
        Box(board, Local(spinnerCenter), Vector2.one * 13, Ink, true);
        Box(board, Local(spinnerCenter), Vector2.one * 6, Color.white, true);

        coinArt = Rect(board, "Gold coin", Vector2.zero, Vector2.one * 26);
        Box(coinArt, Vector2.zero, Vector2.one * 26, Gold, true);
        Box(coinArt, Vector2.zero, Vector2.one * 20, new Color32(191, 123, 45, 255), true);
        Box(coinArt, new Vector2(-3, 3), new Vector2(3, 10), new Color32(255, 238, 166, 255));
        coinTimer = Box(coinArt, new Vector2(0, -18), new Vector2(24, 2), Gold).rectTransform;
        leftArt = Box(board, Vector2.zero, new Vector2(FlipperLength, 10), Mint).rectTransform;
        rightArt = Box(board, Vector2.zero, new Vector2(FlipperLength, 10), Mint).rectTransform;
        spring = Box(board, Local(new(532, 17)), new Vector2(25, 7), Gold).rectTransform;
        for (int i = 0; i < rewardSlots.Length; i++) rewardSlots[i] = Box(board, Local(new Vector2(532, 136 + i * 12)), Vector2.one * 5, Gold, true);
        for (int ball = 0; ball < 2; ball++)
            for (int i = 5; i >= 0; i--)
                trails[ball, i] = Box(board, Vector2.zero, Vector2.one * (Radius * 2 - i), new Color(ball == 0 ? 1 : .45f, 1, 1, .18f - i * .023f), true).rectTransform;
        ballArt = Box(board, Vector2.zero, Vector2.one * Radius * 2, Color.white, true).rectTransform;
        bonusArt = Box(board, Vector2.zero, Vector2.one * Radius * 2, new Color32(112, 244, 244, 255), true).rectTransform;
        rewardText = Label(board, "+10", Vector2.zero, new Vector2(75, 28), 21, Gold);
        launch = Button(Content, "", new Vector2(0, -354), new Vector2(290, 42), () => Launch(.8f), false);
        launchText = launch.GetComponentInChildren<TMP_Text>();
        var left = Button(Content, "A", new Vector2(-245, -354), new Vector2(140, 42), () => { }, false);
        var right = Button(Content, "D", new Vector2(245, -354), new Vector2(140, 42), () => { }, false);
        Hold(left, v => mouseLeft = v); Hold(right, v => mouseRight = v);
        sounds = Content.gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false; sounds.spatialBlend = 0; sounds.ignoreListenerPause = true;
        AudioService.Instance?.RouteExternalSource(sounds, AudioCategory.UI);
        if (rewardClip == null) rewardClip = MakeSound(true);
        if (clickClip == null) clickClip = MakeSound(false);
    }

    private void FillTriangle(Vector2[] points, Color color)
    {
        float minY = Mathf.Min(points[0].y, points[1].y, points[2].y);
        float maxY = Mathf.Max(points[0].y, points[1].y, points[2].y);
        for (float y = minY + 2; y < maxY; y += 4)
        {
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            for (int i = 0; i < 3; i++)
            {
                var a = points[i]; var b = points[(i + 1) % 3];
                if (y < Mathf.Min(a.y, b.y) || y > Mathf.Max(a.y, b.y) || Mathf.Abs(a.y - b.y) < .001f) continue;
                float x = Mathf.Lerp(a.x, b.x, (y - a.y) / (b.y - a.y));
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            }
            if (maxX > minX) Box(board, Local(new Vector2((minX + maxX) * .5f, y)), new Vector2(Mathf.Round(maxX - minX), 4), color);
        }
    }
    private void Segment(RectTransform graphic, Vector2 a, Vector2 b, float width)
    {
        graphic.anchoredPosition = Local((a + b) * .5f);
        graphic.sizeDelta = new Vector2(Vector2.Distance(a, b), width);
        graphic.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
    }
    private void ResetTrail()
    {
        for (int i = 0; i < 6; i++) { trailPoints[0, i] = primary.Position; trailPoints[1, i] = secondary.Position; }
    }
    private void Draw()
    {
        ballArt.anchoredPosition = Local(new Vector2(Mathf.Round(primary.Position.x), Mathf.Round(primary.Position.y)));
        ballArt.gameObject.SetActive(IsRunning);
        bonusArt.anchoredPosition = Local(secondary.Position);
        bonusArt.gameObject.SetActive(bonusActive && IsRunning);
        Segment(leftArt, new(165, 60), Tip(true, leftAngle), 10);
        Segment(rightArt, new(355, 60), Tip(false, rightAngle), 10);
        for (int ball = 0; ball < 2; ball++)
            for (int i = 5; i >= 0; i--)
            {
                trails[ball, i].gameObject.SetActive(paid && !waiting && IsRunning && (ball == 0 || bonusActive));
                trails[ball, i].anchoredPosition = Local(trailPoints[ball, i]);
                trailPoints[ball, i] = i == 0 ? ball == 0 ? primary.Position : secondary.Position : trailPoints[ball, i - 1];
            }
        for (int i = 0; i < bumperArt.Length; i++) bumperArt[i].color = cooldown[i] > .04f ? Color.white : Gold;
        for (int i = 0; i < zoneArt.Length; i++)
        {
            bool active = zonePulse[i] > 0;
            Color color = i == 0 ? Purple : Mint;
            zoneArt[i].color = color * new Color(1, 1, 1, active ? .2f : .08f);
            for (int j = 0; j < 8; j++)
            {
                float angle = (j * 45 + elapsed * (i == 0 ? 15 : -15)) * Mathf.Deg2Rad;
                float phase = Mathf.Repeat(elapsed * .8f + j * .13f, 1);
                float radius = Mathf.Lerp(12, ZoneRadius - 3, i == 0 ? 1 - phase : phase);
                zoneMotes[i, j].anchoredPosition = Local(zones[i]) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                zoneMotes[i, j].localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg + (i == 0 ? 180 : 0));
            }
            for (int edge = 0; edge < 3; edge++) slingEdges[i, edge].color = slingFlash[i] > 0 ? Color.white : Gold;
            for (int j = 0; j < 5; j++) routeLights[i, j].color = routeFlash[i] > 0 ? Mint : new Color32(64, 102, 102, 255);
        }
        for (int i = 0; i < targets.Length; i++)
        {
            targetArt[i].color = targetDown[i] ? new Color32(50, 64, 64, 255) : Mint;
            targetLamps[i].color = targetDown[i] ? Color.white : new Color32(83, 78, 50, 255);
        }
        multiballBar.gameObject.SetActive(bonusActive);
        multiballBar.sizeDelta = new Vector2(74 * Mathf.Clamp01(multiballTime / 15), 3);
        spinnerArt.localEulerAngles = new Vector3(0, 0, SpinnerAngle);
        spinnerArt.GetComponentInChildren<Image>().color = spinnerFlash > 0 ? Color.white : new Color32(214, 151, 99, 255);
        for (int i = 0; i < portalArt.Length; i++)
        {
            portalArt[i].color = portalFlash > 0 ? Color.white : new Color32(93, 178, 237, 255);
            portalArt[i].rectTransform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(elapsed * 5));
        }
        for (int i = 0; i < rewardSlots.Length; i++) rewardSlots[i].color = i < MaxCoinRewards - coinsCollected ? Gold : new Color32(53, 62, 61, 255);
        coinArt.gameObject.SetActive(coinActive);
        coinArt.anchoredPosition = Local(coinPosition);
        coinArt.localScale = Vector3.one * (1 + .08f * Mathf.Sin(elapsed * 7));
        coinTimer.sizeDelta = new Vector2(24 * Mathf.Clamp01(coinTime / 10), 2);
        spring.anchoredPosition = Local(new Vector2(532, 17 - charge * 8));
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
        scoreText.text = L($"Очки {Score:N0}", $"Score {Score:N0}");
        bestText.text = L($"Рекорд {Mathf.Max(BestScore, Score):N0}", $"Best {Mathf.Max(BestScore, Score):N0}");
        for (int i = 0; i < ballIcons.Length; i++) ballIcons[i].color = i < lives ? Color.white : new Color32(60, 72, 73, 255);
        launch.gameObject.SetActive(waiting || !IsRunning);
        launch.interactable = (paid && IsRunning || balance >= SessionCost) && (waiting || !IsRunning);
        launchText.text = !IsRunning ? L($"Ещё партия · {SessionCost} золота", $"Play again · {SessionCost} gold") : paid
            ? L("Пуск · Пробел", "Launch · Space") : L($"Старт · {SessionCost} золота", $"Start · {SessionCost} gold");
    }
}
