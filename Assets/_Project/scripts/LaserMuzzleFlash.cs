using UnityEngine;

public class LaserMuzzleFlash : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.06f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}