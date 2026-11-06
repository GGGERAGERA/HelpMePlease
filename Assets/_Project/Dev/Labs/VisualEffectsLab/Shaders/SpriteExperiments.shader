Shader "Subject42/Dev/SpriteExperiments"
{
    Properties
    {
        _MainTex ("Sprite texture", 2D) = "white" {}
        _SpriteRect ("Atlas rectangle (normalized XYWH)", Vector) = (0,0,1,1)
        _SpritePadding ("Transparent margin on each side", Range(0,0.25)) = 0.1
        _Color ("Tint", Color) = (1,1,1,1)
        [IntRange] _Mode ("Experiment: base 0-7 / shell, scan, infection, assembly, phase, armour 8-13", Range(0,13)) = 0
        _LabTime ("Preview time", Float) = 0
        _Intensity ("Intensity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _SpriteRect;
            float4 _Color;
            float _SpritePadding, _Mode, _LabTime, _Intensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = (v.uv - 0.5) * (1.0 + 2.0 * _SpritePadding) + 0.5;
                return o;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Every lookup is confined to this sprite, including displaced and outline taps.
            // Sampling texel centers also keeps pixel art crisp on bilinear source textures.
            float4 sampleSprite(float2 uv, float2 pixelSize)
            {
                float inside = step(0.0, uv.x) * step(0.0, uv.y)
                    * (1.0 - step(1.0, uv.x)) * (1.0 - step(1.0, uv.y));
                float2 pixel = clamp(floor(uv * pixelSize), 0.0, pixelSize - 1.0);
                float2 atlasUV = _SpriteRect.xy + (pixel + 0.5) * abs(_MainTex_TexelSize.xy);
                return tex2Dlod(_MainTex, float4(atlasUV, 0.0, 0.0)) * inside * _Color;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 size = max(1.0, floor(_SpriteRect.zw / abs(_MainTex_TexelSize.xy) + 0.5));
                float2 texel = 1.0 / size;
                float2 pixel = floor(i.uv * size);
                float t = _LabTime;
                float amount = max(0.0, _Intensity);
                float4 source = sampleSprite(i.uv, size);
                float4 effect = source;

                if (_Mode > 0.5 && _Mode < 1.5)
                {
                    float outline = 0.0;
                    [unroll] for (int x = -1; x <= 1; x++)
                        [unroll] for (int y = -1; y <= 1; y++)
                            outline = max(outline, sampleSprite(i.uv + float2(x, y) * texel, size).a);
                    float pulse = 0.75 + 0.25 * sin(t * 3.0 + i.uv.y * 5.0);
                    float3 rim = lerp(float3(0.1, 0.8, 0.48), float3(0.7, 1.0, 0.45), pulse);
                    effect = float4(lerp(rim, source.rgb, source.a), max(source.a, outline * 0.95));
                }
                else if (_Mode < 2.5 && _Mode > 1.5)
                {
                    float noise = hash(floor(pixel / 2.0));
                    float threshold = 0.48 + 0.40 * sin(t * 1.25);
                    float edge = 1.0 - smoothstep(threshold, threshold + 0.14, noise);
                    effect.rgb = lerp(source.rgb, lerp(float3(1.0, 0.25, 0.03), float3(1.0, 0.95, 0.35), edge), edge);
                    effect.a = source.a * step(threshold, noise);
                }
                else if (_Mode < 3.5 && _Mode > 2.5)
                {
                    float scanline = step(1.0, fmod(pixel.y + floor(t * 9.0), 3.0));
                    float sweep = 1.0 - smoothstep(0.0, 0.08, abs(i.uv.y - frac(t * 0.3)));
                    float luminance = dot(source.rgb, float3(0.25, 0.55, 0.2));
                    effect.rgb = float3(0.12, 0.8, 1.0) * (0.65 + luminance * 0.55 + sweep * 0.8);
                    effect.a = source.a * (0.30 + scanline * 0.45 + sweep * 0.2);
                }
                else if (_Mode < 4.5 && _Mode > 3.5)
                {
                    float interior = min(min(sampleSprite(i.uv + float2(texel.x, 0.0), size).a,
                        sampleSprite(i.uv - float2(texel.x, 0.0), size).a),
                        min(sampleSprite(i.uv + float2(0.0, texel.y), size).a,
                        sampleSprite(i.uv - float2(0.0, texel.y), size).a));
                    float facet = hash(floor(pixel / 3.0));
                    float sparkle = step(0.94, hash(pixel)) * pow(saturate(sin(t * 3.0 + facet * 40.0)), 12.0);
                    effect.rgb = lerp(source.rgb * float3(0.6, 0.85, 1.0), float3(0.38, 0.74, 0.98), 0.62);
                    effect.rgb += (1.0 - interior) * float3(0.35, 0.5, 0.65) + facet * 0.12 + sparkle * 0.8;
                }
                else if (_Mode < 5.5 && _Mode > 4.5)
                {
                    float heat = 0.5 + 0.5 * sin(t * 3.5 + pixel.y * 0.23);
                    float crack = step(0.76, hash(pixel) + heat * 0.13);
                    float ember = step(0.88, hash(floor(pixel / 2.0))) * heat;
                    effect.rgb = source.rgb * float3(1.0, 0.35, 0.16) * 0.65;
                    effect.rgb += float3(1.0, 0.22, 0.02) * (0.23 + 0.25 * heat);
                    effect.rgb = lerp(effect.rgb, float3(1.0, 0.82, 0.16), saturate(crack * 0.85 + ember * 0.4));
                }
                else if (_Mode < 6.5 && _Mode > 5.5)
                {
                    float tick = floor(t * 9.0);
                    float row = floor(pixel.y / 3.0);
                    float stripe = hash(float2(row, tick));
                    float split = min(1.5, _SpritePadding * size.x * 0.3);
                    float travel = min(4.0 * amount, max(0.0, _SpritePadding * size.x - split));
                    float offset = step(0.73, stripe) * (hash(float2(tick, row + 31.0)) - 0.5) * 2.0 * travel;
                    float2 displaced = i.uv + float2(offset * texel.x, 0.0);
                    float4 center = sampleSprite(displaced, size);
                    float4 red = sampleSprite(displaced + float2(texel.x * split, 0.0), size);
                    float4 blue = sampleSprite(displaced - float2(texel.x * split, 0.0), size);
                    effect = float4(red.r, center.g, blue.b, max(center.a, max(red.a, blue.a)));
                    effect.rgb *= 0.8 + 0.2 * step(0.18, stripe);
                }
                else if (_Mode > 6.5 && _Mode < 7.5)
                {
                    float2 margin = min(float2(3.0, 2.0), _SpritePadding * size * 0.45);
                    float2 drift = margin * (0.7 + 0.3 * sin(t * 2.0));
                    float echo = sampleSprite(i.uv - drift * texel, size).a * 0.22;
                    float tail = sampleSprite(i.uv - drift * texel * 2.0, size).a * 0.12;
                    float body = source.a * (0.42 + 0.08 * sin(t * 2.7 + i.uv.y * 5.0));
                    effect.a = saturate(body + echo + tail);
                    effect.rgb = ((source.rgb * 0.3 + float3(0.4, 0.7, 0.9)) * body
                        + float3(0.3, 0.9, 0.95) * echo + float3(0.7, 0.3, 0.95) * tail) / max(effect.a, 0.001);
                }
                else if (_Mode > 7.5 && _Mode < 8.5)
                {
                    float2 p = (i.uv - 0.5) * 2;
                    float radius = length(p * float2(1.05, 0.9));
                    float angle = atan2(p.y, p.x);
                    float rim = saturate(1 - abs(radius - 1.04) / 0.022);
                    float impact = pow(saturate(cos(angle - t * 1.4)), 18);
                    float2 lattice = float2(p.x * 12 + floor(p.y * 12) * 0.5, p.y * 12);
                    float wire = (1 - step(0.06, min(abs(frac(lattice.x) - 0.5), abs(frac(lattice.y) - 0.5))));
                    float shellAlpha = rim * (0.65 + 0.35 * impact) + wire * step(radius, 1.02) * 0.07;
                    float3 shellColor = lerp(float3(0.12,0.55,0.95), float3(1,0.7,0.25), impact);
                    effect.a = source.a + shellAlpha * (1 - source.a);
                    effect.rgb = (source.rgb * source.a + shellColor * shellAlpha * (1 - source.a)) / max(effect.a, 0.001);
                    effect.rgb += shellColor * rim * 0.22;
                }
                else if (_Mode > 8.5 && _Mode < 9.5)
                {
                    float sweep = frac(t * 0.22);
                    float scan = 1 - smoothstep(0.025, 0.075, abs(i.uv.y - sweep));
                    float reveal = 1 - smoothstep(0.0, 0.22, abs(i.uv.y - sweep));
                    float wire = step(0.83, frac(pixel.x / 5)) + step(0.83, frac(pixel.y / 5));
                    float luminance = dot(source.rgb, float3(0.25,0.6,0.15));
                    float contour = max(max(sampleSprite(i.uv + float2(texel.x,0),size).a,
                        sampleSprite(i.uv - float2(texel.x,0),size).a), max(sampleSprite(i.uv + float2(0,texel.y),size).a,
                        sampleSprite(i.uv - float2(0,texel.y),size).a)) - source.a;
                    effect.rgb = lerp(source.rgb * 0.28, float3(0.12,0.65,0.75) * (0.35 + luminance + wire * 0.35), reveal);
                    effect.rgb += float3(0.35,1,0.85) * scan;
                    effect.a = max(source.a, contour * 0.85);
                    effect.rgb = lerp(effect.rgb, float3(0.15,0.8,0.75), contour);
                }
                else if (_Mode > 9.5 && _Mode < 10.5)
                {
                    float growth = 0.3 + 0.65 * (0.5 + 0.5 * sin(t * 0.85));
                    float patch = hash(floor(pixel / 6)) * 0.6 + hash(floor(pixel / 13)) * 0.4;
                    float infected = 1 - smoothstep(growth - 0.08, growth + 0.08, patch);
                    float veins = 1 - smoothstep(0.06, 0.2, abs(sin(pixel.x * 0.58 + sin(pixel.y * 0.32) * 2 + t * 0.35)));
                    float pulse = 0.6 + 0.4 * sin(t * 3 - pixel.y * 0.15);
                    float3 infection = lerp(float3(0.11,0.12,0.17),float3(0.32,0.95,0.12),veins * pulse);
                    infection += step(0.97,hash(pixel)) * float3(0.8,0.45,0.04) * pulse;
                    effect.rgb = lerp(source.rgb, infection, infected * 0.8);
                }
                else if (_Mode > 10.5 && _Mode < 11.5)
                {
                    float progress = 0.12 + 0.88 * (0.5 + 0.5 * sin(t * 0.9));
                    float row = floor(pixel.y / 4);
                    float unbuilt = saturate((i.uv.y - progress) * 6 + 0.35);
                    float drift = (hash(float2(row,7)) - 0.5) * 0.32 * unbuilt;
                    float4 piece = sampleSprite(i.uv + float2(drift,unbuilt * 0.035),size);
                    float built = step(hash(floor(pixel / 4)),progress);
                    float blueprint = source.a * (0.12 + 0.12 * step(0.8,frac(pixel.x / 4)));
                    float frontier = 1 - smoothstep(0.01,0.08,abs(i.uv.y-progress));
                    effect.a = max(piece.a * built,blueprint);
                    effect.rgb = lerp(float3(0.08,0.55,0.7),piece.rgb,built * (1-unbuilt * 0.65));
                    effect.rgb += float3(0.2,0.9,1) * frontier * source.a;
                }
                else if (_Mode > 11.5 && _Mode < 12.5)
                {
                    float fold = 0.25 + 0.75 * pow(0.5 + 0.5 * sin(t * 1.5),0.7);
                    float2 uv = float2(0.5 + (i.uv.x-0.5)/fold,i.uv.y);
                    float4 phased = sampleSprite(uv,size);
                    float split = (1-fold) * 0.025;
                    float4 cyan = sampleSprite(uv+float2(split,0),size);
                    float4 violet = sampleSprite(uv-float2(split,0),size);
                    effect.a = max(phased.a,max(cyan.a,violet.a) * 0.6);
                    effect.rgb = phased.rgb * phased.a + float3(0.12,0.8,1) * max(0,cyan.a-phased.a)
                        + float3(0.8,0.18,0.9) * max(0,violet.a-phased.a);
                    effect.rgb /= max(effect.a,0.001);
                    effect.rgb = lerp(effect.rgb,effect.rgb * float3(0.6,0.7,1)+float3(0.08,0.03,0.14),1-fold);
                }
                else if (_Mode > 12.5)
                {
                    float luminance = dot(source.rgb,float3(0.3,0.55,0.15));
                    float plate = hash(floor(pixel / 7));
                    float facet = 0.45 + luminance * 0.65 + plate * 0.18;
                    float gleam = pow(saturate(1 - abs(i.uv.x+i.uv.y-frac(t*0.22)*2.5) / 0.16),3);
                    float seams = step(0.86,frac(pixel.x / 7)) + step(0.86,frac(pixel.y / 7));
                    effect.rgb = float3(0.52,0.65,0.78) * facet - seams * 0.07;
                    effect.rgb += float3(0.65,0.85,1) * gleam;
                    float eyes = step(source.g * 1.4,source.r) * step(0.45,source.r);
                    effect.rgb = lerp(effect.rgb,source.rgb,eyes);
                    float charge = pow(saturate(sin(t*2-pixel.y*0.09)),5) * step(0.88,plate);
                    effect.rgb += float3(1,0.38,0.04) * charge * 0.45;
                }

                // Mix in premultiplied space, then return straight alpha for the blend state.
                float blend = saturate(amount);
                float alpha = lerp(source.a, effect.a, blend);
                float3 color = lerp(source.rgb * source.a, effect.rgb * effect.a, blend) / max(alpha, 0.001);
                return float4(color, alpha);
            }
            ENDCG
        }
    }
}
