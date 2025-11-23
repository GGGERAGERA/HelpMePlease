using UnityEngine;

public class PlayerCollector : MonoBehaviour
{
    PlayerStats player;
    CircleCollider2D playerCollector;
    public float pullSpeed;
     void Start()
    {
      player = FindAnyObjectByType<PlayerStats>();  
      playerCollector = GetComponent<CircleCollider2D>();
    }

    void Update()
    {
        playerCollector.radius = player.currentMagnet;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        // Check if the other game object has the ICollectible interface
        if(col.gameObject.TryGetComponent(out ICollectible collectible))
        {
            Rigidbody2D rb = col.gameObject.GetComponent<Rigidbody2D>();
            Vector2 forceDirection = (transform.position - col.transform.position).normalized;
            rb.AddForce(forceDirection * pullSpeed); 
            collectible.Collect();
        }
    }
}
