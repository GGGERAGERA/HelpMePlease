using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class FootballGoal : MonoBehaviour
{
    [SerializeField] private FootballMinigame minigame;

    private int ballContacts;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsConfiguredBall(other))
            return;

        ballContacts++;
        if (ballContacts > 1)
            return;

        minigame.OnGoalScored(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsConfiguredBall(other))
            ballContacts = Mathf.Max(0, ballContacts - 1);
    }

    private void OnDisable()
    {
        ballContacts = 0;
    }

    private bool IsConfiguredBall(Collider2D other)
    {
        if (minigame == null || minigame.Ball == null)
            return false;

        return other.GetComponentInParent<BallRollVisual>() == minigame.Ball;
    }
}
