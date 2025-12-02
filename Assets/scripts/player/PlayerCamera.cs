using UnityEngine;

/// <summary>
/// Камера, которая плавно следует за игроком
/// </summary>
public class PlayerCamera : MonoBehaviour
{
    [Header("Настройки камеры")]
    [Tooltip("Цель, за которой следует камера")]
    [SerializeField] private Transform target;

    [Tooltip("Скорость плавного следования")]
    [SerializeField] private float smoothSpeed = 0.125f;

    //[Tooltip("Смещение камеры относительно цели")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("Цель камеры не задана!");
            return;
        }

        // Вычисляем целевую позицию
        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Перемещаем камеру
        transform.position = smoothedPosition;
    }
}