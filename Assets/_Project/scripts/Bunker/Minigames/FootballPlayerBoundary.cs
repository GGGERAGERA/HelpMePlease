using UnityEngine;

// The original shooting-area barrier is authored in the arena. Only its
// ball exceptions are dynamic, and only registered balls are inspected.
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FootballPlayerBoundary : MonoBehaviour
{
    [SerializeField] private SpriteRenderer boundaryVisual;
    public BoxCollider2D Collider => GetComponent<BoxCollider2D>();
    public SpriteRenderer Visual => boundaryVisual;
    public void IgnoreBall(BallRollVisual ball)
    {
        foreach (var collider in ball.GetComponentsInChildren<Collider2D>(true))
            if (!collider.isTrigger) Physics2D.IgnoreCollision(Collider, collider, true);
    }
}
