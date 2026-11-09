using Subject42.Combat.OrbitalStation;
using UnityEngine.UI;

internal static class OrbitalRewardIconView
{
    internal static void Set(Image image, OrbitalRewardIconResolver.Icon icon, float alpha = 1f)
    {
        var color = icon.ImageTint;
        color.a *= alpha;
        image.sprite = icon.Sprite;
        image.color = color;
        image.enabled = icon.Sprite != null;
    }
}
