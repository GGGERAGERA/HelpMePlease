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
            Debug.Log("Player picked up: " + lootType + " with value: " + value);
            if (lootType == LootType.Crystal)
                ScoreManager.Instance.AddCrystal(value);
            else if (lootType == LootType.Gems)
                ScoreManager.Instance.AddGems(value);
            Destroy(gameObject);
        }
    }
}
