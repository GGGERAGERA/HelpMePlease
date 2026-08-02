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
        _InnerPatternIntensity ("Inner Pattern Intensity", Range(0, 0.25)) = 0.14
        _InnerPatternSpeed ("Inner Pattern Speed", Float) = 1.8
        _InnerPatternScale ("Inner Pattern Scale", Float) = 3.5
        _WarningPulseFrequency ("Warning Pulse Frequency", Float) = 0.22
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
                float _InnerPatternIntensity;
                float _InnerPatternSpeed;
                float _InnerPatternScale;
                float _WarningPulseFrequency;
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
                float2 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.positionWS = positionWS.xy;
                return output;
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 345.45));
                value += dot(value, value + 34.345);
                return frac(value.x * value.y);
            }

            float BerserkStrokes(float2 positionWS)
            {
                float2 direction = normalize(float2(1.0, 0.2));
                float2 perpendicular = float2(-direction.y, direction.x);
                float2 flowPosition = float2(
                    dot(positionWS, direction) -
                        _VisualTime * _InnerPatternSpeed,
                    dot(positionWS, perpendicular)
                );
                float cellSize = max(0.5, _InnerPatternScale);
                float2 cellPosition = flowPosition / cellSize;
                float2 cell = floor(cellPosition);
                float2 localPosition = frac(cellPosition) - 0.5;
                float randomValue = Hash21(cell);
                localPosition.y += (randomValue - 0.5) * 0.12;

                float shortDash =
                    (1.0 - smoothstep(0.28, 0.38, abs(localPosition.x))) *
                    (1.0 - smoothstep(0.025, 0.07, abs(localPosition.y)));
                shortDash *= step(0.42, randomValue);

                float warningPhase = frac(
                    _VisualTime * max(0.01, _WarningPulseFrequency) +
                    randomValue
                );
                float brightPulse =
                    1.0 - smoothstep(0.0, 0.12, warningPhase);
                return shortDash * (0.62 + brightPulse * 0.38);
            }

            float ExplosiveWarnings(float2 positionWS)
            {
                float cellSize = max(0.75, _InnerPatternScale);
                float2 cellPosition = positionWS / cellSize;
                float2 cell = floor(cellPosition);
                float2 localPosition = frac(cellPosition) - 0.5;
                float randomValue = Hash21(cell);
                float2 offset = float2(
                    Hash21(cell + 17.3),
                    Hash21(cell + 41.7)
                ) - 0.5;
                localPosition -= offset * 0.34;

                float age = frac(
                    _VisualTime * max(0.01, _WarningPulseFrequency) +
                    randomValue
                );
                float active = step(0.76, randomValue) *
                    (1.0 - smoothstep(0.0, 0.3, age));
                float expansion = saturate(
                    age * max(0.1, _InnerPatternSpeed) * 4.0
                );
                float distanceToCenter = length(localPosition);
                float ringRadius = lerp(0.055, 0.24, expansion);
                float ring = 1.0 - smoothstep(
                    0.018,
                    0.045,
                    abs(distanceToCenter - ringRadius)
                );
                float centerPoint  = 1.0 - smoothstep(
                    0.025,
                    0.075,
                    distanceToCenter
                );
                return active * max(ring, centerPoint  * (1.0 - expansion));
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
                float berserkPattern = BerserkStrokes(input.positionWS);
                float explosivePattern =
                    ExplosiveWarnings(input.positionWS);
                float innerPattern = lerp(
                    berserkPattern,
                    explosivePattern,
                    warningMode
                ) * saturate(1.0 - edge);
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
                    saturate(edge + innerPattern * 0.55)
                );
                float patternAlpha = innerPattern *
                    _InnerPatternIntensity * 0.6;
                float alpha = lerp(
                    _InnerColor.a * innerPulse + patternAlpha,
                    _EdgeColor.a * borderPulse,
                    edge
                );

                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
