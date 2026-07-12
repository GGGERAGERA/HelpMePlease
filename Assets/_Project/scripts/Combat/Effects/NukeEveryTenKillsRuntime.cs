using UnityEngine;

public class NukeEveryTenKillsRuntime : MonoBehaviour
{
    [SerializeField] private float radius = 6f;
    [SerializeField] private float damage = 999f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private ParticleSystem nukeFxPrefab;

    private PlayerCombatModifiers modifiers;
    private int lastTriggeredKillCount;

    private void Awake()
    {
        modifiers = GetComponent<PlayerCombatModifiers>();
    }

    private void Update()
    {
        if (modifiers == null || !modifiers.nukeEveryKills)
            return;

        if (KillManager.Instance == null)
            return;

        int kills = KillManager.Instance.Kills;

        if (kills <= 0)
            return;

        int killsSinceLastNuke = kills - lastTriggeredKillCount;

        if (killsSinceLastNuke < modifiers.nukeKillsRequired)
            return;

        lastTriggeredKillCount = kills;
        DropNuke();
    }

    private void DropNuke()
    {
        Vector2 position = transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            radius,
            enemyMask
        );

        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
                enemy.TakeDamage(damage, position, false);
        }

        if (nukeFxPrefab != null)
        {
            ParticleSystem fx = Instantiate(
                nukeFxPrefab,
                position,
                Quaternion.identity
            );

            fx.Play();
            Destroy(fx.gameObject, fx.main.duration);
        }
    }
}