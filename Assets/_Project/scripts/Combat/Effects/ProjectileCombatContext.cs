using UnityEngine;

public class ProjectileCombatContext : MonoBehaviour
{
    public PlayerCombatModifiers Modifiers { get; private set; }
    public BaseWeapon Weapon { get; private set; }
    public WeaponData WeaponData { get; private set; }

    public void Initialize(WeaponFireContext context)
    {
        Modifiers = context.Modifiers;
        Weapon = context.Weapon;
        WeaponData = context.WeaponData;
    }
}
