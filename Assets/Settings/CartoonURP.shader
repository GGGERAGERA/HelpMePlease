Shader "Custom/SimpleStylizedToonShaderFixed"
{
    Properties
    {
        // ============================================================
        [Header(Main Layer)]
        // ============================================================
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)

        [KeywordEnum(UV, TriplanarWorld, TriplanarObject)] _MainTexMapping ("Projection", Float) = 0
        [Toggle] _HardTriplanar ("Hard Triplanar", Float) = 0

        [Toggle] _UseVertexColors ("Use Vertex Colors", Float) = 0

        _Brightness ("Brightness", Range(0, 10)) = 1.0
        _Saturation ("Saturation", Range(0, 10)) = 1.0

        [KeywordEnum(Opaque, Transparent, Cutout)] _AlphaMode ("Alpha Mode", Float) = 0
        _Cutoff ("Cutoff Threshold", Range(0, 1)) = 0.5

        // ============================================================
        [Header(Second Layer)]
        // ============================================================
        [Toggle] _UseSecondLayer ("Enable Second Layer", Float) = 0
        _SecondTex ("Texture", 2D) = "white" {}
        [HDR] _SecondColor ("Color", Color) = (1, 1, 1, 1)
        _MaskTex ("Mask (UV)", 2D) = "white" {}
        [KeywordEnum(Mix, Add, Multiply)] _SecondLayerBlend ("Blend Mode", Float) = 0
        [Toggle] _SecondLayerAsEmission ("Use as Emission", Float) = 0

        // ============================================================
        [Header(Shadow)]
        // ============================================================
        [Toggle] _DisableShadows ("Disable Shadows", Float) = 0
        [KeywordEnum(Smooth, Crisp, PaintCrisp)] _ShadowType ("Shadow Type", Float) = 0
        _ShadowSteps ("Crisp Steps", Range(1, 5)) = 2
        _ShadowThreshold ("Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Smoothness", Range(0, 1)) = 0.01
        [HDR] _ShadowColor ("Shadow Color", Color) = (0.2, 0.2, 0.3, 1)

        _NoiseScale ("Noise Scale (Paint)", Range(0.1, 100)) = 5.0
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.5

        // ============================================================
        [Header(Specular)]
        // ============================================================
        [Toggle] _UseSpecular ("Enable Specular", Float) = 1
        [KeywordEnum(Smooth, Crisp, PaintCrisp)] _SpecularType ("Specular Type", Float) = 0
        [HDR] _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularPower ("Power (Spot Size)", Range(0.01, 100)) = 50
        _SpecularIntensity ("Intensity", Range(0, 2)) = 0.5
        _SpecularThreshold ("Threshold (Crisp)", Range(0, 1)) = 0.8
        _SpecularSmoothness ("Smoothness", Range(0, 1)) = 0.1
        _SpecularNoiseScale ("Noise Scale", Range(0.1, 20)) = 5.0
        _SpecularNoiseStrength ("Noise Strength", Range(0, 1)) = 0.5

        // ============================================================
        [Header(Rim Light)]
        // ============================================================
        [Toggle] _UseRimLight ("Enable Rim Light", Float) = 0
        [HDR] _RimColor ("Color", Color) = (1, 1, 1, 1)
        _RimWidth ("Width", Range(0.1, 20)) = 3.0
        _RimIntensity ("Intensity", Range(0, 2)) = 1.0
        _RimSmoothness ("Smoothness", Range(0, 1)) = 0.1

        // ============================================================
        [Header(Emission)]
        // ============================================================
        [Toggle] _UseEmission ("Enable Emission", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)

        // ============================================================
        [Header(Outline)]
        // ============================================================
        [Toggle] _UseOutline ("Enable Outline", Float) = 1
        [HDR] _OutlineColor ("Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Width", Range(0.001, 1.0)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        // ------------------------------------------------------------
        // FORWARD LIT PASS
        // ------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _MAINTEXMAPPING_UV _MAINTEXMAPPING_TRIPLANARWORLD _MAINTEXMAPPING_TRIPLANAROBJECT
            #pragma shader_feature_local _HARDTRIPLANAR_ON
            #pragma shader_feature_local _USEVERTEXCOLORS_ON
            #pragma shader_feature_local _ALPHAMODE_OPAQUE _ALPHAMODE_TRANSPARENT _ALPHAMODE_CUTOUT
            #pragma shader_feature_local _USESECONDLAYER_ON
            #pragma shader_feature_local _SECONDLAYERBLEND_MIX _SECONDLAYERBLEND_ADD _SECONDLAYERBLEND_MULTIPLY
            #pragma shader_feature_local _SECONDLAYERASEMISSION_ON
            #pragma shader_feature_local _SHADOWTYPE_SMOOTH _SHADOWTYPE_CRISP _SHADOWTYPE_PAINTCRISP
            #pragma shader_feature_local _USESPECULAR_ON
            #pragma shader_feature_local _SPECULARTYPE_SMOOTH _SPECULARTYPE_CRISP _SPECULARTYPE_PAINTCRISP
            #pragma shader_feature_local _USERIMLIGHT_ON
            #pragma shader_feature_local _USEEMISSION_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SecondTex);          SAMPLER(sampler_SecondTex);
            TEXTURE2D(_MaskTex);            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _HardTriplanar;
                half _UseVertexColors;
                half _Brightness;
                half _Saturation;
                half _Cutoff;

                half _UseSecondLayer;
                float4 _SecondTex_ST;        // обязательно для TRANSFORM_TEX
                float4 _MaskTex_ST;          // обязательно
                half4 _SecondColor;
                half _SecondLayerAsEmission;

                half _DisableShadows;
                half _ShadowSteps;
                half _ShadowThreshold;
                half _ShadowSmoothness;
                half4 _ShadowColor;
                float _NoiseScale;
                half _NoiseStrength;

                half _UseSpecular;
                half4 _SpecularColor;
                half _SpecularPower;
                half _SpecularIntensity;
                half _SpecularThreshold;
                half _SpecularSmoothness;
                float _SpecularNoiseScale;
                half _SpecularNoiseStrength;

                half _UseRimLight;
                half4 _RimColor;
                half _RimWidth;
                half _RimIntensity;
                half _RimSmoothness;

                half _UseEmission;
                float4 _EmissionMap_ST;      // обязательно
                half4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 secondUV   : TEXCOORD1;
                float2 maskUV     : TEXCOORD2;
                float2 emissionUV : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float3 normalWS   : TEXCOORD5;
                float4 shadowCoord: TEXCOORD6;
                float3 viewDirWS  : TEXCOORD7;
                float3 positionOS : TEXCOORD8;
                float3 normalOS   : TEXCOORD9;
                half4 vertexColor : COLOR0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS = normalInput.normalWS;
                OUT.normalOS = IN.normalOS;

                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                #if defined(_USESECONDLAYER_ON)
                    OUT.secondUV = TRANSFORM_TEX(IN.uv, _SecondTex);
                    OUT.maskUV   = TRANSFORM_TEX(IN.uv, _MaskTex);
                #else
                    OUT.secondUV = 0;
                    OUT.maskUV   = 0;
                #endif

                #if defined(_USEEMISSION_ON)
                    OUT.emissionUV = TRANSFORM_TEX(IN.uv, _EmissionMap);
                #else
                    OUT.emissionUV = 0;
                #endif

                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                OUT.vertexColor = IN.color;
                return OUT;
            }

            float SimpleNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n = i.x + i.y * 57.0 + i.z * 113.0;
                return lerp(
                    lerp(
                        lerp(frac(sin(n) * 43758.5453), frac(sin(n + 1.0) * 43758.5453), f.x),
                        lerp(frac(sin(n + 57.0) * 43758.5453), frac(sin(n + 58.0) * 43758.5453), f.x),
                        f.y
                    ),
                    lerp(
                        lerp(frac(sin(n + 113.0) * 43758.5453), frac(sin(n + 114.0) * 43758.5453), f.x),
                        lerp(frac(sin(n + 170.0) * 43758.5453), frac(sin(n + 171.0) * 43758.5453), f.x),
                        f.y
                    ),
                    f.z
                );
            }

            half4 SampleTriplanar(TEXTURE2D_PARAM(tex, samplerTex), float3 pos, float3 normal, float4 st, half hardEdge)
            {
                float3 absNormal = abs(normal);
                float3 weights;
                if (hardEdge > 0.5)
                {
                    float maxComp = max(absNormal.x, max(absNormal.y, absNormal.z));
                    weights = step(maxComp - 0.001, absNormal);
                    weights /= (weights.x + weights.y + weights.z);
                }
                else
                {
                    weights = absNormal;
                    weights /= (weights.x + weights.y + weights.z);
                }
                float2 uvX = pos.zy * st.xy + st.zw;
                float2 uvY = pos.xz * st.xy + st.zw;
                float2 uvZ = pos.xy * st.xy + st.zw;
                half4 colX = SAMPLE_TEXTURE2D(tex, samplerTex, uvX);
                half4 colY = SAMPLE_TEXTURE2D(tex, samplerTex, uvY);
                half4 colZ = SAMPLE_TEXTURE2D(tex, samplerTex, uvZ);
                return colX * weights.x + colY * weights.y + colZ * weights.z;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex;
                #if defined(_MAINTEXMAPPING_TRIPLANARWORLD)
                    tex = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), IN.positionWS, IN.normalWS, _BaseMap_ST, _HardTriplanar);
                #elif defined(_MAINTEXMAPPING_TRIPLANAROBJECT)
                    tex = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), IN.positionOS, IN.normalOS, _BaseMap_ST, _HardTriplanar);
                #else
                    tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                #endif

                half3 albedo = tex.rgb * _BaseColor.rgb;
                half alpha = tex.a * _BaseColor.a;

                #if defined(_USEVERTEXCOLORS_ON)
                    albedo *= IN.vertexColor.rgb;
                    alpha *= IN.vertexColor.a;
                #endif

                half3 secondEmission = 0;
                #if defined(_USESECONDLAYER_ON)
                    half4 secondTex = SAMPLE_TEXTURE2D(_SecondTex, sampler_SecondTex, IN.secondUV);
                    half3 secondLayer = secondTex.rgb * _SecondColor.rgb;
                    half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.maskUV).r;

                    #if defined(_SECONDLAYERASEMISSION_ON)
                        secondEmission = secondLayer * mask;
                    #else
                        #if defined(_SECONDLAYERBLEND_ADD)
                            albedo += secondLayer * mask;
                        #elif defined(_SECONDLAYERBLEND_MULTIPLY)
                            albedo *= lerp(half3(1,1,1), secondLayer, mask);
                        #else
                            albedo = lerp(albedo, secondLayer, mask * secondTex.a);
                        #endif
                        alpha = max(alpha, secondTex.a * mask);
                    #endif
                #endif

                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 lightDir = mainLight.direction;
                float3 lightColor = mainLight.color;
                float shadowAtten = mainLight.shadowAttenuation;

                half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
                ambient = max(ambient, 0.1);

                half NdotL = dot(IN.normalWS, lightDir) * 0.5 + 0.5;
                half cel;

                if (_DisableShadows > 0.5)
                {
                    cel = 1.0;
                }
                else
                {
                    #if defined(_SHADOWTYPE_SMOOTH)
                        cel = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);
                    #elif defined(_SHADOWTYPE_CRISP)
                        half steps = max(2, _ShadowSteps);
                        half stepSize = 1.0 / (steps - 1.0);
                        half stepped = floor(NdotL / stepSize + 0.5) * stepSize;
                        stepped = clamp(stepped, 0.0, 1.0);
                        cel = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, stepped);
                    #elif defined(_SHADOWTYPE_PAINTCRISP)
                        float3 noisePos = IN.positionWS * _NoiseScale;
                        half noise = SimpleNoise(noisePos);
                        half noisyThreshold = _ShadowThreshold + (noise - 0.5) * _NoiseStrength;
                        cel = smoothstep(noisyThreshold - _ShadowSmoothness, noisyThreshold + _ShadowSmoothness, NdotL);
                    #endif
                }

                half3 lighting = lerp(_ShadowColor.rgb, half3(1,1,1), cel);
                lighting = lighting * lightColor * shadowAtten + ambient;
                half3 finalColor = albedo * lighting;

                #if defined(_USESPECULAR_ON)
                    float3 viewDir = normalize(IN.viewDirWS);
                    float3 reflectVec = reflect(-lightDir, IN.normalWS);
                    half specBase = pow(max(dot(viewDir, reflectVec), 0.0), _SpecularPower);
                    
                    #if defined(_SPECULARTYPE_SMOOTH)
                        half specSmooth = _SpecularSmoothness * _SpecularSmoothness;
                        specBase = smoothstep(0.5 - specSmooth, 0.5 + specSmooth, specBase);
                    #elif defined(_SPECULARTYPE_CRISP)
                        specBase = step(_SpecularThreshold, specBase);
                    #elif defined(_SPECULARTYPE_PAINTCRISP)
                        float3 specNoisePos = IN.positionWS * _SpecularNoiseScale;
                        half specNoise = SimpleNoise(specNoisePos);
                        half specThresh = _SpecularThreshold + (specNoise - 0.5) * _SpecularNoiseStrength;
                        specBase = step(specThresh, specBase);
                    #endif
                    
                    half specValue = clamp(specBase, 0, 1) * _SpecularIntensity * shadowAtten;
                    finalColor += specValue * _SpecularColor.rgb * lightColor;
                #endif

                #if defined(_USERIMLIGHT_ON)
                    float3 viewDirNorm = normalize(IN.viewDirWS);
                    half rim = 1.0 - saturate(dot(viewDirNorm, IN.normalWS));
                    rim = pow(rim, _RimWidth);
                    rim = smoothstep(0.0, _RimSmoothness, rim) * _RimIntensity;
                    finalColor += rim * _RimColor.rgb;
                #endif

                #if defined(_USEEMISSION_ON)
                    half4 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.emissionUV);
                    half3 emission = emissionTex.rgb * _EmissionColor.rgb;
                    finalColor += emission;
                #endif

                finalColor += secondEmission;

                finalColor = finalColor * _Brightness;
                half gray = dot(finalColor, half3(0.299, 0.587, 0.114));
                finalColor = lerp(gray, finalColor, _Saturation);

                #if defined(_ALPHAMODE_TRANSPARENT)
                    // alpha уже задана
                #elif defined(_ALPHAMODE_CUTOUT)
                    clip(alpha - _Cutoff);
                    alpha = 1.0;
                #else
                    alpha = 1.0;
                #endif

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // OUTLINE PASS
        // ------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _USEOUTLINE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _UseOutline;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                #if defined(_USEOUTLINE_ON)
                    float3 extrudedPos = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                    OUT.positionCS = TransformObjectToHClip(extrudedPos);
                    OUT.color = _OutlineColor;
                #else
                    OUT.positionCS = float4(0,0,0,1);
                    OUT.color = 0;
                #endif
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                #if defined(_USEOUTLINE_ON)
                    return IN.color;
                #else
                    discard;
                    return 0;
                #endif
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // SHADOW CASTER PASS
        // ------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                float bias = 0.001;
                positionWS += normalWS * bias;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // DEPTH ONLY PASS
        // ------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // DEPTH NORMALS PASS
        // ------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DepthNormalsFrag(Varyings IN) : SV_Target
            {
                return half4(normalize(IN.normalWS), 0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}