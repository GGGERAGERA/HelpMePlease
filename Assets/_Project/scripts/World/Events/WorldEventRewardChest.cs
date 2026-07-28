using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class WorldEventRewardChest : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private bool opened;
    private bool improved;
    private DoubleOrLeave doubleOrLeave;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    public void Initialize(bool isImproved, DoubleOrLeave rewardChoice)
    {
        improved = isImproved;
        doubleOrLeave = rewardChoice;
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

        if (improved)
        {
            opened = true;
            UpgradeManager.Instance.ShowChestRewardChoices(
                choiceCount: 3,
                guaranteeBehavior: true,
                onClosed: () =>
                {
                    doubleOrLeave?.ResetState();
                    Destroy(gameObject);
                }
            );
            return;
        }

        if (doubleOrLeave == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[WorldEventRewardChest] DoubleOrLeave is not available."
            );
#endif
            Destroy(gameObject);
            return;
        }

        opened = doubleOrLeave.BeginRewardChoice(
            takeReward: () =>
            {
                UpgradeManager.Instance.ShowChestRewardChoices(
                    choiceCount: 2,
                    guaranteeBehavior: false,
                    onClosed: () =>
                    {
                        doubleOrLeave.ResetState();
                        Destroy(gameObject);
                    }
                );
            },
            riskReward: () => Destroy(gameObject)
        );
    }
}
