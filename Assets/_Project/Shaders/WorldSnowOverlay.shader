Shader "World/Snow Overlay"
{
    Properties
    {
        _SnowColor ("Coverage Color", Color) = (0.82, 0.92, 1, 0.72)
        _SnowIntensity ("Coverage Intensity", Range(0, 1)) = 0
        _SnowDensity ("Patch Density", Range(0.5, 12)) = 3.2
        _SnowScale ("Coverage Scale", Range(0.25, 8)) = 2.4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SnowColor;
            float _SnowIntensity;
            float _SnowDensity;
            float _SnowScale;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 worldPosition : TEXCOORD0;
            };

            float Hash(float2 value)
            {
                return frac(
                    sin(dot(value, float2(127.1, 311.7))) *
                    43758.5453
                );
            }

            float ValueNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 local = frac(position);
                local = local * local * (3.0 - 2.0 * local);

                float bottom = lerp(
                    Hash(cell),
                    Hash(cell + float2(1.0, 0.0)),
                    local.x
                );
                float top = lerp(
                    Hash(cell + float2(0.0, 1.0)),
                    Hash(cell + float2(1.0, 1.0)),
                    local.x
                );
                return lerp(bottom, top, local.y);
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldPosition =
                    mul(unity_ObjectToWorld, input.vertex).xy;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 coverageUv =
                    input.worldPosition *
                    (_SnowDensity / max(0.25, _SnowScale)) *
                    0.085;

                float broadPatches = ValueNoise(coverageUv);
                float mediumVariation = ValueNoise(
                    coverageUv * 2.35 + 17.4
                );
                float fineTexture = ValueNoise(
                    coverageUv * 6.8 - 9.1
                );

                float patchMask = smoothstep(
                    0.28,
                    0.72,
                    broadPatches * 0.72 +
                    mediumVariation * 0.28
                );
                float textureVariation = lerp(
                    0.72,
                    1.0,
                    fineTexture
                );
                float coverage = lerp(
                    0.18,
                    0.82,
                    patchMask
                ) * textureVariation;
                float alpha = coverage *
                    _SnowColor.a *
                    _SnowIntensity;

                fixed3 shadedSnow = _SnowColor.rgb * lerp(
                    0.84,
                    1.04,
                    mediumVariation
                );
                return fixed4(shadedSnow, alpha);
            }
            ENDCG
        }
    }
}
