Shader "World/Capture Zone"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (0.18, 0.86, 1, 0.72)
        _ProgressColor ("Progress Color", Color) = (0.45, 1, 0.86, 0.95)
        _EdgeWidth ("Edge Width", Range(0.01, 0.3)) = 0.075
        _Progress ("Progress", Range(0, 1)) = 0
        _CompletionFlash ("Completion Flash", Range(0, 3)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
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
                half4 _EdgeColor;
                half4 _ProgressColor;
                float _EdgeWidth;
                float _Progress;
                float _CompletionFlash;
                float _Fade;
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
                float boundary = 0.855;

                float edgeDistance = abs(radius - boundary);
                float edge = 1.0 - smoothstep(
                    _EdgeWidth * 0.25,
                    _EdgeWidth,
                    edgeDistance
                );
                float progressVisible = step(0.001, _Progress);
                float progressArc = 1.0 - smoothstep(
                    _Progress,
                    _Progress + 0.012,
                    angle01
                );
                progressArc *= progressVisible;
                float progressEdge = edge * progressArc;
                float progressBrightness = lerp(
                    0.72,
                    1.28,
                    smoothstep(0.0, 1.0, _Progress)
                );
                half3 color = _EdgeColor.rgb;
                color = lerp(
                    color,
                    _ProgressColor.rgb,
                    saturate(progressEdge * progressBrightness)
                );
                color += _CompletionFlash *
                    lerp(_ProgressColor.rgb, half3(1, 1, 1), 0.7);

                float alpha =
                    edge * _EdgeColor.a +
                    progressEdge * _ProgressColor.a * progressBrightness;
                alpha += _CompletionFlash * 0.32 * edge;

                return half4(
                    saturate(color),
                    saturate(alpha) * _Fade
                );
            }
            ENDHLSL
        }
    }
}
