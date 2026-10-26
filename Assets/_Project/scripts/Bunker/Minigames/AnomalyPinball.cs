using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class AnomalyPinball : ArcadeMinigame
{
    private const float Step = 1f / 240f, Radius = 5, ZoneRadius = 64, PortalRadius = 15, FlipperLength = 82;
    private const int Target = 5000;
    public const int SessionCost = 30, CoinReward = 10, MaxCoinRewards = 6;
    protected override string RecordKey => "BunkerAnomalyPinballBest";
    protected override string Title => "";
    protected override Color ScreenBackdrop => new Color(.025f, .035f, .04f, .78f);

    // Both balls use exactly the same contact solver and table geometry.
    private sealed class BallState
    {
        public Vector2 Position, Velocity, Anchor;
        public float PortalCooldown, RouteCooldown, ZoneTime, CellTime;
        public void Place(Vector2 position, Vector2 velocity)
        {
            Position = Anchor = position; Velocity = velocity;
            PortalCooldown = RouteCooldown = ZoneTime = CellTime = 0;
        }
        public void CopyFrom(BallState other)
        {
            Position = other.Position; Velocity = other.Velocity; Anchor = other.Anchor;
            PortalCooldown = other.PortalCooldown; RouteCooldown = other.RouteCooldown;
            ZoneTime = other.ZoneTime; CellTime = other.CellTime;
        }
    }
    private readonly BallState primary = new(), secondary = new();
    private readonly float[] cooldown = new float[3], slingFlash = new float[2], routeFlash = new float[2], zonePulse = new float[2];
    private readonly bool[] targetDown = new bool[3];
    private Vector2 coinPosition, rewardPosition;
    private float accumulator, elapsed, comboTime, charge, leftAngle, rightAngle, coinTime, nextCoin;
    private float portalFlash, rewardFlash, spinnerFlash, hitSoundCooldown, multiballTime, bankResetTime;
    private int lives, hits, coinsCollected, lastCoinSpot = -1;
    private bool waiting, charging, mouseLeft, mouseRight, paid, bonusActive;
    private AudioSource sounds;
    private AudioClip rewardClip, clickClip;

    public Vector2 BallPosition => primary.Position;
    public Vector2 BallVelocity => primary.Velocity;
    public int BallsRemaining => lives;
    public bool WaitingForLaunch => waiting;
    public int ActiveBallCount => !IsRunning || !paid || waiting ? 0 : bonusActive ? 2 : 1;
    public float MultiballRemaining => bonusActive ? multiballTime : 0;
    private int Level => Mathf.Min(4, 1 + Score / 6000 + (int)(elapsed / 90));
    private int Multiplier => Mathf.Min(5, 1 + hits / 3);
    private float SpeedLimit => 720 + Level * 12;
    private float SpinnerAngle => elapsed * 85;

    protected override void BeginRound()
    {
        lives = 3; hits = coinsCollected = 0; lastCoinSpot = -1;
        accumulator = elapsed = comboTime = charge = coinTime = nextCoin = portalFlash = rewardFlash = spinnerFlash = hitSoundCooldown = multiballTime = bankResetTime = 0;
        mouseLeft = mouseRight = paid = coinActive = bonusActive = false;
        leftAngle = rightAngle = -20;
        System.Array.Clear(cooldown, 0, cooldown.Length);
        System.Array.Clear(targetDown, 0, targetDown.Length);
        System.Array.Clear(slingFlash, 0, slingFlash.Length);
        System.Array.Clear(routeFlash, 0, routeFlash.Length);
        System.Array.Clear(zonePulse, 0, zonePulse.Length);
        PrepareBall();
        Draw();
    }

    private void PrepareBall()
    {
        primary.Place(new Vector2(532, 35), Vector2.zero);
        waiting = true; charging = false; charge = 0; hits = 0; comboTime = 0;
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
        primary.Velocity = new Vector2(-26, Mathf.Lerp(610, 710, Mathf.Clamp01(power)));
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

    // Bound every integration step, including callers outside Update.
    public void Simulate(float dt, bool left, bool right)
    {
        if (!IsRunning || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
        dt = Mathf.Min(dt, .1f);
        while (dt > .000001f && IsRunning)
        {
            float part = Mathf.Min(Step, dt);
            AdvanceStep(part, left, right);
            dt -= part;
        }
    }

    private void AdvanceStep(float dt, bool left, bool right)
    {
        float oldLeft = leftAngle, oldRight = rightAngle;
        leftAngle = Mathf.MoveTowards(leftAngle, left ? 38 : -20, 760 * dt);
        rightAngle = Mathf.MoveTowards(rightAngle, right ? 38 : -20, 760 * dt);
        if (!paid) return;
        elapsed += dt;
        comboTime -= dt; if (comboTime <= 0) hits = 0;
        portalFlash = Mathf.Max(0, portalFlash - dt);
        rewardFlash = Mathf.Max(0, rewardFlash - dt);
        spinnerFlash = Mathf.Max(0, spinnerFlash - dt);
        hitSoundCooldown = Mathf.Max(0, hitSoundCooldown - dt);
        for (int i = 0; i < cooldown.Length; i++) cooldown[i] = Mathf.Max(0, cooldown[i] - dt);
        for (int i = 0; i < 2; i++)
        {
            slingFlash[i] = Mathf.Max(0, slingFlash[i] - dt);
            routeFlash[i] = Mathf.Max(0, routeFlash[i] - dt);
            zonePulse[i] = Mathf.Max(0, zonePulse[i] - dt);
        }
        if (bonusActive)
        {
            multiballTime -= dt;
            if (multiballTime <= 0) EndMultiball();
        }
        if (bankResetTime > 0)
        {
            bankResetTime -= dt;
            if (bankResetTime <= 0) System.Array.Clear(targetDown, 0, targetDown.Length);
        }
        TickCoin(dt);
        if (waiting) return;

        bool mainSurvived = AdvanceBall(primary, dt, oldLeft, oldRight, left, right);
        if (bonusActive && !AdvanceBall(secondary, dt, oldLeft, oldRight, left, right)) EndMultiball();
        if (!mainSurvived)
        {
            if (bonusActive)
            {
                // The remaining bonus ball becomes the normal ball; no life is lost.
                primary.CopyFrom(secondary); EndMultiball(); ResetTrail();
            }
            else
            {
                lives--;
                if (lives == 0) Finish(Score >= Target, "");
                else PrepareBall();
            }
        }
        else if (bonusActive)
        {
            SeparateBalls();
            primary.Velocity = Vector2.ClampMagnitude(primary.Velocity, SpeedLimit);
            secondary.Velocity = Vector2.ClampMagnitude(secondary.Velocity, SpeedLimit);
        }
    }

    private bool AdvanceBall(BallState ball, float dt, float oldLeft, float oldRight, bool left, bool right)
    {
        ball.PortalCooldown = Mathf.Max(0, ball.PortalCooldown - dt);
        ball.RouteCooldown = Mathf.Max(0, ball.RouteCooldown - dt);
        ball.Velocity += Vector2.down * (225 + Level * 9) * dt;
        ApplyAnomalies(ball, dt);
        ball.Velocity = Vector2.ClampMagnitude(ball.Velocity, SpeedLimit);
        ball.Position += ball.Velocity * dt;
        foreach (var path in rails)
            for (int i = 1; i < path.Length; i++) HitSegment(ball, path[i - 1], path[i], 3, Vector2.zero, out _);
        Flipper(ball, true, leftAngle, oldLeft, dt);
        Flipper(ball, false, rightAngle, oldRight, dt);

        for (int i = 0; i < bumpers.Length; i++)
        {
            Vector2 delta = ball.Position - bumpers[i];
            if (delta.sqrMagnitude >= 26 * 26) continue;
            Vector2 normal = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.up;
            ball.Position = bumpers[i] + normal * 26;
            float approach = Vector2.Dot(ball.Velocity, normal);
            if (approach < 0) ball.Velocity -= normal * (1.94f * approach);
            if (cooldown[i] > 0) continue;
            ball.Velocity += normal * 165;
            cooldown[i] = .12f; ScoreHit(100); HitSound(.15f);
        }
        for (int i = 0; i < slings.Length; i++)
        {
            var triangle = slings[i];
            for (int edge = 0; edge < 3; edge++)
            {
                if (!HitSegment(ball, triangle[edge], triangle[(edge + 1) % 3], 4, Vector2.zero, out var normal) || slingFlash[i] > 0) continue;
                ball.Velocity += normal * 175 + Vector2.up * 65;
                slingFlash[i] = .16f; ScoreHit(50); HitSound(.16f);
            }
        }
        foreach (var post in posts)
        {
            Vector2 delta = ball.Position - post;
            if (delta.sqrMagnitude >= 15 * 15) continue;
            Vector2 normal = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.up;
            ball.Position = post + normal * 15;
            float approach = Vector2.Dot(ball.Velocity, normal);
            if (approach < 0) ball.Velocity -= normal * 1.94f * approach;
        }

        var rotor = new Vector2(Mathf.Cos(SpinnerAngle * Mathf.Deg2Rad), Mathf.Sin(SpinnerAngle * Mathf.Deg2Rad));
        var nearest = spinnerCenter + rotor * Mathf.Clamp(Vector2.Dot(ball.Position - spinnerCenter, rotor), -34, 34);
        var arm = nearest - spinnerCenter;
        Vector2 surface = new Vector2(-arm.y, arm.x) * (85 * Mathf.Deg2Rad);
        if (HitSegment(ball, spinnerCenter - rotor * 34, spinnerCenter + rotor * 34, 4, surface, out _) && spinnerFlash <= 0)
        { spinnerFlash = .14f; ScoreHit(75); HitSound(.13f); }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targetDown[i] || !HitSegment(ball, targets[i] - Vector2.right * 12, targets[i] + Vector2.right * 12, 5, Vector2.zero, out _)) continue;
            targetDown[i] = true; ScoreHit(250); HitSound(.2f);
            if (targetDown[0] && targetDown[1] && targetDown[2])
            {
                Score += 1000;
                secondary.Place(new Vector2(260, 552), new Vector2(135, -270));
                secondary.PortalCooldown = .7f;
                bonusActive = true; multiballTime = 15; bankResetTime = 0;
                ResetTrail();
                Play(rewardClip, .28f);
            }
        }
        TryRoute(ball);
        TryPortal(ball);
        if (coinActive && (ball.Position - coinPosition).sqrMagnitude <= 18 * 18) CollectCoin();

        ball.Velocity = Vector2.ClampMagnitude(ball.Velocity, SpeedLimit);
        if ((ball.Position - ball.Anchor).sqrMagnitude > 24 * 24)
        { ball.Anchor = ball.Position; ball.CellTime = 0; }
        else ball.CellTime += dt;
        // Release rare contact loops, but permit deliberate cradling on a held flipper.
        if (ball.CellTime > 2.5f && !(ball.Position.y < 135 && (left || right)))
        {
            ball.Velocity = new Vector2(ball.Position.x < 260 ? 130 : -130, 280);
            ball.CellTime = 0; ball.Anchor = ball.Position;
        }
        if (ball.Position.x > 516 && ball.Position.y < 22)
        {
            if (ball == primary && !bonusActive) { PrepareBall(); return true; }
            return false;
        }
        return ball.Position.y >= -12;
    }

    private void ApplyAnomalies(BallState ball, float dt)
    {
        bool insidePull = false;
        for (int i = 0; i < zones.Length; i++)
        {
            Vector2 delta = zones[i] - ball.Position;
            float distance = delta.magnitude;
            if (distance >= ZoneRadius) continue;
            zonePulse[i] = .2f;
            if (i == 0) insidePull = true;
            if (distance > .01f)
                ball.Velocity += delta / distance * ((i == 0 ? 1 : -1) * Mathf.Min(1300, 1220 + Level * 20) * (1 - distance / ZoneRadius)) * dt;
        }
        ball.ZoneTime = insidePull ? ball.ZoneTime + dt : 0;
        // A long orbit is expelled rather than capturing a ball forever.
        if (ball.ZoneTime > 1.4f)
        {
            Vector2 outward = ball.Position - zones[0];
            if (outward.sqrMagnitude < .01f) outward = Vector2.up;
            ball.Velocity += outward.normalized * 280 + Vector2.up * 120;
            ball.ZoneTime = 0;
        }
    }

    private void TickCoin(float dt)
    {
        if (coinsCollected >= MaxCoinRewards) { coinActive = false; return; }
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
    }
    private void SpawnCoin()
    {
        if (coinsCollected >= MaxCoinRewards) { coinActive = false; return; }
        int spot = Random.Range(0, coinSpots.Length);
        if (spot == lastCoinSpot) spot = (spot + 1) % coinSpots.Length;
        lastCoinSpot = spot; coinPosition = coinSpots[spot];
        coinTime = 10; coinActive = true;
    }
    private void CollectCoin()
    {
        var wallet = CurrencyManager.Instance;
        if (!coinActive || coinsCollected >= MaxCoinRewards || wallet == null) return;
        if (!wallet.TryChangeGoldExact(CoinReward, () =>
            { coinActive = false; coinsCollected++; nextCoin = 5; rewardPosition = coinPosition; rewardFlash = .8f; })) return;
        Score += 250; Play(rewardClip, .35f);
    }
    private void TryRoute(BallState ball)
    {
        if (ball.RouteCooldown > 0 || ball.Velocity.y <= 0 || ball.Position.y < 185 || ball.Position.y > 245) return;
        int route = ball.Position.x is > 22 and < 70 ? 0 : ball.Position.x is > 456 and < 494 ? 1 : -1;
        if (route < 0) return;
        ball.Velocity = new Vector2(route == 0 ? 20 : -20, Mathf.Max(580, ball.Velocity.y));
        ball.RouteCooldown = 1; routeFlash[route] = .7f;
        ScoreHit(200); HitSound(.18f);
    }
    private void TryPortal(BallState ball)
    {
        if (ball.PortalCooldown > 0) return;
        for (int i = 0; i < portals.Length; i++)
        {
            if ((ball.Position - portals[i]).sqrMagnitude > (PortalRadius + Radius) * (PortalRadius + Radius)) continue;
            int exit = 1 - i;
            float speed = Mathf.Clamp(ball.Velocity.magnitude, 340, SpeedLimit);
            ball.Position = portals[exit] + portalOut[exit] * (PortalRadius + Radius + 7);
            ball.Velocity = portalOut[exit] * speed;
            ball.PortalCooldown = .55f; ball.ZoneTime = ball.CellTime = 0; ball.Anchor = ball.Position; portalFlash = .4f;
            ResetTrail(); HitSound(.18f);
            return;
        }
    }
    private void EndMultiball()
    {
        bonusActive = false; multiballTime = 0; bankResetTime = 3;
    }
    private void SeparateBalls()
    {
        Vector2 delta = primary.Position - secondary.Position;
        if (delta.sqrMagnitude >= Radius * Radius * 4) return;
        float distance = delta.magnitude;
        Vector2 normal = distance > .001f ? delta / distance : Vector2.right;
        Vector2 separation = normal * ((Radius * 2 - distance) * .5f);
        primary.Position += separation; secondary.Position -= separation;
        float approach = Vector2.Dot(primary.Velocity - secondary.Velocity, normal);
        if (approach >= 0) return;
        primary.Velocity -= normal * approach; secondary.Velocity += normal * approach;
    }
    private void ScoreHit(int points) { hits++; comboTime = 2; Score += points * Multiplier; }

    private bool HitSegment(BallState ball, Vector2 a, Vector2 b, float thickness, Vector2 surface, out Vector2 normal)
    {
        Vector2 edge = b - a;
        Vector2 nearest = a + edge * Mathf.Clamp01(Vector2.Dot(ball.Position - a, edge) / edge.sqrMagnitude);
        Vector2 delta = ball.Position - nearest;
        float distance = delta.magnitude;
        normal = distance > .001f ? delta / distance : new Vector2(-edge.y, edge.x).normalized;
        if (distance >= Radius + thickness) return false;
        if (distance <= .001f && Vector2.Dot(ball.Velocity - surface, normal) > 0) normal = -normal;
        ball.Position = nearest + normal * (Radius + thickness);
        float approach = Vector2.Dot(ball.Velocity - surface, normal);
        if (approach >= 0) return false;
        ball.Velocity -= normal * (1.94f * approach);
        return true;
    }
    private Vector2 Tip(bool left, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(left ? 165 : 355, 60) + new Vector2((left ? 1 : -1) * Mathf.Cos(radians), Mathf.Sin(radians)) * FlipperLength;
    }
    private void Flipper(BallState ball, bool left, float angle, float oldAngle, float dt)
    {
        Vector2 pivot = new(left ? 165 : 355, 60), tip = Tip(left, angle), edge = tip - pivot;
        float t = Mathf.Clamp01(Vector2.Dot(ball.Position - pivot, edge) / edge.sqrMagnitude);
        Vector2 nearest = pivot + edge * t;
        Vector2 delta = ball.Position - nearest;
        if (delta.sqrMagnitude >= 10 * 10) return;
        Vector2 normal = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.up;
        Vector2 surface = (tip - Tip(left, oldAngle)) * (t / dt);
        ball.Position = nearest + normal * 10;
        float approach = Vector2.Dot(ball.Velocity - surface, normal);
        if (approach >= 0) return;
        ball.Velocity -= normal * 1.94f * approach;
        if (angle > oldAngle + .01f && normal.y > .15f && t > .08f)
        {
            // Early contact sends a cross-table shot; a late contact aims toward the side return.
            float radians = angle * Mathf.Deg2Rad;
            Vector2 aim = new((left ? -1 : 1) * Mathf.Sin(radians), Mathf.Cos(radians));
            aim.x += (left ? 1 : -1) * (t - .5f) * .16f;
            ball.Velocity = aim.normalized * Mathf.Max(ball.Velocity.magnitude, Mathf.Lerp(530, 710, t));
            HitSound(.2f);
        }
    }

    protected override void ShowRoundResult(bool won, string details, bool record)
    {
        coinActive = bonusActive = false; rewardFlash = 0; multiballTime = 0;
        Result.text = L("ПАРТИЯ ОКОНЧЕНА", "ROUND COMPLETE")
            + L($"\n\nОчки: {Score:N0}\nЗолото: +{coinsCollected * CoinReward}",
                $"\n\nScore: {Score:N0}\nGold: +{coinsCollected * CoinReward}")
            + (record ? L("\n\nНОВЫЙ РЕКОРД", "\n\nNEW RECORD") : "");
        Draw();
    }
    private void Hold(Button button, System.Action<bool> set)
    {
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        foreach (var type in new[] { EventTriggerType.PointerDown, EventTriggerType.PointerUp, EventTriggerType.PointerExit })
        {
            var entry = new EventTrigger.Entry { eventID = type };
            bool pressed = type == EventTriggerType.PointerDown;
            entry.callback.AddListener(_ => set(pressed)); trigger.triggers.Add(entry);
        }
    }
    private void HitSound(float volume)
    {
        if (hitSoundCooldown > 0) return;
        Play(clickClip, volume); hitSoundCooldown = .035f;
    }
    private void Play(AudioClip clip, float volume)
    {
        if (sounds == null || clip == null) return;
        AudioService.Instance?.RouteExternalSource(sounds, AudioCategory.UI);
        sounds.PlayOneShot(clip, volume);
        AudioService.Instance?.DebugRecordExternalPlayback(clip, AudioCategory.UI, context: this);
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
