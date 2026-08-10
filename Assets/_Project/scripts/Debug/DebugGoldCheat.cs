using UnityEngine;

public sealed class DebugGoldCheat : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private int amount = 1000;

    private void Awake()
    {
        if (FindFirstObjectByType<Subject42DebugMenu>() == null)
            gameObject.AddComponent<Subject42DebugMenu>();
    }
#endif

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.G))
        {
            if (CurrencyManager.Instance == null)
            {
                Debug.LogError("[DebugGoldCheat] CurrencyManager not found.");
                return;
            }

            CurrencyManager.Instance.AddGold(amount);
            Debug.Log($"[DebugGoldCheat] Added {amount} gold.");
        }
#endif
    }
}
