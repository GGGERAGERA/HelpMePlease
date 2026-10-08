using UnityEngine;

// Explicit optional policy for development-owned targets; the runtime depends only on this contract.
public abstract class EnemyDebugDamagePolicy : MonoBehaviour
{
    public abstract bool Invulnerable { get; set; }
    public abstract bool SuppressesProductionRewards { get; }
}
