#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Rendering only. Legacy renderers stay enabled so the orbital hit/contact envelope is unchanged.
public sealed class LabWeaponVisualComparison : MonoBehaviour
{
    public SpriteRenderer[] LegacySprites;
    public SpriteRenderer CompactSprite;
    public bool Compact { get; private set; }
    public void SetCompact(bool value)
    {
        Compact = value;
        foreach (var sprite in LegacySprites) if (sprite != null) sprite.forceRenderingOff = value;
        CompactSprite.enabled = value;
    }
}
#endif
