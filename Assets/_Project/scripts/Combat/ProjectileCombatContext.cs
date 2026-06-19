using UnityEngine;

public class ProjectileCombatContext : MonoBehaviour
{
    public PlayerCombatModifiers Modifiers { get; private set; }

    public void Initialize(PlayerCombatModifiers modifiers)
    {
        Modifiers = modifiers;
    }
}
