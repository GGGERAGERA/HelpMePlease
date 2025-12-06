using UnityEngine;

/// <summary>
/// Управление движением игрока: WASD, стрелки, мышь (как джойстик)
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Ссылка на ScriptableObject с параметрами игрока")]
    [SerializeField] private PlayerStatsSO playerStats;

    [Tooltip("Скорость перемещения")]
    [SerializeField] private float currentMoveSpeed = 5f;

    [Tooltip("Чувствительность мыши (для управления как джойстиком)")]
    //[SerializeField] private float mouseSensitivity = 2f;

    //[Tooltip("Максимальное расстояние от мыши до центра экрана для управления")]
    [SerializeField] private float maxMouseDistance = 100f;
    private Vector3 left = new Vector3(-1, 1, 1);
    private Vector3 right = new Vector3(1, 1, 1);

    public Rigidbody2D rb;
    private Vector2 inputDirection;
    public Camera mainCamera;

    private void Awake()
    {
        // Применяем скорость из SO, если она задана
        if (playerStats != null)
        {
            currentMoveSpeed = playerStats.playerSpeed;
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("У объекта нет Rigidbody2D!");
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("Main Camera не найдена. Используйте камеру по умолчанию.");
            mainCamera = FindFirstObjectByType<Camera>();
        }
    }

    private void Update()
    {
        // Получаем ввод с клавиатуры (WASD / стрелки)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Получаем ввод с мыши (как джойстик)
        Vector2 mousePosition = Input.mousePosition;
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 mouseOffset = mousePosition - screenCenter;

        // Нормализуем и ограничиваем расстояние
        if (mouseOffset.magnitude > maxMouseDistance)
        {
            mouseOffset = mouseOffset.normalized * maxMouseDistance;
        }

        // Если мышь используется для управления, используем её данные
        if (Input.GetMouseButton(0)) // ЛКМ нажата — управляем мышью
        {
            inputDirection = mouseOffset.normalized;
        }
        else
        {
            // Иначе — клавиатура
            inputDirection = new Vector2(horizontal, vertical/2).normalized;
        }


    }

    private void FixedUpdate()
    {
        // Двигаемся через Rigidbody2D
        rb.linearVelocityX = inputDirection.x * currentMoveSpeed;
        rb.linearVelocityY = inputDirection.y * currentMoveSpeed;
        
        if(inputDirection.x > 0)
        gameObject.transform.localScale = left;
        if(inputDirection.x < 0)
        gameObject.transform.localScale = right;
    }
    
    /// Получаем текущее направление движения (для анимаций)
    public Vector2 GetMovementDirection()
    {
        return inputDirection;
    }
}