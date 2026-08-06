Shader "World/Gravity Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.055, 0.025, 0.14, 0.1)
        _EdgeColor ("Edge Color", Color) = (0.42, 0.28, 1, 0.55)
        _FlowColor ("Inward Flow Color", Color) = (0.32, 0.48, 1, 0.18)
        _CenterColor ("Center Color", Color) = (0.68, 0.42, 1, 0.24)
        _EdgeWidth ("Edge Width (World Units)", Range(0.1, 0.75)) = 0.35
        _FlowSpeed ("Inward Flow Speed", Float) = 0.65
        _CenterPulseSpeed ("Center Pulse Speed", Float) = 1.1
        _RegionSize ("Region Size", Vector) = (1, 1, 0, 0)
        _Fade ("Fade", Range(0, 1)) = 0
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
                half4 _FlowColor;
                half4 _CenterColor;
                float _EdgeWidth;
                float _FlowSpeed;
                float _CenterPulseSpeed;
                float4 _RegionSize;
                float _Fade;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 samplePoint = (input.uv - 0.5) * 2.0;
                float2 rectanglePoint = abs(samplePoint);
                float2 halfSize = max(
                    _RegionSize.xy * 0.5,
                    float2(0.001, 0.001)
                );
                float2 worldPoint = samplePoint * halfSize;
                float2 distanceToEdge =
                    (1.0 - rectanglePoint) * halfSize;
                float insideDistance = min(
                    distanceToEdge.x,
                    distanceToEdge.y
                );
                float antialias = max(fwidth(insideDistance), 0.002);
                float edge = 1.0 - smoothstep(
                    max(0.0, _EdgeWidth - antialias),
                    _EdgeWidth + antialias,
                    insideDistance
                );

                float radius = length(worldPoint);
                float inwardPhase = frac(
                    radius * 0.42 + _VisualTime * _FlowSpeed
                );
                float ringDistance = abs(inwardPhase - 0.5);
                float inwardRing = 1.0 - smoothstep(
                    0.035,
                    0.12,
                    ringDistance
                );
                float angle = atan2(worldPoint.y, worldPoint.x);
                float spoke = pow(
                    saturate(abs(cos(angle * 6.0))),
                    12.0
                );
                float inwardFlow = inwardRing *
                    lerp(0.28, 1.0, spoke) *
                    saturate(1.0 - edge);

                float centerGlow = exp2(-radius * radius * 0.32);
                float centerPulse = 0.82 +
                    sin(_VisualTime * _CenterPulseSpeed * 6.28318) * 0.18;
                centerGlow *= centerPulse;

                half3 color = _InnerColor.rgb;
                color = lerp(color, _FlowColor.rgb, inwardFlow * 0.6);
                color = lerp(color, _CenterColor.rgb, centerGlow * 0.7);
                color = lerp(color, _EdgeColor.rgb, edge);

                float alpha = _InnerColor.a +
                    inwardFlow * _FlowColor.a +
                    centerGlow * _CenterColor.a;
                alpha = lerp(alpha, _EdgeColor.a, edge);

                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
