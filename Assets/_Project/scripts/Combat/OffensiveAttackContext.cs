using UnityEngine;

public readonly struct OffensiveAttackContext
{
    public readonly float Damage;
    public readonly bool IsCritical;
    public readonly float AttackSizeMultiplier;

    private OffensiveAttackContext(
        float damage,
        bool isCritical,
        float attackSizeMultiplier)
    {
        Damage = damage;
        IsCritical = isCritical;
        AttackSizeMultiplier = attackSizeMultiplier;
    }

    public static OffensiveAttackContext Resolve(
        GameObject owner,
        float baseDamage,
        float baseCritChance = 0.05f,
        float critMultiplier = 2f)
    {
        PlayerCombatModifiers modifiers = owner != null
            ? owner.GetComponentInParent<PlayerCombatModifiers>()
            : null;
        float damageMultiplier = modifiers != null
            ? modifiers.TotalDamageMultiplier
            : 1f;
        float critChance = Mathf.Clamp01(
            baseCritChance +
            (modifiers != null ? modifiers.RunCritChanceBonus : 0f));
        bool critical = Random.value < critChance;
        float damage = Mathf.Max(0f, baseDamage) * damageMultiplier;

        if (critical)
            damage *= Mathf.Max(1f, critMultiplier);

        return new OffensiveAttackContext(
            damage,
            critical,
            modifiers != null ? modifiers.RunAttackSizeMultiplier : 1f);
    }

    public static float GetAttackSize(GameObject owner)
    {
        PlayerCombatModifiers modifiers = owner != null
            ? owner.GetComponentInParent<PlayerCombatModifiers>()
            : null;
        return modifiers != null ? modifiers.RunAttackSizeMultiplier : 1f;
    }
}
