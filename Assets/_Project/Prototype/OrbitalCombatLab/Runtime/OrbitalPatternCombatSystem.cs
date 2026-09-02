using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalPatternCombatSystem
    {
        private const int MaxNodes = 64;
        private const int MaxLinks = 128;
        private readonly OrbitalCombatLabController lab;
        private readonly OrbitalLinkNode[] nodes = new OrbitalLinkNode[MaxNodes];
        private readonly OrbitalMountedObject[] aligned = new OrbitalMountedObject[OrbitalCombatLabController.MaxMountedObjects];
        private readonly LineRenderer[] links = new LineRenderer[MaxLinks];
        private readonly int[] linkA = new int[MaxLinks];
        private readonly int[] linkB = new int[MaxLinks];
        private readonly float[] targetLinkCooldown = new float[OrbitalEnemyCrowd.Capacity];
        private readonly float[,] targetFieldCooldown = new float[OrbitalCombatLabController.MaxRings, OrbitalEnemyCrowd.Capacity];
        private readonly float[] nextRingPulse = new float[OrbitalCombatLabController.MaxRings];
        private readonly float[] conductorUntil = new float[OrbitalCombatLabController.MaxRings];
        private readonly LineRenderer resonanceLine;
        private readonly LineRenderer placementPreview;
        private int nodeCount;
        private int linkCount;
        private int cycleIndex;
        private bool alignmentLatched;
        private float nextResonance;
        private float resonanceLineUntil;
        private int currentAlignmentSignature;
        private int lastTriggeredSignature;
        private float linksBoostUntil;
        private float linksBoostPower = 1f;

        public int ActiveLinks => linkCount;

        public void BoostLinks(float duration, float power)
        {
            linksBoostUntil = Mathf.Max(linksBoostUntil, Time.unscaledTime + duration);
            linksBoostPower = Mathf.Max(linksBoostPower, power);
        }

        public void PlaceTestEnemyOnLink()
        {
            if (linkCount == 0 || lab.Crowd.DesiredCount == 0)
            {
                lab.Notify("Нужны хотя бы одна Link-связь и один враг.");
                return;
            }
            OrbitalEnemyCrowd.Enemy enemy = lab.Crowd.Enemies[0];
            Vector2 a = nodes[linkA[0]].Transform.position;
            Vector2 b = nodes[linkB[0]].Transform.position;
            Vector2 position = Vector2.Lerp(a, b, .5f);
            enemy.Transform.position = position;
            if (enemy.Body != null) enemy.Body.position = position;
            lab.Notify("Тестовый враг помещён на первую фиолетовую связь.");
        }

        public OrbitalPatternCombatSystem(OrbitalCombatLabController lab,
            Transform parent, OrbitalPrimitiveFactory factory)
        {
            this.lab = lab;
            Transform root = new GameObject("Pattern Combat Runtime").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < MaxLinks; i++)
            {
                links[i] = factory.CreateCircleLine($"Energy Link {i + 1:00}", root, 9, 2);
                links[i].loop = false;
                links[i].enabled = false;
            }
            resonanceLine = factory.CreateCircleLine("Alignment Resonance", root, 14, 2);
            resonanceLine.loop = false;
            resonanceLine.enabled = false;
            placementPreview = factory.CreateCircleLine("Golden Path Link Preview", root, 15, 2);
            placementPreview.loop = false;
            placementPreview.startWidth = placementPreview.endWidth = .045f;
            placementPreview.enabled = false;
        }

        public void Tick(float deltaTime)
        {
            GatherNodes();
            BuildLinks();
            UpdateLinks();
            TickResonanceLine();
            if (lab.PatternCombat)
            {
                TickLinkDamage();
                TickRingFields();
                TickResonance();
            }
            else
            {
                alignmentLatched = false;
                resonanceLine.enabled = false;
            }
            lab.Stats.ActiveLinks = linkCount;
        }

        public void Reset()
        {
            nodeCount = linkCount = 0;
            alignmentLatched = false;
            nextResonance = resonanceLineUntil = 0f;
            currentAlignmentSignature = lastTriggeredSignature = 0;
            for (int i = 0; i < links.Length; i++) links[i].enabled = false;
            resonanceLine.enabled = false;
            placementPreview.enabled = false;
            for (int i = 0; i < targetLinkCooldown.Length; i++) targetLinkCooldown[i] = 0f;
            for (int r = 0; r < OrbitalCombatLabController.MaxRings; r++)
            {
                nextRingPulse[r] = conductorUntil[r] = 0f;
                for (int e = 0; e < OrbitalEnemyCrowd.Capacity; e++) targetFieldCooldown[r, e] = 0f;
            }
        }

        private void GatherNodes()
        {
            nodeCount = 0;
            for (int i = 0; i < lab.MountedCount && nodeCount < MaxNodes; i++)
                if (lab.MountedObjects[i] is OrbitalLinkNode node && !node.IsDragging)
                    nodes[nodeCount++] = node;
            for (int i = nodeCount; i < nodes.Length; i++) nodes[i] = null;
        }

        private void BuildLinks()
        {
            linkCount = 0;
            if (!lab.Links.ShowLinks || nodeCount < 2)
            {
                DisableUnusedLinks();
                return;
            }
            int allowedLinks = Mathf.Clamp(24 + lab.Core.LinkCapacityBonus, 1, MaxLinks);
            if (lab.Links.Mode == OrbitalLinkMode.Pairs)
            {
                for (int i = 0; i + 1 < nodeCount && linkCount < allowedLinks; i += 2)
                    AddLink(i, i + 1);
            }
            else if (lab.Links.Mode == OrbitalLinkMode.Chain)
            {
                for (int i = 0; i + 1 < nodeCount && linkCount < allowedLinks; i++)
                    AddLink(i, i + 1);
            }
            else
            {
                float distance = lab.Links.MaxDistance * lab.Core.LinkRangeMultiplier;
                float maxSqr = distance * distance;
                for (int i = 0; i < nodeCount && linkCount < allowedLinks; i++)
                {
                    int nearest = -1;
                    float nearestSqr = maxSqr;
                    for (int j = 0; j < nodeCount; j++)
                    {
                        if (i == j) continue;
                        float sqr = ((Vector2)nodes[i].Transform.position -
                            (Vector2)nodes[j].Transform.position).sqrMagnitude;
                        if (sqr >= nearestSqr) continue;
                        nearestSqr = sqr;
                        nearest = j;
                    }
                    if (nearest >= 0 && !ContainsLink(i, nearest)) AddLink(i, nearest);
                }
            }
            DisableUnusedLinks();
        }

        private void AddLink(int a, int b)
        {
            if (linkCount >= MaxLinks) return;
            linkA[linkCount] = a;
            linkB[linkCount] = b;
            linkCount++;
        }

        private bool ContainsLink(int a, int b)
        {
            for (int i = 0; i < linkCount; i++)
                if ((linkA[i] == a && linkB[i] == b) || (linkA[i] == b && linkB[i] == a)) return true;
            return false;
        }

        private void DisableUnusedLinks()
        {
            for (int i = linkCount; i < links.Length; i++) links[i].enabled = false;
        }

        private void UpdateLinks()
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * lab.Links.PulseSpeed) * .22f;
            bool boosted = Time.unscaledTime < linksBoostUntil;
            float coreBoost = boosted ? Mathf.Max(1.25f, linksBoostPower) : 1f;
            if (!boosted) linksBoostPower = 1f;
            Color color = lab.Links.LineColor;
            for (int i = 0; i < linkCount; i++)
            {
                LineRenderer line = links[i];
                line.enabled = true;
                line.SetPosition(0, nodes[linkA[i]].Transform.position);
                line.SetPosition(1, nodes[linkB[i]].Transform.position);
                float ringPower = LinkRingPower(i);
                line.startWidth = line.endWidth = lab.Links.LineWidth * pulse *
                    Mathf.Lerp(1f, ringPower, .35f) * coreBoost;
                Color poweredColor = color;
                poweredColor.a = Mathf.Clamp01(lab.LinkAlpha * (.58f + pulse * .18f) *
                    Mathf.Lerp(1f, ringPower, .28f) * Mathf.Lerp(1f, coreBoost, .35f));
                line.startColor = line.endColor = poweredColor;
            }
        }

        private void TickLinkDamage()
        {
            if (!lab.Links.DealDamage || linkCount == 0) return;
            float now = Time.unscaledTime;
            float hitRadiusSqr = Mathf.Pow(Mathf.Max(.08f, lab.Links.LineWidth * 2.2f), 2f);
            int parity = Time.frameCount & 1;
            for (int link = parity; link < linkCount; link += 2)
            {
                Vector2 a = nodes[linkA[link]].Transform.position;
                Vector2 b = nodes[linkB[link]].Transform.position;
                for (int enemyIndex = 0; enemyIndex < lab.Crowd.DesiredCount; enemyIndex++)
                {
                    OrbitalEnemyCrowd.Enemy enemy = lab.Crowd.Enemies[enemyIndex];
                    if (!enemy.Active || now < targetLinkCooldown[enemyIndex]) continue;
                    Vector2 point = enemy.Transform.position;
                    if (DistanceToSegmentSqr(point, a, b) > hitRadiusSqr) continue;
                    targetLinkCooldown[enemyIndex] = now + lab.Links.HitCooldown;
                    lab.Crowd.Damage(enemyIndex, lab.Links.Damage * LinkRingPower(link) *
                        lab.Core.GlobalDamageMultiplier * (Time.unscaledTime < linksBoostUntil ? linksBoostPower : 1f));
                    lab.Stats.LinkHits++;
                    lab.EmitPulse(point, new Color(1f, .08f, .88f, .82f), .38f, .11f);
                }
            }
        }

        private void TickResonance()
        {
            if (!lab.Resonance.Enabled || lab.MountedCount < lab.Resonance.MinimumObjects) return;
            int count = FindAlignment();
            bool alignedNow = count >= Mathf.Clamp(lab.Resonance.MinimumObjects, 2, 6);
            if (!alignedNow)
            {
                alignmentLatched = false;
                return;
            }
            if ((alignmentLatched && currentAlignmentSignature == lastTriggeredSignature) ||
                Time.unscaledTime < nextResonance) return;
            alignmentLatched = true;
            lastTriggeredSignature = currentAlignmentSignature;
            nextResonance = Time.unscaledTime + lab.Resonance.Cooldown;
            TriggerResonance(count);
        }

        private int FindAlignment()
        {
            currentAlignmentSignature = 0;
            Vector2 center = lab.PlayerPosition;
            float tolerance = Mathf.Clamp(lab.Resonance.AlignmentTolerance, 1f, 45f);
            for (int anchorIndex = 0; anchorIndex < lab.MountedCount; anchorIndex++)
            {
                OrbitalMountedObject anchor = lab.MountedObjects[anchorIndex];
                if (anchor == null || anchor.IsDragging || anchor.Ring == null) continue;
                float anchorAngle = AngleOf((Vector2)anchor.Transform.position - center);
                int count = 0;
                int signature = 17;
                aligned[count++] = anchor;
                signature = signature * 31 + anchorIndex + 1;
                for (int i = 0; i < lab.MountedCount; i++)
                {
                    OrbitalMountedObject candidate = lab.MountedObjects[i];
                    if (candidate == null || candidate == anchor || candidate.IsDragging ||
                        candidate.Ring == null || candidate.Ring == anchor.Ring) continue;
                    float angle = AngleOf((Vector2)candidate.Transform.position - center);
                    if (Mathf.Abs(Mathf.DeltaAngle(anchorAngle, angle)) <= tolerance)
                    {
                        aligned[count++] = candidate;
                        signature = signature * 31 + i + 1;
                    }
                }
                if (count >= lab.Resonance.MinimumObjects)
                {
                    currentAlignmentSignature = signature;
                    return count;
                }
            }
            return 0;
        }

        private void TriggerResonance(int count)
        {
            OrbitalResonanceMode mode = lab.Resonance.Mode;
            if (mode == OrbitalResonanceMode.Cycle)
                mode = (OrbitalResonanceMode)(cycleIndex++ % 3);
            lab.Stats.Resonances++;
            lab.Stats.LastResonance = mode == OrbitalResonanceMode.RadialVolley ? "RADIAL VOLLEY" :
                mode == OrbitalResonanceMode.Beam ? "BEAM" : "SHOCKWAVE";
            Vector2 center = lab.PlayerPosition;
            OrbitalMountedObject outer = aligned[0];
            float outerDistance = 0f;
            for (int i = 0; i < count; i++)
            {
                aligned[i].FlashResonance(.28f);
                float distance = Vector2.Distance(center, aligned[i].Transform.position);
                if (distance > outerDistance) { outerDistance = distance; outer = aligned[i]; }
            }
            for (int i = 0; i < nodeCount; i++)
            {
                int ringIndex = nodes[i].Ring.Index;
                conductorUntil[ringIndex] = Time.unscaledTime + .32f;
                nodes[i].Ring.FlashField(.32f);
            }
            Vector2 direction = ((Vector2)outer.Transform.position - center).normalized;
            Vector2 end = center + direction * lab.Resonance.Range;
            ShowResonanceLine(center - direction * .35f, end, mode);
            lab.EmitPulse(outer.Transform.position, new Color(1f, .25f, .95f, .9f), 1.1f, .24f);
            lab.ImpulseCamera(.045f);
            if (lab.Resonance.VisualOnly) return;

            if (mode == OrbitalResonanceMode.RadialVolley)
            {
                int guns = 0;
                for (int i = 0; i < count; i++)
                    if (aligned[i] is OrbitalGun gun) { gun.FireResonance(direction); guns++; }
                if (guns == 0)
                    lab.Projectiles.Fire(outer.Transform.position, direction, 24f,
                        lab.Resonance.Damage * ResonanceRingPower(count) * lab.Core.GlobalDamageMultiplier,
                        lab.Resonance.Range * lab.Core.GlobalEffectSizeMultiplier);
            }
            else if (mode == OrbitalResonanceMode.Beam)
                DamageSegment(center, end,
                    lab.Resonance.Damage * ResonanceRingPower(count) * lab.Core.GlobalDamageMultiplier,
                    .24f * lab.Core.GlobalEffectSizeMultiplier);
            else
                Shockwave(outer.Transform.position, lab.Resonance.Range * .42f * lab.Core.GlobalEffectSizeMultiplier,
                    lab.Resonance.Damage * .55f * lab.Core.GlobalDamageMultiplier, 11f);
        }

        private void ShowResonanceLine(Vector2 start, Vector2 end, OrbitalResonanceMode mode)
        {
            resonanceLine.enabled = true;
            resonanceLine.SetPosition(0, start);
            resonanceLine.SetPosition(1, end);
            resonanceLine.startWidth = resonanceLine.endWidth = mode == OrbitalResonanceMode.Beam ? .16f : .075f;
            Color color = new(1f, .35f, .92f, Mathf.Clamp01(.8f * lab.ResonanceFlash));
            resonanceLine.startColor = resonanceLine.endColor = color;
            resonanceLineUntil = Time.unscaledTime + (mode == OrbitalResonanceMode.Beam ? .28f : .18f);
        }

        private void TickResonanceLine()
        {
            if (resonanceLine.enabled && Time.unscaledTime >= resonanceLineUntil)
                resonanceLine.enabled = false;
        }

        private void DamageSegment(Vector2 start, Vector2 end, float damage, float width)
        {
            float widthSqr = width * width;
            for (int i = 0; i < lab.Crowd.DesiredCount; i++)
            {
                OrbitalEnemyCrowd.Enemy enemy = lab.Crowd.Enemies[i];
                if (!enemy.Active || DistanceToSegmentSqr(enemy.Transform.position, start, end) > widthSqr) continue;
                lab.Crowd.Damage(i, damage);
                lab.EmitPulse(enemy.Transform.position, new Color(1f, .3f, .9f, .7f), .3f, .1f);
            }
        }

        private void Shockwave(Vector2 origin, float radius, float damage, float push)
        {
            float radiusSqr = radius * radius;
            for (int i = 0; i < lab.Crowd.DesiredCount; i++)
            {
                OrbitalEnemyCrowd.Enemy enemy = lab.Crowd.Enemies[i];
                if (!enemy.Active || ((Vector2)enemy.Transform.position - origin).sqrMagnitude > radiusSqr) continue;
                lab.Crowd.Damage(i, damage);
                if (enemy.Active) lab.Crowd.Push(i, origin, push);
            }
            lab.EmitPulse(origin, new Color(.75f, .22f, 1f, .72f), radius * 2f, .3f);
        }

        private void TickRingFields()
        {
            float now = Time.unscaledTime;
            for (int ringIndex = 0; ringIndex < lab.RingCount; ringIndex++)
            {
                OrbitalRing ring = lab.Rings[ringIndex];
                OrbitalRingSettings settings = ring.Settings;
                if (settings.FieldMode == OrbitalRingFieldMode.Ghost) continue;
                if (settings.FieldMode == OrbitalRingFieldMode.Pulse && now >= nextRingPulse[ringIndex])
                {
                    nextRingPulse[ringIndex] = now + Mathf.Max(.15f, settings.PulseInterval);
                    PulseRing(ring);
                    continue;
                }
                bool conductor = settings.FieldMode == OrbitalRingFieldMode.Conductor &&
                    now < conductorUntil[ringIndex];
                if (settings.FieldMode == OrbitalRingFieldMode.Conductor && !conductor) continue;
                for (int enemyIndex = 0; enemyIndex < lab.Crowd.DesiredCount; enemyIndex++)
                {
                    OrbitalEnemyCrowd.Enemy enemy = lab.Crowd.Enemies[enemyIndex];
                    float effectiveFieldWidth = settings.FieldWidth * ring.Upgrades.EffectSizeMultiplier *
                        lab.Core.GlobalEffectSizeMultiplier;
                    if (!enemy.Active || ring.DistanceToPath(lab.PlayerPosition,
                        enemy.Transform.position) > effectiveFieldWidth) continue;
                    if (settings.FieldMode == OrbitalRingFieldMode.Slow)
                        lab.Crowd.Slow(enemyIndex, settings.SlowMultiplier, .18f);
                    else if (now >= targetFieldCooldown[ringIndex, enemyIndex])
                    {
                        targetFieldCooldown[ringIndex, enemyIndex] = now + settings.FieldTargetCooldown;
                        lab.Crowd.Damage(enemyIndex, settings.FieldDamage * ring.Upgrades.DamageMultiplier *
                            lab.Core.GlobalDamageMultiplier);
                        lab.Stats.RingFieldHits++;
                    }
                }
            }
        }

        private void PulseRing(OrbitalRing ring)
        {
            OrbitalRingSettings settings = ring.Settings;
            for (int i = 0; i < lab.Crowd.DesiredCount; i++)
            {
                OrbitalEnemyCrowd.Enemy enemy = lab.Crowd.Enemies[i];
                if (!enemy.Active || ring.DistanceToPath(lab.PlayerPosition,
                    enemy.Transform.position) > settings.FieldWidth * 2.2f * ring.Upgrades.EffectSizeMultiplier *
                    lab.Core.GlobalEffectSizeMultiplier) continue;
                lab.Crowd.Push(i, lab.PlayerPosition, settings.FieldPushForce * ring.Upgrades.PushMultiplier);
                lab.Stats.RingFieldHits++;
            }
            ring.FlashField(.22f);
            lab.EmitPulse(lab.PlayerPosition, new Color(.2f, .8f, 1f, .28f),
                ring.MaximumVisualRadius * 2f, .28f);
        }

        private static float AngleOf(Vector2 value) => Mathf.Atan2(value.y, value.x) * Mathf.Rad2Deg;

        private float LinkRingPower(int link)
        {
            OrbitalRing a = nodes[linkA[link]].Ring;
            OrbitalRing b = nodes[linkB[link]].Ring;
            float powerA = a != null ? a.Upgrades.DamageMultiplier * a.Upgrades.LinkPowerMultiplier : 1f;
            float powerB = b != null ? b.Upgrades.DamageMultiplier * b.Upgrades.LinkPowerMultiplier : 1f;
            return (powerA + powerB) * .5f;
        }

        public void ShowGoldenLinkPreview(Vector2 prospectivePosition)
        {
            OrbitalLinkNode previous = null;
            for (int i = 0; i < lab.MountedCount; i++)
                if (lab.MountedObjects[i] is OrbitalLinkNode node && !node.IsDragging) previous = node;
            if (previous == null)
            {
                placementPreview.enabled = false;
                return;
            }
            placementPreview.enabled = true;
            placementPreview.SetPosition(0, previous.Transform.position);
            placementPreview.SetPosition(1, prospectivePosition);
            Color color = lab.Links.LineColor;
            color.a = .34f;
            placementPreview.startColor = placementPreview.endColor = color;
        }

        public void ClearGoldenLinkPreview() => placementPreview.enabled = false;

        private float ResonanceRingPower(int count)
        {
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                OrbitalRing ring = aligned[i] != null ? aligned[i].Ring : null;
                sum += ring != null ? ring.Upgrades.DamageMultiplier * ring.Upgrades.LinkPowerMultiplier : 1f;
            }
            return sum / Mathf.Max(1, count);
        }

        private static float DistanceToSegmentSqr(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSqr = segment.sqrMagnitude;
            if (lengthSqr < .00001f) return (point - start).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSqr);
            return (point - (start + segment * t)).sqrMagnitude;
        }
    }
}
