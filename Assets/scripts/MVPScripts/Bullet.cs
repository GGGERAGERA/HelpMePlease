using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    private float direction = 1f; // 1 - вправо, -1 - влево

    // Этот метод будет вызывать скрипт Shoot
    public void SetDirection(float dir)
    {
        direction = dir;
    }

    void Update()
    {
        // Движение пули
        transform.Translate(Vector3.right * direction * speed * Time.deltaTime);
    }

    void Start()
    {
        // Самоуничтожение через 2 секунды, чтобы пули не накапливались
        Destroy(gameObject, 2f);
    }
}
