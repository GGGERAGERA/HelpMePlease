Shader "World/Stasis Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.015, 0.14, 0.24, 0.13)
        _EdgeColor ("Edge Color", Color) = (0.12, 0.7, 1, 0.48)
        _RippleColor ("Ripple Color", Color) = (0.22, 0.82, 1, 0.2)
        _EdgeWidth ("Edge Width", Range(0.01, 0.4)) = 0.18
        _PulseSpeed ("Pulse Speed", Float) = 0.65
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
                float time = _VisualTime * _PulseSpeed;
                float edgeCoordinate = rectanglePoint.x > rectanglePoint.y
                    ? rectanglePoint.y
                    : rectanglePoint.x;
                float boundary = 0.9 +
                    sin(edgeCoordinate * 11.0 + 0.4) * 0.004;
                float signedDistance =
                    max(rectanglePoint.x, rectanglePoint.y) - boundary;
                float antialias = max(fwidth(signedDistance), 0.002);
                float shape = 1.0 - smoothstep(
                    -antialias,
                    antialias,
                    signedDistance
                );

                float edgeDistance = abs(signedDistance);
                float edge = 1.0 - smoothstep(
                    _EdgeWidth * 0.25,
                    _EdgeWidth,
                    edgeDistance
                );
                float distanceFromEdge = max(0.0, -signedDistance);
                float rippleWave =
                    sin(distanceFromEdge * 24.0 - time);
                float ripple = smoothstep(0.88, 1.0, rippleWave);
                ripple *= saturate(1.0 - edge) *
                    smoothstep(0.08, 0.3, distanceFromEdge);
                half3 color = lerp(
                    _InnerColor.rgb,
                    _RippleColor.rgb,
                    ripple * 0.3
                );
                color = lerp(color, _EdgeColor.rgb, edge);

                float innerAlpha = _InnerColor.a;
                float alpha =
                    innerAlpha +
                    ripple * _RippleColor.a * 0.55 +
                    edge * _EdgeColor.a;

                return half4(color, alpha * shape * _Fade);
            }
            ENDHLSL
        }
    }
}
