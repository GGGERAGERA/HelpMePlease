using UnityEngine;

/// <summary>
/// Очищает боевую сцену после завершения уровня.
/// Не начисляет опыт и награды за удалённых врагов.
/// </summary>
public sealed class RunCompletionCleaner : MonoBehaviour
{
    [Header("Enemy cleanup")]
    [SerializeField] private string enemyTag = "Enemy";

    public void ClearRemainingEnemies()
    {
        GameObject[] enemies;

        try
        {
            enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        }
        catch (UnityException)
        {
            Debug.LogError(
                $"[RunCompletionCleaner] Tag '{enemyTag}' does not exist."
            );
            return;
        }

        int removedCount = 0;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null)
                continue;

            Destroy(enemy);
            removedCount++;
        }

        Debug.Log(
            $"[RunCompletionCleaner] Removed enemies: {removedCount}."
        );
    }
}