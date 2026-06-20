using UnityEngine;

public class EnemyDeathExplosionRuntime : MonoBehaviour
{
    private PlayerCombatModifiers modifiers;

    public void Initialize(PlayerCombatModifiers playerModifiers)
    {
        modifiers = playerModifiers;
    }

    private void OnEnable()
    {
        EnemyHealth enemy = GetComponent<EnemyHealth>();

        if (enemy != null)
            enemy.OnDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        EnemyHealth enemy = GetComponent<EnemyHealth>();

        if (enemy != null)
            enemy.OnDied -= HandleEnemyDied;
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        if (modifiers == null)
            return;

        if (!modifiers.enemyDeathExplosion)
            return;

        CombatExplosionService.Explode(
            enemy.transform.position,
            20f,
            modifiers,
            modifiers.enemyMask
        );
    }
}