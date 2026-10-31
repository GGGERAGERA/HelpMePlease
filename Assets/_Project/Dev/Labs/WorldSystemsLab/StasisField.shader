Shader "Subject42/Lab/StasisField"
{
    Properties
    {
        [PerRendererData] _MainTex ("Floor engraving", 2D) = "white" {}
        _GlowTex ("Circuits / soft halo", 2D) = "black" {}
        _FieldSize ("Footprint", Vector) = (14,10,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_GlowTex); SAMPLER(sampler_GlowTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _FieldSize;
            CBUFFER_END
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying Vert(Input v) { Varying o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 Frag(Varying i):SV_Target
            {
                half4 ink=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                half2 circuits=SAMPLE_TEXTURE2D(_GlowTex,sampler_GlowTex,i.uv).rg;
                float2 p=(i.uv-.5)*_FieldSize.xy;
                float angle=atan2(p.y,p.x);
                float wave=pow(saturate(cos(angle*2-_Time.y*.85)),18);
                float sweep=pow(saturate(cos(length(p)*2.3-_Time.y*1.25)),12);
                float breath=.83+.08*sin(_Time.y*1.35);
                half a=saturate(circuits.x*(breath+.28*wave+.12*sweep)+circuits.y*.8);
                half3 light=lerp(half3(.13,.76,.84),half3(.64,1,1),wave*.65+sweep*.20);
                half alpha=ink.a+a*(1-ink.a);
                half3 rgb=(ink.rgb*ink.a*(1-a)+light*a)/max(alpha,.001);
                return half4(rgb,alpha);
            }
            ENDHLSL
        }
    }
}
