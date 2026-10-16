using UnityEngine;

/// <summary>A maze command uses the existing bunker gate; an occupied aperture stays open.</summary>
public sealed class FootballMazeDoor : MonoBehaviour
{
    public BunkerGateVisual Gate;
    public BoxCollider2D Aperture;
    public Rigidbody2D Player;
    public Rigidbody2D Ball;
    public int Section;
    public bool InitiallyOpen;
    public SpriteRenderer Status;
    public Color CircuitColor = Color.cyan;
    private bool requestedOpen;
    private readonly Collider2D[] overlaps = new Collider2D[32];
    public bool PendingClose => !requestedOpen && Gate.IsOpen;

    public void Restore() => SetOpen(InitiallyOpen);

    public void SetOpen(bool open)
    {
        requestedOpen = open;
        Apply();
    }

    private void FixedUpdate() => Apply();

    private void Apply()
    {
        bool open = requestedOpen || (Gate.IsOpen && Occupied());
        if (Gate.IsOpen != open) Gate.SetOpen(open);
        if (Status != null)
            Status.color = PendingClose ? Color.Lerp(CircuitColor, Color.white,
                .5f + .5f * Mathf.Sin(Time.unscaledTime * 8f)) : CircuitColor * (open ? 1f : .55f);
    }

    private bool Occupied()
    {
        // The separate aperture remains enabled while the solid door is hidden.
        int count = Physics2D.OverlapBox(Aperture.bounds.center,
            (Vector2)Aperture.bounds.size + Vector2.one * .15f, 0f,
            new ContactFilter2D { useTriggers = false }, overlaps);
        for (int i = 0; i < count; i++)
            if (overlaps[i].attachedRigidbody == Player || overlaps[i].attachedRigidbody == Ball)
                return true;
        return false;
    }
}
