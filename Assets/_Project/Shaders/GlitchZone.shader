Shader "World/Glitch Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.08, 0.015, 0.12, 0.09)
        _EdgeColor ("Edge Color", Color) = (0.85, 0.08, 0.9, 0.65)
        _LineColor ("Line Color", Color) = (0.05, 0.9, 1, 0.28)
        _EdgeWidth ("Edge Width (World Units)", Range(0.1, 0.75)) = 0.3
        _RegionSize ("Region Size", Vector) = (1, 1, 0, 0)
        _Fade ("Fade", Range(0, 1)) = 0
        _Pulse ("Pulse", Range(0, 1)) = 0
        _VisualTime ("Visual Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InnerColor;
                half4 _EdgeColor;
                half4 _LineColor;
                float _EdgeWidth;
                float4 _RegionSize;
                float _Fade;
                float _Pulse;
                float _VisualTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) *
                    43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float rowIndex = floor(input.uv.y * 38.0);
                float shiftGate = step(0.86, Hash(float2(rowIndex,
                    floor(_VisualTime * 7.0))));
                float shift = (Hash(float2(rowIndex, 4.17)) - 0.5) *
                    0.025 * shiftGate * (0.3 + _Pulse * 1.7);
                float3 position = input.positionOS.xyz;
                position.x += shift;
                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 distanceToEdgeUv = min(input.uv, 1.0 - input.uv);
                float2 uvPerPixel = max(
                    fwidth(input.uv),
                    float2(0.00001, 0.00001)
                );
                float2 pixelsToEdge = distanceToEdgeUv / uvPerPixel;
                float edgeDistancePixels = min(
                    pixelsToEdge.x,
                    pixelsToEdge.y
                );
                float edge = 1.0 - smoothstep(
                    2.0,
                    4.0,
                    edgeDistancePixels
                );

                float lineIndex = floor(input.uv.y * 42.0);
                float lineCell = frac(input.uv.y * 42.0);
                float lineShape = 1.0 - smoothstep(
                    0.06,
                    0.16,
                    abs(lineCell - 0.5)
                );
                float lineGate = step(
                    0.66,
                    Hash(float2(lineIndex, floor(_VisualTime * 5.0)))
                );
                float glitchLine = lineShape * lineGate *
                    (0.38 + _Pulse * 0.9);
                float scan = 0.5 + 0.5 * sin(
                    input.uv.y * 150.0 + _VisualTime * 5.0
                );

                half3 color = _InnerColor.rgb;
                color = lerp(color, _LineColor.rgb, glitchLine);
                float edgeIntensity = lerp(0.72, 1.0, _Pulse);
                color = lerp(
                    color,
                    _EdgeColor.rgb,
                    edge * edgeIntensity
                );

                float alpha = _InnerColor.a + scan * 0.025 +
                    glitchLine * _LineColor.a + _Pulse * 0.07;
                float edgeAlpha = _EdgeColor.a *
                    lerp(0.62, 0.75, _Pulse);
                alpha = lerp(alpha, edgeAlpha, edge);

                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
