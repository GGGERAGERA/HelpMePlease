using System.Collections.Generic;
using UnityEngine;

public enum FootballScoreZoneType
{
    Green,
    Yellow,
    Red
}

[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public sealed class FootballScoreZone : MonoBehaviour
{
    [SerializeField] private FootballMinigame minigame;

    private CircleCollider2D zoneCollider;
    private SpriteRenderer zoneRenderer;
    private readonly HashSet<BallRollVisual> ballsAwaitingExit = new();

    public FootballScoreZoneType Type { get; private set; }
    public int Points { get; private set; }
    public bool IsAcceptingBalls { get; private set; }
    public int LaneIndex { get; private set; } = -1;

    private FootballPingPongMover laneMover;

    private void Awake()
    {
        zoneCollider = GetComponent<CircleCollider2D>();
        zoneRenderer = GetComponent<SpriteRenderer>();
        zoneCollider.isTrigger = true;
        Hide();
    }

    public void ConfigureOwner(FootballMinigame owner)
    {
        minigame = owner;
    }

    public void ResetContacts()
    {
        ballsAwaitingExit.Clear();
    }

    public void Show(FootballScoreZoneType type, int points, float radius, Color color)
    {
        EnsureComponents();
        Type = type;
        Points = points;
        zoneCollider.radius = radius;
        zoneCollider.enabled = true;
        zoneRenderer.color = color;
        zoneRenderer.size = Vector2.one * radius * 2f;
        zoneRenderer.enabled = true;
        IsAcceptingBalls = true;
    }

    public void ConfigureLane(
        FootballTargetLane lane,
        int laneIndex,
        float moveSpeed,
        bool moveRight)
    {
        if (lane == null || !lane.IsValid)
            return;

        if (laneMover == null)
            laneMover = GetComponent<FootballPingPongMover>();
        if (laneMover == null)
            laneMover = gameObject.AddComponent<FootballPingPongMover>();
        LaneIndex = laneIndex;
        laneMover.Configure(lane.LeftAnchor, lane.RightAnchor, moveSpeed, moveRight);
        transform.position = moveRight ? lane.LeftAnchor.position : lane.RightAnchor.position;
        laneMover.enabled = true;
    }

    public void Hide()
    {
        EnsureComponents();
        IsAcceptingBalls = false;
        LaneIndex = -1;
        zoneCollider.enabled = false;
        zoneRenderer.enabled = false;
        if (laneMover != null)
            laneMover.enabled = false;
    }

    private void FixedUpdate()
    {
        if (zoneCollider == null || !zoneCollider.enabled || ballsAwaitingExit.Count == 0)
            return;

        ballsAwaitingExit.RemoveWhere(ball => !OverlapsBall(ball));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAcceptingBalls || minigame == null || !minigame.IsRunning)
            return;

        BallRollVisual ball = minigame.GetRegisteredBall(other);
        if (ball == null || ballsAwaitingExit.Contains(ball))
            return;

        // Close immediately so simultaneous contacts cannot score twice.
        ballsAwaitingExit.Add(ball);
        IsAcceptingBalls = false;
        zoneCollider.enabled = false;
        minigame.OnBallEnteredScoreZone(this, ball);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (minigame == null)
            return;

        BallRollVisual ball = minigame.GetRegisteredBall(other);
        if (ball != null)
            ballsAwaitingExit.Remove(ball);
    }

    private void EnsureComponents()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<CircleCollider2D>();
        if (zoneRenderer == null)
            zoneRenderer = GetComponent<SpriteRenderer>();
    }

    private bool OverlapsBall(BallRollVisual ball)
    {
        if (ball == null || !ball.gameObject.activeInHierarchy)
            return false;

        foreach (Collider2D collider in ball.GetComponentsInChildren<Collider2D>())
        {
            if (collider != null && collider.enabled &&
                zoneCollider.Distance(collider).isOverlapped)
            {
                return true;
            }
        }
        return false;
    }
}
