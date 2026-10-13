Shader "HotelTogether/City Skydome"
{
 Properties
 {
  _MainTex ("Day panorama", 2D) = "white" {}
  _NightTex ("Night panorama", 2D) = "black" {}
  _NightBlend ("Night blend", Range(0,1)) = 0
  _Exposure ("Brightness", Range(0.1,2)) = 1
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Pass
  {
   Name "CityBackdrop"
   Tags { "LightMode"="SRPDefaultUnlit" }
   Cull Off ZWrite Off ZTest LEqual
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half _NightBlend;
    half _Exposure;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   Varyings Vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=TRANSFORM_TEX(input.uv,_MainTex);return o;}
   half4 Frag(Varyings input):SV_Target
   {
    half3 dayColor=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).rgb;
    half3 nightColor=SAMPLE_TEXTURE2D(_NightTex,sampler_NightTex,input.uv).rgb;
    half ground=1-smoothstep(.375,.435,input.uv.y);
    dayColor=lerp(dayColor,dayColor*half3(.82,1.16,.65),ground);
    half3 color=lerp(dayColor,nightColor,saturate(_NightBlend));
    return half4(color*_Exposure,1);
   }
   ENDHLSL
  }
 }
}
