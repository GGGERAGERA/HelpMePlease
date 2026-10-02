Shader "Subject42/Football Field"
{
 Properties {
  _InnerColor("Fill",Color)=(.1,.1,.3,.12)
  _EdgeColor("Edge",Color)=(.3,.2,.7,1)
  _Fade("Fade",Float)=1
  _RegionSize("Size",Vector)=(7,3.2,0,0)
 }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off
  Cull Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   CBUFFER_START(UnityPerMaterial)
   float4 _InnerColor,_EdgeColor,_RegionSize;
   float _Fade;
   CBUFFER_END
   Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
   half4 frag(Varyings i):SV_Target {
    float2 count=max(floor(_RegionSize.xy*16),1);
    float2 p=floor(i.uv*count);
    float2 edge=min(p,count-1-p);
    float border=1-step(1,min(edge.x,edge.y));
    float2 c=abs(p-floor(count*.5));
    float cross=max((1-step(1,c.x))*(1-step(5,c.y)),(1-step(1,c.y))*(1-step(5,c.x)));
    half4 col=lerp(_InnerColor,_EdgeColor,max(border,cross));col.a*=_Fade;return col;
   }
   ENDHLSL
  }
 }
}
