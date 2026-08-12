using UnityEngine;

public class CircularBurstRuntime : MonoBehaviour
{
    [SerializeField] private int projectileCount = 16;

    private float timer;
    private BaseWeapon attackWeapon;
    private PlayerCombatModifiers modifiers;

    private void Awake()
    {
        modifiers = GetComponent<PlayerCombatModifiers>();
        attackWeapon = GetComponentInChildren<BaseWeapon>();
    }

    private void Update()
    {
        if (modifiers == null || !modifiers.circularBurst)
            return;

        if (attackWeapon == null)
            attackWeapon = GetComponentInChildren<BaseWeapon>();

        if (attackWeapon == null)
            return;

        timer += Time.deltaTime;

        if (timer < modifiers.circularBurstCooldown)
            return;

        timer = 0f;
        FireBurst();
    }

    private void FireBurst()
    {
        for (int i = 0; i < projectileCount; i++)
        {
            float angle = 360f / projectileCount * i;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;

            attackWeapon.TryEmitExternalAttack(direction);
        }
    }
}
