Shader "Subject42/AnomalyFocus"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FocusAmount ("Focus Amount", Range(0, 1)) = 0
        _OutsideDarkness ("Outside Darkness", Range(0, 1)) = 0.48
        _OutsideDesaturation ("Outside Desaturation", Range(0, 1)) = 0.55
        _ClearRatio ("Clear Ratio", Vector) = (0.2, 0.2, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+100"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _FocusAmount;
            float _OutsideDarkness;
            float _OutsideDesaturation;
            float4 _ClearRatio;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 distanceFromCenter = abs(input.uv - 0.5) * 2.0;
                float2 edge = max(fwidth(distanceFromCenter) * 2.0, 0.003);
                float2 outsideAxis = smoothstep(
                    _ClearRatio.xy - edge,
                    _ClearRatio.xy + edge,
                    distanceFromCenter
                );
                float outside = max(outsideAxis.x, outsideAxis.y);
                float neutral = lerp(0.0, 0.28, _OutsideDesaturation);
                float alpha = max(
                    _OutsideDarkness,
                    _OutsideDesaturation
                );
                return fixed4(
                    neutral,
                    neutral,
                    neutral,
                    outside * alpha * _FocusAmount
                );
            }
            ENDCG
        }
    }
}
