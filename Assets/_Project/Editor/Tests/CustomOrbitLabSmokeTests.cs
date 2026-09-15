#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Subject42.DebugLabs;
using Subject42.Combat.OrbitalStation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CustomOrbitLabSmokeTests
{
    [Test]
    public void UnequalSegmentsUseDistanceIncludingClosingEdge()
    {
        var points = new[] { Vector2.zero, new Vector2(1, 0), new Vector2(9, 0), new Vector2(9, 1), new Vector2(0, 1), Vector2.zero };
        Assert.That(CustomOrbitPath.TryBuild(points, .05f, .2f, 0, out var path, out _), Is.True);
        Assert.That(path.TotalPathLength, Is.EqualTo(20).Within(.001f));
        Assert.That(Vector2.Distance(path.PositionAtDistance(5), new Vector2(5, 0)), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(path.PositionAtDistance(10), new Vector2(9, 1)), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(path.PositionAtDistance(15), new Vector2(4, 1)), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(path.PositionAtDistance(-.5f), new Vector2(0, .5f)), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(path.PositionAtDistance(20), Vector2.zero), Is.LessThan(.001f));
    }

    [UnityTest]
    public IEnumerator FourShapesMovementAndRedraw()
    {
        // The Unity test runner owns and restores the user's scene setup.
        EditorSceneManager.OpenScene(CustomOrbitLab.ScenePath);
        yield return new EnterPlayMode();
        var lab = Object.FindFirstObjectByType<CustomOrbitLab>();
        Assert.That(Object.FindFirstObjectByType<OrbitalStationRuntime>(), Is.Null);
        for (int shape = 0; shape < 4; shape++)
        {
            var input = Shape(shape);
            lab.BeginStroke(input[0]);
            foreach (var point in input) lab.AppendPoint(point);
            lab.EndStroke(input[input.Count - 1]);
            Assert.That(lab.State, Is.EqualTo(CustomOrbitLab.PathState.VALID), $"shape {shape}");
            lab.Confirm();
            var before = lab.Mounts[0].transform.localPosition;
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(Vector3.Distance(before, lab.Mounts[0].transform.localPosition), Is.GreaterThan(.05f));
            Assert.That(lab.Mounts.Count(m => m.gameObject.activeSelf), Is.EqualTo(4));
            // Recover mount distances from the actual rendered line, independently of the evaluator.
            var distances = new List<float>();
            for (int mount = 0; mount < 4; mount++) distances.Add(ProjectDistance(lab.PathLine, lab.Mounts[mount].transform.localPosition));
            distances.Sort();
            for (int i = 0; i < 4; i++)
            {
                float gap = Mathf.Repeat(distances[(i + 1) % 4] - distances[i], lab.TotalPathLength);
                Assert.That(gap, Is.EqualTo(lab.TotalPathLength / 4).Within(.02f));
            }
            lab.Direction = CustomOrbitLab.TravelDirection.AgainstDrawing;
            float distance = lab.TravelDistance;
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(Mathf.Repeat(distance - lab.TravelDistance, lab.TotalPathLength), Is.InRange(.001f, lab.TotalPathLength * .5f));
            lab.Direction = CustomOrbitLab.TravelDirection.AlongDrawing;
            lab.Player.transform.position += Vector3.right;
            yield return null;
            Assert.That(lab.OrbitRoot.position, Is.EqualTo(lab.Player.transform.position));
            lab.Player.transform.position = Vector3.zero;
            for (int frame = 0; frame < 10; frame++) yield return null;
            // Captures are temporary and stay in the project's existing ignored QA directory.
            string output = "Artifacts/GeneratedQA/CustomOrbitLab/";
            Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(output + $"shape-{shape}.png");
            yield return null;
            lab.Clear();
            Assert.That(lab.TotalPathLength, Is.Zero);
            Assert.That(lab.Mounts.All(m => !m.gameObject.activeSelf), Is.True);
        }
        lab.BeginStroke(Vector2.zero);
        lab.AppendPoint(Vector2.right * 4);
        lab.AppendPoint(Vector2.one * 4);
        lab.EndStroke(Vector2.up * 4);
        lab.Confirm();
        Assert.That(lab.State, Is.EqualTo(CustomOrbitLab.PathState.INVALID));
        Assert.That(lab.Running, Is.False);
        lab.Clear();
        yield return new ExitPlayMode();
        File.WriteAllText("Artifacts/GeneratedQA/CustomOrbitLab/smoke.result", "PASS: four shapes, arc spacing, reverse, movement, clear and invalid closure.");
    }

    private static List<Vector2> Shape(int shape)
    {
        var points = new List<Vector2>();
        for (int i = 0; i <= 180; i++)
        {
            float t = i * Mathf.PI * 2 / 180;
            points.Add(shape switch
            {
                0 => new Vector2(3 * Mathf.Cos(t), 3 * Mathf.Sin(t)),
                1 => new Vector2(5 * Mathf.Cos(t), 1.4f * Mathf.Sin(t)),
                2 => new Vector2(4 * Mathf.Cos(t), 2 * Mathf.Sin(2 * t)),
                _ => new Vector2((3 + 1.5f * Mathf.Cos(t)) * Mathf.Cos(t), (2 + Mathf.Cos(t)) * Mathf.Sin(t))
            });
        }
        return points;
    }
    private static float ProjectDistance(LineRenderer line, Vector2 point)
    {
        float best = float.PositiveInfinity, result = 0, accumulated = 0;
        for (int i = 0; i < line.positionCount; i++)
        {
            Vector2 a = line.GetPosition(i), b = line.GetPosition((i + 1) % line.positionCount), delta = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
            float error = (point - a - delta * t).sqrMagnitude;
            if (error < best) { best = error; result = accumulated + delta.magnitude * t; }
            accumulated += delta.magnitude;
        }
        Assert.That(best, Is.LessThan(.0001f));
        return result;
    }
}
#endif
