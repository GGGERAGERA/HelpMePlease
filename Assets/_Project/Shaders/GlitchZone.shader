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

            #include "AnomalyPixel.hlsl"

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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 size = max(_RegionSize.xy, float2(0.0625, 0.0625));
                float2 worldPoint = AnomalySnap((input.uv - 0.5) * size);
                // Keep scanlines on the same world-space pixel grid as other zones.
                float rowHeight = max(0.25, size.y / 42.0);
                float row = (worldPoint.y + size.y * 0.5) / rowHeight;
                float lineIndex = floor(row);
                float lineDistance = abs(frac(row) - 0.5) * rowHeight;
                float lineShape = 1.0 - step(0.0625, lineDistance);
                float lineGate = step(
                    0.66,
                    Hash(float2(lineIndex, floor(_VisualTime * 5.0)))
                );
                float glitchLine = lineShape * lineGate *
                    (0.38 + _Pulse * 0.9);
                float scan = step(0.0, sin(
                    worldPoint.y / size.y * 150.0 + _VisualTime * 5.0
                ));

                half3 color = _InnerColor.rgb;
                color = lerp(color, _LineColor.rgb, glitchLine);
                float alpha = _InnerColor.a + scan * 0.025 +
                    glitchLine * _LineColor.a + _Pulse * 0.07;


                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
