Shader "UI/World Rule Overlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _HasteColor ("Haste Color", Color) = (0.08, 0.72, 0.95, 1)
        _RegenerationColor ("Regeneration Color", Color) = (0.15, 0.82, 0.38, 1)
        _ExplosiveColor ("Explosive Color", Color) = (1, 0.16, 0.05, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _RuleType ("Rule Type", Float) = 0
        _VisualTime ("Unscaled Visual Time", Float) = 0
        _PulseSpeed ("Pulse Speed", Range(0, 3)) = 1
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.3
        _EdgeIntensity ("Edge Intensity", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _HasteColor;
            fixed4 _RegenerationColor;
            fixed4 _ExplosiveColor;
            float _Intensity;
            float _RuleType;
            float _VisualTime;
            float _PulseSpeed;
            float _PulseStrength;
            float _EdgeIntensity;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color;
                return output;
            }

            float EdgeMask(float2 uv)
            {
                float2 edgeDistance = min(uv, 1.0 - uv);
                float nearestEdge = min(edgeDistance.x, edgeDistance.y);
                return 1.0 - smoothstep(0.0, 0.3, nearestEdge);
            }

            float Hash(float2 value)
            {
                return frac(
                    sin(dot(value, float2(12.9898, 78.233))) *
                    43758.5453
                );
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;
                float2 centered = uv - 0.5;
                float edge = EdgeMask(uv);
                float time = _VisualTime * _PulseSpeed;
                fixed3 color;
                float alpha;

                if (_RuleType > 1.5)
                {
                    float radius = length(centered);
                    float ring = 1.0 - smoothstep(
                        0.0,
                        0.035,
                        abs(frac(radius * 4.0 - time * 0.18) - 0.5)
                    );
                    float breathe = 1.0 - _PulseStrength +
                        _PulseStrength * sin(time * 1.7);
                    color = _RegenerationColor.rgb;
                    alpha = edge * _EdgeIntensity *
                        (0.12 + ring * 0.055 * breathe);
                }
                else if (_RuleType > 0.5)
                {
                    const float rowCount = 13.0;
                    const float horizontalCells = 2.5;
                    float row = floor(uv.y * rowCount);
                    float movingX =
                        uv.x * horizontalCells - time * 0.55;
                    float cell = floor(movingX);
                    float localX = frac(movingX);
                    float randomLength = Hash(float2(cell, row));
                    float segmentLength =
                        lerp(0.12, 0.36, randomLength);
                    float segmentEnabled = step(
                        0.68,
                        Hash(float2(cell + 17.0, row + 31.0))
                    );
                    float verticalJitter =
                        lerp(
                            0.22,
                            0.78,
                            Hash(float2(cell + 47.0, row + 7.0))
                        );
                    float lineY =
                        (row + verticalJitter) / rowCount;
                    float horizontalShape =
                        smoothstep(0.0, 0.035, localX) *
                        (1.0 - smoothstep(
                            segmentLength,
                            segmentLength + 0.045,
                            localX
                        ));
                    float verticalShape =
                        1.0 - smoothstep(
                            0.002,
                            0.0065,
                            abs(uv.y - lineY)
                        );
                    float glowShape =
                        1.0 - smoothstep(
                            0.0065,
                            0.018,
                            abs(uv.y - lineY)
                        );
                    float streakCore =
                        horizontalShape *
                        verticalShape *
                        segmentEnabled;
                    float streakGlow =
                        horizontalShape *
                        glowShape *
                        segmentEnabled;
                    float flow = 1.0 - _PulseStrength +
                        _PulseStrength *
                        sin(uv.y * 14.0);
                    color = _HasteColor.rgb;
                    alpha = edge * _EdgeIntensity *
                        (
                            0.055 +
                            streakCore * 0.15 +
                            streakGlow * 0.065
                        ) * flow;
                }
                else
                {
                    float pulse = 1.0 - _PulseStrength +
                        _PulseStrength * sin(time * 3.2);
                    float cells = step(
                        0.91,
                        Hash(floor(uv * float2(24.0, 14.0)))
                    );
                    float flicker = step(
                        0.54,
                        Hash(
                            floor(uv * float2(24.0, 14.0)) +
                            floor(time * 3.0)
                        )
                    );
                    color = _ExplosiveColor.rgb;
                    alpha = edge * _EdgeIntensity *
                        (0.14 * pulse + cells * flicker * 0.08);
                }

                alpha *= _Intensity * input.color.a;
                return fixed4(color * input.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
