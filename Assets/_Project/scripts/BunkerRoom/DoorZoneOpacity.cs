using UnityEngine;

public class DoorZoneOpacity : MonoBehaviour
{
    [Header("Зона")]
    [Tooltip("Рендереры зоны (визуальные элементы)")]
    [SerializeField] private SpriteRenderer[] zoneRenderers;
    
    [Tooltip("Точка от которой считается расстояние. Если не задана - сам объект двери")]
    [SerializeField] private Transform distanceFrom;
    
    [Tooltip("Минимальная прозрачность (на границе триггера)")]
    [SerializeField] private float minAlpha = 0.05f;
    
    [Tooltip("Максимальная прозрачность (в точке distanceFrom или ближе minDistance)")]
    [SerializeField] private float maxAlpha = 0.9f;
    
    [Tooltip("Коллайдер-триггер зоны (определяет maxDistance и присутствие)")]
    [SerializeField] private Collider2D triggerZone;

    [Tooltip("Минимальное расстояние от distanceFrom, при котором альфа = maxAlpha. " +
             "Всё что ближе этой точки - максимально видно.")]
    [SerializeField] private float minDistance = 1f;

    [Header("Игрок")]
    [Tooltip("Трансформ игрока. Если не задан - ищем по тегу Player")]
    [SerializeField] private Transform playerTransform;

    [Header("Отладка")]
    [SerializeField] private bool debugMode = false;

    private Camera mainCam;
    private Color[] originalColors;
    private float maxDistance = 0f;
    private const float MIN_MAX_DISTANCE = 0.5f;

    private void Awake()
    {
        mainCam = Camera.main;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }

        if (distanceFrom == null)
            distanceFrom = transform;

        // Кэшируем исходные цвета зон
        if (zoneRenderers != null && zoneRenderers.Length > 0)
        {
            originalColors = new Color[zoneRenderers.Length];
            for (int i = 0; i < zoneRenderers.Length; i++)
            {
                if (zoneRenderers[i] != null)
                    originalColors[i] = zoneRenderers[i].color;
            }
        }

        // Считаем maxDistance из bounds триггера
        if (triggerZone != null)
        {
            maxDistance = GetMaxDistanceFromBounds(distanceFrom.position, triggerZone.bounds);
            maxDistance = Mathf.Max(maxDistance, MIN_MAX_DISTANCE);
        }

        if (debugMode)
        {
            Debug.Log($"[DoorZone] Player: {playerTransform != null}");
            Debug.Log($"[DoorZone] Renderers: {zoneRenderers?.Length ?? 0}");
            Debug.Log($"[DoorZone] Trigger: {triggerZone != null}");
            Debug.Log($"[DoorZone] DistanceFrom: {distanceFrom?.name}");
            Debug.Log($"[DoorZone] MinDistance: {minDistance:F2}");
            Debug.Log($"[DoorZone] MaxDistance: {maxDistance:F2}");
        }

        ApplyAlpha(minAlpha);
    }

    // Находим самую дальнюю точку bounds от заданной позиции
    private float GetMaxDistanceFromBounds(Vector2 fromPoint, Bounds bounds)
    {
        float maxDist = 0f;
        
        Vector2[] corners = new Vector2[]
        {
            new Vector2(bounds.min.x, bounds.min.y),
            new Vector2(bounds.max.x, bounds.min.y),
            new Vector2(bounds.min.x, bounds.max.y),
            new Vector2(bounds.max.x, bounds.max.y)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            float dist = Vector2.Distance(fromPoint, corners[i]);
            if (dist > maxDist)
                maxDist = dist;
        }

        return maxDist;
    }

    private void Update()
    {
        // Если нет триггера или рендереров - ничего не делаем
        if (triggerZone == null || zoneRenderers == null || zoneRenderers.Length == 0)
            return;

        // Проверяем присутствие курсора и игрока в зоне
        bool cursorInZone = IsCursorInZone();
        bool playerInZone = IsPlayerInZone();

        // Если никого нет - прячем зону и выходим
        if (!cursorInZone && !playerInZone)
        {
            ApplyAlpha(minAlpha);
            return;
        }

        // Считаем альфу от каждого источника
        float cursorAlpha = cursorInZone ? GetAlphaFromCursor() : minAlpha;
        float playerAlpha = playerInZone ? GetAlphaFromPlayer() : minAlpha;

        // Берём максимальную - кто ближе, тот и "ярче"
        ApplyAlpha(Mathf.Max(cursorAlpha, playerAlpha));
    }

    // Проверяем, наведён ли курсор на триггер
    private bool IsCursorInZone()
    {
        if (mainCam == null) return false;

        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, Vector2.zero);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == triggerZone)
                return true;
        }

        return false;
    }

    // Проверяем, находится ли игрок внутри триггера
    private bool IsPlayerInZone()
    {
        if (playerTransform == null) return false;
        return triggerZone.bounds.Contains(playerTransform.position);
    }

    // Альфа на основе позиции курсора
    private float GetAlphaFromCursor()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        float distance = Vector2.Distance(distanceFrom.position, mousePos);
        return CalculateAlpha(distance);
    }

    // Альфа на основе позиции игрока
    private float GetAlphaFromPlayer()
    {
        float distance = Vector2.Distance(distanceFrom.position, playerTransform.position);
        return CalculateAlpha(distance);
    }

    // Главная формула: расстояние -> альфа
    // distance <= minDistance  -> maxAlpha (максимально видно)
    // distance >= maxDistance  -> minAlpha (почти не видно)
    // между ними               -> плавная интерполяция
    private float CalculateAlpha(float distance)
    {
        // Близко - полная видимость
        if (distance <= minDistance)
            return maxAlpha;

        // Далеко - минимальная видимость
        if (distance >= maxDistance)
            return minAlpha;

        // Плавный переход между minDistance и maxDistance
        float t = 1f - ((distance - minDistance) / (maxDistance - minDistance));
        return Mathf.Lerp(minAlpha, maxAlpha, t);
    }

    // Применяем альфу ко всем рендерерам зоны
    private void ApplyAlpha(float alpha)
    {
        for (int i = 0; i < zoneRenderers.Length; i++)
        {
            if (zoneRenderers[i] == null)
                continue;

            Color color = originalColors[i];
            color.a = alpha;
            zoneRenderers[i].color = color;
        }
    }

    // Публичные методы для внешнего управления
    public void ShowZone() => ApplyAlpha(maxAlpha);
    public void HideZone() => ApplyAlpha(minAlpha);
    public void SetZoneAlpha(float alpha) => ApplyAlpha(Mathf.Clamp01(alpha));
}