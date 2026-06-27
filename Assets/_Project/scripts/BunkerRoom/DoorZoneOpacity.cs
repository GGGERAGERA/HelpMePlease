using UnityEngine;

public class DoorZoneOpacity : MonoBehaviour
{
    [Header("Зона")]
    [SerializeField] private SpriteRenderer[] zoneRenderers;
    [SerializeField] private Transform distanceFrom;
    [SerializeField] private float minAlpha = 0.05f;
    [SerializeField] private float maxAlpha = 1f;
    [SerializeField] private Collider2D triggerZone;
    [SerializeField] private float minDistance = 1f;

    [Header("Настройки взаимодействия")]
     [SerializeField] private bool ignoreCursor = false; // Поставь галочку, если курсор не нужен


    [Header("Игрок")]
    [SerializeField] private Transform playerTransform;

    private Camera mainCam;
    private Color[] originalColors;
    private float maxDistance;
    private bool wasActive; // флаг — был ли кто-то в зоне

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

        // Кэшируем исходные цвета
        if (zoneRenderers != null && zoneRenderers.Length > 0)
        {
            originalColors = new Color[zoneRenderers.Length];
            for (int i = 0; i < zoneRenderers.Length; i++)
            {
                if (zoneRenderers[i] != null)
                    originalColors[i] = zoneRenderers[i].color;
            }
        }

        // Считаем максимальное расстояние из bounds триггера
        if (triggerZone != null)
        {
            maxDistance = GetMaxDistanceFromBounds(distanceFrom.position, triggerZone.bounds);
            maxDistance = Mathf.Max(maxDistance, 0.5f);
        }

        ApplyAlpha(minAlpha);
    }

    private void Update()
    {
        // Проверяем присутствие — без дорогих Raycast
        bool cursorInZone = ignoreCursor ? false : IsCursorInZone();
        bool playerInZone = IsPlayerInZone();

        // Если никого нет и раньше не было — выходим сразу, не считаем
        if (!cursorInZone && !playerInZone && !wasActive)
            return;

        wasActive = cursorInZone || playerInZone;

        // Оба вышли — прячем зону
        if (!cursorInZone && !playerInZone)
        {
            ApplyAlpha(minAlpha);
            return;
        }

        // Считаем альфу от каждого источника, берём максимум
        float cursorAlpha = cursorInZone ? GetAlphaFromCursor() : minAlpha;
        float playerAlpha = playerInZone ? GetAlphaFromPlayer() : minAlpha;

        ApplyAlpha(Mathf.Max(cursorAlpha, playerAlpha));
    }

    // Проверяем курсор через bounds (дешевле чем RaycastAll)
    private bool IsCursorInZone()
    {
        if (mainCam == null || triggerZone == null)
            return false;

        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        return triggerZone.bounds.Contains(mousePos);
    }

    // Проверяем игрока через bounds
    private bool IsPlayerInZone()
    {
        if (playerTransform == null || triggerZone == null)
            return false;

        return triggerZone.bounds.Contains(playerTransform.position);
    }

    private float GetAlphaFromCursor()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        float distance = Vector2.Distance(distanceFrom.position, mousePos);
        return CalculateAlpha(distance);
    }

    private float GetAlphaFromPlayer()
    {
        float distance = Vector2.Distance(distanceFrom.position, playerTransform.position);
        return CalculateAlpha(distance);
    }

    // Главная формула: расстояние -> альфа
    private float CalculateAlpha(float distance)
    {
        if (distance <= minDistance)
            return maxAlpha;

        if (distance >= maxDistance)
            return minAlpha;

        float t = 1f - ((distance - minDistance) / (maxDistance - minDistance));
        return Mathf.Lerp(minAlpha, maxAlpha, t);
    }

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

    public void ShowZone() => ApplyAlpha(maxAlpha);
    public void HideZone() => ApplyAlpha(minAlpha);
    public void SetZoneAlpha(float alpha) => ApplyAlpha(Mathf.Clamp01(alpha));
}