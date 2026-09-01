using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalLinkNode : OrbitalMountedObject
    {
        protected override Color BaseColor => new(1f, .08f, .86f, 1f);

        public OrbitalLinkNode(OrbitalCombatLabController lab, OrbitalPrimitiveFactory factory)
            : base(OrbitalMountType.LinkNode, "Link Node", lab, factory, factory.Circle,
                new Color(1f, .08f, .86f, 1f), new Vector2(.46f, .46f)) { }

        protected override void TickCombat(float deltaTime)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.6f + Slot) * .12f;
            SetPrimitiveVisualScale(pulse * Lab.WeaponVisuals.LinkNodeScale);
            Renderer.color = Color.Lerp(BaseColor, new Color(.7f, .18f, 1f, 1f),
                Mathf.Sin(Time.unscaledTime * 3.2f) * .25f + .25f);
        }

        public override void SetRangesVisible(bool visible) => base.SetRangesVisible(false);
    }
}
