using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FootballGateScoreZone : MonoBehaviour
{
    [SerializeField] private FootballMinigame minigame;
    [SerializeField, Min(0)] private int points = 20;

    private readonly HashSet<BallRollVisual> ballsInside = new();
    private BoxCollider2D scoreTrigger;

    public void Configure(
        FootballMinigame owner,
        int score,
        Vector2 worldSize)
    {
        minigame = owner;
        points = Mathf.Max(0, score);
        scoreTrigger ??= GetComponent<BoxCollider2D>();
        scoreTrigger.isTrigger = true;
        Vector3 scale = transform.lossyScale;
        scoreTrigger.size = new Vector2(
            worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
        scoreTrigger.offset = Vector2.zero;
    }

    public void ResetContacts()
    {
        ballsInside.Clear();
    }

    private void FixedUpdate()
    {
        if (scoreTrigger == null || ballsInside.Count == 0)
            return;

        ballsInside.RemoveWhere(ball => !OverlapsBall(ball));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (minigame == null || !minigame.IsRunning)
            return;

        BallRollVisual ball = minigame.GetRegisteredBall(other);
        if (ball != null && ballsInside.Add(ball))
            minigame.AddScore(points);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (minigame == null)
            return;

        BallRollVisual ball = minigame.GetRegisteredBall(other);
        if (ball != null)
            ballsInside.Remove(ball);
    }

    private void OnDisable()
    {
        ballsInside.Clear();
    }

    private bool OverlapsBall(BallRollVisual ball)
    {
        if (ball == null || !ball.gameObject.activeInHierarchy)
            return false;

        foreach (Collider2D collider in ball.GetComponentsInChildren<Collider2D>())
        {
            if (collider != null && collider.enabled &&
                scoreTrigger.Distance(collider).isOverlapped)
            {
                return true;
            }
        }
        return false;
    }
}
