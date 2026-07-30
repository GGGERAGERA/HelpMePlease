using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class WorldEventRewardChest : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private bool opened;
    private bool improved;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    public void Initialize(bool isImproved, DoubleOrLeave rewardChoice)
    {
        improved = isImproved;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (opened || !other.CompareTag(playerTag))
            return;

        if (UpgradeManager.Instance == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[WorldEventRewardChest] UpgradeManager is not available."
            );
#endif
            Destroy(gameObject);
            return;
        }

        opened = true;
        UpgradeManager.Instance.ShowChestRewardChoices(
            choiceCount: 3,
            guaranteeBehavior: improved,
            onClosed: () => Destroy(gameObject)
        );
    }
}
