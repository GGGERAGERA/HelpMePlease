Shader "UI/Card Scan Overlay"
{
    Properties
    {
        _ScanColor ("Scan Color", Color) = (0, 0.9, 1, 1)
        _ScanStrength ("Scan Strength", Range(0, 2)) = 0.8
        _ScanSpeed ("Scan Speed", Range(0, 5)) = 0.8
        _ScanWidth ("Scan Width", Range(0.001, 0.2)) = 0.045
        _GridStrength ("Grid Strength", Range(0, 1)) = 0.12
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

            fixed4 _ScanColor;
            float _ScanStrength;
            float _ScanSpeed;
            float _ScanWidth;
            float _GridStrength;

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
                float2 uv = i.uv;

                float scanY = frac(_Time.y * _ScanSpeed);
                float dist = abs(uv.y - scanY);

                float core = 1.0 - step(_ScanWidth * 0.2, dist);
                float glow = 1.0 - smoothstep(0.0, _ScanWidth, dist);
                float scan = core + glow * 0.45;

                float verticalGrid = step(0.965, sin(uv.x * 80.0) * 0.5 + 0.5);
                float horizontalGrid = step(0.965, sin(uv.y * 80.0) * 0.5 + 0.5);
                float grid = (verticalGrid + horizontalGrid) * _GridStrength;

                fixed4 col = _ScanColor;
                col.a = saturate((scan * _ScanStrength + grid) * i.color.a);

                return col;
            }
            ENDCG
        }
    }
}