using UnityEngine;

public sealed class DebugGoldCheat : MonoBehaviour
{
    [SerializeField] private int amount = 1000;

    private void Update()
    {
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
        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("[DebugBunkerReset] PlayerPrefs cleared.");
        }

    }
}