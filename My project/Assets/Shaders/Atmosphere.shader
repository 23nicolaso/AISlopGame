Shader "Rift/Atmosphere"
{
 Properties { _PlanetCenter ("Center", Vector)=(0,-1200,0,0) _Radius ("Radius", Float)=1200 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Front
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 _PlanetCenter;
   float _Radius;
   struct A { float4 positionOS:POSITION; };
   struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
   V vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o; }
   half4 frag(V i):SV_Target
   {
    float3 radial=normalize(_WorldSpaceCameraPos-_PlanetCenter.xyz);
    float3 ray=normalize(i.world-_WorldSpaceCameraPos);
    float altitude=length(_WorldSpaceCameraPos-_PlanetCenter.xyz)-_Radius;
    float density=exp(-max(0,altitude)/350);
    float horizon=pow(1-abs(dot(ray,radial)),4);
    float mu=dot(ray,radial);
    float closest=(_Radius+altitude)*sqrt(saturate(1-mu*mu));
    float limb=exp(-abs(closest-_Radius)/65)*step(mu,0);
    float outerFade=exp(-max(0,closest-_Radius)/100);
    return half4(.11,.36,.59,saturate((.1+horizon*.65)*density*outerFade+limb*.55));
   }
   ENDHLSL
  }
 }
}
