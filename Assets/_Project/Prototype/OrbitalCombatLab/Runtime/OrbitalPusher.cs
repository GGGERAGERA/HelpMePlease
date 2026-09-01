using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalPusher : OrbitalMountedObject
    {
        protected override Color BaseColor => new(1f, .72f, .05f, 1f);

        public OrbitalPusher(OrbitalCombatLabController lab, OrbitalPrimitiveFactory factory)
            : base(OrbitalMountType.Pusher, "Pusher", lab, factory, factory.Circle,
                new Color(1f, .72f, .05f, 1f), new Vector2(.55f, .55f)) { }

        protected override void TickCombat(float deltaTime)
        {
            PusherSettings settings = Lab.Pusher;
            SetRangeCircle(settings.PushRadius);
            SetPrototypeColliderRadius(settings.PushRadius);
            Transform.rotation = Quaternion.Euler(0f, 0f, Ring.GetMountedAngle(this));
            if (Time.time < NextActionTime) return;
            float radiusSqr = settings.PushRadius * settings.PushRadius;
            int hits = 0;
            for (int i = 0; i < Lab.Crowd.DesiredCount; i++)
            {
                OrbitalEnemyCrowd.Enemy enemy = Lab.Crowd.Enemies[i];
                if (!enemy.Active || ((Vector2)enemy.Transform.position -
                    (Vector2)Transform.position).sqrMagnitude > radiusSqr) continue;
                Lab.Crowd.Push(i, Transform.position, settings.PushForce);
                hits++;
            }
            if (hits == 0) return;
            NextActionTime = Time.time + Mathf.Max(.05f, settings.Cooldown);
            Lab.Stats.PushHits += hits;
            TriggerVisual(OrbitalVisualAction.PusherPulse);
            Lab.EmitPulse(Transform.position, new Color(1f, .78f, .08f, .8f),
                settings.PushRadius * 2f, .22f);
            if (hits >= 5) Lab.ImpulseCamera(Mathf.Clamp(hits * .008f, .035f, .12f));
        }
    }
}
