Shader "World/Capture Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.02, 0.24, 0.34, 0.08)
        _EdgeColor ("Edge Color", Color) = (0.18, 0.86, 1, 0.72)
        _PatternColor ("Pattern Color", Color) = (0.16, 0.72, 0.92, 0.12)
        _ProgressColor ("Progress Color", Color) = (0.45, 1, 0.86, 0.95)
        _EdgeWidth ("Edge Width", Range(0.01, 0.3)) = 0.075
        _PulseSpeed ("Pulse Speed", Float) = 0.85
        _FillIntensity ("Fill Intensity", Range(0, 2)) = 0.7
        _Progress ("Progress", Range(0, 1)) = 0
        _CompletionFlash ("Completion Flash", Range(0, 3)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
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
                half4 _PatternColor;
                half4 _ProgressColor;
                float _EdgeWidth;
                float _PulseSpeed;
                float _FillIntensity;
                float _Progress;
                float _CompletionFlash;
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
                const float TwoPi = 6.28318530718;
                float2 samplePoint = (input.uv - 0.5) * 2.0;
                float radius = length(samplePoint);
                float angle = atan2(samplePoint.y, samplePoint.x);
                float angle01 = frac(0.25 - angle / TwoPi);
                float time = _VisualTime * _PulseSpeed;

                float boundary =
                    0.855 +
                    sin(angle * 8.0) * 0.018 +
                    sin(angle * 16.0 + 0.6) * 0.007;
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
                float glow = 1.0 - smoothstep(
                    _EdgeWidth,
                    _EdgeWidth * 3.2,
                    edgeDistance
                );

                float edgePulse =
                    0.72 +
                    0.28 * (sin(time * TwoPi) * 0.5 + 0.5);
                float edgeBreaks =
                    0.76 +
                    0.24 *
                    smoothstep(
                        -0.25,
                        0.55,
                        sin(angle * 24.0 + time * 0.18)
                    );
                edge *= edgeBreaks;

                float diagonal = abs(
                    frac(
                        (samplePoint.x + samplePoint.y * 0.72) * 3.2 -
                        time * 0.08
                    ) - 0.5
                );
                float movingBands =
                    1.0 - smoothstep(0.035, 0.085, diagonal);
                float radialTicks =
                    1.0 - smoothstep(
                        0.22,
                        0.48,
                        abs(sin(angle * 12.0 - time * 0.1))
                    );
                radialTicks *= smoothstep(0.26, 0.62, radius);
                float pattern =
                    saturate(movingBands * 0.65 + radialTicks * 0.35);
                pattern *= shape * saturate(1.0 - edge);

                float progressVisible = step(0.001, _Progress);
                float progressArc = 1.0 - smoothstep(
                    _Progress,
                    _Progress + 0.012,
                    angle01
                );
                progressArc *= progressVisible;
                float progressEdge = edge * progressArc;
                float progressHead = 1.0 - smoothstep(
                    0.0,
                    0.025,
                    abs(angle01 - _Progress)
                );
                progressHead *= edge * progressVisible;

                half3 color = _InnerColor.rgb;
                color = lerp(
                    color,
                    _PatternColor.rgb,
                    pattern * 0.45
                );
                color = lerp(color, _EdgeColor.rgb, edge);
                color = lerp(
                    color,
                    _ProgressColor.rgb,
                    saturate(progressEdge + progressHead)
                );
                color += _CompletionFlash *
                    lerp(_ProgressColor.rgb, half3(1, 1, 1), 0.7);

                float innerAlpha =
                    _InnerColor.a *
                    _FillIntensity *
                    (0.72 + pattern * 0.28);
                float alpha =
                    innerAlpha * shape +
                    glow * _EdgeColor.a * 0.18 * edgePulse +
                    edge * _EdgeColor.a * edgePulse +
                    progressEdge * _ProgressColor.a +
                    progressHead * _ProgressColor.a * 0.8 +
                    pattern * _PatternColor.a * _FillIntensity;
                alpha += _CompletionFlash * 0.28 * shape;

                return half4(
                    saturate(color),
                    saturate(alpha) * _Fade
                );
            }
            ENDHLSL
        }
    }
}
