Shader "Subject42/UI/GameplayFocus"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Dim ("Selection dim", Range(0,1)) = 0
        _Bullet ("Bullet time blend", Range(0,1)) = 0
        _Tint ("Cold tint", Range(0,1)) = .16
        _Vignette ("Edge intensity", Range(0,1)) = .45
        _Focus ("Clear center and extents", Vector) = (.5,.5,.2,.3)
        _UnscaledTime ("Presentation clock", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Dim, _Bullet, _Tint, _Vignette, _UnscaledTime;
            float4 _Focus;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Keep the player/orbit footprint clear with a soft circular falloff.
                float2 focus = abs((i.uv - _Focus.xy) / max(_Focus.zw, .001));
                float outside = smoothstep(.95, 1.18, length(focus));
                float dim = _Dim * outside;
                float edge = smoothstep(.35, .95, length((i.uv - .5) * 1.6));
                float pulse = .97 + .03 * sin(_UnscaledTime * 2.5);
                float bullet = _Bullet * (_Tint + edge * _Vignette) * pulse;
                float alpha = dim + bullet * (1 - dim);
                float3 cold = lerp(float3(.12,.48,.65), float3(.015,.10,.19), edge);
                float3 color = (cold * bullet * (1-dim) + float3(.008,.012,.025) * dim) / max(alpha,.0001);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
