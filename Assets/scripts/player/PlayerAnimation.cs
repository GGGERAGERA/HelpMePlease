using UnityEngine;

/// <summary>
/// Управление анимациями игрока: idle и walk
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform localtransform1;
    [Header("Настройки")]
    [Tooltip("Ссылка на ScriptableObject с параметрами игрока")]
    public PlayerStatsSO playerStats;

    [Tooltip("Аниматор игрока")]
    [SerializeField] private Animator animator;

    private PlayerMovement playerMovement;
    private Vector3 left = new Vector3(-1, 1, 1);
    private void Awake()
    {
            if (spriteRenderer == null)
            {
            spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                Debug.LogWarning("SpriteRenderer не найден. Поворот через Flip не будет работать.");
                }
            }
            if (animator == null)
            {
            animator = GetComponent<Animator>();
                if (animator == null)
                {
                Debug.LogError("Animator не найден!");
                return;
                }
            }

            playerMovement = GetComponent<PlayerMovement>();
            if (playerMovement == null)
            {
            Debug.LogWarning("PlayerMovement не найден. Анимации могут работать некорректно.");
            }
    }

    private void Update()
    {
        if (animator == null) return;
        // Получаем направление движения
        Vector2 movementDirection = Vector2.zero;
        if (playerMovement != null)
        {
            movementDirection = playerMovement.GetMovementDirection();
        }
        // Вычисляем скорость
        float speed = movementDirection.magnitude;
        // Передаем в аниматор
        if(speed!=0)
        animator.SetBool("IsMoving", true);
        else 
        animator.SetBool("IsMoving", false);
    }
}