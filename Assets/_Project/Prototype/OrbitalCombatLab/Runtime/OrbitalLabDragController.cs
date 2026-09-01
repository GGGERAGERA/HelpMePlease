using UnityEngine;

namespace Subject42.Prototype.OrbitalCombatLab
{
    public sealed class OrbitalLabDragController : MonoBehaviour
    {
        public bool IsDragging => dragged != null;
        public int CandidateRing { get; private set; } = -1;
        public int CandidateSlot { get; private set; } = -1;

        private OrbitalCombatLabController lab;
        private OrbitalMountedObject dragged;
        private OrbitalRing originalRing;
        private int originalSlot;
        private float previousTimeScale = 1f;

        public void Configure(OrbitalCombatLabController controller) => lab = controller;

        public void Tick()
        {
            if (lab == null || Camera.main == null) return;
            if (!IsDragging)
            {
                if (!Input.GetMouseButtonDown(0) || lab.DebugUI.PointerOverMenu) return;
                Vector2 world = MouseWorld();
                OrbitalMountedObject best = null;
                float bestSqr = .65f * .65f;
                for (int i = 0; i < lab.MountedCount; i++)
                {
                    OrbitalMountedObject mounted = lab.MountedObjects[i];
                    if (mounted == null) continue;
                    float sqr = ((Vector2)mounted.Transform.position - world).sqrMagnitude;
                    if (sqr > bestSqr) continue;
                    bestSqr = sqr;
                    best = mounted;
                }
                if (best != null) BeginDrag(best);
                return;
            }

            if (dragged.IsDestroyed)
            {
                CancelDrag();
                return;
            }
            Vector2 mouseWorld = MouseWorld();
            dragged.SetDraggedPosition(mouseWorld);
            FindCandidate(mouseWorld);

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelDrag();
                return;
            }
            if (Input.GetMouseButtonUp(0)) EndDrag();
        }

        public void CancelDrag()
        {
            if (dragged != null)
            {
                if (originalRing != null && lab != null && lab.ContainsRing(originalRing) &&
                    originalSlot >= 0 && originalSlot < originalRing.Settings.MaxMounts &&
                    originalRing.Mounts[originalSlot] == null)
                    dragged.Attach(originalRing, originalSlot);
                else if (!dragged.IsDestroyed)
                    AttachToFirstFree(dragged);
            }
            dragged = null;
            originalRing = null;
            CandidateRing = CandidateSlot = -1;
            RestoreTimeScale();
        }

        private void BeginDrag(OrbitalMountedObject mounted)
        {
            dragged = mounted;
            originalRing = mounted.Ring;
            originalSlot = mounted.Slot;
            mounted.Detach();
            mounted.IsDragging = true;
            previousTimeScale = Time.timeScale;
            if (lab.SlowDuringDrag && Time.timeScale > 0f)
                Time.timeScale = .2f;
        }

        private void EndDrag()
        {
            if (CandidateRing >= 0 && CandidateRing < lab.RingCount && CandidateSlot >= 0)
            {
                OrbitalRing ring = lab.Rings[CandidateRing];
                if (CandidateSlot < ring.Settings.MaxMounts && ring.Mounts[CandidateSlot] == null)
                    dragged.Attach(ring, CandidateSlot);
                else ReturnToOriginal();
            }
            else ReturnToOriginal();
            dragged = null;
            originalRing = null;
            CandidateRing = CandidateSlot = -1;
            RestoreTimeScale();
        }

        private void ReturnToOriginal()
        {
            if (originalRing != null && lab.ContainsRing(originalRing) &&
                originalSlot < originalRing.Settings.MaxMounts && originalRing.Mounts[originalSlot] == null)
                dragged.Attach(originalRing, originalSlot);
            else AttachToFirstFree(dragged);
        }

        private void AttachToFirstFree(OrbitalMountedObject mounted)
        {
            for (int r = 0; r < lab.RingCount; r++)
            {
                OrbitalRing ring = lab.Rings[r];
                int slot = ring.FindFreeSlot(lab.PlayerPosition, mounted.Transform.position);
                if (slot < 0) continue;
                mounted.Attach(ring, slot);
                return;
            }
            mounted.IsDragging = false;
        }

        private void FindCandidate(Vector2 mouseWorld)
        {
            CandidateRing = CandidateSlot = -1;
            float bestDistance = .72f;
            for (int i = 0; i < lab.RingCount; i++)
            {
                OrbitalRing ring = lab.Rings[i];
                float radialDistance = Mathf.Abs(Vector2.Distance(mouseWorld, lab.PlayerPosition) -
                    ring.Settings.Radius);
                if (radialDistance >= bestDistance) continue;
                int slot = ring.FindFreeSlot(lab.PlayerPosition, mouseWorld);
                if (slot < 0) continue;
                bestDistance = radialDistance;
                CandidateRing = i;
                CandidateSlot = slot;
            }
        }

        private Vector2 MouseWorld()
        {
            Vector3 screen = Input.mousePosition;
            screen.z = -Camera.main.transform.position.z;
            return Camera.main.ScreenToWorldPoint(screen);
        }

        private void RestoreTimeScale()
        {
            if (lab != null && lab.SlowDuringDrag)
                Time.timeScale = Mathf.Max(0f, previousTimeScale);
        }

        private void OnDisable() => CancelDrag();
    }
}
