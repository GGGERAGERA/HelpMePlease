using UnityEngine;

public sealed class EnemyIdentity : MonoBehaviour
{
    [SerializeField] private string enemyId;

    public string EnemyId => enemyId;
}