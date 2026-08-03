using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAnomalyEffects : MonoBehaviour
{
    private struct ZoneEffect
    {
        public Object source;
        public float speedMultiplier;
        public Color tint;
        public uint order;
    }

    private readonly List<ZoneEffect> effects = new();

    private EnemyMovement movement;
    private SpriteRenderer bodyRenderer;
    private Color baseColor = Color.white;
    private uint nextOrder;
    private bool hasBaseColor;

    public Color BaseColor => baseColor;

    public static EnemyAnomalyEffects GetOrCreate(EnemyHealth enemy)
    {
        if (enemy == null)
            return null;

        EnemyAnomalyEffects result =
            enemy.GetComponent<EnemyAnomalyEffects>();

        if (result == null)
            result = enemy.gameObject.AddComponent<EnemyAnomalyEffects>();

        result.ResolveReferences();
        return result;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public void EnterZone(
        Object source,
        float speedMultiplier,
        Color tint)
    {
        if (source == null)
            return;

        ResolveReferences();

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].source != source)
                continue;

            ZoneEffect updated = effects[i];
            updated.speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            updated.tint = tint;
            updated.order = ++nextOrder;
            effects[i] = updated;
            ApplyCombinedEffect();
            return;
        }

        effects.Add(new ZoneEffect
        {
            source = source,
            speedMultiplier = Mathf.Max(0.1f, speedMultiplier),
            tint = tint,
            order = ++nextOrder
        });
        ApplyCombinedEffect();
    }

    public void ExitZone(Object source)
    {
        if (ReferenceEquals(source, null))
            return;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].source == source)
                effects.RemoveAt(i);
        }

        ApplyCombinedEffect();
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        hasBaseColor = true;
        ApplyTint();
    }

    private void ResolveReferences()
    {
        if (movement == null)
        {
            movement = GetComponent<EnemyMovement>();

            if (movement == null)
                movement = GetComponentInChildren<EnemyMovement>();

            if (movement == null)
                movement = GetComponentInParent<EnemyMovement>();
        }

        if (bodyRenderer == null)
        {
            EnemyWhiteFlash whiteFlash = GetComponent<EnemyWhiteFlash>();
            bodyRenderer = whiteFlash != null
                ? whiteFlash.TargetRenderer
                : GetComponentInChildren<SpriteRenderer>();
        }

        if (!hasBaseColor && bodyRenderer != null)
        {
            baseColor = bodyRenderer.color;
            hasBaseColor = true;
        }
    }

    private void ApplyCombinedEffect()
    {
        float combinedSpeed = 1f;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].source == null)
            {
                effects.RemoveAt(i);
                continue;
            }

            combinedSpeed *= effects[i].speedMultiplier;
        }

        if (movement != null)
            movement.SetAnomalySpeedMultiplier(combinedSpeed);

        ApplyTint();
    }

    private void ApplyTint()
    {
        if (bodyRenderer == null)
            return;

        int latestIndex = -1;
        uint latestOrder = 0;

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].order < latestOrder)
                continue;

            latestOrder = effects[i].order;
            latestIndex = i;
        }

        if (latestIndex < 0)
        {
            bodyRenderer.color = baseColor;
            return;
        }

        Color tint = effects[latestIndex].tint;
        bodyRenderer.color = new Color(
            baseColor.r * tint.r,
            baseColor.g * tint.g,
            baseColor.b * tint.b,
            baseColor.a
        );
    }

    private void OnDisable()
    {
        effects.Clear();
        nextOrder = 0;

        if (movement != null)
            movement.SetAnomalySpeedMultiplier(1f);

        if (bodyRenderer != null && hasBaseColor)
            bodyRenderer.color = baseColor;
    }
}
