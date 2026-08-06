using UnityEngine;

public sealed class DarknessEnemyMarker : MonoBehaviour
{
    private const string MarkerRootName = "DarknessMarker";

    private SpriteRenderer leftMarker;
    private SpriteRenderer rightMarker;
    private float leftBaseAlpha;
    private float rightBaseAlpha;
    private bool configured;

    public void SetActive(
        bool active,
        Sprite markerSprite,
        Material markerMaterial,
        float intensity,
        EnemyHealth enemy)
    {
        if (active && !configured)
            Configure(markerSprite, markerMaterial, enemy);

        if (!configured)
            return;

        float clampedIntensity = Mathf.Clamp(intensity, 0f, 2f);
        SetRendererActive(
            leftMarker,
            active,
            clampedIntensity,
            leftBaseAlpha
        );
        SetRendererActive(
            rightMarker,
            active,
            clampedIntensity,
            rightBaseAlpha
        );
    }

    private void Configure(
        Sprite markerSprite,
        Material markerMaterial,
        EnemyHealth enemy)
    {
        if (markerSprite == null || markerMaterial == null || enemy == null)
            return;

        SpriteRenderer body = GetComponentInChildren<SpriteRenderer>(true);
        bool isBoss = enemy.IsBoss;
        bool isShooter = GetComponent<EnemyShooterMovement>() != null;
        bool isBomber = GetComponent<EnemyBomberMovement>() != null;

        Color color = isBomber
            ? new Color(1f, 0.28f, 0.04f, 0.72f)
            : isShooter
                ? new Color(1f, 0.06f, 0.02f, 0.88f)
                : new Color(1f, 0.16f, 0.04f, 0.58f);
        float diameter = isBoss ? 0.19f : isShooter ? 0.13f : 0.1f;
        float separation = isBoss ? 0.18f : isShooter ? 0.13f : 0.1f;
        float height = isBoss ? 0.18f : 0.12f;

        Transform markerRoot = new GameObject(MarkerRootName).transform;
        markerRoot.SetParent(transform, false);
        markerRoot.localPosition = new Vector3(0f, height, -0.01f);

        leftMarker = CreateMarker(
            "Left",
            markerRoot,
            markerSprite,
            markerMaterial,
            color,
            diameter,
            -separation * 0.5f,
            body
        );
        rightMarker = CreateMarker(
            "Right",
            markerRoot,
            markerSprite,
            markerMaterial,
            color,
            isBomber ? diameter * 1.25f : diameter,
            separation * 0.5f,
            body
        );
        leftBaseAlpha = leftMarker.color.a;
        rightBaseAlpha = rightMarker.color.a;
        configured = true;
    }

    private static SpriteRenderer CreateMarker(
        string name,
        Transform parent,
        Sprite sprite,
        Material material,
        Color color,
        float diameter,
        float x,
        SpriteRenderer body)
    {
        GameObject markerObject = new(name);
        markerObject.layer = parent.gameObject.layer;
        markerObject.transform.SetParent(parent, false);
        markerObject.transform.localPosition = new Vector3(x, 0f, 0f);

        SpriteRenderer marker = markerObject.AddComponent<SpriteRenderer>();
        marker.sprite = sprite;
        marker.sharedMaterial = material;
        marker.color = color;

        if (body != null)
        {
            marker.sortingLayerID = body.sortingLayerID;
            marker.sortingOrder = body.sortingOrder + 2;
        }

        Vector2 spriteSize = sprite.bounds.size;
        float largestSide = Mathf.Max(spriteSize.x, spriteSize.y, 0.001f);
        markerObject.transform.localScale = Vector3.one *
            (diameter / largestSide);
        marker.enabled = false;
        return marker;
    }

    private static void SetRendererActive(
        SpriteRenderer marker,
        bool active,
        float intensity,
        float baseAlpha)
    {
        if (marker == null)
            return;

        Color color = marker.color;
        color.a = Mathf.Clamp01(baseAlpha * intensity);
        marker.color = color;
        marker.enabled = active && intensity > 0f;
    }

    private void OnDisable()
    {
        if (leftMarker != null)
            leftMarker.enabled = false;
        if (rightMarker != null)
            rightMarker.enabled = false;
    }
}
