Shader "Custom/2D/Sprite-Lit-Combo"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}

        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // --- Цветокоррекция (единицы = без изменений) ---
        _Brightness("Brightness", Range(0.0, 3.0)) = 1.0
        _Contrast("Contrast", Range(0.0, 3.0)) = 1.0
        _Saturation("Saturation", Range(0.0, 3.0)) = 1.0

        // --- Обводка ---
        [Toggle] _UseOutline("Use Outline", Float) = 0
        _OutlineThickness("Outline Thickness", Range(1, 10)) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        [Toggle] _SmoothOutline("Smooth Outline", Float) = 0

        [Toggle] _UseTextureOutline("Use Texture Color as Outline", Float) = 0
        _OutlineBrightness("Outline Brightness", Range(0.0, 3.0)) = 0.6
        _OutlineSaturation("Outline Saturation", Range(0.0, 3.0)) = 0.8

        // --- Эмиссия ---
        [Toggle] _UseEmission("Use Emission", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _EmissionStrength("Emission Strength", Range(0.0, 10.0)) = 0.0

        // Legacy
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Name "Lit2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Brightness, _Contrast, _Saturation;
                float _UseOutline, _OutlineThickness;
                half4 _OutlineColor;
                float _SmoothOutline;
                float _UseTextureOutline;
                half _OutlineBrightness, _OutlineSaturation;
                float _UseEmission;
                half4 _EmissionColor;
                float _EmissionStrength;
            CBUFFER_END

            // Проверка: все ползунки в единице?
            bool IsIdentity()
            {
                return (_Brightness == 1.0 && _Contrast == 1.0 && _Saturation == 1.0);
            }

            // Цветокоррекция (применяется, только если не идентична)
            half3 AdjustColor(half3 color)
            {
                // Если все настройки дефолтные – не трогаем цвет
                if (IsIdentity())
                    return color;

                // Контраст
                color = (color - 0.5) * _Contrast + 0.5;
                // Яркость
                color *= _Brightness;
                // Насыщенность
                half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(luminance.xxx, color, _Saturation);
                return saturate(color);
            }

            // Поиск обводки
            void FindOutlineColor(float2 uv, inout half3 outlineColor, inout half outlineAlpha)
            {
                outlineColor = half3(0,0,0);
                outlineAlpha = 0;

                if (_OutlineThickness <= 0)
                    return;

                float2 texSize;
                _MainTex.GetDimensions(texSize.x, texSize.y);
                float2 texelSize = 1.0 / texSize;

                int radius = (int)_OutlineThickness;
                radius = clamp(radius, 1, 10);

                [loop]
                for (int dx = -radius; dx <= radius; dx++)
                {
                    [loop]
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        float2 neighborUV = uv + float2(dx * texelSize.x, dy * texelSize.y);

                        half4 neighborColor;
                        if (_SmoothOutline)
                            neighborColor = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, neighborUV);
                        else
                            neighborColor = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, neighborUV);

                        if (neighborColor.a > 0.5)
                        {
                            if (_UseTextureOutline)
                            {
                                half3 adjusted = neighborColor.rgb * _OutlineBrightness;
                                half lum = dot(adjusted, half3(0.2126, 0.7152, 0.0722));
                                adjusted = lerp(lum.xxx, adjusted, _OutlineSaturation);
                                outlineColor = adjusted;
                            }
                            else
                            {
                                outlineColor = _OutlineColor.rgb;
                            }
                            outlineAlpha = 1.0;
                            return;
                        }
                    }
                }
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = texColor.a;

                // Если коррекция идентична – используем цвет как есть
                half3 finalColor = IsIdentity() ? texColor.rgb : AdjustColor(texColor.rgb);
                half finalAlpha = alpha;

                if (_UseOutline && alpha < 0.5)
                {
                    half3 outlineColor;
                    half outlineAlpha;
                    FindOutlineColor(input.uv, outlineColor, outlineAlpha);
                    if (outlineAlpha > 0.5)
                    {
                        finalColor = outlineColor;
                        finalAlpha = 1.0;
                    }
                    else
                    {
                        finalAlpha = 0;
                    }
                }

                half4 litColor = CommonLitFragment(input, half4(finalColor, finalAlpha) * input.color);

                if (_UseEmission)
                    litColor.rgb += _EmissionColor.rgb * _EmissionStrength;

                return litColor;
            }
            ENDHLSL
        }

        // Остальные проходы без изменений
        Pass
        {
            Name "NormalsRendering"
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}