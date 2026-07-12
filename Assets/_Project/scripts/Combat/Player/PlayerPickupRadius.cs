using UnityEngine;

public class PlayerPickupRadius : MonoBehaviour
{
    [SerializeField] private float baseRadius = 3f;

    private float multiplier = 1f;

    public float CurrentRadius => baseRadius * multiplier;

    public void AddRadiusPercent(float percent)
    {
        multiplier *= 1f + percent;
    }
}
