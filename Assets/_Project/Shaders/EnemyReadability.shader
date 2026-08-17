Shader "Hidden/Subject42/EnemyReadability"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _ReadabilitySaturation ("Saturation", Float) = 1
        _ReadabilityBrightness ("Brightness", Float) = 1
        _ReadabilityTint ("Readability Tint", Color) = (0.05,1,1,1)
        _ReadabilityTintStrength ("Tint Strength", Range(0,1)) = 0
        _ReadabilityHueShift ("Hue Shift", Range(-180,180)) = 0
        _ReadabilityRecolorTarget ("Recolor Target", Color) = (0,1,1,1)
        _ReadabilityRecolorStrength ("Recolor Strength", Range(0,1)) = 0
        _ReadabilityOutlineColor ("Outline Color", Color) = (0.025,0.055,0.09,1)
        _ReadabilityOutlineStrength ("Outline Strength", Range(0,2)) = 0
        _ReadabilityOutlineWidth ("Outline Width", Range(0.5,4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFrag
            #pragma target 2.0
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _AlphaTex;
            fixed _EnableExternalAlpha;
            fixed4 _Color;
            float _ReadabilitySaturation;
            float _ReadabilityBrightness;
            fixed4 _ReadabilityTint;
            float _ReadabilityTintStrength;
            float _ReadabilityHueShift;
            fixed4 _ReadabilityRecolorTarget;
            float _ReadabilityRecolorStrength;
            fixed4 _ReadabilityOutlineColor;
            float _ReadabilityOutlineStrength;
            float _ReadabilityOutlineWidth;

            v2f SpriteVert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;

                #ifdef PIXELSNAP_ON
                output.vertex = UnityPixelSnap(output.vertex);
                #endif

                return output;
            }

            fixed4 SampleSprite(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);

                #if ETC1_EXTERNAL_ALPHA
                fixed4 alpha = tex2D(_AlphaTex, uv);
                color.a = lerp(color.a, alpha.r, _EnableExternalAlpha);
                #endif

                return color;
            }

            fixed3 RotateHuePreservingLuminance(fixed3 color, float degrees)
            {
                float angle = radians(degrees);
                float cosine = cos(angle);
                float sine = sin(angle);
                float y = dot(color, fixed3(0.299, 0.587, 0.114));
                float i = dot(color, fixed3(0.596, -0.274, -0.322));
                float q = dot(color, fixed3(0.211, -0.523, 0.312));
                float rotatedI = i * cosine - q * sine;
                float rotatedQ = i * sine + q * cosine;

                return fixed3(
                    y + 0.956 * rotatedI + 0.621 * rotatedQ,
                    y - 0.272 * rotatedI - 0.647 * rotatedQ,
                    y - 1.106 * rotatedI + 1.703 * rotatedQ
                );
            }

            fixed3 RecolorPreservingLuminance(fixed3 color, fixed3 target)
            {
                fixed luminance = dot(color, fixed3(0.299, 0.587, 0.114));
                fixed targetLuminance = max(
                    dot(target, fixed3(0.299, 0.587, 0.114)),
                    0.001
                );
                fixed3 recolored = saturate(
                    target * (luminance / targetLuminance)
                );
                fixed clippedLuminance = dot(
                    recolored,
                    fixed3(0.299, 0.587, 0.114)
                );

                return saturate(recolored + (luminance - clippedLuminance));
            }

            fixed4 SpriteFrag(v2f input) : SV_Target
            {
                fixed4 source = SampleSprite(input.texcoord);
                fixed outlineAlpha = 0;

                if (_ReadabilityOutlineStrength > 0.001)
                {
                    float2 texel = _MainTex_TexelSize.xy *
                        max(0.0, _ReadabilityOutlineWidth);
                    fixed neighbourAlpha = max(
                        max(
                            SampleSprite(input.texcoord + float2(texel.x, 0)).a,
                            SampleSprite(input.texcoord - float2(texel.x, 0)).a
                        ),
                        max(
                            SampleSprite(input.texcoord + float2(0, texel.y)).a,
                            SampleSprite(input.texcoord - float2(0, texel.y)).a
                        )
                    );
                    outlineAlpha = neighbourAlpha *
                        (1.0 - source.a) *
                        saturate(_ReadabilityOutlineStrength * 0.65) *
                        input.color.a *
                        _ReadabilityOutlineColor.a;
                }

                if (source.a <= 0.001 && outlineAlpha > 0.001)
                {
                    return fixed4(
                        _ReadabilityOutlineColor.rgb,
                        outlineAlpha
                    );
                }

                fixed luminance = dot(
                    source.rgb,
                    fixed3(0.299, 0.587, 0.114)
                );
                fixed3 readable = lerp(
                    luminance.xxx,
                    source.rgb,
                    max(0.0, _ReadabilitySaturation)
                );
                readable *= max(0.0, _ReadabilityBrightness);
                fixed peak = max(
                    0.5,
                    max(readable.r, max(readable.g, readable.b))
                );
                fixed3 obviousTint = _ReadabilityTint.rgb * peak;
                readable = lerp(
                    readable,
                    obviousTint,
                    saturate(_ReadabilityTintStrength)
                );
                readable = saturate(RotateHuePreservingLuminance(
                    readable,
                    _ReadabilityHueShift
                ));
                fixed3 recolored = RecolorPreservingLuminance(
                    readable,
                    saturate(_ReadabilityRecolorTarget.rgb)
                );
                readable = lerp(
                    readable,
                    recolored,
                    saturate(_ReadabilityRecolorStrength)
                );

                return fixed4(
                    saturate(readable) * input.color.rgb,
                    source.a * input.color.a
                );
            }
            ENDCG
        }
    }
}
