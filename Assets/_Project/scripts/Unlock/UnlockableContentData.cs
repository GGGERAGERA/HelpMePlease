using UnityEngine;

[CreateAssetMenu(
    fileName = "UnlockableContent",
    menuName = "Game/Unlocks/Unlockable Content"
)]
public class UnlockableContentData : ScriptableObject
{
    public string id;
    public string displayName;
    public UnlockableContentType contentType;
    public bool unlockedByDefault;

    [TextArea]
    public string lockedDescription;

    public UnlockConditionData condition;
}