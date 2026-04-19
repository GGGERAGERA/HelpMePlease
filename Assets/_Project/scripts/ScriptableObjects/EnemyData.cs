using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/EnemyData")]
public class EnemyData : ScriptableObject
{
    public float speed = 2f;
    public float maxHealth = 100f;
    public int damage = 20;
    public GameObject prefab;
    public Color color;
}
