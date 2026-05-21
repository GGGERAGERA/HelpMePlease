using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;
    public GameObject pauseCanvas;
    private bool isPaused = false;
    public bool IsPaused => isPaused;


    // Ссылка на здоровье игрока
    private PlayerHealth playerHealth;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        // Если игрок мёртв — не даём открыть паузу
        if (playerHealth != null && playerHealth.IsDead) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }
    void Start()
    {
        // Найдём игрока при старте
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (pauseCanvas != null) pauseCanvas.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseCanvas != null) pauseCanvas.SetActive(false);
    }
}