using UnityEngine;

[DisallowMultipleComponent]
public sealed class WindRuleIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform arrowTransform;

    private Vector3 baseArrowScale = Vector3.one;

    private void Awake()
    {
        if (arrowTransform != null)
            baseArrowScale = arrowTransform.localScale;
    }

    public void Show(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            Hide();
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        float angle = Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg;

        if (arrowTransform != null)
        {
            arrowTransform.localRotation =
                Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }

    public void Hide()
    {
        if (arrowTransform != null)
            arrowTransform.localScale = baseArrowScale;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}
