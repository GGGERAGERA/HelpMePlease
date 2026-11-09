Shader "Subject42/Dev/CombatGravityWell"
{
    Properties
    {
        _RegionSize ("Region size", Vector) = (6,6,0,0)
        _VisualTime ("Zone visual time", Float) = 0
        _Fade ("Zone fade", Range(0,1)) = 0
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
            #include "../../../../Shaders/AnomalyPixel.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RegionSize;
                float _VisualTime, _Fade;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(_Fade - .0001);
                float2 size = max(_RegionSize.xy, .0625);
                float2 local = input.uv * 2 - 1;
                float2 p = AnomalySnap(local * size * .5) * 2 / size;
                float r = length(p);
                float angle = atan2(p.y, p.x);
                half spiral = smoothstep(.79, .97, cos(angle * 3 + r * 10 + _VisualTime * 2.7));
                spiral *= smoothstep(.1, .25, r);
                half rim = 1 - smoothstep(.015, .042, abs(r - .17));
                half core = 1 - smoothstep(.10, .17, r);
                half3 color = lerp(half3(.24, .12, .54), half3(.62, .40, .98), max(spiral, rim));
                color = lerp(color, half3(.026, .015, .064), core);
                half alpha = .055 + spiral * .30 + rim * .56 + core * .58;
                half feather = 1 - smoothstep(.55, .99, length(local));
                return half4(color, saturate(alpha) * feather * _Fade);
            }
            ENDHLSL
        }
    }
}
