using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;

public class PlayerController1 : MonoBehaviour
{
    public float moveSpeed;

    public bool isMoving1;

    private Vector2 input;

    public Animator PlayerAnimator1;

    // Update is called once per frame
    void Update()
    {
        if(isMoving1)
        {
            input.x = Input.GetAxisRaw("Horisontal");
            input.y = Input.GetAxisRaw("Vertical");

            if(input.x!=0) input.y=0;

            if(input != Vector2.zero)
            {
                PlayerAnimator1.SetFloat("moveX", input.x);
                PlayerAnimator1.SetFloat("moveY", input.y);

                var targetPos = transform.position;
                targetPos.x += input.x;
                targetPos.y += input.y;

                StartCoroutine(Move(targetPos));
            }
        }
        PlayerAnimator1.SetBool("isMoving",isMoving1);
    }

    IEnumerator Move(Vector3 targetPos)
    {
        isMoving1 = true;

        while((targetPos - transform.position).sqrMagnitude > Mathf.Epsilon)
        {
             transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
             yield return null;
        }
        transform.position = targetPos;

        isMoving1 = false;
    }

}
