#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subject42.Prototype.OrbitalCombatLab.Editor
{
    public static class OrbitalCombatLabBuilder
    {
        public const string ScenePath = "Assets/_Project/Prototype/OrbitalCombatLab/OrbitalCombatLab.unity";
        private static int smokeFrames;
        private static int smokePhase;
        private static double phaseStarted;
        private static int errors;
        private static bool previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousEnterPlayModeOptions;
        private static int captureFrames;
        private static int reactionLinkHits;
        private static int reactionFieldHits;
        private static int reactionResonances;
        private static int patternCapturePhase;

        [MenuItem("Tools/Prototype/Build Orbital Combat Lab")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "OrbitalCombatLab";
            GameObject bootstrap = new("ORBITAL COMBAT LAB");
            bootstrap.AddComponent<OrbitalCombatLabController>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OrbitalCombatLab] Scene built: {ScenePath}");
        }

        public static void BuildSceneBatch()
        {
            BuildScene();
            EditorApplication.Exit(0);
        }

        public static void RunSmokeTestBatch()
        {
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            smokeFrames = 0;
            smokePhase = 0;
            errors = 0;
            phaseStarted = EditorApplication.timeSinceStartup;
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.update -= SmokeUpdate;
            EditorApplication.update += SmokeUpdate;
            EditorApplication.EnterPlaymode();
        }

        public static void CaptureComparisonBatch()
        {
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            captureFrames = 0;
            EditorApplication.update -= CaptureUpdate;
            EditorApplication.update += CaptureUpdate;
            EditorApplication.EnterPlaymode();
        }

        public static void CapturePatternQABatch()
        {
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            captureFrames = 0;
            patternCapturePhase = 0;
            EditorApplication.update -= PatternCaptureUpdate;
            EditorApplication.update += PatternCaptureUpdate;
            EditorApplication.EnterPlaymode();
        }

        private static void PatternCaptureUpdate()
        {
            if (!EditorApplication.isPlaying) return;
            captureFrames++;
            OrbitalCombatLabController lab = UnityEngine.Object.FindFirstObjectByType<OrbitalCombatLabController>();
            if (lab == null) return;
            if (captureFrames == 70) lab.ApplyPatternFlower();
            else if (captureFrames == 240) CapturePattern("PATTERN_FLOWER");
            else if (captureFrames == 270) lab.ApplyCombatWeb();
            else if (captureFrames == 470) CapturePattern("COMBAT_WEB");
            else if (captureFrames == 500) lab.ApplyOrbitalFortress();
            else if (captureFrames == 740) CapturePattern("ORBITAL_FORTRESS");
            else if (captureFrames == 770) lab.ApplyHypnosis();
            else if (captureFrames == 1030) CapturePattern("HYPNOSIS");
            else if (captureFrames == 1060) lab.ApplyDirectedFortress();
            else if (captureFrames == 1280) CapturePattern("DIRECTED_FORTRESS");
            else if (captureFrames >= 1320)
            {
                EditorApplication.update -= PatternCaptureUpdate;
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                AssetDatabase.Refresh();
                EditorApplication.Exit(0);
            }
        }

        private static void CapturePattern(string name)
        {
            ScreenCapture.CaptureScreenshot(
                $"Assets/_Project/Prototype/OrbitalCombatLab/QA_{name}.png");
            patternCapturePhase++;
            Debug.Log($"[OrbitalCombatLab QA] Captured {patternCapturePhase}/5: {name}");
        }

        private static void CaptureUpdate()
        {
            if (!EditorApplication.isPlaying) return;
            captureFrames++;
            OrbitalCombatLabController lab = UnityEngine.Object.FindFirstObjectByType<OrbitalCombatLabController>();
            if (lab == null) return;
            if (captureFrames == 100)
            {
                ScreenCapture.CaptureScreenshot(
                    "Assets/_Project/Prototype/OrbitalCombatLab/OrbitalCombatLab_START.png");
            }
            else if (captureFrames == 140)
            {
                lab.ApplyFinalState();
            }
            else if (captureFrames == 380)
            {
                ScreenCapture.CaptureScreenshot(
                    "Assets/_Project/Prototype/OrbitalCombatLab/OrbitalCombatLab_FINAL.png");
            }
            else if (captureFrames >= 420)
            {
                EditorApplication.update -= CaptureUpdate;
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                AssetDatabase.Refresh();
                EditorApplication.Exit(0);
            }
        }

        private static void SmokeUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;
            smokeFrames++;
            OrbitalCombatLabController lab = UnityEngine.Object.FindFirstObjectByType<OrbitalCombatLabController>();
            if (lab == null || smokeFrames < 90) return;

            try
            {
                switch (smokePhase)
                {
                    case 0:
                        Check(lab.RingCount == 1, "START has one ring");
                        Check(lab.MountedCount == 1, "START has one mounted object");
                        Check(lab.Crowd.DesiredCount == 50, "START has 50 enemies");
                        Check(lab.Stats.Shots > 0, "Gun fires in START");
                        LogPerformance("50", lab);
                        TestDragAndRingLifecycle(lab);
                        lab.ApplyMidState();
                        NextPhase();
                        break;
                    case 1 when smokeFrames >= 180:
                        Check(lab.RingCount == 3, "MID has three rings");
                        Check(lab.Crowd.DesiredCount == 120, "MID has 120 enemies");
                        Check(CountType(lab, OrbitalMountType.Gun) > 0, "MID has Gun");
                        Check(CountType(lab, OrbitalMountType.Blade) > 0, "MID has Blade");
                        Check(CountType(lab, OrbitalMountType.Pusher) > 0, "MID has Pusher");
                        LogPerformance("120", lab);
                        lab.SpawnEnemies(100);
                        Check(lab.Crowd.DesiredCount == 100, "100 enemy density applies live");
                        NextPhase();
                        break;
                    case 2 when smokeFrames >= 180:
                        LogPerformance("100", lab);
                        lab.ApplyFinalState();
                        NextPhase();
                        break;
                    case 3 when smokeFrames >= 300:
                        Check(lab.RingCount == 6, "FINAL has six rings");
                        Check(lab.MountedCount >= 30, "FINAL has at least 30 mounted objects");
                        Check(lab.Crowd.DesiredCount == 300, "FINAL has 300 enemies");
                        Check(lab.CameraRig.AutoCamera, "Auto Camera is active");
                        Check(Camera.main != null && Camera.main.orthographicSize > 10f,
                            "FINAL camera fits outer ring");
                        LogPerformance("300", lab);
                        lab.SpawnEnemies(200);
                        NextPhase();
                        break;
                    case 4 when smokeFrames >= 300:
                        Check(lab.Crowd.DesiredCount == 200, "200 enemy density applies live");
                        Check(lab.Stats.SmoothedFps > 0f, "FPS sampling is active");
                        Check(lab.Stats.BladeHits > 0, "Blade deals contact damage");
                        Check(lab.Stats.PushHits > 0, "Pusher displaces the crowd");
                        LogPerformance("200", lab);
                        lab.ApplyCombatWeb();
                        ForceLinkContact(lab);
                        NextPhase();
                        break;
                    case 5 when smokeFrames >= 300:
                        Check(lab.PatternCombat, "COMBAT WEB enables PATTERN COMBAT");
                        Check(CountType(lab, OrbitalMountType.LinkNode) >= 8, "COMBAT WEB has Link Nodes");
                        Check(lab.Stats.ActiveLinks > 0, "Links rebuild while nodes move");
                        Check(lab.Stats.LinkHits > 0, "Links damage enemies with cooldown");
                        Check(lab.Stats.Resonances > 0, "Alignment resonance triggers");
                        TestLinkDrag(lab);
                        reactionLinkHits = lab.Stats.LinkHits;
                        reactionFieldHits = lab.Stats.RingFieldHits;
                        reactionResonances = lab.Stats.Resonances;
                        lab.PatternCombat = false;
                        NextPhase();
                        break;
                    case 6 when smokeFrames >= 240:
                        Check(lab.Stats.LinkHits == reactionLinkHits &&
                            lab.Stats.RingFieldHits == reactionFieldHits &&
                            lab.Stats.Resonances == reactionResonances,
                            "PATTERN COMBAT OFF disables all new combat reactions");
                        Check(lab.Stats.ActiveLinks > 0, "Visual links remain available for comparison");
                        lab.ClearMounted();
                        NextPhase();
                        break;
                    case 7 when smokeFrames >= 120:
                        Check(lab.Stats.ActiveLinks == 0, "Links disappear when nodes are removed");
                        TestMovementShapesAndFreeze(lab);
                        lab.ApplyOrbitalFortress();
                        NextPhase();
                        break;
                    case 8 when smokeFrames >= 320:
                        Check(lab.RingCount == 6 && lab.Crowd.DesiredCount == 300,
                            "ORBITAL FORTRESS runs with six rings and 300 enemies");
                        Check(CountType(lab, OrbitalMountType.LinkNode) > 0 &&
                            CountType(lab, OrbitalMountType.Gun) > 0 &&
                            CountType(lab, OrbitalMountType.Blade) > 0 &&
                            CountType(lab, OrbitalMountType.Pusher) > 0,
                            "ORBITAL FORTRESS contains all four object roles");
                        Check(HasDifferentFields(lab), "Ring Field modes remain independent");
                        Check(lab.Stats.SmoothedFps > 0f, "300 enemy pattern FPS sampling is active");
                        LogPerformance("FORTRESS 300", lab);
                        lab.ApplyHypnosis();
                        NextPhase();
                        break;
                    case 9 when smokeFrames >= 180:
                        Check(lab.Crowd.DesiredCount == 0, "HYPNOSIS removes combat noise");
                        Check(lab.Trails.Mode == OrbitalTrailMode.Hypnotic &&
                            CountType(lab, OrbitalMountType.LinkNode) >= 12,
                            "HYPNOSIS enables long trails and many Link Nodes");
                        lab.ApplyDirectedFortress();
                        NextPhase();
                        break;
                    case 10 when smokeFrames >= 180:
                        Check(lab.FreeMountPhase, "DIRECTED FORTRESS enables free mount phase");
                        Check(HasCustomPhases(lab), "Directed formation groups objects into custom arcs");
                        Check(lab.Crowd.DesiredCount == 200, "DIRECTED FORTRESS runs with 200 enemies");
                        lab.ResetTest();
                        Check(!lab.PatternCombat && lab.Trails.Mode == OrbitalTrailMode.Off &&
                            lab.Stats.ActiveLinks == 0 && lab.Stats.Resonances == 0,
                            "Reset clears pattern state, links, trails and resonance stats");
                        FinishSmoke();
                        break;
                }
            }
            catch (Exception exception)
            {
                errors++;
                Debug.LogException(exception);
                FinishSmoke();
            }
        }

        private static int CountType(OrbitalCombatLabController lab, OrbitalMountType type)
        {
            int count = 0;
            for (int i = 0; i < lab.MountedCount; i++)
                if (lab.MountedObjects[i] != null && lab.MountedObjects[i].Type == type) count++;
            return count;
        }

        private static void LogPerformance(string density, OrbitalCombatLabController lab)
        {
            Debug.Log($"[OrbitalCombatLab Smoke][PERF {density}] " +
                $"FPS~{lab.Stats.SmoothedFps:0}, active={lab.Stats.ActiveEnemies}, " +
                $"kills={lab.Stats.Kills}, shots={lab.Stats.Shots}, " +
                $"bladeHits={lab.Stats.BladeHits}, pushHits={lab.Stats.PushHits}");
        }

        private static void TestDragAndRingLifecycle(OrbitalCombatLabController lab)
        {
            OrbitalMountedObject mounted = lab.MountedObjects[0];
            lab.AddRing();
            MethodInfo begin = typeof(OrbitalLabDragController).GetMethod("BeginDrag",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo find = typeof(OrbitalLabDragController).GetMethod("FindCandidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo end = typeof(OrbitalLabDragController).GetMethod("EndDrag",
                BindingFlags.Instance | BindingFlags.NonPublic);
            begin?.Invoke(lab.Drag, new object[] { mounted });
            Check(Mathf.Approximately(Time.timeScale, .2f), "Drag slows time");
            Vector2 target = lab.Rings[1].GetSlotPosition(lab.PlayerPosition, 0);
            find?.Invoke(lab.Drag, new object[] { target });
            Check(lab.Drag.CandidateRing == 1 && lab.Drag.CandidateSlot >= 0,
                "Drag previews a free slot on another ring");
            end?.Invoke(lab.Drag, null);
            Check(mounted.Ring == lab.Rings[1], "Drag moves object between rings");
            Check(Mathf.Approximately(Time.timeScale, 1f), "Drag restores time scale");
            int beforeRemove = lab.MountedCount;
            lab.RemoveRing();
            Check(lab.MountedCount == beforeRemove - 1,
                "Removing a ring safely removes its mounted object");

            while (lab.RingCount < OrbitalCombatLabController.MaxRings) lab.AddRing();
            Check(!lab.AddRing() && lab.RingCount == OrbitalCombatLabController.MaxRings,
                "Ring count is capped at six");
            lab.ResetTest();
            Check(lab.RingCount == 1 && lab.MountedCount == 1 && lab.Crowd.DesiredCount == 50,
                "Reset restores a clean START state");

            Time.timeScale = 0f;
            begin?.Invoke(lab.Drag, new object[] { lab.MountedObjects[0] });
            lab.Drag.CancelDrag();
            Check(Mathf.Approximately(Time.timeScale, 0f), "Drag preserves an existing pause");
            Time.timeScale = 1f;
        }

        private static void TestLinkDrag(OrbitalCombatLabController lab)
        {
            OrbitalMountedObject link = null;
            for (int i = 0; i < lab.MountedCount; i++)
                if (lab.MountedObjects[i].Type == OrbitalMountType.LinkNode) { link = lab.MountedObjects[i]; break; }
            if (link == null) { Check(false, "Link Node drag target exists"); return; }
            OrbitalRing origin = link.Ring;
            OrbitalRing targetRing = lab.Rings[lab.RingCount - 1];
            if (origin == targetRing) targetRing = lab.Rings[0];
            int freeSlot = targetRing.FindFreeSlot(lab.PlayerPosition,
                lab.PlayerPosition + Vector2.up * targetRing.Settings.Radius);
            if (freeSlot < 0)
            {
                Check(false, "Link Node target ring has a free slot");
                return;
            }
            MethodInfo begin = typeof(OrbitalLabDragController).GetMethod("BeginDrag",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo find = typeof(OrbitalLabDragController).GetMethod("FindCandidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo end = typeof(OrbitalLabDragController).GetMethod("EndDrag",
                BindingFlags.Instance | BindingFlags.NonPublic);
            begin?.Invoke(lab.Drag, new object[] { link });
            find?.Invoke(lab.Drag, new object[] { targetRing.GetSlotPosition(lab.PlayerPosition, freeSlot) });
            end?.Invoke(lab.Drag, null);
            Check(link.Ring != null && link.Ring != origin, "Link Node drag works between rings");
        }

        private static void ForceLinkContact(OrbitalCombatLabController lab)
        {
            OrbitalMountedObject first = null, second = null;
            for (int i = 0; i < lab.MountedCount; i++)
            {
                if (lab.MountedObjects[i].Type != OrbitalMountType.LinkNode) continue;
                if (first == null) first = lab.MountedObjects[i]; else { second = lab.MountedObjects[i]; break; }
            }
            if (first != null && second != null && lab.Crowd.DesiredCount > 0)
                lab.Crowd.Enemies[0].Transform.position =
                    ((Vector2)first.Transform.position + (Vector2)second.Transform.position) * .5f;
        }

        private static void TestMovementShapesAndFreeze(OrbitalCombatLabController lab)
        {
            lab.ApplyMovementPreset(OrbitalMovementPreset.Gear);
            Check(lab.Rings[0].Settings.Clockwise != lab.Rings[1].Settings.Clockwise,
                "GEAR alternates ring directions");
            float gearSpeed = lab.Rings[0].Settings.RotationSpeed;
            lab.ApplyMovementPreset(OrbitalMovementPreset.Flower);
            Check(!Mathf.Approximately(gearSpeed, lab.Rings[0].Settings.RotationSpeed) &&
                !Mathf.Approximately(lab.Rings[0].Angle, lab.Rings[1].Angle),
                "FLOWER produces a distinct speed and phase pattern");
            float saved = lab.Rings[0].Settings.RotationSpeed;
            lab.ToggleFreeze();
            Check(Mathf.Approximately(lab.Rings[0].Settings.RotationSpeed, 0f), "FREEZE stops rings");
            lab.ToggleFreeze();
            Check(Mathf.Approximately(lab.Rings[0].Settings.RotationSpeed, saved), "FREEZE restores prior speeds");
            OrbitalRing ring = lab.Rings[0];
            ring.Settings.Shape = OrbitalShape.Circle;
            Vector2 circle = ring.GetPositionForAngle(lab.PlayerPosition, 0f);
            ring.Settings.Shape = OrbitalShape.Ellipse;
            Vector2 ellipse = ring.GetPositionForAngle(lab.PlayerPosition, 0f);
            Check(Vector2.Distance(circle, ellipse) > .2f, "CIRCLE and ELLIPSE paths differ");
            ring.Settings.Shape = OrbitalShape.Breathing;
            Check(ring.MaximumVisualRadius > ring.Settings.Radius, "BREATHING reserves stable camera margin");
            ring.Settings.Shape = OrbitalShape.Wobble;
            Check(ring.MaximumVisualRadius > ring.Settings.Radius, "WOBBLE reserves stable camera margin");
            ring.Settings.Shape = OrbitalShape.Circle;
        }

        private static bool HasDifferentFields(OrbitalCombatLabController lab)
        {
            if (lab.RingCount < 2) return false;
            OrbitalRingFieldMode first = lab.Rings[0].Settings.FieldMode;
            for (int i = 1; i < lab.RingCount; i++) if (lab.Rings[i].Settings.FieldMode != first) return true;
            return false;
        }

        private static bool HasCustomPhases(OrbitalCombatLabController lab)
        {
            for (int i = 0; i < lab.MountedCount; i++)
                if (Mathf.Abs(lab.MountedObjects[i].PhaseOffset) > 1f) return true;
            return false;
        }

        private static void Check(bool condition, string label)
        {
            if (condition)
                Debug.Log("[OrbitalCombatLab Smoke][PASS] " + label);
            else
            {
                errors++;
                Debug.LogError("[OrbitalCombatLab Smoke][FAIL] " + label);
            }
        }

        private static void NextPhase()
        {
            smokePhase++;
            smokeFrames = 90;
            phaseStarted = EditorApplication.timeSinceStartup;
        }

        private static void FinishSmoke()
        {
            Debug.Log($"[OrbitalCombatLab Smoke] COMPLETE errors={errors}, " +
                $"duration={EditorApplication.timeSinceStartup - phaseStarted:0.00}s (last phase)");
            EditorApplication.update -= SmokeUpdate;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
    }
}
#endif
