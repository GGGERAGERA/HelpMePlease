Shader "World/Berserk Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.32, 0.015, 0.005, 0.16)
        _EdgeColor ("Edge Color", Color) = (1, 0.12, 0.015, 0.8)
        _EdgeWidth ("Edge Width (World Units)", Range(0.1, 0.75)) = 0.35
        _PulseSpeed ("Pulse Speed", Float) = 0.35
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.08
        _PulseSharpness ("Pulse Sharpness", Range(1, 10)) = 1
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
                float _EdgeWidth;
                float _PulseSpeed;
                float _PulseStrength;
                float _PulseSharpness;
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
                float2 rectanglePoint = abs((input.uv - 0.5) * 2.0);
                float2 halfSize = max(
                    _RegionSize.xy * 0.5,
                    float2(0.001, 0.001)
                );
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

                float pulseWave =
                    sin(_VisualTime * _PulseSpeed) * 0.5 + 0.5;
                float sharpPulse = pow(
                    saturate(pulseWave),
                    max(1.0, _PulseSharpness)
                );
                float warningMode = step(2.0, _PulseSharpness);
                float borderPulse = lerp(
                    1.0 - _PulseStrength + _PulseStrength * pulseWave,
                    1.0,
                    warningMode
                );
                float innerPulse = 1.0 + _PulseStrength * lerp(
                    pulseWave * 0.25,
                    sharpPulse,
                    warningMode
                );
                half3 color = lerp(
                    _InnerColor.rgb,
                    _EdgeColor.rgb,
                    edge
                );
                float alpha = lerp(
                    _InnerColor.a * innerPulse,
                    _EdgeColor.a * borderPulse,
                    edge
                );

                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
