Shader "Subject42/Bunker Floor Navigation"
{
    Properties
    {
        _Color ("Guide light", Color) = (0.26, 0.72, 0.65, 1)
        _Active ("Guidance", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 marker:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float marker:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Active;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.marker = v.marker.x;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float edge = abs(i.uv.y);
                float core = 1 - smoothstep(.24, .46, edge);
                float halo = (1 - smoothstep(.4, 1, edge)) * .24;
                float travel = pow(saturate(.5 + .5 * cos((i.uv.x - _Time.y * 1.6) * 2.5)), 10);
                float pulse = .5 + .5 * sin(_Time.y * 2);
                float light = lerp(travel, pulse, i.marker);
                float intensity = lerp(.12, .48 + .42 * light, _Active);
                return half4(_Color.rgb * (1 + _Active * light * .45), (core + halo) * intensity * _Color.a);
            }
            ENDHLSL
        }
    }
}
