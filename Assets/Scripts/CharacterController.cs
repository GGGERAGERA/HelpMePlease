using UnityEngine;

public class CharacterController : MonoBehaviour
{
    
    // ========================
    // 🎮 ГЛОБАЛЬНЫЕ ПЕРЕМЕННЫЕ
    // ========================

    // Ссылка на CharacterController
    public CharacterController controller;

    // Ссылка на камеру (должна быть дочерней)
    public Transform playerCamera;

    // Базовая скорость движения
    public float walkSpeed = 5f;

    // Скорость бега (при нажатии Shift)
    public float runSpeed = 8f;

    // Скорость при ходьбе с зажатым Alt
    public float crouchSpeed = 2f;

    // Высота прыжка
    public float jumpHeight = 2f;

    // Сила гравитации
    public float gravity = -9.81f;

    // Чувствительность мыши
    public float mouseSensitivity = 2f;

    // Максимальный угол наклона камеры вверх/вниз
    public float maxLookUp = 80f;
    public float maxLookDown = -80f;

    // Внутренние переменные
    private Vector3 velocity;
    private float xRotation = 0f;
    private bool isCrouching = false;
    private bool isRunning = false;
    private bool isSlowingDown = false;

    private bool isGrounded = true;

    // ========================
    // 🚀 ИНИЦИАЛИЗАЦИЯ
    // ========================

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("На объекте нет Component 'CharacterController'!");
        }

        // Отключаем курсор и захватываем его
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ========================
    // 🔄 ОСНОВНОЙ ЦИКЛ
    // ========================

    void Update()
    {
        HandleInput();
        HandleMovement();
        //HandleCameraRotation();
    }

    // ========================
    // 🖱️ ОБРАБОТКА ВВОДА
    // ========================

    void HandleInput()
    {
        // Приседание (C)
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
            //AdjustHeight();
        }

        // Бег (Shift)
        isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Замедление (Alt)
        isSlowingDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        // Прыжок (Пробел)
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    // ========================
    // 🚶 ОБРАБОТКА ДВИЖЕНИЯ
    // ========================

    void HandleMovement()
    {
        // Получаем ввод с клавиатуры
        float x = Input.GetAxisRaw("Horizontal"); // A/D или стрелки
        float z = Input.GetAxisRaw("Vertical");   // W/S или стрелки

        // Определяем текущую скорость
        float currentSpeed = walkSpeed;

        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (isRunning && !isSlowingDown)
        {
            currentSpeed = runSpeed;
        }
        else if (isSlowingDown)
        {
            currentSpeed = crouchSpeed; // Можно сделать отдельную "ползучую" скорость
        }
/*
        // Создаём вектор движения в локальном пространстве игрока
        Vector3 move = transform.right * x + transform.forward * z;

        // Применяем движение
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Применяем гравитацию
        if (!controller.isGrounded)
        {
            velocity.y += gravity * Time.deltaTime;
        }
        else
        {
            velocity.y = -0.1f; // Лёгкий "прижим" к земле
        }

        controller.Move(velocity * Time.deltaTime);
    }
*/
    // ========================
    // 📷 ОБРАБОТКА ВРАЩЕНИЯ КАМЕРЫ
    // ========================

    void HandleCameraRotation()
    {
        // Вращение камеры по Y (вокруг вертикальной оси)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);

        // Вращение камеры по X (вверх/вниз)
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, maxLookDown, maxLookUp);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    // ========================
    // 🧍‍♂️ АДАПТАЦИЯ РОСТА ПРИ ПРИСЕДЕ
    // ========================

    /*void AdjustHeight()
    {
        if (isCrouching)
        {
            controller.height = 1f;          // Уменьшаем высоту
            controller.center = new Vector3(0, 0.5f, 0); // Смещаем центр
        }
        else
        {
            controller.height = 2f;          // Возвращаем стандартную высоту
            controller.center = new Vector3(0, 1f, 0);   // Стандартный центр
        }
    }*/
    }
}
