Shader "Custom/2D/PixelSpriteLit"
{
    Properties
    {
        [MainTexture] _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}

        [Toggle] _ZWrite("ZWrite", Float) = 0

        _Brightness("Brightness", Range(0,3)) = 1
        _Saturation("Saturation", Range(-2,2)) = 0

        [Toggle] _OutlineEnable("Enable Outline", Float) = 0
        _OutlineWidth("Outline Width (px)", Range(0,16)) = 2
        [KeywordEnum(Solid, NeighborColor)] _OutlineMode("Outline Mode", Float) = 0
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineDarken("Neighbor Darken", Range(0,1)) = 0.3
        _OutlineBrightness("Outline Brightness", Range(0,5)) = 1
        _OutlineFade("Edge Fade", Range(0,1)) = 0
        [Toggle] _OutlineBehind("Draw Behind Sprite", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        // === Проход 1: основной спрайт (с освещением) ===
        Pass
        {
            Name "Sprite"
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
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Brightness;
                float _Saturation;
            CBUFFER_END

            half3 AdjustSaturation(half3 c, float s)
            {
                half gray = dot(c, half3(0.2125, 0.7154, 0.0721));
                if (s < 0.0)
                {
                    float t = saturate(-s * 0.5);
                    return lerp(c, gray.xxx, t);
                }
                else
                {
                    half3 diff = c - gray.xxx;
                    return gray.xxx + diff * (1.0 + s);
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
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseColor = input.color * mainTex;
                half4 lit = CommonLitFragment(input, baseColor);
                half3 col = lit.rgb * _Brightness;
                col = AdjustSaturation(col, _Saturation);
                return half4(col, lit.a);
            }
            ENDHLSL
        }

        // === Проход 2: Обводка (расширенная геометрия) ===
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "Universal2D" }
            Cull Front

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            // Явное объявление текстуры и сэмплера
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            uniform float4 _MainTex_TexelSize;

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _OutlineEnable;
                float _OutlineWidth;
                float _OutlineDarken;
                float _OutlineBrightness;
                float _OutlineFade;
                half4 _OutlineColor;
                float _OutlineBehind;
                float _OutlineMode;
            CBUFFER_END

            Varyings OutlineVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();

                float3 posOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                float2 uv = input.uv;

                // Направление от центра спрайта (0.5,0.5)
                float2 dir = uv - 0.5;
                float len = length(dir);
                if (len > 0.0001) dir /= len;

                // Размер одного пикселя в object space
                float2 pixelSizeOS = _MainTex_TexelSize.xy;
                float outlineWidthOS = _OutlineWidth * length(pixelSizeOS);

                posOS.xy += dir * outlineWidthOS;

                Varyings o;
                o.positionCS = TransformObjectToHClip(posOS);
                o.uv = uv;
                return o;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                if (_OutlineEnable < 0.5) discard;

                float2 uv = input.uv;
                float2 texelSize = _MainTex_TexelSize.xy * _OutlineWidth;

                bool hasOpaque = false;
                half4 neighborSum = half4(0,0,0,0);
                float count = 0;

                float2 offsets[4] = { float2(-1,0), float2(1,0), float2(0,1), float2(0,-1) };
                for (int i = 0; i < 4; i++)
                {
                    float2 sampleUV = uv + offsets[i] * texelSize;
                    half4 n = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                    if (n.a > 0.01)
                    {
                        hasOpaque = true;
                        neighborSum += n;
                        count++;
                    }
                }

                if (!hasOpaque) discard;

                half4 outlineCol;
                if (_OutlineMode < 0.5)
                {
                    outlineCol = _OutlineColor;
                }
                else
                {
                    outlineCol = neighborSum / count;
                    outlineCol.rgb *= (1.0 - _OutlineDarken);
                    outlineCol.a = 1.0;
                }

                outlineCol.rgb *= _OutlineBrightness;

                if (_OutlineFade > 0.0)
                {
                    float2 d = abs(uv - 0.5) * 2.0;
                    float edgeDist = max(d.x, d.y);
                    float fade = saturate(1.0 - edgeDist * _OutlineFade);
                    outlineCol.rgb *= fade;
                }

                return outlineCol;
            }
            ENDHLSL
        }

        // === Проход нормалей ===
        Pass
        {
            Name "NormalsRendering"
            Tags { "LightMode" = "NormalsRendering" }

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

        // === Forward проход ===
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

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