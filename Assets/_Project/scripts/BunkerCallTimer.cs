using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BunkerCallTimer : MonoBehaviour
{
    [Header("Настройки времени")]
    public float minTime = 60f;   // минимальное время (секунды)
    public float maxTime = 120f;  // максимальное время (секунды)

    [Header("UI")]
    public GameObject bunkerCallButton;   // кнопка вызова бункера (по умолчанию неактивна)
    public Text timerText;                // текст с обратным отсчётом (опционально)

    public Notification notification;

    private float timeUntilCall;
    private bool isReady = false;

    void Start()
    {
        // Выбираем случайное время
        timeUntilCall = Random.Range(minTime, maxTime);
        Debug.Log($"Бункер будет готов через {timeUntilCall:F0} секунд");

        if (bunkerCallButton != null)
            bunkerCallButton.SetActive(false);
    }

    void Update()
    {
        if (!isReady)
        {
            timeUntilCall -= Time.deltaTime;

            // Обновляем текст таймера (если есть)
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(timeUntilCall);
                timerText.text = $"Бункер: {seconds} сек";
            }

            // Когда время истекло — активируем вызов
            if (timeUntilCall <= 0)
            {
                isReady = true;
                OnBunkerReady();
            }
        }
    }

    void OnBunkerReady()
    {
        Debug.Log("📡 Бункер готов к вызову!");

        if (notification != null)
            notification.Show("Бункер готов! Нажмите кнопку.");

        // Активируем кнопку вызова
        if (bunkerCallButton != null)
            bunkerCallButton.SetActive(true);

        // Можно показать уведомление на экране
        ShowNotification("Бункер готов! Нажмите кнопку для вызова.");
    }

    public void CallBunker()
    {
        if (!isReady)
        {
            Debug.Log("Бункер ещё не готов!");
            return;
        }

        Debug.Log("🚀 Вызов бункера!");
        // Здесь позже добавим: остановка времени, открытие меню, телепортация и т.д.

        // Пока просто скрываем кнопку и сбрасываем таймер
        if (bunkerCallButton != null)
            bunkerCallButton.SetActive(false);

        isReady = false;
        timeUntilCall = Random.Range(minTime, maxTime);
        Debug.Log($"Следующий вызов через {timeUntilCall:F0} секунд");
    }

    void ShowNotification(string message)
    {
        // Самый простой способ — Debug.Log
        Debug.Log(message);

        // Позже можешь добавить всплывающее UI уведомление
    }
}