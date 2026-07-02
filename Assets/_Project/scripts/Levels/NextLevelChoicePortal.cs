using UnityEngine;

public sealed class NextLevelChoicePortal : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        LevelChoiceManager manager = FindFirstObjectByType<LevelChoiceManager>();

        if (manager == null)
        {
            Debug.LogError("[NextLevelChoicePortal] LevelChoiceManager not found.");
            return;
        }

        manager.ShowChoices();
    }
}