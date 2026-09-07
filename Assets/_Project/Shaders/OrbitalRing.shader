Shader "Subject42/Orbital Ring"
{
    Properties
    {
        _RingSurface ("Emission / Glow / Core Whitening", Vector) = (1, .12, .08, 0)
    }
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
            CBUFFER_START(UnityPerMaterial)
                float4 _RingSurface;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float distance = abs(input.uv.y * 2 - 1);
                float aa = max(fwidth(distance), .001);
                float core = 1 - smoothstep(1.0 / 3 - aa, 1.0 / 3 + aa, distance);
                float skirt = pow(saturate(1 - distance), 2) * _RingSurface.y;
                float alpha = saturate(core + skirt) * input.color.a;
                float3 color = lerp(input.color.rgb, float3(1, 1, 1), core * _RingSurface.z);
                return half4(color * _RingSurface.x, alpha);
            }
            ENDHLSL
        }
    }
}
