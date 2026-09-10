#ifndef SUBJECT42_ANOMALY_PIXEL_INCLUDED
#define SUBJECT42_ANOMALY_PIXEL_INCLUDED

// Two sprite pixels per effect texel at the game's 32 PPU art scale.
// Quantize only spatial sampling; retain each effect's original animation clock.
float2 AnomalySnap(float2 position)
{
    return (floor(position * 16.0) + 0.5) / 16.0;
}

float2 AnomalyUV(float2 uv, float2 size)
{
    size = max(size, float2(0.0625, 0.0625));
    return saturate(AnomalySnap((uv - 0.5) * size) / size + 0.5);
}

// Four tonal steps retain the original wave profiles without a soft blur.
float AnomalyRamp(float low, float high, float value)
{
    return floor(smoothstep(low, high, value) * 4.0 + 0.5) / 4.0;
}

// Screen effects use square pixels at an integer scale of a 360-line canvas.
float2 PixelScreenUV(float2 uv, float2 resolution)
{
    float scale = max(1.0, floor(resolution.y / 360.0 + 0.5));
    float2 grid = max(float2(1, 1), floor(resolution / scale));
    return (floor(uv * grid) + 0.5) / grid;
}

float PixelHardStep(float low, float high, float value)
{
    return step((low + high) * 0.5, value);
}
#endif
