using System.Linq;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

namespace Subject42.Combat.Telekinetic
{
    public sealed partial class TelekineticRig
    {
        public static bool WeaponKind(TelekineticPart kind) => kind>=TelekineticPart.Pistol && kind<=TelekineticPart.Saw || kind==TelekineticPart.Sniper;
        public static Color FrameTint(TelekineticPart kind) => WeaponKind(kind)?new Color(.28f,.92f,1,1):new Color(.4f,.43f,.48f,.62f);
        public static Color PlateTint(TelekineticPart kind) => WeaponKind(kind)?new Color(.045f,.19f,.25f,.96f):new Color(.12f,.13f,.16f,.32f);
        public Sprite Icon(TelekineticPart kind) => kind switch
        {
            TelekineticPart.DMG=>SegmentedModulePresentation.Icon(SegmentedLinkKind.Damage),
            TelekineticPart.FR=>SegmentedModulePresentation.Icon(SegmentedLinkKind.FireRate),
            TelekineticPart.Double=>SegmentedModulePresentation.Icon(SegmentedLinkKind.ExtraShot),
            TelekineticPart.RIC=>SegmentedModulePresentation.Icon(SegmentedLinkKind.Ricochet), TelekineticPart.Sniper=>OrbitalRewardIconResolver.Resolve(OrbitalModuleKind.Sniper).Sprite, _=>ItemIcons[(int)kind]
        };
        private void ApplyIcon(SpriteRenderer renderer,TelekineticPart kind)
        {
            renderer.sprite=Icon(kind); renderer.sharedMaterial=SpriteMaterial; renderer.color=PartColor(kind);
            var texture=new MaterialPropertyBlock(); texture.SetTexture("_MainTex",renderer.sprite.texture);
            renderer.SetPropertyBlock(texture);
        }
        private SpriteRenderer[] sniperLayers;

        private void DrawIcon(Rect rect, TelekineticPart kind)
        {
            if (kind == TelekineticPart.Sniper)
            {
                DrawSniperArt(rect);
                return;
            }

            var sprite = Icon(kind);
            Vector2 size = sprite.textureRect.size;
            size *= Mathf.Min(rect.width / size.x, rect.height / size.y);
            DrawSprite(new Rect(rect.center - size * .5f, size), sprite, WeaponKind(kind) ? Color.white : PartColor(kind));
        }

        private void DrawSniperArt(Rect rect)
        {
            if (sniperLayers == null)
            {
                var visual = SniperVisual.GetComponent<OrbitalModuleView>();
                sniperLayers = visual.Sprites.Where(layer => layer.enabled && layer != visual.Halo)
                    .OrderBy(layer => layer.sortingOrder).ToArray();
            }

            Rect LayerRect(SpriteRenderer layer)
            {
                var local = SniperVisual.transform.worldToLocalMatrix * layer.transform.localToWorldMatrix;
                var bounds = layer.localBounds;
                Vector3 min = local.MultiplyPoint3x4(bounds.min);
                Vector3 max = local.MultiplyPoint3x4(bounds.max);
                return Rect.MinMaxRect(min.x, -max.y, max.x, -min.y);
            }

            Rect art = LayerRect(sniperLayers[0]);
            foreach (var layer in sniperLayers)
            {
                Rect bounds = LayerRect(layer);
                art = Rect.MinMaxRect(Mathf.Min(art.xMin, bounds.xMin), Mathf.Min(art.yMin, bounds.yMin),
                    Mathf.Max(art.xMax, bounds.xMax), Mathf.Max(art.yMax, bounds.yMax));
            }

            // The body and barrel sprites have wide transparent margins in the weapon sheet.
            const float sniperIconScale = 1.55f;
            float scale = Mathf.Min(rect.width / art.width, rect.height / art.height) * sniperIconScale;
            foreach (var layer in sniperLayers)
            {
                Rect bounds = LayerRect(layer);
                DrawSprite(new Rect(rect.center + (bounds.position - art.center) * scale, bounds.size * scale),
                    layer.sprite, layer.color);
            }
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
