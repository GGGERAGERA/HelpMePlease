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

    public void SetDamage(int damage)
    {
        if (text != null)
        {
            text.text = damage.ToString();
            Debug.Log($"DamagePopup: set text to {damage}"); // ← отладка
        }
        else
        {
            Debug.LogError("DamagePopup: text is null, cannot set damage!");
        }
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