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
        _FlowPhaseOffset ("Flow Phase Offset", Float) = 0
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

            #include "AnomalyPixel.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InnerColor;
                half4 _EdgeColor;
                half4 _FlowColor;
                half4 _CenterColor;
                float _EdgeWidth;
                float _FlowSpeed;
                float _FlowPhaseOffset;
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
                float2 size = max(_RegionSize.xy, float2(0.0625, 0.0625));
                float2 worldPoint = AnomalySnap((input.uv - 0.5) * size);
                const float texel = 1.0 / 16.0;
                float radius = length(worldPoint);
                float inwardPhase = frac(
                    radius * 0.42 + floor(_VisualTime * _FlowSpeed / (0.42 * texel)) * (0.42 * texel) + _FlowPhaseOffset
                );
                float ringDistance = abs(inwardPhase - 0.5);
                float inwardRing = 1.0 - step(texel * 0.42, ringDistance);
                float angle = atan2(worldPoint.y, worldPoint.x);
                float spoke = pow(
                    saturate(abs(cos(angle * 6.0))),
                    12.0
                );
                float inwardFlow = inwardRing *
                    lerp(0.28, 1.0, floor(spoke * 3.0 + 0.5) / 3.0);

                float centerGlow = floor(exp2(-radius * radius * 0.32) * 4.0 + 0.5) / 4.0;
                float centerPulse = 0.82 +
                    sin(_VisualTime * _CenterPulseSpeed * 6.28318) * 0.18;
                centerGlow *= centerPulse;

                half3 color = _InnerColor.rgb;
                color = lerp(color, _FlowColor.rgb, inwardFlow * 0.6);
                color = lerp(color, _CenterColor.rgb, centerGlow * 0.7);


                float alpha = _InnerColor.a +
                    inwardFlow * _FlowColor.a +
                    centerGlow * _CenterColor.a;


                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
