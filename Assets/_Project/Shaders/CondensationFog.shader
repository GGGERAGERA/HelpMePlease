Shader "UI/Condensation Fog"
{
    Properties
    {
        _MaskTex ("Fog Mask", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.72, 0.8, 0.82, 1)
        _Fade ("Fade", Range(0, 1)) = 0
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

        Pass
        {
            Name "FogOverlay"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Fade;
                float _FogTime;
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float mask = SAMPLE_TEXTURE2D(
                    _MaskTex,
                    sampler_MaskTex,
                    input.uv
                ).r;
                float2 noiseCell = floor(input.uv * float2(64.0, 36.0));
                float noise = Hash21(noiseCell + floor(_FogTime * 0.35));
                float density = lerp(0.78, 1.0, noise);
                return half4(
                    _FogColor.rgb,
                    mask * density * _Fade * _FogColor.a
                );
            }
            ENDHLSL
        }

        Pass
        {
            Name "RestoreMask"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask R

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment RestoreFrag

            float _RestoreAmount;

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            MaskVaryings MaskVert(MaskAttributes input)
            {
                MaskVaryings output;
                output.positionCS = float4(
                    input.positionOS.xy * 2.0 - 1.0,
                    0.0,
                    1.0
                );
                output.uv = input.uv;
                return output;
            }

            half4 RestoreFrag(MaskVaryings input) : SV_Target
            {
                return half4(1.0, 1.0, 1.0, _RestoreAmount);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ClearMask"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask R

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment BrushFrag

            float4 _BrushCenter;
            float _BrushRadius;
            float _ScreenAspect;

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            MaskVaryings MaskVert(MaskAttributes input)
            {
                MaskVaryings output;
                output.positionCS = float4(
                    input.positionOS.xy * 2.0 - 1.0,
                    0.0,
                    1.0
                );
                output.uv = input.uv;
                return output;
            }

            half4 BrushFrag(MaskVaryings input) : SV_Target
            {
                float2 delta = input.uv - _BrushCenter.xy;
                delta.x *= max(0.01, _ScreenAspect);
                float distanceToBrush = length(delta);
                float strength = 1.0 - smoothstep(
                    _BrushRadius * 0.62,
                    _BrushRadius,
                    distanceToBrush
                );
                return half4(0.0, 0.0, 0.0, strength);
            }
            ENDHLSL
        }
    }
}
