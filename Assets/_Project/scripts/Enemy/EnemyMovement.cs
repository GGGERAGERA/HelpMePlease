using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public float speed = 2f;
    private Transform target;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            EnemyMovement movement = GetComponent<EnemyMovement>();
            if (movement != null)
                movement.SetTarget(playerObj.transform);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    // Update is called once per frame
    void Update()
    {
        if (target != null)
        {
            Vector2 direction = (target.position - transform.position).normalized;
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            if (spriteRenderer != null)
            {
                // ≈сли движетс€ влево (direction.x < 0), зеркалим спрайт по X
                spriteRenderer.flipX = direction.x < 0;
            }
        }
        
    }
}
