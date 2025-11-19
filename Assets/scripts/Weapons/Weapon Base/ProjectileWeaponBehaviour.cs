using UnityEngine;

/// <summary>
/// Base script of all projectile behaviours
/// </summary>

public class ProjectileWeaponBehaviour : MonoBehaviour
{

    protected Vector3 direction;
    public float destroyAfterSeconds;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
   protected virtual void Start()
    {
        Destroy(gameObject, destroyAfterSeconds);
    }

    public void DirectionChecker(Vector3 dir)
    {
        direction = dir;

        float dirx = direction.x;
        float diry = direction.y;

        Vector3 scale = transform.localScale;
        Vector3 rotation = transform.rotation.eulerAngles;

        if (dirx < 0 && diry == 0) // left
        {
            scale.x *= -1;
            scale.y *= -1;
        }
        else if (dirx == 0 && diry < 0) // down
        {
            scale.y *= -1;

        }
        else if (dirx == 0 && diry > 0) // up
        {
            scale.x *= -1;
        }
        else if (dirx > 0 && diry > 0) // right up
        {
            rotation.z = 0f;
        }
        else if (dirx > 0 && diry < 0) // right down
        {
            rotation.z = -90f;
        }
        else if (dirx < 0 && diry > 0) // left up
        {
            scale.x *= -1;
            scale.y *= -1;
            rotation.z = -90f;
        }
        else if (dirx < 0 && diry < 0) // left down
        {
            scale.x *= -1;
            scale.y *= -1;
            rotation.z = 0f;
        }

            transform.localScale = scale;
        transform.rotation = Quaternion.Euler(rotation); 
    }
}
