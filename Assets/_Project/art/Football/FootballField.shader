Shader "Subject42/Football Field"
{
 Properties {
  _InnerColor("Fill",Color)=(.1,.1,.3,.12)
  _EdgeColor("Edge",Color)=(.3,.2,.7,1)
  _Fade("Fade",Float)=1
  _RegionSize("Size",Vector)=(7,3.2,0,0)
  _FlowPhaseOffset("Radial Flow Phase",Float)=0
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
   float _Fade, _FlowPhaseOffset;
   CBUFFER_END
   Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
   half4 frag(Varyings i):SV_Target {
    float2 count=max(floor(_RegionSize.xy*16),1);
    float2 p=floor(i.uv*count);
    float2 edge=min(p,count-1-p);
    float border=1-step(2,min(edge.x,edge.y));
    // The quad is only a carrier for local, world-sized pixel effects.
    float2 fieldPoint=(p+.5-count*.5)/16;
    float radius=length(fieldPoint);
    // GravityZone advances this phase with the actual signed polarity:
    // positive moves rings inward, negative moves them outward.
    float ring=(1-step(.065,abs(frac(radius*.65+_FlowPhaseOffset)-.5)))
        *(1-step(1.35,radius));
    float halo=1-step(.04,abs(radius-.95));
    float center=1-step(.18,radius);
    float brackets=border*(1-step(8,max(edge.x,edge.y)));
    // Small corner ticks indicate trigger extent without outlining a second pitch.
    // No rectangular fill; the room's authored floor remains fully visible.
    half3 edgeColor=lerp(_EdgeColor.rgb,half3(1,1,1),.25);
    float alpha=max(halo*.2,max(brackets*.6,max(ring*.55,center*.8)));
    return half4(edgeColor,alpha*_Fade);
   }
   ENDHLSL
  }
 }
}
