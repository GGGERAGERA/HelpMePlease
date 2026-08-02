Shader "World/Evacuation Corridor"
{
    Properties
    {
        _InsideColor ("Inside Color", Color) = (0.22, 0.82, 0.95, 0.11)
        _EdgeColor ("Edge Color", Color) = (0.24, 0.94, 1, 0.48)
        _DirectionColor ("Direction Color", Color) = (0.48, 1, 1, 0.2)
        _OutsideDarkness ("Outside Darkness", Range(0, 0.8)) = 0.32
        _Reveal ("Reveal", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 0
        _CorridorRatio ("Corridor Ratio", Vector) = (0.08, 0.035, 0, 0)
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
                half4 _InsideColor;
                half4 _EdgeColor;
                half4 _DirectionColor;
                float _OutsideDarkness;
                float _Reveal;
                float _Fade;
                float4 _CorridorRatio;
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
                float2 localPosition : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.localPosition = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 localPosition = input.localPosition;
                float2 halfSize = max(
                    _CorridorRatio.xy,
                    float2(0.0001, 0.0001)
                );
                float revealFront = lerp(
                    -halfSize.x,
                    halfSize.x,
                    _Reveal
                );
                float revealMask = 1.0 - smoothstep(
                    revealFront,
                    revealFront + 0.012,
                    localPosition.x
                );

                float2 rectangleDistance =
                    abs(localPosition) - halfSize;
                float signedDistance =
                    max(rectangleDistance.x, rectangleDistance.y);
                float softness = 0.008;
                float inside = 1.0 - smoothstep(
                    -softness,
                    softness,
                    signedDistance
                );
                inside *= revealMask;

                float edge = 1.0 - smoothstep(
                    0.0,
                    0.014,
                    abs(signedDistance)
                );
                edge *= revealMask;

                float time = _VisualTime;
                float edgeAlpha = edge * _EdgeColor.a;

                float2 normalized = localPosition / halfSize;
                float movingCell = frac(
                    (normalized.x - time * 0.12) * 2.5
                );
                float chevronDistance = abs(
                    abs(normalized.y) * 0.22 -
                    abs(movingCell - 0.5)
                );
                float directionMark = 1.0 - smoothstep(
                    0.025,
                    0.07,
                    chevronDistance
                );
                directionMark *= inside *
                    (1.0 - smoothstep(
                        0.45,
                        0.92,
                        abs(normalized.y)
                    ));

                float outside = 1.0 - inside;
                half3 color = lerp(
                    half3(0.005, 0.012, 0.02),
                    _InsideColor.rgb,
                    inside
                );
                color = lerp(
                    color,
                    _DirectionColor.rgb,
                    directionMark * 0.45
                );
                color = lerp(color, _EdgeColor.rgb, edge);

                float alpha =
                    outside * _OutsideDarkness +
                    inside * _InsideColor.a +
                    directionMark * _DirectionColor.a +
                    edgeAlpha;

                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
