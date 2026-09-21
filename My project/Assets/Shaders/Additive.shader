Shader "Rift/Additive"
{
 // One additive unlit pass for every glow that has to survive a bright sky: starfield, refinery beacon pillars, wind streaks.
 // _BaseColor is per-renderer (MaterialPropertyBlock), _Fade is per-material, so a gate can push the owner colour through
 // the same block it already pushes to its rings while the material keeps its own opacity.
 // _TopFade (0 = off) dissolves the beam over its own length so a 400 m pillar ends in sky instead of a flat lid.
 Properties { _BaseColor ("Color", Color)=(1,1,1,1) _Fade ("Fade", Float)=1 _TopFade ("Top fade", Float)=0 }
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
   float _TopFade;
   struct A { float4 positionOS:POSITION; };
   struct V { float4 positionCS:SV_POSITION; float height:TEXCOORD0; };
   // A Unity cube spans -.5..+.5 in object space, so (y+.5) is 0 at the foot of the beam and 1 at its cap.
   V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.height=i.positionOS.y+.5;return o; }
   // Smoothstep rather than a straight ramp: the lower half of the beam stays solid enough to read as a marker,
   // and only the last third actually dissolves. _TopFade=0 leaves the starfield and wind streaks untouched.
   half4 frag(V i):SV_Target { float k=saturate(1-i.height);k=k*k*(3-2*k);return half4(_BaseColor.rgb*_Fade*lerp(1,k,_TopFade),0); }
   ENDHLSL
  }
 }
}
