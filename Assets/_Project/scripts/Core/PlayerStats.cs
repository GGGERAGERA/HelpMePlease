using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    [Header("Damage")]
    public int baseDamage = 10;
    private float damageMultiplier = 1f;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public int GetDamage()
    {
        return Mathf.RoundToInt(baseDamage * damageMultiplier);
    }

    public void IncreaseDamage(int amount)
    {
        baseDamage += amount;
        Debug.Log($"Damage increased to {GetDamage()}");
    }
}