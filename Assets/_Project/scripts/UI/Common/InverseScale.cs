using UnityEngine;

public class InverseScale : MonoBehaviour
{
    void LateUpdate()
    {
        Transform parent = transform.parent;
        if (parent != null)
        {
            // Компенсируем масштаб родителя, чтобы Canvas оставался прямым
            Vector3 parentScale = parent.lossyScale;
            transform.localScale = new Vector3(
                1f / parentScale.x,
                1f / parentScale.y,
                1f / parentScale.z
            );
        }
    }
}
