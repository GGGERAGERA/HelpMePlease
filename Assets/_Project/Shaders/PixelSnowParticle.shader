Shader "World/Pixel Snow Particle"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float4 world = mul(unity_ObjectToWorld, input.vertex);
                // Native particle quads, snapped by the GPU to the 16 PPU effect grid.
                world.xy = floor(world.xy * 16.0 + 0.5) / 16.0;
                output.vertex = mul(UNITY_MATRIX_VP, world);
                output.world = world.xy;
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            fixed4 frag(Varyings input) : SV_Target
            {
                // A flake occupies ONE effect texel, even if an old/live emitter
                // supplies a huge quad. Reconstruct its center without CPU work.
                float2 dx = ddx(input.world), dy = ddy(input.world);
                float2 du = ddx(input.uv), dv = ddy(input.uv);
                float determinant = du.x * dv.y - du.y * dv.x;
                clip(abs(determinant) - 0.00000001);
                float inverse = abs(determinant) > 0.00000001 ? rcp(determinant) : 0;
                float2 axisX = (dx * dv.y - dy * du.y) * inverse;
                float2 axisY = (dy * du.x - dx * dv.x) * inverse;
                float2 center = input.world - axisX * (input.uv.x - 0.5) -
                    axisY * (input.uv.y - 0.5);
                float2 cell = floor(input.world * 16.0);
                float2 centerCell = floor(center * 16.0 + 0.0001);
                clip(0.5 - max(abs(cell.x - centerCell.x), abs(cell.y - centerCell.y)));
                return fixed4(input.color.rgb, input.color.a * 0.35);
            }
            ENDCG
        }
    }
}
