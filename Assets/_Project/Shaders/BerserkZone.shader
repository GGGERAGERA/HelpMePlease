Shader "World/Berserk Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.32, 0.015, 0.005, 0.16)
        _EdgeColor ("Edge Color", Color) = (1, 0.12, 0.015, 0.8)
        _EdgeWidth ("Edge Width", Range(0.01, 0.4)) = 0.16
        _PulseSpeed ("Pulse Speed", Float) = 1.2
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.22
        _DistortionStrength ("Distortion Strength", Range(0, 0.25)) = 0.055
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
                float _DistortionStrength;
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
                float radius = length(samplePoint);
                float angle = atan2(samplePoint.y, samplePoint.x);
                float time = _VisualTime * _PulseSpeed;

                float coarseWave =
                    sin(angle * 7.0 + 0.35) * 0.65 +
                    sin(angle * 13.0 - 0.8) * 0.35;
                float boundary =
                    0.91 + coarseWave * _DistortionStrength;
                float antialias = max(fwidth(radius), 0.002);
                float shape = 1.0 - smoothstep(
                    boundary - antialias,
                    boundary + antialias,
                    radius
                );

                float edgeDistance = abs(radius - boundary);
                float edge = 1.0 - smoothstep(
                    _EdgeWidth * 0.35,
                    _EdgeWidth,
                    edgeDistance
                );
                float pulseWave = sin(time) * 0.5 + 0.5;
                float warningPulse = pow(pulseWave, 7.0);
                float pulse = 1.0 - _PulseStrength +
                    _PulseStrength * pulseWave;
                half3 color = lerp(
                    _InnerColor.rgb,
                    _EdgeColor.rgb,
                    edge
                );
                float innerAlpha = _InnerColor.a *
                    (1.0 + warningPulse * _PulseStrength);
                float alpha = lerp(
                    innerAlpha,
                    _EdgeColor.a * pulse,
                    edge
                );

                return half4(color, alpha * shape * _Fade);
            }
            ENDHLSL
        }
    }
}
