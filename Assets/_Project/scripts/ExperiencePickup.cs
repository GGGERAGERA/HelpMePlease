using UnityEngine;

public class ExperiencePickup : MonoBehaviour
{
    public int expValue = 10;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            ExperienceManager.Instance.AddExperience(expValue);
            Destroy(gameObject);
        }
    }
}
