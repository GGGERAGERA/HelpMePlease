using UnityEngine;

public class MenuZombieWalker : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.5f;
    [SerializeField] private Vector2 direction = Vector2.right;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool syncAnimationWithMoveSpeed = true;
    [SerializeField] private float animationSpeed = 1f;
    [SerializeField] private float animationSpeedMultiplier = 2f;

    [Header("Loop")]
    [SerializeField] private float leftX = -12f;
    [SerializeField] private float rightX = 12f;

    [Header("Flip")]
    [SerializeField] private bool spriteFacesRightByDefault = false;
    [SerializeField] private bool flipByDirection = true;

    [Header("Animation State")]
    [SerializeField] private bool useRunAnimation;
    [SerializeField] private string runParameterName = "IsRunning";



    private Vector3 visualStartScale;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (visualRoot == null && animator != null)
            visualRoot = animator.transform;

        if (visualRoot != null)
            visualStartScale = visualRoot.localScale;
    }

    private void Update()
    {
        transform.position += (Vector3)(direction.normalized * Mathf.Abs(moveSpeed) * Time.deltaTime);

        if (animator != null)
        {
            animator.speed = syncAnimationWithMoveSpeed
                ? Mathf.Abs(moveSpeed) * animationSpeedMultiplier
                : animationSpeed;

            if (useRunAnimation)
                animator.SetBool(runParameterName, Mathf.Abs(moveSpeed) > 0f);
        }

        UpdateFlip();
        Wrap();
    }

    private void UpdateFlip()
    {
        if (!flipByDirection || visualRoot == null)
            return;

        if (Mathf.Abs(direction.x) < 0.01f)
            return;

        bool shouldFaceRight = direction.x > 0f;

        float sign = shouldFaceRight ? 1f : -1f;

        if (!spriteFacesRightByDefault)
            sign *= -1f;

        visualRoot.localScale = new Vector3(
            Mathf.Abs(visualStartScale.x) * sign,
            visualStartScale.y,
            visualStartScale.z
        );
    }

    private void Wrap()
    {
        Vector3 pos = transform.position;

        if (direction.x > 0f && pos.x > rightX)
            pos.x = leftX;

        if (direction.x < 0f && pos.x < leftX)
            pos.x = rightX;

        transform.position = pos;
    }
}