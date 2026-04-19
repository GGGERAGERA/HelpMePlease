using UnityEngine;
using UnityEngine.UI;
public class EnemyHealthBar : MonoBehaviour
{
    private Slider slider;
    private EnemyHealth health;
    void Start()
    {
        slider = GetComponentInChildren<Slider>();
        health = GetComponentInParent<EnemyHealth>(); // ищем на родительском объекте (враге)
        if (health == null)
        {
            Debug.LogError("EnemyHealth not found on parent!");
            return;
        }
        // Подписываемся на событие изменения здоровья
        health.OnHealthChanged.AddListener(UpdateHealthBar);
        // Инициализируем
        slider.maxValue = health.maxHealth;
        slider.value = health.maxHealth;
    }
    void UpdateHealthBar(float current, float max)
    {
        if (slider != null)
            slider.value = current;
    }

    void LateUpdate()
    {
        // Полоска всегда повёрнута к камере
        if (Camera.main != null)
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
    }

}
