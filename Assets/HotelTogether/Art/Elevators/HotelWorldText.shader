Shader "HotelTogether/WorldText"
{
 Properties { _MainTex("Font atlas",2D)="white"{} }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Pass
  {
   ZWrite Off ZTest LEqual Cull Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Input {float4 position:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct Output {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   Output vert(Input v){Output o;o.position=TransformObjectToHClip(v.position.xyz);o.uv=v.uv;o.color=v.color;return o;}
   half4 frag(Output i):SV_Target{half4 c=i.color;c.a*=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;clip(c.a-.005);return c;}
   ENDHLSL
  }
 }
}
