using UnityEngine;

public class PlayerMovement1 : MonoBehaviour
{


    Rigidbody2D rb;
    SpriteRenderer sr;

    [SerializeField]
    float moveSpeed;
    [SerializeField]
    float jumpForce;


    public Vector2 moveDir;
    public bool isGround;
    public float rayDistance = 0.6f;
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit2D hit = Physics2D.Raycast(rb.position, Vector2.down, rayDistance, LayerMask.GetMask("Ground"));

        if (hit.collider != null)
        {
            isGround = true;

        }
        else
        {
            isGround = false;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space");
            rb.AddForce((Vector2.up * jumpForce), ForceMode2D.Force);
        }

        if (Input.GetAxis("Horizontal") > 0)
        {
            sr.flipX = false;
            moveDir = new Vector2(1, 0).normalized;
        }
        else if (Input.GetAxis("Horizontal") < 0)
        {
            sr.flipX = true;
            moveDir = new Vector2(-1, 0).normalized;
        }
        else
            moveDir = new Vector2(0, 0).normalized;

        rb.linearVelocity = moveDir * moveSpeed;
        

    }


}
