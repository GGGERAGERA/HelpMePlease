using UnityEngine;
public class SimplePlayer : MonoBehaviour

{
    // ========================
    // 🎮 ГЛОБАЛЬНЫЕ ПЕРЕМЕННЫЕ
    // ========================

    // Публичная ссылка на CharacterController (можно задать в инспекторе)
    // Обычно GetComponent<CharacterController>() достаточно, но по твоей просьбе — публично!
    [Header("Компоненты")]
    public CharacterController characterController;

    [Header("Камера")]
    public Transform playerCamera; // Должна быть дочерней!

    [Header("Скорости")]
    public float walkSpeed = 5f;        // Обычная ходьба
    public float runSpeed = 8f;         // Бег (Shift)
    public float crouchSpeed = 2f;      // Присед или Alt

    [Header("Прыжок и гравитация")]
    public float jumpPower = 5f;        // Сила прыжка
    public float gravity = -9.81f;      // Гравитация

    [Header("Мышь")]
    public float mouseSensitivity = 2f;
    public float maxLookUp = 80f;
    public float maxLookDown = -80f;

    // Внутренние переменные
    private Vector3 verticalVelocity = Vector3.zero;
    private float cameraPitch = 0f;
    private bool isCrouching = false;

    // ========================
    // 🚀 ИНИЦИАЛИЗАЦИЯ
    // ========================

    void Start()
    {
        // Если в инспекторе не задан CharacterController — ищем сам
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                Debug.LogError("На объекте нет CharacterController! Добавь компонент.");
                enabled = false;
                return;
            }
        }

        // Проверка камеры
        if (playerCamera == null)
        {
            Debug.LogError("Привяжи камеру в инспекторе!");
            enabled = false;
            return;
        }

        // Захватываем курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ========================
    // 🔄 ОСНОВНОЙ ЦИКЛ
    // ========================

    void Update()
    {
        HandleMouseLook();
        HandleMovementAndJump();
    }

    // ========================
    // 🖱️ ВРАЩЕНИЕ КАМЕРЫ МЫШЬЮ
    // ========================

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Вращаем тело по горизонтали
        transform.Rotate(Vector3.up * mouseX);

        // Вращаем камеру по вертикали
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, maxLookDown, maxLookUp);
        playerCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    // ========================
    // 🚶 ДВИЖЕНИЕ И ПРЫЖОК
    // ========================

    void HandleMovementAndJump()
    {
        // === Определяем скорость ===
        float currentSpeed = walkSpeed;

        if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            currentSpeed = crouchSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed = runSpeed;
        }

        // === Горизонтальное движение (WASD) ===
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = Input.GetAxisRaw("Vertical");     // W/S

        Vector3 moveDirection = (transform.right * horizontal + transform.forward * vertical).normalized;
        characterController.Move(moveDirection * currentSpeed * Time.deltaTime);

        // === Гравитация ===
        if (!characterController.isGrounded)
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }
        else
        {
            if (verticalVelocity.y < 0) verticalVelocity.y = -1f; // лёгкий прижим

            // === Прыжок ===
            if (Input.GetButtonDown("Jump"))
            {
                verticalVelocity.y = Mathf.Sqrt(jumpPower * -2f * gravity);
            }
        }

        // Применяем вертикальное движение (прыжок + гравитация)
        characterController.Move(verticalVelocity * Time.deltaTime);
    }
}