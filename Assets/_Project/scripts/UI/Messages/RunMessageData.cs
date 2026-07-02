using UnityEngine;

[CreateAssetMenu(
    fileName = "RunMessageData",
    menuName = "Game/UI/Run Message Data"
)]
public sealed class RunMessageData : ScriptableObject
{
    public RunMessageType messageType;

    [Header("Text")]
    public string title;

    [TextArea(2, 4)]
    public string description;

    [Header("Timing")]
    [Min(0.1f)]
    public float duration = 3f;

    [Header("Audio")]
    public AudioClip sound;
    [Range(0f, 1f)]
    public float volume = 0.8f;
}