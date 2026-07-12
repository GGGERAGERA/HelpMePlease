using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UnlockRegistry",
    menuName = "Game/Unlocks/Unlock Registry"
)]
public class UnlockRegistry : ScriptableObject
{
    [SerializeField] private List<UnlockableContentData> contents = new();

    public IReadOnlyList<UnlockableContentData> Contents => contents;
}