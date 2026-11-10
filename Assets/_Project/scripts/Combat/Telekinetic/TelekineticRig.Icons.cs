using UnityEngine;
using Subject42.Combat.OrbitalStation;

namespace Subject42.Combat.Telekinetic
{
    public sealed partial class TelekineticRig
    {
        public static bool WeaponKind(TelekineticPart kind) => kind>=TelekineticPart.Pistol && kind<=TelekineticPart.Saw || kind==TelekineticPart.Sniper;
        public static Color FrameTint(TelekineticPart kind) => WeaponKind(kind)?new Color(.28f,.92f,1,1):new Color(.4f,.43f,.48f,.62f);
        public static Color PlateTint(TelekineticPart kind) => WeaponKind(kind)?new Color(.045f,.19f,.25f,.96f):new Color(.12f,.13f,.16f,.32f);
        private Sprite PartSprite(TelekineticPart kind) => kind switch
        {
            TelekineticPart.DMG=>SegmentedModulePresentation.Icon(SegmentedLinkKind.Damage),
            TelekineticPart.FR=>OrbitalPresentationConfig.Active.ShootingRateRewardIcon,
            TelekineticPart.Double=>SegmentedModulePresentation.Icon(SegmentedLinkKind.ExtraShot),
            TelekineticPart.RIC=>SegmentedModulePresentation.Icon(SegmentedLinkKind.Ricochet), _=>ItemIcons[(int)kind]
        };
        private void ApplyIcon(SpriteRenderer renderer,TelekineticPart kind)
        {
            var icon = GetItemIcon(kind);
            renderer.sprite=icon.Sprite; renderer.sharedMaterial=SpriteMaterial; renderer.color=icon.ImageTint;
            var texture=new MaterialPropertyBlock(); texture.SetTexture("_MainTex",renderer.sprite.texture);
            renderer.SetPropertyBlock(texture);
        }
        internal OrbitalRewardIconResolver.Icon GetItemIcon(TelekineticPart kind) =>
            new(PartSprite(kind), kind == TelekineticPart.FR ? Color.white : PartColor(kind), WeaponKind(kind));

        private void DrawIcon(Rect rect, TelekineticPart kind)
        {
            var icon = GetItemIcon(kind);
            Vector2 size = icon.Sprite.textureRect.size;
            size *= Mathf.Min(rect.width / size.x, rect.height / size.y);
            DrawSprite(new Rect(rect.center - size * .5f, size), icon.Sprite, icon.ImageTint);
        }

        private static void DrawSprite(Rect rect, Sprite sprite, Color tint)
        {
            var texture = sprite.texture;
            Rect uv = sprite.textureRect;
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture, new Rect(uv.x / texture.width, uv.y / texture.height,
                uv.width / texture.width, uv.height / texture.height));
            GUI.color = old;
        }
    }
}
