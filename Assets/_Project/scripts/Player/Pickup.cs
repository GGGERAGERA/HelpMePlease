using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum LootType { Crystal, Gems }
    public LootType lootType;
    public int value = 1;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (lootType == LootType.Crystal)
                ScoreManager.Instance.AddCrystal(value);
            Destroy(gameObject);
        }
    }
}
