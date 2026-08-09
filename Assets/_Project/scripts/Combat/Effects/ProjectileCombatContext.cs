using UnityEngine;

public class ProjectileCombatContext : MonoBehaviour
{
    public PlayerCombatModifiers Modifiers { get; private set; }
    public BaseWeapon Weapon { get; private set; }
    public Transform Owner { get; private set; }
    public WeaponShotKind ShotKind { get; private set; }
    public WeaponCoreType Core { get; private set; }

    public void Initialize(WeaponFireContext context)
    {
        Modifiers = context.Modifiers;
        Weapon = context.Weapon;
        Owner = context.Owner;
        ShotKind = context.ShotKind;
        Core = context.Core;
    }
}
