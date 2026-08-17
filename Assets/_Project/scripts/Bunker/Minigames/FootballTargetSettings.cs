using System;
using UnityEngine;

[Serializable]
public sealed class FootballTargetSettings
{
    [SerializeField] private FootballScoreZoneType type;
    [SerializeField] private Color color = Color.white;
    [SerializeField, Min(0.1f)] private float sizeScale = 1f;
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0)] private int score = 5;

    public FootballScoreZoneType Type => type;
    public Color Color => color;
    public float SizeScale => Mathf.Max(0.1f, sizeScale);
    public float MoveSpeed => Mathf.Max(0f, moveSpeed);
    public int Score => Mathf.Max(0, score);

    public FootballTargetSettings(
        FootballScoreZoneType targetType,
        Color targetColor,
        float targetSizeScale,
        float targetMoveSpeed,
        int targetScore)
    {
        type = targetType;
        color = targetColor;
        sizeScale = targetSizeScale;
        moveSpeed = targetMoveSpeed;
        score = targetScore;
    }
}
