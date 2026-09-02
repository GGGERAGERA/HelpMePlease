using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    /// <summary>Visual and synchronization heart of the isolated Lab station.</summary>
    public sealed class OrbitalCoreSystem
    {
        private readonly OrbitalCombatLabController lab;
        private readonly Transform root;
        private readonly SpriteRenderer core;
        private readonly SpriteRenderer[] segments = new SpriteRenderer[4];
        private readonly LineRenderer wave;
        private readonly bool[] crossed = new bool[OrbitalCombatLabController.MaxRings];
        private float nextPulse;
        private float pulseBorn;
        private float pulseRadius;
        private bool pulseActive;

        public bool PulseActive => pulseActive;
        public float PulseRadius => pulseRadius;

        public OrbitalCoreSystem(OrbitalCombatLabController lab, Transform parent, OrbitalPrimitiveFactory factory)
        {
            this.lab = lab;
            root = new GameObject("ORBITAL CORE").transform;
            root.SetParent(parent, false);
            core = factory.CreateSprite("Core Glow", root, factory.Circle,
                new Color(.55f, 1f, 1f, .58f), new Vector2(1.05f, 1.05f), 6);
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = factory.CreateSprite($"Core Segment {i + 1}", root, factory.Square,
                    i % 2 == 0 ? new Color(.72f, 1f, 1f, .9f) : new Color(.72f, .3f, 1f, .9f),
                    new Vector2(.13f, .38f), 16);
            }
            wave = factory.CreateCircleLine("Core Pulse Wave", root, 13, 128);
            wave.enabled = false;
            nextPulse = Time.unscaledTime + 1.2f;
        }

        public void Tick()
        {
            OrbitalCoreSettings settings = lab.Core;
            root.position = lab.PlayerPosition;
            float now = Time.unscaledTime;
            float beat = 1f + Mathf.Sin(now * 3.2f) * .08f;
            float scale = beat * (1f + Mathf.Min(.25f, settings.Level * .025f));
            core.transform.localScale = Vector3.one * scale;
            core.color = Color.Lerp(new Color(.34f, 1f, 1f, .5f), new Color(.86f, .55f, 1f, .72f),
                Mathf.Sin(now * 1.7f) * .25f + .25f);
            int visible = Mathf.Clamp(2 + settings.Level / 2 + lab.RingCount / 10, 2, 4);
            for (int i = 0; i < segments.Length; i++)
            {
                bool active = i < visible;
                segments[i].gameObject.SetActive(active);
                if (!active) continue;
                float angle = now * (48f + settings.Level * 2f) * (i % 2 == 0 ? 1f : -1f) + i * 90f;
                float radius = .66f + (i & 1) * .13f;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                segments[i].transform.position = lab.PlayerPosition + direction * radius;
                segments[i].transform.rotation = Quaternion.Euler(0f, 0f, angle + 90f);
            }

            if (!pulseActive && now >= nextPulse) BeginPulse();
            if (pulseActive) TickPulse(now);
        }

        public void ForcePulse()
        {
            pulseActive = false;
            BeginPulse();
        }

        public void Reset()
        {
            pulseActive = false;
            wave.enabled = false;
            nextPulse = Time.unscaledTime + 1.2f;
            for (int i = 0; i < crossed.Length; i++) crossed[i] = false;
        }

        private void BeginPulse()
        {
            pulseActive = true;
            pulseBorn = Time.unscaledTime;
            pulseRadius = 0f;
            for (int i = 0; i < crossed.Length; i++) crossed[i] = false;
            wave.enabled = true;
            lab.Stats.CorePulses++;
            lab.EmitPulse(lab.PlayerPosition, new Color(.62f, 1f, 1f, .72f), 1.8f, .32f);
        }

        private void TickPulse(float now)
        {
            OrbitalCoreSettings settings = lab.Core;
            pulseRadius = (now - pulseBorn) * Mathf.Max(1f, settings.PulseTravelSpeed);
            OrbitalPrimitiveFactory.SetCircle(wave, lab.PlayerPosition, Mathf.Max(.05f, pulseRadius));
            float alpha = Mathf.Clamp01(settings.PulseBrightness * (.82f - pulseRadius / Mathf.Max(4f, lab.OuterRingRadius * 1.4f)));
            Color color = new(.45f, .96f, 1f, alpha);
            wave.startColor = wave.endColor = color;
            wave.startWidth = wave.endWidth = Mathf.Max(.03f, settings.PulseWidth * .12f);

            for (int i = 0; i < lab.RingCount; i++)
            {
                if (crossed[i] || pulseRadius < lab.Rings[i].Settings.Radius) continue;
                crossed[i] = true;
                HitRing(lab.Rings[i]);
            }
            if (pulseRadius <= lab.OuterRingRadius + 2f) return;
            pulseActive = false;
            wave.enabled = false;
            nextPulse = now + Mathf.Max(.75f, settings.PulseInterval);
        }

        private void HitRing(OrbitalRing ring)
        {
            ring.FlashField(.3f);
            float power = ring.Upgrades.ResonancePower * lab.Core.ResonancePowerMultiplier;
            for (int slot = 0; slot < ring.Mounts.Length; slot++)
            {
                OrbitalMountedObject mounted = ring.Mounts[slot];
                if (mounted == null) continue;
                mounted.FlashResonance(.28f);
                if (!lab.Core.PulseGameplayEffect || lab.Core.PulseMode == OrbitalCorePulseMode.Visual) continue;
                bool fire = lab.Core.PulseMode == OrbitalCorePulseMode.Cascade ||
                    (lab.Core.PulseMode == OrbitalCorePulseMode.Volley && mounted is OrbitalGun);
                if (fire) mounted.OnCorePulse(power);
            }
            if (lab.Core.PulseMode == OrbitalCorePulseMode.Resonance ||
                lab.Core.PulseMode == OrbitalCorePulseMode.Cascade)
                lab.Pattern.BoostLinks(.38f, power);
        }
    }
}
