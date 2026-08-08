using UnityEngine;

public enum FootballScoreZoneType
{
    Green,
    Blue,
    Red
}

[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public sealed class FootballScoreZone : MonoBehaviour
{
    [SerializeField] private FootballMinigame minigame;

    private CircleCollider2D zoneCollider;
    private SpriteRenderer zoneRenderer;

    public FootballScoreZoneType Type { get; private set; }
    public int Points { get; private set; }
    public bool IsAcceptingBalls { get; private set; }

    private void Awake()
    {
        zoneCollider = GetComponent<CircleCollider2D>();
        zoneRenderer = GetComponent<SpriteRenderer>();
        zoneCollider.isTrigger = true;
        Hide();
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

    public void Hide()
    {
        EnsureComponents();
        IsAcceptingBalls = false;
        zoneCollider.enabled = false;
        zoneRenderer.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAcceptingBalls || minigame == null || !minigame.IsRunning)
            return;

        if (!minigame.IsRegisteredBall(other))
            return;

        // Close immediately so simultaneous contacts cannot score twice.
        IsAcceptingBalls = false;
        zoneCollider.enabled = false;
        minigame.OnBallEnteredScoreZone(this);
    }

    private void EnsureComponents()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<CircleCollider2D>();
        if (zoneRenderer == null)
            zoneRenderer = GetComponent<SpriteRenderer>();
    }
}
