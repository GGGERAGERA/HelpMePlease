using System.Collections.Generic;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FootballGateScoreZone : MonoBehaviour
{
    [SerializeField] private FootballMinigame minigame;
    [SerializeField, Min(0)] private int points = 20;

    [SerializeField] private TMP_Text goalFeedback;
    [SerializeField] private SpriteRenderer goalFlash;
    private float feedbackRemaining;
    private const float FeedbackDuration = 0.85f;

    private void Update()
    {
        if (feedbackRemaining <= 0f) return;
        feedbackRemaining = Mathf.Max(0f, feedbackRemaining - Time.deltaTime);
        float alpha = Mathf.Clamp01(feedbackRemaining / 0.35f);
        goalFeedback.alpha = alpha;
        Color color = goalFlash.color;
        color.a = alpha * 0.35f;
        goalFlash.color = color;
        if (feedbackRemaining == 0f) HideFeedback();
    }

    private void HideFeedback()
    {
        feedbackRemaining = 0f;
        goalFeedback.gameObject.SetActive(false);
        goalFlash.enabled = false;
    }

    private readonly HashSet<BallRollVisual> ballsInside = new();
    private BoxCollider2D scoreTrigger;

    private void Awake() => scoreTrigger = GetComponent<BoxCollider2D>();

    public void ResetContacts()
    {
        ballsInside.Clear();
        HideFeedback();
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
        {
            minigame.AddGoal(points);
            feedbackRemaining = FeedbackDuration;
            goalFeedback.text = $"ГОЛ +{points}";
            goalFeedback.alpha = 1f;
            goalFeedback.gameObject.SetActive(true);
            goalFlash.enabled = true;
        }
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
        HideFeedback();
    }

    private bool OverlapsBall(BallRollVisual ball)
    {
        if (ball == null || !ball.gameObject.activeInHierarchy)
            return false;

        foreach (Collider2D collider in ball.GetComponentsInChildren<Collider2D>())
        {
            if (collider != null && collider.enabled && !collider.isTrigger &&
                scoreTrigger.Distance(collider).isOverlapped)
            {
                return true;
            }
        }
        return false;
    }
}
