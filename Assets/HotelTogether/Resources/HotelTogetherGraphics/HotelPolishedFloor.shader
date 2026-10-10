Shader "HotelTogether/Polished Lobby Tiles"
{
 Properties
 {
  _BaseColor("Warm porcelain",Color)=(0.83,0.76,0.62,1)
  _TileSize("Tile size",Float)=1.25
  _Smoothness("Polish",Range(0,1))=.82
  _ReflectionStrength("Reflections",Range(0,1))=.48
  _ReflectionBlur("Reflection softness",Range(0,5))=1.3
  [HideInInspector]_BaseMap("Base",2D)="white"{}
  [HideInInspector]_Cutoff("Cutoff",Float)=.5
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;float _TileSize;half _Smoothness,_ReflectionStrength,_ReflectionBlur;
   CBUFFER_END
   TEXTURE2D(_HotelFloorReflection);SAMPLER(sampler_HotelFloorReflection);
   float4x4 _HotelReflectionVP;
   half _HotelReflectionReady;
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half fog:TEXCOORD2;};
   Varyings Vert(Attributes i){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(Varyings i):SV_Target
   {
    float2 tile=i.positionWS.xz/_TileSize;
    float2 edges=min(frac(tile),1-frac(tile))*_TileSize;
    float grout=1-smoothstep(.008,.008+max(fwidth(tile.x),fwidth(tile.y))*_TileSize,min(edges.x,edges.y));
    float variation=frac(sin(dot(floor(tile),float2(127.1,311.7)))*43758.5453)-.5;
    half3 albedo=_BaseColor.rgb*(1+variation*.065);
    float vein=pow(abs(sin(i.positionWS.x*.83+i.positionWS.z*.61+sin(i.positionWS.z*2.1)*.31)),26);
    albedo*=1-vein*.025;
    albedo=lerp(albedo,half3(.42,.38,.30),grout*.48);
    InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=normalize(i.normalWS);input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);input.bakedGI=SampleSH(input.normalWS);input.vertexLighting=VertexLighting(i.positionWS,input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;surface.albedo=albedo;surface.alpha=1;surface.metallic=.02;surface.specular=half3(.04,.04,.04);surface.smoothness=lerp(_Smoothness,.32,grout);surface.normalTS=half3(0,0,1);surface.occlusion=1;
    half4 result=UniversalFragmentPBR(input,surface);
    float4 projected=mul(_HotelReflectionVP,float4(i.positionWS,1));float2 uv=projected.xy/projected.w*.5+.5;
    #if UNITY_UV_STARTS_AT_TOP
    uv.y=1-uv.y;
    #endif
    half3 reflected=SAMPLE_TEXTURE2D_LOD(_HotelFloorReflection,sampler_HotelFloorReflection,uv,_ReflectionBlur).rgb;
    half fresnel=pow(1-saturate(dot(input.normalWS,input.viewDirectionWS)),3);
    half strength=_ReflectionStrength*(.55+.45*fresnel)*(1-grout*.65)*_HotelReflectionReady;
    strength*=step(0,uv.x)*step(0,uv.y)*step(uv.x,1)*step(uv.y,1)*step(0,projected.w);
    result.rgb=lerp(result.rgb,reflected,strength);
    result.rgb=MixFog(result.rgb,i.fog);return result;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
