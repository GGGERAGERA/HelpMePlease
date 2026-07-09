Shader "UI/Emissive Pulse"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (0, 0.8, 1, 1)
        _EmissionColor ("Emission Color", Color) = (0, 1, 1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.5
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
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

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _EmissionColor;
            float _EmissionStrength;
            float _PulseSpeed;
            float _PulseAmount;

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
                fixed4 tex = tex2D(_MainTex, i.uv);
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float emission = _EmissionStrength * (1.0 + pulse * _PulseAmount);

                fixed4 col = tex * _Color * i.color;
                col.rgb += _EmissionColor.rgb * emission * tex.a;
                col.a = tex.a * _Color.a * i.color.a;

                return col;
            }
            ENDCG
        }
    }
}