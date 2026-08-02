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
        _SnowIntensity ("Snow Screen Opacity", Range(0, 0.25)) = 0
        _BlizzardIntensity ("Blizzard Intensity", Range(0, 0.6)) = 0
        _BlizzardLineDensity ("Blizzard Line Density", Range(2, 16)) = 8
        _BlizzardLineSpeed ("Blizzard Line Speed", Range(0.1, 3)) = 1.4
        _BlizzardVeil ("Blizzard Veil", Range(0, 0.4)) = 0
        _RainDropsIntensity ("Rain Drops Intensity", Range(0, 0.5)) = 0
        _RainDropsFrequency ("Rain Drops Frequency", Range(0.05, 2)) = 0.35
        _RainLargeDropsIntensity ("Rain Large Drops Intensity", Range(0, 0.6)) = 0
        _RainLargeDropsCount ("Rain Large Drops Count", Range(4, 8)) = 6
        _RainLargeDropsSpeed ("Rain Large Drops Speed", Range(0.05, 0.5)) = 0.18
        _RainLargeDropsScale ("Rain Large Drops Scale", Range(0.5, 2)) = 1
        _GoldenOverlayIntensity ("Golden Overlay Intensity", Range(0, 0.2)) = 0
        _GoldenOverlayColor ("Golden Overlay Color", Color) = (1, 0.78, 0.32, 1)
        _WindVisualIntensity ("Wind Visual Intensity", Range(0, 0.4)) = 0
        _WindLineDensity ("Wind Line Density", Range(2, 12)) = 5.5
        _WindLineSpeed ("Wind Line Speed", Range(0.05, 2)) = 0.45
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0, 0)
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
            float _SnowIntensity;
            float _BlizzardIntensity;
            float _BlizzardLineDensity;
            float _BlizzardLineSpeed;
            float _BlizzardVeil;
            float _RainDropsIntensity;
            float _RainDropsFrequency;
            float _RainLargeDropsIntensity;
            float _RainLargeDropsCount;
            float _RainLargeDropsSpeed;
            float _RainLargeDropsScale;
            float _GoldenOverlayIntensity;
            fixed4 _GoldenOverlayColor;
            float _WindVisualIntensity;
            float _WindLineDensity;
            float _WindLineSpeed;
            float2 _WindDirection;

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

            float RainDrops(float2 uv)
            {
                const float2 gridSize = float2(9.0, 6.0);
                float2 gridUv = uv * gridSize;
                float2 cell = floor(gridUv);
                float2 local = frac(gridUv);
                float epoch = floor(
                    _VisualTime * max(0.05, _RainDropsFrequency)
                );
                float phase = frac(
                    _VisualTime * max(0.05, _RainDropsFrequency) +
                    Hash(cell + 19.7)
                );
                float enabled = step(
                    0.93,
                    Hash(cell + epoch * float2(17.3, 41.9))
                );
                float dropX = lerp(
                    0.2,
                    0.8,
                    Hash(cell + epoch * 7.1 + 3.4)
                );
                float dropY = 1.15 - phase * 1.3;
                float2 delta = local - float2(dropX, dropY);
                float head = 1.0 - smoothstep(
                    0.025,
                    0.085,
                    length(float2(delta.x * 1.8, delta.y))
                );
                float trail =
                    (1.0 - smoothstep(0.018, 0.045, abs(delta.x))) *
                    smoothstep(0.0, 0.22, delta.y) *
                    (1.0 - smoothstep(0.22, 0.5, delta.y));
                return enabled * saturate(head + trail * 0.32);
            }

            void RainLargeDrops(
                float2 uv,
                out float body,
                out float rim)
            {
                body = 0.0;
                rim = 0.0;
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float index = (float)i;
                    float active = step(index + 0.5, _RainLargeDropsCount);
                    float baseSeed = Hash(float2(index + 11.7, index * 7.3));
                    float individualSpeed = lerp(
                        0.72,
                        1.18,
                        Hash(float2(index + 37.1, index + 5.9))
                    );
                    float travel = _VisualTime *
                        max(0.01, _RainLargeDropsSpeed) *
                        individualSpeed + baseSeed;
                    float cycle = floor(travel);
                    float phase = frac(travel);
                    float dropX = lerp(
                        0.08,
                        0.92,
                        Hash(float2(index * 19.7 + cycle * 3.1, 71.3))
                    );
                    float dropY = 1.08 - phase * 1.16;
                    float sizeVariation = lerp(
                        0.72,
                        1.28,
                        Hash(float2(index + 83.2, cycle + 13.4))
                    );
                    float radius = 0.072 *
                        max(0.2, _RainLargeDropsScale) * sizeVariation;
                    float stretch = lerp(1.08, 1.62, phase);
                    float2 delta = uv - float2(dropX, dropY);
                    delta.x *= aspect;
                    float2 dropSpace = float2(
                        delta.x / radius,
                        delta.y / (radius * stretch)
                    );
                    dropSpace.x *= lerp(
                        0.92,
                        1.08,
                        Hash(float2(index + 29.0, cycle + 47.0))
                    );
                    float distanceToDrop = length(dropSpace);
                    float outer = 1.0 - smoothstep(
                        0.88,
                        1.04,
                        distanceToDrop
                    );
                    float inner = 1.0 - smoothstep(
                        0.56,
                        0.9,
                        distanceToDrop
                    );
                    float edge = saturate(outer - inner * 0.72);
                    float2 highlightOffset =
                        dropSpace - float2(-0.28, 0.25);
                    float highlight = 1.0 - smoothstep(
                        0.08,
                        0.32,
                        length(highlightOffset)
                    );
                    float verticalFlow = 1.0 - smoothstep(
                        0.0,
                        0.92,
                        abs(dropSpace.x)
                    );
                    body = max(
                        body,
                        active * inner *
                            lerp(0.68, 1.0, verticalFlow)
                    );
                    rim = max(
                        rim,
                        active * saturate(edge + highlight * 0.62)
                    );
                }
            }

            float WindLines(float2 uv)
            {
                float directionLength = length(_WindDirection);

                if (directionLength < 0.001)
                    return 0.0;

                float2 direction = _WindDirection / directionLength;
                float2 perpendicular = float2(-direction.y, direction.x);
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 screenUv = float2(uv.x * aspect, uv.y);
                float along = dot(screenUv, direction);
                float across = dot(screenUv, perpendicular);
                float row = floor(across * _WindLineDensity + 37.0);
                float rowUv = frac(across * _WindLineDensity + 37.0);
                float movingAlong =
                    along * 3.2 - _VisualTime * _WindLineSpeed;
                float cell = floor(movingAlong);
                float localAlong = frac(movingAlong);
                float seed = Hash(float2(cell, row));
                float enabled = step(0.88, seed);
                float lineLength = lerp(
                    0.12,
                    0.3,
                    Hash(float2(cell + 13.0, row + 29.0))
                );
                float lineStart = lerp(
                    0.08,
                    0.55,
                    Hash(float2(cell + 41.0, row + 7.0))
                );
                float alongShape =
                    smoothstep(lineStart, lineStart + 0.035, localAlong) *
                    (1.0 - smoothstep(
                        lineStart + lineLength,
                        lineStart + lineLength + 0.05,
                        localAlong
                    ));
                float rowOffset = lerp(
                    0.28,
                    0.72,
                    Hash(float2(cell + 71.0, row + 17.0))
                );
                float crossDistance = abs(rowUv - rowOffset);
                float thinCore = 1.0 - smoothstep(0.012, 0.035, crossDistance);
                float softEdge = 1.0 - smoothstep(0.035, 0.09, crossDistance);
                return enabled * alongShape *
                    (thinCore * 0.7 + softEdge * 0.3);
            }

            float BlizzardLines(float2 uv)
            {
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 screenUv = float2(uv.x * aspect, uv.y);
                float2 direction = normalize(float2(1.0, -0.58));
                float2 perpendicular = float2(-direction.y, direction.x);
                float along = dot(screenUv, direction);
                float across = dot(screenUv, perpendicular);
                float rowPosition = across * _BlizzardLineDensity + 53.0;
                float row = floor(rowPosition);
                float rowUv = frac(rowPosition);
                float movingAlong =
                    along * _BlizzardLineDensity * 0.72 -
                    _VisualTime * _BlizzardLineSpeed;
                float cell = floor(movingAlong);
                float localAlong = frac(movingAlong);
                float seed = Hash(float2(cell + 17.0, row + 31.0));
                float enabled = step(0.28, seed);
                float lineStart = lerp(
                    0.04,
                    0.42,
                    Hash(float2(cell + 47.0, row + 11.0))
                );
                float lineLength = lerp(
                    0.18,
                    0.42,
                    Hash(float2(cell + 7.0, row + 73.0))
                );
                float alongShape =
                    smoothstep(lineStart, lineStart + 0.025, localAlong) *
                    (1.0 - smoothstep(
                        lineStart + lineLength,
                        lineStart + lineLength + 0.045,
                        localAlong
                    ));
                float rowOffset = lerp(
                    0.18,
                    0.82,
                    Hash(float2(cell + 89.0, row + 5.0))
                );
                float crossDistance = abs(rowUv - rowOffset);
                float lineCore = 1.0 - smoothstep(
                    0.018,
                    0.055,
                    crossDistance
                );
                return enabled * alongShape * lineCore;
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
                float ruleAlpha = alpha;

                fixed3 snowVeil = fixed3(0.82, 0.91, 1.0);
                float snowEdge = EdgeMask(uv) * 0.35 + 0.65;
                float snowAlpha = _SnowIntensity * snowEdge *
                    input.color.a;
                float blizzardLineAlpha = BlizzardLines(uv) *
                    _BlizzardIntensity * input.color.a;
                float farSnow = smoothstep(
                    0.08,
                    0.68,
                    length(centered * float2(1.12, 0.9))
                );
                float blizzardVeilAlpha = _BlizzardVeil *
                    lerp(0.64, 1.0, farSnow) * input.color.a;
                float rainAlpha = RainDrops(uv) *
                    _RainDropsIntensity * input.color.a;
                float largeDropBody;
                float largeDropRim;
                RainLargeDrops(uv, largeDropBody, largeDropRim);
                float largeDropBodyAlpha = largeDropBody *
                    _RainLargeDropsIntensity * 0.46 * input.color.a;
                float largeDropRimAlpha = largeDropRim *
                    _RainLargeDropsIntensity * 0.78 * input.color.a;
                float goldenAlpha = _GoldenOverlayIntensity *
                    input.color.a;
                float windAlpha = WindLines(uv) *
                    _WindVisualIntensity * input.color.a;
                fixed3 rainColor = fixed3(0.62, 0.76, 0.86);
                fixed3 windColor = fixed3(0.72, 0.9, 1.0);
                alpha = saturate(
                    ruleAlpha + snowAlpha + rainAlpha + goldenAlpha +
                    windAlpha + blizzardLineAlpha + blizzardVeilAlpha +
                    largeDropBodyAlpha + largeDropRimAlpha
                );
                color = alpha > 0.0001
                    ? (
                        color * ruleAlpha +
                        snowVeil * snowAlpha +
                        rainColor * rainAlpha +
                        _GoldenOverlayColor.rgb * goldenAlpha +
                        windColor * windAlpha +
                        fixed3(0.9, 0.96, 1.0) * blizzardLineAlpha +
                        fixed3(0.48, 0.57, 0.64) * blizzardVeilAlpha +
                        fixed3(0.34, 0.48, 0.58) * largeDropBodyAlpha +
                        fixed3(0.8, 0.92, 0.98) * largeDropRimAlpha
                    ) / alpha
                    : rainColor;
                return fixed4(color * input.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
