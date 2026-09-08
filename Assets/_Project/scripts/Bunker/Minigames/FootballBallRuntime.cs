using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BallRollVisual))]
public sealed class FootballBallRuntime : MonoBehaviour, IAnomalyExternalVelocity
{
    private readonly AnomalyExternalVelocityStack anomalyVelocity = new();
    public Component ExternalVelocityComponent => this;
    public bool IsPhysicalCollider(Collider2D other) => other != null && !other.isTrigger && other.attachedRigidbody == body;
    public void SetAnomalyExternalVelocity(Object source, Vector2 velocity) => anomalyVelocity.Set(source, velocity);
    public void RemoveAnomalyExternalVelocity(Object source) => anomalyVelocity.Remove(source);
    private void OnDisable() => anomalyVelocity.Clear();
    private Rigidbody2D body;
    private BallRollVisual ball;
    private FootballMinigame owner;
    private float stuckDuration;
    private float stillTime;
    private Vector2 progressPosition;
    private float progressTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        ball = GetComponent<BallRollVisual>();
    }
    public void Configure(FootballMinigame minigame, float recoverySeconds)
    {
        owner = minigame;
        stuckDuration = recoverySeconds;
    }
    private void FixedUpdate()
    {
        if (owner == null || !owner.IsRunning) return;
        body.AddForce(anomalyVelocity.Value, ForceMode2D.Force);
        Bounds bounds = owner.PlayBounds;
        Vector2 p = body.position;
        // Recovery only: physical walls contain normal play.
        if (p.x < bounds.min.x - 1f || p.x > bounds.max.x + 1f ||
            p.y < bounds.min.y - 1f || p.y > bounds.max.y + 1f)
        {
            owner.ResetBall(ball);
            return;
        }
        // Do not steal the ball while the player is aiming or dribbling.
        if (ball.InKickRange || ball.IsDribbling)
        {
            stillTime = 0f;
            progressTime = 0f;
            progressPosition = p;
            return;
        }
        stillTime = body.linearVelocity.sqrMagnitude < .04f
            ? stillTime + Time.fixedDeltaTime : 0f;
        progressTime += Time.fixedDeltaTime;
        if ((p - progressPosition).sqrMagnitude > .25f)
        {
            progressTime = 0f;
            progressPosition = p;
        }
        if (stillTime >= stuckDuration || progressTime >= stuckDuration)
            owner.ResetBall(ball);
    }
    public void RespawnNow(Transform spawn)
    {
        // Removing/reinserting physics contacts makes reset work even when the
        // player remains inside the same kick trigger across the teleport.
        ball.gameObject.SetActive(false);
        ball.gameObject.SetActive(true);
        body.simulated = true;
        ball.ResetBall(spawn);
        body.WakeUp();
        owner.RestoreBallBoundary(ball);
        stillTime = 0f;
        progressTime = 0f;
        progressPosition = body.position;
    }
}
