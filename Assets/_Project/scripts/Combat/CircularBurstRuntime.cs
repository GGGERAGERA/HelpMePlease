using UnityEngine;

public class CircularBurstRuntime : MonoBehaviour
{
    [SerializeField] private int projectileCount = 16;
    [SerializeField] private float interval = 3f;

    private float timer;
    private Shoot shootWeapon;
    private PlayerCombatModifiers modifiers;

    private void Awake()
    {
        modifiers = GetComponent<PlayerCombatModifiers>();
    }

    private void Update()
    {
        if (modifiers == null || !modifiers.circularBurst)
            return;

        if (shootWeapon == null)
            shootWeapon = GetComponentInChildren<Shoot>();

        if (shootWeapon == null)
            return;

        timer += Time.deltaTime;

        if (timer < interval)
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

            shootWeapon.FireExternalProjectile(direction);
        }
    }
}