using UnityEngine;

[DefaultExecutionOrder(110)]
public sealed class PixelWeatherParticles : MonoBehaviour
{
    public enum Kind { Snow, Rain, Wind, WarningRing }
    private ParticleSystem source;
    private ParticleSystemRenderer sourceRenderer;
    private ParticleSystem.Particle[] particles = new ParticleSystem.Particle[0];
    private PixelEffectMesh raster;
    private Kind kind;
    private bool originalForceOff;
    public int CellCount => raster?.CellCount ?? 0;

    public static void Attach(GameObject root, Kind kind)
    {
        if (root == null) return;
        foreach (var system in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var adapter = system.GetComponent<PixelWeatherParticles>();
            if (adapter != null) continue;
            adapter = system.gameObject.AddComponent<PixelWeatherParticles>();
            adapter.source = system;
            adapter.sourceRenderer = system.GetComponent<ParticleSystemRenderer>();
            adapter.kind = kind;
            if (adapter.sourceRenderer == null) { Destroy(adapter); continue; }
            adapter.originalForceOff = adapter.sourceRenderer.forceRenderingOff;
            adapter.sourceRenderer.forceRenderingOff = true;
            adapter.raster = new PixelEffectMesh(system.transform);
        }
    }

    private void LateUpdate() => Refresh();

    // Read simulation state; never quantize particle positions back into the simulation.
    public void Refresh()
    {
        if (source == null || raster == null) return;
        sourceRenderer.forceRenderingOff = true;
        if (!sourceRenderer.enabled) { raster.Hide(); return; }
        int capacity = source.main.maxParticles;
        if (particles.Length < capacity) particles = new ParticleSystem.Particle[capacity];
        int count = source.GetParticles(particles);
        var main = source.main;
        Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Local ? source.transform :
            main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : null;
        raster.Begin(sourceRenderer);
        for (int i = 0; i < count; i++)
        {
            var particle = particles[i];
            Vector3 p = space != null ? space.TransformPoint(particle.position) : particle.position;
            Vector3 velocity = space != null ? space.TransformVector(particle.velocity) : particle.velocity;
            Color color = particle.GetCurrentColor(source);
            float sizeScale = main.scalingMode == ParticleSystemScalingMode.Hierarchy
                ? Mathf.Max(Mathf.Abs(source.transform.lossyScale.x), Mathf.Abs(source.transform.lossyScale.y))
                : main.scalingMode == ParticleSystemScalingMode.Local
                    ? Mathf.Max(Mathf.Abs(source.transform.localScale.x), Mathf.Abs(source.transform.localScale.y)) : 1;
            float worldSize = particle.GetCurrentSize(source) * sizeScale;
            int size = Mathf.Clamp(Mathf.RoundToInt(worldSize * PixelEffectMesh.Ppu), 1, 8);
            int x = Mathf.FloorToInt(p.x * PixelEffectMesh.Ppu), y = Mathf.FloorToInt(p.y * PixelEffectMesh.Ppu);
            if (kind == Kind.WarningRing)
            {
                float radius = Mathf.Max(1, Mathf.Round(worldSize * PixelEffectMesh.Ppu * 0.5f));
                // Plot the ring perimeter, keeping work proportional to circumference.
                int samples = Mathf.CeilToInt(radius * Mathf.PI * 4);
                for (int step = 0; step < samples; step++)
                {
                    float angle = step * Mathf.PI * 2 / samples;
                    raster.Cell(x + Mathf.RoundToInt(Mathf.Cos(angle) * radius),
                        y + Mathf.RoundToInt(Mathf.Sin(angle) * radius), p.z, color);
                }
            }
            else if (kind == Kind.Snow)
            {
                for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    if (size < 3 || dx == size / 2 || dy == size / 2)
                        raster.Cell(x + dx - size / 2, y + dy - size / 2, p.z, color, false);
            }
            else
            {
                Vector2 direction = ((Vector2)velocity).sqrMagnitude > 0.001f ? ((Vector2)velocity).normalized :
                    kind == Kind.Rain ? Vector2.down : Vector2.right;
                int length = kind == Kind.Rain ? Mathf.Clamp(size * 3, 3, 12) : Mathf.Clamp(size * 2, 2, 6);
                for (int step = 0; step < length; step++)
                    raster.Cell(x - Mathf.RoundToInt(direction.x * step), y - Mathf.RoundToInt(direction.y * step), p.z, color);
            }
        }
        raster.End();
    }

    private void OnDisable() { raster?.Hide(); if (sourceRenderer != null) sourceRenderer.forceRenderingOff = originalForceOff; }
    private void OnDestroy() { if (sourceRenderer != null) sourceRenderer.forceRenderingOff = originalForceOff; raster?.Dispose(); }
}
