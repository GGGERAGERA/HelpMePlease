Shader "World/Stasis Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.015, 0.14, 0.24, 0.13)
        _EdgeColor ("Edge Color", Color) = (0.12, 0.7, 1, 0.48)
        _RippleColor ("Ripple Color", Color) = (0.22, 0.82, 1, 0.2)
        _DetailColor ("Slow Detail Color", Color) = (0.35, 0.88, 1, 0.12)
        _EdgeWidth ("Edge Width (World Units)", Range(0.1, 0.75)) = 0.35
        _PulseSpeed ("Stripe Speed", Float) = 0.18
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
                half4 _RippleColor;
                half4 _DetailColor;
                float _EdgeWidth;
                float _PulseSpeed;
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

                float worldY = samplePoint.y * halfSize.y;
                float2 worldPoint = samplePoint * halfSize;
                float stripeCoordinate = frac(
                    (worldY + _VisualTime * _PulseSpeed) / 5.0
                );
                float stripeDistance = abs(stripeCoordinate - 0.5);
                float stripe = 1.0 - smoothstep(
                    0.025,
                    0.08,
                    stripeDistance
                );
                stripe *= saturate(1.0 - edge);

                float verticalCoordinate = frac(
                    (worldPoint.x +
                     sin(_VisualTime * 0.22 + worldPoint.y * 0.18) * 0.2) /
                    3.4
                );
                float verticalDistance = abs(verticalCoordinate - 0.5);
                float verticalLine = 1.0 - smoothstep(
                    0.012,
                    0.045,
                    verticalDistance
                );

                float ringCoordinate = frac(
                    length(worldPoint) * 0.19 -
                    _VisualTime * _PulseSpeed * 0.16
                );
                float ringDistance = abs(ringCoordinate - 0.5);
                float slowRing = 1.0 - smoothstep(
                    0.018,
                    0.065,
                    ringDistance
                );
                float detail = (verticalLine * 0.38 + slowRing * 0.24) *
                    saturate(1.0 - edge);

                half3 color = lerp(
                    _InnerColor.rgb,
                    _RippleColor.rgb,
                    stripe * 0.18
                );
                color = lerp(color, _DetailColor.rgb, detail);

                float edgePulse = 0.88 +
                    sin(_VisualTime * max(0.1, _PulseSpeed) * 2.2) * 0.12;
                color = lerp(color, _EdgeColor.rgb, edge * edgePulse);

                float interiorAlpha =
                    _InnerColor.a +
                    stripe * _RippleColor.a * 0.22 +
                    detail * _DetailColor.a;
                float alpha = lerp(
                    interiorAlpha,
                    _EdgeColor.a * edgePulse,
                    edge
                );

                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
