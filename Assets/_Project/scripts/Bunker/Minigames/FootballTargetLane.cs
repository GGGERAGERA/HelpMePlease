using System;
using UnityEngine;

[Serializable]
public sealed class FootballTargetLane
{
    [SerializeField] private Transform leftAnchor;
    [SerializeField] private Transform rightAnchor;
    [SerializeField, Min(0f)] private float speed = 1.4f;

    public Transform LeftAnchor => leftAnchor;
    public Transform RightAnchor => rightAnchor;
    public float Speed => Mathf.Max(0f, speed);
    public bool IsValid => leftAnchor != null && rightAnchor != null;
}
