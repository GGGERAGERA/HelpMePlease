using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro text;
    private float timer = 1f;
    private float floatSpeed = 1.5f;

    void Awake()
    {
        text = GetComponent<TextMeshPro>();
        if (text == null)
            Debug.LogError("DamagePopup: TextMeshPro component not found!");
    }

    void Start()
    {
        // Автоматическое уничтожение через 1 секунду (запасной вариант)
        Destroy(gameObject, 1.2f);
    }

    public void SetDamage(int damage, bool isCritical = false)
    {
        if (text != null)
        {
            text.text = damage.ToString();
            if (isCritical)
            {
                text.color = Color.red; // Изменяем цвет текста для критического удара
                text.fontSize *= 1.2f; // Увеличиваем размер текста для критического удара
            }
        }
    }

    public void SetRewardMultiplier(float multiplier, Color color)
    {
        if (text == null)
            return;

        text.text = $"GOLD ×{multiplier:0.#}";
        text.color = color;
        text.fontSize *= 1.15f;
    }

    void Update()
    {
        // Поднимаем текст вверх
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // Уменьшаем таймер и уничтожаем
        timer -= Time.deltaTime;
        if (timer <= 0)
            Destroy(gameObject);
    }
}
