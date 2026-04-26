Shader "Custom/Sprite-PixelOutline"
{
    Properties
    {
        [MainTexture] _MainTex("Diffuse", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)

        _Brightness("Brightness", Range(0, 2)) = 1.0
        _Saturation("Saturation", Range(-2, 2)) = 1.0

        [Toggle] _OutlineEnabled("Enable Outline", Float) = 1
        _OutlineThickness("Thickness (px)", Range(1, 16)) = 2
        [KeywordEnum(Solid, DarkenEdges)] _OutlineType("Type", Float) = 0
        _OutlineColor("Color", Color) = (0,0,0,1)
        _OutlineDarken("Darken Amount", Range(0, 1)) = 0.6
        [Toggle] _OutlineBehind("Draw Behind Sprite", Float) = 0
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
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma shader_feature_local _OUTLINETYPE_SOLID _OUTLINETYPE_DARKENEDGES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Brightness;
                float _Saturation;

                float _OutlineEnabled;
                float _OutlineThickness;
                float4 _OutlineColor;
                float _OutlineDarken;
                float _OutlineBehind;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                float3 positionOS = input.positionOS.xyz;
                float2 uv = input.uv;

                // Расширение вершин по сторонам прямоугольника без деформации
                if (_OutlineEnabled > 0.5)
                {
                    float thickness = max(1.0, round(_OutlineThickness));
                    float2 texelSize = _MainTex_TexelSize.xy;

                    // Определяем, к какой стороне принадлежит вершина
                    // Предполагаем, что pivot в центре и вершины имеют координаты ±0.5 по X и Y
                    // (стандартный спрайт размером 1x1 юнит)
                    float2 offset = float2(0, 0);

                    // Сторона X
                    if (abs(positionOS.x) > 0.49) // близко к краю
                    {
                        offset.x = sign(positionOS.x) * thickness * texelSize.x * 100.0;
                    }
                    // Сторона Y
                    if (abs(positionOS.y) > 0.49)
                    {
                        offset.y = sign(positionOS.y) * thickness * texelSize.y * 100.0;
                    }

                    positionOS.xy += offset;
                }

                o.positionCS = TransformObjectToHClip(positionOS);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.color = input.color * _Color;
                return o;
            }

            half3 AdjustSaturation(half3 color, float saturation)
            {
                float gray = dot(color, half3(0.2125, 0.7154, 0.0721));
                return lerp(half3(gray, gray, gray), color, saturation);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 spriteColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                spriteColor.rgb = AdjustSaturation(spriteColor.rgb, _Saturation);
                spriteColor.rgb *= _Brightness;
                spriteColor *= input.color;

                // Если пиксель спрайта непрозрачный – рисуем его
                if (spriteColor.a > 0.01)
                    return spriteColor;

                // Обводка отключена
                if (_OutlineEnabled < 0.5)
                    discard;

                float2 texelSize = _MainTex_TexelSize.xy;
                float thickness = max(1.0, round(_OutlineThickness));

                float2 offsets[4] = {
                    float2( 1,  0), float2(-1,  0), float2( 0,  1), float2( 0, -1)
                };

                float found = 0.0;
                half3 edgeColor = half3(0,0,0);

                for (int i = 0; i < 4; i++)
                {
                    float2 offset = offsets[i] * thickness * texelSize;
                    float2 sampleUV = input.uv + offset;

                    half4 sampleCol = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, sampleUV, 0);
                    if (sampleCol.a > 0.01)
                    {
                        found = 1.0;
                        edgeColor = sampleCol.rgb;
                        break;
                    }
                }

                if (found > 0.5)
                {
                    half4 outlineCol;
                    #if defined(_OUTLINETYPE_DARKENEDGES)
                        outlineCol = half4(edgeColor * _OutlineDarken, 1.0);
                    #else
                        outlineCol = half4(_OutlineColor.rgb, 1.0);
                    #endif
                    return outlineCol * input.color;
                }

                discard;
                return half4(0,0,0,0);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}