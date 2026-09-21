Shader "Rift/Additive"
{
 // One additive unlit pass for every glow that has to survive a bright sky: starfield, refinery beacon pillars, wind streaks.
 // _BaseColor is per-renderer (MaterialPropertyBlock), _Fade is per-material, so a gate can push the owner colour through
 // the same block it already pushes to its rings while the material keeps its own opacity.
 Properties { _BaseColor ("Color", Color)=(1,1,1,1) _Fade ("Fade", Float)=1 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend One One
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 _BaseColor;
   float _Fade;
   struct A { float4 positionOS:POSITION; };
   struct V { float4 positionCS:SV_POSITION; };
   V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o; }
   half4 frag(V i):SV_Target { return half4(_BaseColor.rgb*_Fade,0); }
   ENDHLSL
  }
 }
}
