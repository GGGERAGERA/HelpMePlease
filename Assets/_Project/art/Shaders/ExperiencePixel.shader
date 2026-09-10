Shader "Subject42/XP Pixel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Phase ("Idle phase", Float) = 0
        _Pulse ("Brightness pulse", Range(0, 0.1)) = 0.04
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _Phase;
            half _Pulse;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                float2 halfScreen = _ScreenParams.xy * 0.5;
                o.positionCS.xy = round(o.positionCS.xy / o.positionCS.w * halfScreen) / halfScreen * o.positionCS.w;
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c.rgb *= 1 - _Pulse * (0.5 + 0.5 * sin((_Time.y + _Phase) * 4.83322));
                return c;
            }
            ENDHLSL
        }
    }
}
