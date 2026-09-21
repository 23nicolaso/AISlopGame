Shader "Rift/PlanetSurface"
{
 Properties { _BaseMap("Surface",2D)="white" {} }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
   struct V { float4 positionCS:SV_POSITION; float3 normal:TEXCOORD0; float2 uv:TEXCOORD1; float3 positionWS:TEXCOORD2; };
   V vert(A i)
   {
    V o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    float3 n=normalize(i.normal);
    // Raised night-side floor keeps terrain readable once Neutral tonemapping compresses the low end.
    float light=.44+.78*saturate(dot(n,normalize(float3(.37,.62,-.70))));
    float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*light;
    // Fresnel limb draws the horizon silhouette against the black sky when flying low.
    float rim=pow(1-saturate(dot(n,normalize(GetCameraPositionWS()-i.positionWS))),3.5);
    return half4(base+float3(.10,.23,.40)*rim,1);
   }
   ENDHLSL
  }
 }
}
