Shader "Subject42/Dev/FieldExperiments"
{
    Properties
    {
        [Enum(ShieldPulse,0,Shockwave,1,Vortex,2,ElectricGrid,3)] _Mode ("Experiment", Float) = 0
        _LabTime ("Preview time", Float) = 0
        _Intensity ("Intensity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Mode, _LabTime, _Intensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ring(float distance, float radius, float width)
            {
                return saturate(1.0 - abs(distance - radius) / width);
            }

            float4 frag(v2f i) : SV_Target
            {
                // A fixed grid makes the procedural samples read as pixel effects.
                float2 p = (floor(i.uv * 96.0) + 0.5) / 48.0 - 1.0;
                float r = length(p);
                float angle = atan2(p.y, p.x);
                float t = _LabTime;
                float3 color;
                float alpha;

                if (_Mode < 0.5)
                {
                    float radius = 0.73 + 0.025 * sin(t * 3.0);
                    float sweep = pow(saturate(0.5 + 0.5 * cos(angle - t * 2.0)), 10.0);
                    float rim = ring(r, radius, 0.028) + ring(r, radius - 0.07, 0.017) * 0.45;
                    float2 grid = abs(frac(p * 7.0) - 0.5);
                    float wire = 1.0 - step(0.045, min(grid.x, grid.y));
                    float interior = (1.0 - smoothstep(radius - 0.03, radius, r)) * wire * 0.12;
                    alpha = saturate(rim * (0.65 + 0.35 * sweep) + interior);
                    color = lerp(float3(0.08, 0.4, 0.95), float3(0.55, 1.0, 1.0), sweep + rim * 0.25);
                }
                else if (_Mode < 1.5)
                {
                    float phase = frac(t * 0.48 + 0.2);
                    float radius = 0.1 + phase * 0.85;
                    float front = ring(r, radius, 0.036);
                    float wake = ring(r, radius - 0.095, 0.024) * 0.5;
                    float rays = step(0.68, hash(float2(floor((angle + 3.141593) * 18.0), floor(t * 8.0))));
                    float debris = ring(r, radius + 0.035, 0.065) * rays * 0.35;
                    alpha = saturate((front + wake + debris) * (1.0 - phase) * 1.4);
                    color = lerp(float3(1.0, 0.3, 0.06), float3(1.0, 0.92, 0.45), front);
                }
                else if (_Mode < 2.5)
                {
                    float spiral = 0.5 + 0.5 * sin(angle * 3.0 + r * 16.0 - t * 4.5);
                    float arms = pow(spiral, 7.0);
                    float rim = ring(r, 0.78 + 0.025 * sin(angle * 3.0 - t * 2.0), 0.026) * 0.25;
                    float disc = smoothstep(0.08, 0.2, r) * (1.0 - smoothstep(0.72, 0.94, r));
                    alpha = saturate((arms * 0.8 + 0.06 + rim) * disc);
                    color = lerp(float3(0.22, 0.13, 0.78), float3(0.65, 0.48, 1.0), arms);
                    color = lerp(color, float3(0.15, 0.9, 1.0), saturate(1.0 - r) * arms);
                }
                else
                {
                    float tick = floor(t * 12.0);
                    float2 cell = abs(frac((p + 1.0) * 4.0) - 0.5);
                    float wires = 1.0 - step(0.045, min(cell.x, cell.y));
                    float horizontal = 0.15 * sin(p.x * 8.0 - tick * 0.7)
                        + (hash(float2(floor(p.x * 20.0), tick)) - 0.5) * 0.12;
                    float vertical = 0.2 * sin(p.y * 5.0 + tick * 0.5)
                        + (hash(float2(tick, floor(p.y * 20.0))) - 0.5) * 0.1;
                    float bolts = saturate(1.0 - abs(p.y - horizontal) / 0.026)
                        + saturate(1.0 - abs(p.x - vertical) / 0.025) * 0.75;
                    float travel = pow(saturate(0.5 + 0.5 * sin((p.x + p.y) * 10.0 - t * 5.0)), 12.0);
                    float edge = 1.0 - smoothstep(0.84, 0.98, max(abs(p.x), abs(p.y)));
                    alpha = saturate(wires * (0.13 + travel * 0.5) + bolts) * edge;
                    color = lerp(float3(0.08, 0.55, 0.58), float3(0.7, 1.0, 0.95), saturate(bolts + travel));
                }

                return float4(color * (0.8 + 0.2 * _Intensity), saturate(alpha * _Intensity));
            }
            ENDCG
        }
    }
}
