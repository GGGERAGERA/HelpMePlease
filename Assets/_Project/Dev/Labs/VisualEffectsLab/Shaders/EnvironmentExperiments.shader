Shader "Subject42/Dev/EnvironmentExperiments"
{
    Properties
    {
        _FloorTex ("Project floor atlas", 2D) = "white" {}
        _FloorRect ("Floor atlas rectangle XYWH", Vector) = (0,0,1,1)
        _FloorTint ("Authored floor tint", Color) = (0.34,0.39,0.61,1)
        _ActorTex ("Project actor atlas", 2D) = "white" {}
        _ActorRect ("Actor atlas rectangle XYWH", Vector) = (0,0,1,1)
        [IntRange] _Mode ("Rift, impulse, stasis, gravity, links, suppression, bio film, wet floor", Range(0,7)) = 0
        _LabTime ("Preview time", Float) = 0
        _Intensity ("Intensity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _FloorTex, _ActorTex;
            float4 _FloorTex_TexelSize, _ActorTex_TexelSize, _FloorRect, _ActorRect, _FloorTint;
            float _Mode, _LabTime, _Intensity;

            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float band(float value, float center, float width) { return saturate(1 - abs(value - center) / width); }

            float4 atlas(sampler2D tex, float2 uv, float4 rect, float2 texel)
            {
                float2 size = max(1, floor(rect.zw / abs(texel) + 0.5));
                float2 pixel = clamp(floor(uv * size), 0, size - 1);
                return tex2Dlod(tex, float4(rect.xy + (pixel + 0.5) * abs(texel), 0, 0));
            }
            float seam(float2 p)
            {
                float2 cell = frac((p * 0.5 + 0.5) * 8);
                return 1 - smoothstep(0.012, 0.055, min(min(cell.x, 1-cell.x), min(cell.y, 1-cell.y)));
            }
            float4 actorAt(float2 p, float2 center)
            {
                float2 uv = (p - center) / float2(0.38,0.44) + 0.5;
                float inside = step(0,uv.x) * step(0,uv.y) * (1-step(1,uv.x)) * (1-step(1,uv.y));
                float4 sprite = atlas(_ActorTex, uv, _ActorRect, _ActorTex_TexelSize.xy);
                sprite.a *= inside; return sprite;
            }
            float3 actor(float3 floorColor, float2 p, float2 center)
            {
                float4 sprite = actorAt(p,center);
                return lerp(floorColor, sprite.rgb, sprite.a);
            }
            float2 segment(float2 p, float2 a, float2 b)
            {
                float along = saturate(dot(p-a,b-a) / dot(b-a,b-a));
                return float2(length(p-lerp(a,b,along)),along);
            }
            float actorMask(float2 p)
            {
                return max(actorAt(p,float2(-0.34,-0.28)).a,actorAt(p,float2(0.36,0.32)).a);
            }
            float3 stage(float2 p)
            {
                float2 uv = p * 0.5 + 0.5;
                float2 cell = frac(uv * 8);
                float3 floorColor = atlas(_FloorTex, cell, _FloorRect, _FloorTex_TexelSize.xy).rgb * _FloorTint.rgb;
                floorColor *= 0.9 - seam(p) * 0.48;
                float bolt = (1 - step(0.035,length(cell-float2(0.13,0.13))))
                    + (1 - step(0.035,length(cell-float2(0.87,0.87))));
                floorColor += bolt * 0.1;
                // Fixed floor markings make local bending visible without a camera-texture dependency.
                float markings = (1-step(0.018,abs(abs(p.x)-0.78))) * step(0.34,frac(p.y*9));
                floorColor = lerp(floorColor, float3(0.55,0.45,0.14), markings * 0.8);
                floorColor *= 1 - 0.22 * saturate(length(p));
                floorColor = actor(floorColor,p,float2(-0.34,-0.28));
                return actor(floorColor,p,float2(0.36,0.32));
            }
            float4 frag(v2f i) : SV_Target
            {
                // Two world-space panels, point samples and a fixed pixel grid, matching the game's sprites.
                float2 p = ((floor(i.uv*256)+0.5)/256) * 2-1;
                float t = _LabTime;
                float amount = saturate(_Intensity);
                float3 color;
                if (_Mode < 0.5)
                {
                    float center = 0.025*sin(p.y*9+t*1.8) + 0.018*sin(p.y*23-t*3);
                    float2 delta = float2(p.x-center,p.y);
                    float distance = length(delta * float2(1.55,0.85));
                    float influence = pow(saturate(1-distance/1.1),2);
                    float2 radial = delta / max(length(delta),0.03);
                    float2 tangent = float2(-radial.y,radial.x);
                    float2 offset = (radial*(0.16+0.045*sin(distance*19-t*3))
                        + tangent*0.11*sin(t*1.4+distance*7)) * influence * amount;
                    float2 warped = p + offset;
                    float split = influence * 0.014 * amount;
                    color = stage(warped);
                    color.r = stage(warped+float2(split,0)).r;
                    color.b = stage(warped-float2(split,0)).b;
                    float taper = 1-smoothstep(0.42,0.73,abs(p.y));
                    float jagged = center + (hash(float2(floor(p.y*52),floor(t*8)))-0.5)*0.025*taper;
                    float width = 0.024*taper;
                    float edgeDistance = abs(p.x-jagged);
                    float opening = (1-smoothstep(width*0.3,width,edgeDistance)) * taper;
                    color = lerp(color,float3(0.008,0.004,0.025),opening*amount);
                    float rim = band(edgeDistance,width+0.006,0.012)*taper;
                    float halo = exp(-edgeDistance*24)*taper;
                    float flicker = 0.78+0.22*sin(p.y*24-t*5);
                    color += (float3(0.13,0.9,1)*rim*flicker + float3(0.29,0.08,0.55)*halo*0.65)*amount;
                    float arc = band(distance,0.58+0.025*sin(t*2),0.008)
                        * step(0.56,frac(atan2(delta.y,delta.x)*1.9-t*0.2));
                    color += float3(0.25,0.13,0.55)*arc*amount;
                }
                else if (_Mode < 1.5)
                {
                    float radius = frac(t/3.2)*1.6;
                    float r = length(p);
                    float wave = exp(-pow((r-radius)/0.065,2));
                    float wake = exp(-max(radius-r,0)*11)*step(r,radius);
                    float envelope = smoothstep(0.02,0.16,radius)*(1-smoothstep(1.28,1.6,radius));
                    float2 radial = p/max(r,0.02);
                    float2 displaced = p + radial*wave*0.021*amount*envelope;
                    color = stage(displaced);
                    float seams = seam(displaced);
                    float echo = band(r,radius-0.11,0.025)*0.4;
                    float glow = (wave*(0.18+seams*0.95)+wake*seams*0.28+echo*seams)*envelope;
                    color += float3(0.13,0.8,0.94)*glow*amount;
                    color += float3(0.25,0.32,0.3)*wave*0.2*envelope*amount;
                    // Small plate-like emitter gives the floor response an obvious origin.
                    float core = 1-smoothstep(0.035,0.055,r);
                    float rim = band(r,0.075,0.012);
                    color = lerp(color,float3(0.09,0.12,0.16),core);
                    color += float3(0.24,0.95,1)*(core*(0.3+0.7*exp(-radius*7))+rim*0.6)*amount;
                }
                else if (_Mode < 2.5)
                {
                    float shape = max(abs(p.x)*0.9+abs(p.y)*0.3,abs(p.y)*0.85+abs(p.x)*0.25);
                    float inside = 1-smoothstep(0.6,0.66,shape);
                    color = stage(p);
                    color = lerp(color,color*float3(0.65,0.8,1)+float3(0.015,0.035,0.09),inside*amount*0.7);
                    float localTime = lerp(t,floor(t*0.12*8)/8,inside*amount);
                    float packets = pow(saturate(sin(p.x*24-localTime*6)),14)*step(0.82,frac(p.y*7));
                    float rim = band(shape,0.64,0.012);
                    float ticks = step(0.6,frac(atan2(p.y,p.x)*5.1));
                    color += float3(0.14,0.55,0.95)*(packets*0.65+rim*(0.45+ticks*0.45))*amount;
                    float clock = band(length(p),0.16,0.008);
                    float2 hand = segment(p,float2(0,0),float2(cos(t*0.12),sin(t*0.12))*0.12);
                    color += float3(0.4,0.8,1)*(clock+band(hand.x,0,0.008))*amount*0.65;
                }
                else if (_Mode < 3.5)
                {
                    float r = length(p);
                    float influence = pow(saturate(1-r/1.05),2)*amount;
                    float twist = influence*(1.4+sin(t*0.7)*0.3);
                    float c = cos(twist), s = sin(twist);
                    float2 warped = float2(p.x*c-p.y*s,p.x*s+p.y*c)*(1+influence*0.7);
                    color = stage(warped);
                    float angle = atan2(p.y,p.x);
                    float spiral = pow(saturate(sin(angle*3+r*20-t*3)),12)*influence;
                    color += float3(0.32,0.12,0.7)*spiral*0.5;
                    float rim = band(r,0.13,0.014)+band(r,0.2,0.007)*0.4;
                    color = lerp(color,float3(0.01,0.004,0.025),(1-smoothstep(0.085,0.12,r))*amount);
                    color += float3(0.4,0.16,0.9)*rim*amount;
                }
                else if (_Mode < 4.5)
                {
                    color = stage(p);
                    float2 a = float2(-0.68,0.08), b = float2(-0.1,-0.3), c = float2(0.65,0.2);
                    float2 link1 = segment(p,a,b), link2 = segment(p,b,c);
                    float path1 = band(link1.x,0,0.009), path2 = band(link2.x,0,0.009);
                    float charge1 = pow(saturate(cos((link1.y-t*0.8)*6.283185)),18);
                    float charge2 = pow(saturate(cos((link2.y-t*0.8+0.35)*6.283185)),18);
                    float charge = path1*(0.22+charge1)+path2*(0.22+charge2);
                    float nodes = band(length(p-a),0.06,0.012)+band(length(p-b),0.06,0.012)+band(length(p-c),0.06,0.012);
                    float glow = exp(-link1.x*65)*charge1+exp(-link2.x*65)*charge2;
                    color += (float3(0.1,0.75,0.9)*(charge+nodes)+float3(0.3,0.8,1)*glow*0.16)*amount;
                }
                else if (_Mode < 5.5)
                {
                    float shape = max(abs(p.x)*0.92+abs(p.y)*0.28,abs(p.y)*0.85+abs(p.x)*0.26);
                    float inside = 1-smoothstep(0.58,0.63,shape);
                    color = stage(p);
                    float grey = dot(color,float3(0.3,0.55,0.15));
                    float flicker = 0.72+0.1*step(0.75,hash(float2(floor(p.y*30),floor(t*8))));
                    color = lerp(color,grey*float3(0.7,0.74,0.8)*flicker,inside*amount*0.9);
                    float rim = band(shape,0.62,0.014);
                    float stripes = step(0.5,frac((p.x+p.y)*15));
                    float interrupted = step(0.72,frac(p.x*8))*step(0.91,frac(p.y*13-t*0.07));
                    color += float3(0.8,0.39,0.08)*(rim*(0.35+stripes*0.65)+inside*interrupted*0.3)*amount;
                }
                else if (_Mode < 6.5)
                {
                    color = stage(p);
                    float r = length(p), angle = atan2(p.y,p.x);
                    float boundary = 0.62+sin(angle*5+t*0.45)*0.06+sin(angle*9-t*0.7)*0.035;
                    float pool = 1-smoothstep(boundary-0.02,boundary+0.02,r);
                    float veins = pow(saturate(sin(p.x*23+sin(p.y*18+t*0.6)*1.5)),10);
                    float patches = hash(floor(p*19));
                    float bubbles = 0;
                    [unroll] for(int n=0;n<4;n++)
                    {
                        float2 center = (float2(hash(float2(n,13)),hash(float2(n,41)))-0.5)*0.85;
                        float age = frac(t*0.38+n*0.27);
                        bubbles += band(length(p-center),age*0.11,0.012)*(1-age);
                    }
                    float3 film = lerp(float3(0.015,0.08,0.035),float3(0.12,0.42,0.08),patches*0.5+veins*0.4);
                    film += float3(0.2,0.65,0.12)*bubbles;
                    float exposed = 1-actorMask(p);
                    color = lerp(color,film,pool*amount*exposed*0.88);
                    color += float3(0.13,0.65,0.08)*band(r,boundary,0.012)*amount*exposed;
                }
                else
                {
                    color = stage(p);
                    float exposed = 1-actorMask(p);
                    color *= 1-exposed*amount*0.3;
                    float wobble = sin(p.y*45+t*1.4)*0.008;
                    float4 reflection1 = actorAt(float2(p.x+wobble,-1-p.y),float2(-0.34,-0.28));
                    float4 reflection2 = actorAt(float2(p.x-wobble,0.2-p.y),float2(0.36,0.32));
                    float a1 = reflection1.a*step(p.y,-0.5)*saturate((p.y+0.94)/0.44);
                    float a2 = reflection2.a*step(p.y,0.1)*saturate((p.y+0.34)/0.44);
                    color = lerp(color,reflection1.rgb*float3(0.45,0.58,0.75),a1*0.4*amount*exposed);
                    color = lerp(color,reflection2.rgb*float3(0.45,0.58,0.75),a2*0.4*amount*exposed);
                    float ripples = 0;
                    [unroll] for(int n=0;n<9;n++)
                    {
                        float tick = floor(t*0.6+n*0.31);
                        float2 center = (float2(hash(float2(n,tick)),hash(float2(tick,n+17)))-0.5)*1.7;
                        float age = frac(t*0.6+n*0.31);
                        ripples += band(length(p-center),age*0.15,0.007)*(1-age);
                    }
                    float sheen = pow(saturate(sin(p.x*2+p.y*3-t*0.18)),22)*0.13;
                    color += float3(0.24,0.42,0.58)*(ripples*0.55+sheen)*amount*exposed;
                }
                float border = step(0.982,max(abs(p.x),abs(p.y)));
                return float4(lerp(color,float3(0.12,0.19,0.25),border),1);
            }
            ENDCG
        }
    }
}
