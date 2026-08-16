using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballPingPongMover : MonoBehaviour
{
    [SerializeField] private Transform leftPoint;
    [SerializeField] private Transform rightPoint;
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField] private bool startTowardRight = true;

    private bool movingRight;

    public void Configure(Transform left, Transform right, float speed, bool towardRight)
    {
        leftPoint = left;
        rightPoint = right;
        moveSpeed = Mathf.Max(0f, speed);
        startTowardRight = towardRight;
        movingRight = towardRight;
    }

    private void OnEnable()
    {
        movingRight = startTowardRight;
    }

    private void Update()
    {
        if (leftPoint == null || rightPoint == null || moveSpeed <= 0f)
            return;

        Vector3 destination = movingRight ? rightPoint.position : leftPoint.position;
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            moveSpeed * Time.deltaTime);

        if ((transform.position - destination).sqrMagnitude <= 0.0001f)
            movingRight = !movingRight;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (leftPoint == null || rightPoint == null)
            return;

        Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(leftPoint.position, rightPoint.position);
        Gizmos.DrawWireSphere(leftPoint.position, 0.15f);
        Gizmos.DrawWireSphere(rightPoint.position, 0.15f);
    }
#endif
}
