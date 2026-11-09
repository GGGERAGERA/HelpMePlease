Shader "Subject42/Dev/CombatFloorRefraction"
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

            TEXTURE2D(_CameraSortingLayerTexture);
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float2 screenPerLocalUnit : TEXCOORD2;
                float2 pixelGrid : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                // The MVP camera is orthographic and the zone is an XY quad.
                float4 unitX = TransformObjectToHClip(input.positionOS.xyz + float3(1, 0, 0));
                float4 unitY = TransformObjectToHClip(input.positionOS.xyz + float3(0, 1, 0));
                output.screenPerLocalUnit = float2(unitX.x - output.positionCS.x,
                    (unitY.y - output.positionCS.y) * _ProjectionParams.x) * .5;
                output.pixelGrid = float2(length(TransformObjectToWorldDir(float3(1, 0, 0), false)),
                    length(TransformObjectToWorldDir(float3(0, 1, 0), false))) * 16;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float edge = length(input.uv * 2 - 1);
                clip(.99 - edge);
                float2 p = (floor(input.uv * input.pixelGrid) + .5) * 2 / input.pixelGrid - 1;
                float r = length(p);
                float influence = 1 - smoothstep(.10, .98, r);
                float twist = influence * (1.25 + sin(_Time.y * 1.9 - r * 6) * .38);
                float s, c;
                sincos(twist, s, c);
                float2 warped = float2(p.x * c - p.y * s, p.x * s + p.y * c) * (1 + influence * .56);
                float2 offset = (warped - p) * input.screenPerLocalUnit * .5;
                float2 sampleUV = input.screenPos.xy / input.screenPos.w + offset;
                half3 floorColor = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_PointClamp, sampleUV).rgb;
                return half4(floorColor, 1 - smoothstep(.56, .99, edge));
            }
            ENDHLSL
        }
    }
}
