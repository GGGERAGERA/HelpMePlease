Shader "World/Rule Pixel Particle"
{
    Properties
    {
        _MainTex ("Pixel Sprite (Point)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelsPerUnit ("Pixels Per World Unit", Float) = 16
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            float _PixelsPerUnit;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            Output vert(Input v)
            {
                Output o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                world.xy = floor(world.xy * _PixelsPerUnit + 0.5) / _PixelsPerUnit;
                o.vertex = mul(UNITY_MATRIX_VP, world);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a - 0.5);
                return fixed4(tex.rgb * i.color.rgb, i.color.a);
            }
            ENDCG
        }
    }
}
