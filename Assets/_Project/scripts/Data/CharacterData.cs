using UnityEngine;

[CreateAssetMenu(fileName = "New Character", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{

    [Header("Unlock")]
    public UnlockableContentData unlockData;

    [Header("Identity")]
    public string characterName;

    [TextArea(3, 6)]
    public string description;

    public Sprite portrait;

    [Header("Prefabs")]
    public GameObject characterPrefab;

    [Header("Base Character Stats")]
    public int maxHealth = 100;
    public float moveSpeed = 5f;

    [Header("Special")]
    [TextArea(2, 4)]
    public string specialDescription = "No special ability yet.";
}