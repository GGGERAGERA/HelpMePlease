using UnityEngine;

public class CharacterMovement2D : MonoBehaviour
{
    [Header("Движение")]
    public float speed = 5f;

    private Vector3 movement;
    private Rigidbody rb;                  
    private Animator animator;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        // замораживаем вращение и движение по Y
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }
    }

    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        movement = new Vector3(moveX, moveY, 0).normalized;
        transform.Translate(movement * speed * Time.deltaTime, Space.World);

        // Зеркалирование по горизонтали
        if (moveX != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * -Mathf.Sign(moveX);
            transform.localScale = scale;
        }
    }

    void FixedUpdate()
    {
    }

}
