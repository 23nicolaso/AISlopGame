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
   struct V { float4 positionCS:SV_POSITION; float3 normal:TEXCOORD0; float2 uv:TEXCOORD1; };
   V vert(A i)
   {
    V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
    o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    float light=.32+.85*saturate(dot(normalize(i.normal),normalize(float3(.37,.62,-.70))));
    return half4(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*light,1);
   }
   ENDHLSL
  }
 }
}
