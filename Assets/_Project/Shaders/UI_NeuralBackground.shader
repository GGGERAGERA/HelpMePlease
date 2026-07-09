Shader "UI/Neural Background"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.02, 0.05, 0.08, 1)
        _GlowColor ("Glow Color", Color) = (0.0, 0.8, 1.0, 1)
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.08
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.12
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.18
        _ScanlineSpeed ("Scanline Speed", Range(0, 5)) = 0.35
        _ScanlineWidth ("Scanline Width", Range(0.001, 0.2)) = 0.035
        _WarningColor ("Warning Color", Color) = (1, 0, 0, 1)
        _WarningStrength ("Warning Strength", Range(0, 1)) = 0.25
        _WarningSpeed ("Warning Speed", Range(0, 10)) = 1.2
        _WarningFrequency ("Warning Frequency", Range(1, 30)) = 12
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
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
            fixed4 _BaseColor;
            fixed4 _GlowColor;
            float _NoiseStrength;
            float _PulseStrength;
            float _ScanlineStrength;
            float _ScanlineSpeed;
            float _ScanlineWidth;
            fixed4 _WarningColor;
            float _WarningStrength;
            float _WarningSpeed;
            float _WarningFrequency;

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

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

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

                float noise = Hash(floor((uv + _Time.y * 0.03) * 80.0));
                float pulse = sin(_Time.y * 1.2) * 0.5 + 0.5;
                float warningCycle = frac(_Time.y / _WarningFrequency);
                float warningWindow = 1.0 - smoothstep(0.0, 0.12, warningCycle);
                float warningBlink = step(0.5, sin(_Time.y * _WarningSpeed * 18.0) * 0.5 + 0.5);
                float warning = warningWindow * warningBlink;

                float scanY = frac(_Time.y * _ScanlineSpeed);
                float dist = abs(uv.y - scanY);
                float scanCore = 1.0 - step(_ScanlineWidth * 0.18, dist);
                float scanGlow = 1.0 - smoothstep(0.0, _ScanlineWidth, dist);
                float scan = scanCore + scanGlow * 0.35;
                float thinLines = sin(uv.y * 900.0 + _Time.y * 2.0) * 0.5 + 0.5;
                thinLines = step(0.92, thinLines) * 0.035;

                fixed4 col = _BaseColor;
                col.rgb += noise * _NoiseStrength;
                col.rgb += _GlowColor.rgb * pulse * _PulseStrength;
                col.rgb += _GlowColor.rgb * scan * _ScanlineStrength;
                col.rgb += _WarningColor.rgb * warning * _WarningStrength;
                col.rgb += _GlowColor.rgb * thinLines;

                col.a *= i.color.a;
                return col;
            }
            ENDCG
        }
    }
}