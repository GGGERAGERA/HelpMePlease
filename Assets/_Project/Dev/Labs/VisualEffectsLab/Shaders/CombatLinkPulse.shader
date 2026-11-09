Shader "Subject42/Dev/CombatLinkPulse"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 link : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 link : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.link = input.link;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float steppedDistance = floor(input.link.x * 32) / 32;
                float phase = frac(steppedDistance * 1.55 - _Time.y * 1.45);
                half charge = (1 - smoothstep(.02, .23, abs(phase - .5))) * input.link.y;
                half3 color = lerp(input.color.rgb, half3(.95, 1, 1), charge);
                return half4(color, input.color.a);
            }
            ENDHLSL
        }
    }
}
