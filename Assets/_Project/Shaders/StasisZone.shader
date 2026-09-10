Shader "World/Stasis Zone"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.015, 0.14, 0.24, 0.13)
        _EdgeColor ("Edge Color", Color) = (0.12, 0.7, 1, 0.48)
        _RippleColor ("Ripple Color", Color) = (0.22, 0.82, 1, 0.2)
        _DetailColor ("Slow Detail Color", Color) = (0.35, 0.88, 1, 0.12)
        _EdgeWidth ("Edge Width (World Units)", Range(0.1, 0.75)) = 0.35
        _PulseSpeed ("Wave Speed", Float) = 0.18
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
                float2 size = max(_RegionSize.xy, float2(0.0625, 0.0625));
                float2 worldPoint = AnomalySnap((input.uv - 0.5) * size);

                // Keep the original slow outward waves, advancing one effect texel
                // at a time. A binary mask gives every ring a solid pixel edge.
                const float spacing = 1.0 / 0.19;
                const float texel = 1.0 / 16.0;
                float travel = floor(_VisualTime * _PulseSpeed *
                    (0.16 / 0.19) / texel) * texel;
                float ringDistance = abs(frac((length(worldPoint) - travel) /
                    spacing) - 0.5) * spacing;
                float ring = 1.0 - step(texel, ringDistance);

                // No inset frame, crossing scanlines or independently pulsing border.
                half3 ringColor = lerp(_RippleColor.rgb, _DetailColor.rgb, 0.5);
                half3 color = lerp(_InnerColor.rgb, ringColor, ring);
                float alpha = _InnerColor.a + ring * (_RippleColor.a + _DetailColor.a);
                return half4(color, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
