Shader "Universal Render Pipeline/2D/Sprite-Lit-CustomAdvanced"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0

        _Brightness("Brightness", Range(-1, 1)) = 0
        _Saturation("Saturation", Range(-1, 1)) = 0
        _Contrast("Contrast", Range(-1, 1)) = 0

        [Toggle(_EMISSION_ON)] _EnableEmission("Enable Emission", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity("Emission Intensity", Range(0, 10)) = 1

        [Toggle(_OUTLINE_ON)] _EnableOutline("Enable Outline", Float) = 0
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width (Pixels)", Range(0, 100)) = 2
        [Toggle(_OUTLINE_PIXELATED)] _OutlinePixelated("Pixelated Outline", Float) = 0
        [Toggle(_OUTLINE_GLOW)] _OutlineGlow("Outline Glows (Unlit)", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE
            
            #pragma multi_compile _ _EMISSION_ON
            #pragma multi_compile _ _OUTLINE_ON
            #pragma multi_compile _ _OUTLINE_PIXELATED
            #pragma multi_compile _ _OUTLINE_GLOW

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
                float2 customUV    : TEXCOORD6;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Brightness;
                half _Saturation;
                half _Contrast;
                half4 _EmissionColor;
                half _EmissionIntensity;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            half3 AdjustSaturation(half3 col, half sat)
            {
                half gray = dot(col, half3(0.299, 0.587, 0.114));
                half3 neutral = half3(gray, gray, gray);
                if (sat >= 0)
                {
                    return lerp(neutral, col, 1.0 + sat);
                }
                else
                {
                    return lerp(col, neutral, -sat);
                }
            }

            half4 ApplyCustomEffects(half4 col, float2 uv)
            {
                col.rgb += _Brightness;
                col.rgb = AdjustSaturation(col.rgb, _Saturation);
                col.rgb = (col.rgb - 0.5) * (1.0 + _Contrast) + 0.5;

                #if defined(_EMISSION_ON)
                    col.rgb += _EmissionColor.rgb * _EmissionIntensity;
                #endif

                #if defined(_OUTLINE_ON)
                    float2 texelSize = float2(abs(ddx(uv.x)), abs(ddy(uv.y)));
                    float currentAlpha = col.a;
                    bool isEdge = false;
                    float width = _OutlineWidth;

                    #if defined(_OUTLINE_PIXELATED)
                        float2 offsets[8] = {
                            float2(width, 0), float2(-width, 0),
                            float2(0, width), float2(0, -width),
                            float2(width, width), float2(-width, -width),
                            float2(width, -width), float2(-width, width)
                        };
                        
                        [unroll]
                        for (int i = 0; i < 8; i++)
                        {
                            float2 offset = offsets[i] * texelSize;
                            float neighborAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset).a;
                            if (currentAlpha > 0.5 && neighborAlpha < 0.5)
                            {
                                isEdge = true;
                            }
                        }
                    #else
                        [unroll]
                        for (int d = 0; d < 16; d++)
                        {
                            float angle = d * 0.392699082;
                            float2 offset = float2(cos(angle), sin(angle)) * width * texelSize;
                            float neighborAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset).a;
                            if (currentAlpha > 0.5 && neighborAlpha < 0.5)
                            {
                                isEdge = true;
                            }
                        }
                    #endif

                    if (isEdge)
                    {
                        #if defined(_OUTLINE_GLOW)
                            // Обводка светится (unlit) - не зависит от освещения
                            half3 outlineCol = _OutlineColor.rgb;
                            #if defined(_EMISSION_ON)
                                outlineCol += _EmissionColor.rgb * _EmissionIntensity;
                            #endif
                            col.rgb = outlineCol;
                        #else
                            // Обводка затеняется как обычный спрайт - смешиваем с освещённым цветом
                            col.rgb = lerp(col.rgb, _OutlineColor.rgb, 0.7);
                        #endif
                        
                        col.a = _OutlineColor.a;
                    }
                #endif

                return col;
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.customUV = input.uv;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 finalColor = CommonLitFragment(input, input.color);
                return ApplyCustomEffects(finalColor, input.customUV);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes { COMMON_2D_NORMALS_INPUTS float4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            CBUFFER_START( UnityPerMaterial ) half4 _Color; half _Brightness; half _Saturation; half _Contrast; half4 _EmissionColor; half _EmissionIntensity; half4 _OutlineColor; half _OutlineWidth; CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 NormalsRenderingFragment(Varyings input) : SV_Target { return CommonNormalsFragment(input, input.color); }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE
            #pragma multi_compile _ _EMISSION_ON
            #pragma multi_compile _ _OUTLINE_ON
            #pragma multi_compile _ _OUTLINE_PIXELATED
            #pragma multi_compile _ _OUTLINE_GLOW

            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings 
            { 
                COMMON_2D_OUTPUTS 
                half4 color : COLOR; 
                float2 customUV : TEXCOORD6;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
          
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Brightness;
                half _Saturation;
                half _Contrast;
                half4 _EmissionColor;
                half _EmissionIntensity;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            half3 AdjustSaturation(half3 col, half sat)
            {
                half gray = dot(col, half3(0.299, 0.587, 0.114));
                half3 neutral = half3(gray, gray, gray);
                if (sat >= 0)
                {
                    return lerp(neutral, col, 1.0 + sat);
                }
                else
                {
                    return lerp(col, neutral, -sat);
                }
            }

            half4 ApplyCustomEffects(half4 col, float2 uv)
            {
                col.rgb += _Brightness;
                col.rgb = AdjustSaturation(col.rgb, _Saturation);
                col.rgb = (col.rgb - 0.5) * (1.0 + _Contrast) + 0.5;

                #if defined(_EMISSION_ON)
                    col.rgb += _EmissionColor.rgb * _EmissionIntensity;
                #endif

                #if defined(_OUTLINE_ON)
                    float2 texelSize = float2(abs(ddx(uv.x)), abs(ddy(uv.y)));
                    float currentAlpha = col.a;
                    bool isEdge = false;
                    float width = _OutlineWidth;

                    #if defined(_OUTLINE_PIXELATED)
                        float2 offsets[8] = {
                            float2(width, 0), float2(-width, 0),
                            float2(0, width), float2(0, -width),
                            float2(width, width), float2(-width, -width),
                            float2(width, -width), float2(-width, width)
                        };
                        
                        [unroll]
                        for (int i = 0; i < 8; i++)
                        {
                            float2 offset = offsets[i] * texelSize;
                            float neighborAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset).a;
                            if (currentAlpha > 0.5 && neighborAlpha < 0.5)
                            {
                                isEdge = true;
                            }
                        }
                    #else
                        [unroll]
                        for (int d = 0; d < 16; d++)
                        {
                            float angle = d * 0.392699082;
                            float2 offset = float2(cos(angle), sin(angle)) * width * texelSize;
                            float neighborAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset).a;
                            if (currentAlpha > 0.5 && neighborAlpha < 0.5)
                            {
                                isEdge = true;
                            }
                        }
                    #endif

                    if (isEdge)
                    {
                        #if defined(_OUTLINE_GLOW)
                            half3 outlineCol = _OutlineColor.rgb;
                            #if defined(_EMISSION_ON)
                                outlineCol += _EmissionColor.rgb * _EmissionIntensity;
                            #endif
                            col.rgb = outlineCol;
                        #else
                            col.rgb = lerp(col.rgb, _OutlineColor.rgb, 0.7);
                        #endif
                        
                        col.a = _OutlineColor.a;
                    }
                #endif
                return col;
            }

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color *_Color * unity_SpriteColor;
                o.customUV = input.uv;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 finalColor = CommonUnlitFragment(input, input.color);
                return ApplyCustomEffects(finalColor, input.customUV);
            }
            ENDHLSL
        }
    }
}