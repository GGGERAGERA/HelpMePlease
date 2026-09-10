using NUnit.Framework;
using UnityEngine;

public sealed class PixelWorldEffectTests
{
    private GameObject root;

    [TearDown]
    public void Cleanup()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void RotatedEventLinesUseWorldGridAndFollowVisibility()
    {
        root = new GameObject("Pixel line test");
        var line = root.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(-1, -.2f));
        line.SetPosition(1, new Vector3(1, .4f));
        line.startWidth = line.endWidth = .125f;
        root.transform.SetPositionAndRotation(new Vector3(.113f, .077f), Quaternion.Euler(0, 0, 27));
        PixelEventLine.Attach(line);
        PixelEventLine.Attach(line);
        Assert.That(root.GetComponents<PixelEventLine>().Length, Is.EqualTo(1));
        var pixelLine = root.GetComponent<PixelEventLine>();
        pixelLine.Refresh();
        Assert.That(pixelLine.CellCount, Is.GreaterThan(0));
        Assert.That(line.forceRenderingOff, Is.True);
        AssertGrid();
        line.enabled = false;
        pixelLine.Refresh();
        Assert.That(root.GetComponentInChildren<MeshRenderer>().enabled, Is.False);
        line.enabled = true;
        pixelLine.Refresh();
        Assert.That(root.GetComponentInChildren<MeshRenderer>().enabled, Is.True);
        Assert.That(line.GetPosition(1), Is.EqualTo(new Vector3(1, .4f)));
    }

    [TestCase(ParticleSystemSimulationSpace.World)]
    [TestCase(ParticleSystemSimulationSpace.Local)]
    [TestCase(ParticleSystemSimulationSpace.Custom)]
    public void WeatherRasterNeverWritesIntoSimulation(ParticleSystemSimulationSpace space)
    {
        root = new GameObject("Pixel snow test");
        root.transform.position = new Vector3(.117f, .191f);
        var ps = root.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = space;
        main.customSimulationSpace = root.transform;
        main.maxParticles = 8;
        var emission = ps.emission;
        emission.enabled = false;
        ps.Play();
        ps.Emit(new ParticleSystem.EmitParams {
            position = new Vector3(.13f, .27f), velocity = new Vector3(.4f, -1),
            startSize = .125f, startLifetime = 10, startColor = Color.white
        }, 1);
        ps.Simulate(.01f, false, false);
        var before = new ParticleSystem.Particle[8];
        int count = ps.GetParticles(before);
        Assert.That(count, Is.EqualTo(1));
        var atlas = ps.textureSheetAnimation;
        atlas.enabled = true;
        atlas.mode = ParticleSystemAnimationMode.Sprites;
        PixelWeatherParticles.Attach(root, PixelWeatherParticles.Kind.Snow);
        Assert.That(ps.textureSheetAnimation.enabled, Is.False);
        var raster = root.GetComponent<PixelWeatherParticles>();
        raster.Refresh();
        Assert.That(raster.UsesNativeRenderer, Is.True);
        Assert.That(root.GetComponentsInChildren<MeshFilter>(), Is.Empty);
        var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
        Assert.That(particleRenderer.forceRenderingOff, Is.False);
        Assert.That(particleRenderer.sharedMaterial, Is.EqualTo(Resources.Load<Material>("PixelSnowParticle")));

        AssertGrid();
        var after = new ParticleSystem.Particle[8];
        Assert.That(ps.GetParticles(after), Is.EqualTo(count));
        Assert.That(after[0].position, Is.EqualTo(before[0].position));
        Assert.That(after[0].velocity, Is.EqualTo(before[0].velocity));
        Assert.That(after[0].remainingLifetime, Is.EqualTo(before[0].remainingLifetime));
        // Toggling the whole effect restarts Unity's particle system by design.
        // Verify material restoration separately from simulation invariance above.
        // EditMode does not dispatch normal gameplay MonoBehaviour callbacks.
        typeof(PixelWeatherParticles).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(raster, null);
        root.SetActive(false);
        Assert.That(ps.textureSheetAnimation.enabled, Is.True);
        root.SetActive(true);
        typeof(PixelWeatherParticles).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(raster, null);
        Assert.That(ps.textureSheetAnimation.enabled, Is.False);
        Assert.That(particleRenderer.sharedMaterial, Is.EqualTo(Resources.Load<Material>("PixelSnowParticle")));
    }

    private void AssertGrid()
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        foreach (var vertex in filter.sharedMesh.vertices)
        {
            Vector3 cell = filter.transform.TransformPoint(vertex) * PixelEffectMesh.Ppu;
            Assert.That(cell.x, Is.EqualTo(Mathf.Round(cell.x)).Within(.001f));
            Assert.That(cell.y, Is.EqualTo(Mathf.Round(cell.y)).Within(.001f));
        }
    }
}
