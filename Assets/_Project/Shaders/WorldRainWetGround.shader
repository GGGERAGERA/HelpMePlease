Shader "World/Rain Wet Ground"
{
    Properties
    {
        _WetGroundIntensity ("Wet Ground Intensity", Range(0, 1)) = 0
        _WetPatternScale ("Wet Pattern Scale", Range(0.25, 8)) = 2.8
        _VisualTime ("Unscaled Visual Time", Float) = 0
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

            float _WetGroundIntensity;
            float _WetPatternScale;
            float _VisualTime;

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
                float2 patternUv = input.worldPosition *
                    max(0.25, _WetPatternScale) * 0.12;
                float broad = ValueNoise(patternUv);
                float detail = ValueNoise(patternUv * 3.7 + 12.4);
                float wetMask = smoothstep(
                    0.38,
                    0.76,
                    broad * 0.78 + detail * 0.22
                );

                float2 glintCell = floor(patternUv * 4.5);
                float glintSeed = Hash(glintCell + 31.7);
                float glintPulse = 0.5 + 0.5 * sin(
                    _VisualTime * 0.65 + glintSeed * 18.0
                );
                float glint = step(0.965, glintSeed) *
                    smoothstep(0.78, 1.0, glintPulse) * wetMask;

                fixed3 darkWet = fixed3(0.025, 0.045, 0.055);
                fixed3 softGlint = fixed3(0.48, 0.62, 0.68);
                fixed3 color = lerp(darkWet, softGlint, glint * 0.55);
                float alpha = _WetGroundIntensity *
                    (wetMask * 0.18 + glint * 0.12);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
