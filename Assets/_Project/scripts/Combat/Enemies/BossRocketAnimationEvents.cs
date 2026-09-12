using UnityEngine;

// Animation Events are delivered to the graphic Animator, not the boss root.
public sealed class BossRocketAnimationEvents : MonoBehaviour
{
    [SerializeField] private BossRocketAttack attack;
    public void FireRocket() => attack?.FireRocket();
}
