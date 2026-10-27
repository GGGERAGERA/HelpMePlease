Shader "Subject42/Environment Tile Lit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        _EnvironmentTint("Environment Tint", Color) = (1,1,1,1)
        _EnvironmentTintAmount("Environment Tint Amount", Range(0,1)) = 0
        _EnvironmentSaturation("Environment Saturation", Range(0,3)) = 1
        _EnvironmentBrightness("Environment Brightness", Range(0.25,2.5)) = 1
        _StudioHue("Studio Hue", Float) = 0
        _StudioSaturation("Studio Saturation", Float) = 1
        _StudioBrightness("Studio Brightness", Float) = 1
        _StudioContrast("Studio Contrast", Float) = 1
        _StudioGamma("Studio Gamma", Float) = 1
        _MidtoneLift("Sprite Midtones", Range(0,0.3)) = 0
        _MinimumLight("Minimum Sprite Light", Range(0,1)) = 0
        _ContourStrength("Inner Pixel Contour", Range(0,1)) = 0
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

        HLSLINCLUDE
        half4 ApplyEnvironmentLook(half4 source, half4 tint,
            half tintAmount, half saturation, half brightness)
        {
            half alpha = source.a;
            half luminance = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));
            half3 tinted = luminance * tint.rgb;
            half3 color = lerp(source.rgb, tinted, saturate(tintAmount));
            luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
            color = lerp(luminance.xxx, color, max(0.0h, saturation));
            return half4(color * max(0.0h, brightness), alpha);
        }
        float4 ApplyStudioLook(float4 source, float hue, float saturation, float brightness, float contrast, float gamma)
        {
            if (hue == 0 && saturation == 1 && brightness == 1 && contrast == 1 && gamma == 1) return source;
            float angle = radians(hue);
            float3 axis = normalize(float3(1, 1, 1));
            float3 color = source.rgb * cos(angle) + cross(axis, source.rgb) * sin(angle) + axis * dot(axis, source.rgb) * (1 - cos(angle));
            float luminance = dot(color, float3(.2126, .7152, .0722));
            color = lerp(luminance.xxx, color, saturation);
            color = max(0, (color - .5) * contrast + .5);
            color = pow(color, rcp(max(.05, gamma))) * brightness;
            return float4(min(color, 65504), source.a);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex LitVertex
            #pragma fragment LitFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EnvironmentTint;
                half _EnvironmentTintAmount;
                half _EnvironmentSaturation;
                half _EnvironmentBrightness;
                half _MidtoneLift;
                half _MinimumLight;
                half _ContourStrength;
                float _StudioHue, _StudioSaturation, _StudioBrightness, _StudioContrast, _StudioGamma;
            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonLitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 main = input.color *
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                main = ApplyEnvironmentLook(main, _EnvironmentTint,
                    _EnvironmentTintAmount, _EnvironmentSaturation,
                    _EnvironmentBrightness);
                main = ApplyStudioLook(main, _StudioHue, _StudioSaturation, _StudioBrightness, _StudioContrast, _StudioGamma);
                // Lift existing painted planes, preserving black and white endpoints.
                main.rgb += _MidtoneLift * 4.0h * main.rgb * (1.0h - saturate(main.rgb));
                if (_ContourStrength > 0.0h)
                {
                    float2 texel = _MainTex_TexelSize.xy;
                    half neighbour = min(min(
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texel.x, 0)).a,
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(texel.x, 0)).a), min(
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, texel.y)).a,
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, texel.y)).a));
                    main.rgb *= 1.0h - _ContourStrength * (1.0h - neighbour);
                }
                half4 mask = SAMPLE_TEXTURE2D(
                    _MaskTex, sampler_MaskTex, input.uv);
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(
                    _NormalMap, sampler_NormalMap, input.uv));
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(
                    main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);
                #if defined(DEBUG_DISPLAY)
                    SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(
                        inputData, input.positionWS, input.positionCS, _MainTex);
                    surfaceData.normalWS = input.normalWS;
                #endif
                half4 lit = CombinedShapeLightShared(surfaceData, inputData);
                lit.rgb = max(lit.rgb, main.rgb * _MinimumLight);
                return lit;
            }
        ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="NormalsRendering" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            struct Attributes { COMMON_2D_NORMALS_INPUTS float4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_NORMALS_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EnvironmentTint;
                half _EnvironmentTintAmount;
                half _EnvironmentSaturation;
                half _EnvironmentBrightness;
                half _MidtoneLift;
                half _MinimumLight;
                half _ContourStrength;
                float _StudioHue, _StudioSaturation, _StudioBrightness, _StudioContrast, _StudioGamma;
            CBUFFER_END
            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonNormalsVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            { return CommonNormalsFragment(input, input.color); }
        ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="UniversalForward" "Queue"="Transparent" "RenderType"="Transparent" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EnvironmentTint;
                half _EnvironmentTintAmount;
                half _EnvironmentSaturation;
                half _EnvironmentBrightness;
                half _MidtoneLift;
                half _MinimumLight;
                half _ContourStrength;
                float _StudioHue, _StudioSaturation, _StudioBrightness, _StudioContrast, _StudioGamma;
            CBUFFER_END
            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);
                color = ApplyStudioLook(color, _StudioHue, _StudioSaturation, _StudioBrightness, _StudioContrast, _StudioGamma);
                return ApplyEnvironmentLook(color, _EnvironmentTint,
                    _EnvironmentTintAmount, _EnvironmentSaturation,
                    _EnvironmentBrightness);
            }
        ENDHLSL
        }
    }
}
