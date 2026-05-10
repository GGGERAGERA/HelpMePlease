using UnityEngine;
using UnityEngine.UI;

public class GameTimer : MonoBehaviour
{
    public Text timerText;           // UI Text для отображения времени
    private float elapsedTime = 0f;  // прошедшее время в секундах
    private bool isRunning = true;   // запущен ли секундомер

    void Update()
    {
        if (isRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateDisplay();
        }
    }

    void UpdateDisplay()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(elapsedTime / 60);
            int seconds = Mathf.FloorToInt(elapsedTime % 60);
            int milliseconds = Mathf.FloorToInt((elapsedTime * 100) % 100);

            // Формат: ММ:СС:МС (например, 03:45:12)
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    // Остановить секундомер (например, при смерти)
    public void Stop() => isRunning = false;

    // Запустить заново
    public void Start() => isRunning = true;

    // Сбросить в ноль
    public void Reset()
    {
        elapsedTime = 0f;
        UpdateDisplay();
    }

    // Получить текущее время (для сохранения рекорда)
    public float GetElapsedTime() => elapsedTime;
}