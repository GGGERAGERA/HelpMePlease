Shader "UI/Vignette Overlay"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.75)
        _Radius ("Radius", Range(0, 1)) = 0.45
        _Softness ("Softness", Range(0.01, 1)) = 0.45
        _Intensity ("Intensity", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
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

            fixed4 _Color;
            float _Radius;
            float _Softness;
            float _Intensity;

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

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv - 0.5;
                float dist = length(centered);

                float vignette = smoothstep(_Radius, _Radius + _Softness, dist);
                fixed4 col = _Color;
                col.a *= vignette * _Intensity * i.color.a;

                return col;
            }
            ENDCG
        }
    }
}