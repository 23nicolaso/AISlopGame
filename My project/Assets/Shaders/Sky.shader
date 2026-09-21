Shader "Rift/Sky"
{
 Properties { _SunDir ("Sun direction", Vector)=(0,1,0,0) _PlanetCenter ("Center", Vector)=(0,-1200,0,0) _Radius ("Radius", Float)=1200 }
 SubShader
 {
  Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   ZWrite Off
   Cull Front
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 _SunDir;
   float4 _PlanetCenter;
   float _Radius;
   struct A { float4 positionOS:POSITION; };
   struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
   V vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o; }
   half4 frag(V i):SV_Target
   {
    float3 ray=normalize(i.world-_WorldSpaceCameraPos);
    float3 radial=normalize(_WorldSpaceCameraPos-_PlanetCenter.xyz);
    float altitude=length(_WorldSpaceCameraPos-_PlanetCenter.xyz)-_Radius;
    // Same 280 m scale height the flight model's Density() uses: the daylight thins out exactly as the wings stop biting.
    float air=exp(-max(0,altitude)/280);
    // The raw ratio would have the sky half gone at the 165 m launch line; the .4 power holds a real daytime sky down
    // low and still empties it out by 700 m, which is where the flight model's air stops mattering anyway.
    float veil=pow(saturate((air-.07)/.93),.4);
    // 1 at the zenith, 0 at the local horizon, -1 straight down: everything below is shading for the limb, not for the sky.
    float h=dot(ray,radial);
    float sd=saturate(dot(ray,_SunDir.xyz));
    float3 sky=lerp(float3(.10,.17,.34),float3(.03,.115,.40),pow(saturate(h),.45));
    // Below the local horizontal is distant ground haze, not sky: it has to go dark fast or the lower half reads as fog.
    sky=lerp(sky,float3(.05,.06,.085),pow(saturate(-h),.5));
    // Warm band hugging the horizon, biased hard toward the sun's side so the air has a direction as well as a floor.
    sky=lerp(sky,float3(.55,.34,.16),pow(saturate(1-abs(h)),5)*(.25+.6*sd*sd));
    // Disc is deliberately over the volume's bloom threshold of 1; the two halo lobes carry the glow the 0.25 bloom cannot.
    float disc=pow(sd,1500)*6;
    float halo=pow(sd,40)*.55+pow(sd,8)*.10;
    float3 col=lerp(float3(.004,.008,.022),sky,veil)+float3(1,.86,.62)*(disc+halo*(.30+.70*veil));
    return half4(col,1);
   }
   ENDHLSL
  }
 }
}
