Shader "World/Stasis Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.015, 0.14, 0.24, 0.13)
        _EdgeColor ("Edge Color", Color) = (0.12, 0.7, 1, 0.48)
        _RippleColor ("Ripple Color", Color) = (0.22, 0.82, 1, 0.2)
        _EdgeWidth ("Edge Width", Range(0.01, 0.4)) = 0.18
        _PulseSpeed ("Pulse Speed", Float) = 0.65
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.18
        _DistortionStrength ("Distortion Strength", Range(0, 0.25)) = 0.035
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
                half4 _RippleColor;
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

                float boundaryWave =
                    sin(angle * 6.0 + time * 0.2) * 0.6 +
                    sin(angle * 11.0 - time * 0.14) * 0.4;
                float boundary =
                    0.91 + boundaryWave * _DistortionStrength;
                float antialias = max(fwidth(radius), 0.002);
                float shape = 1.0 - smoothstep(
                    boundary - antialias,
                    boundary + antialias,
                    radius
                );

                float edgeDistance = abs(radius - boundary);
                float edge = 1.0 - smoothstep(
                    _EdgeWidth * 0.25,
                    _EdgeWidth,
                    edgeDistance
                );
                float brokenEdge =
                    0.72 + 0.28 *
                    (sin(angle * 9.0 - time * 0.3) * 0.5 + 0.5);
                edge *= brokenEdge;

                float rippleWave =
                    sin(radius * 30.0 - time * 1.25);
                float ripple = smoothstep(0.72, 1.0, rippleWave);
                ripple *= saturate(1.0 - edge) *
                    smoothstep(0.08, 0.35, radius);

                float frozenPattern =
                    sin(samplePoint.x * 6.0 + time * 0.12) *
                    sin(samplePoint.y * 5.0 - time * 0.1);
                frozenPattern = frozenPattern * 0.5 + 0.5;

                float pulse =
                    1.0 - _PulseStrength +
                    _PulseStrength * (sin(time) * 0.5 + 0.5);
                half3 color = lerp(
                    _InnerColor.rgb,
                    _RippleColor.rgb,
                    ripple * 0.45
                );
                color = lerp(color, _EdgeColor.rgb, edge);

                float innerAlpha =
                    _InnerColor.a * lerp(0.78, 1.0, frozenPattern);
                float alpha =
                    innerAlpha +
                    ripple * _RippleColor.a +
                    edge * _EdgeColor.a * pulse;

                return half4(color, alpha * shape * _Fade);
            }
            ENDHLSL
        }
    }
}
