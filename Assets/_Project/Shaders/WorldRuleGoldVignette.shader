Shader "UI/World Rule Gold Vignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Color ("Gold", Color) = (1,0.68,0.12,0.62)
        _EdgeWidth ("Edge Width", Range(0.01,0.45)) = 0.22
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _EdgeWidth;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            Output vert(Input v)
            {
                Output o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                float2 edge = min(i.uv, 1.0-i.uv);
                float mask = 1.0-smoothstep(0.0, _EdgeWidth, min(edge.x, edge.y));
                return fixed4(_Color.rgb * i.color.rgb, mask * _Color.a * i.color.a);
            }
            ENDCG
        }
    }
}
