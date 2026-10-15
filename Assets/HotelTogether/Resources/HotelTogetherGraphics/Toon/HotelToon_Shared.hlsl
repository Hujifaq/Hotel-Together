// Hotel Together toon shader - shared code for every pass.
// Structure follows "SimpleURPToonLitOutlineExample_Shared.hlsl" by ColinLeung-NiloCat (MIT License).
// https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#include "NiloOutlineUtil.hlsl"
#include "NiloZOffset.hlsl"
#include "NiloInvLerpRemap.hlsl"

// ---------------------------------------------------------------------------------------------- inputs
struct Attributes
{
    float3 positionOS       : POSITION;
    half3  normalOS         : NORMAL;
    half4  tangentOS        : TANGENT;
    float2 uv               : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float3 smoothNormalOS   : TEXCOORD7; // written by HotelToonShading for outlined props (zero when absent)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    float4 positionWSAndFogFactor   : TEXCOORD1; // xyz: positionWS, w: vertex fog factor
    half3  normalWS                 : TEXCOORD2;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 3);
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion           : TEXCOORD4;
#endif
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_OcclusionMap);

// Every pass shares one UnityPerMaterial layout so the SRP Batcher can batch all toon materials together.
CBUFFER_START(UnityPerMaterial)
    float4  _BaseMap_ST;
    half4   _BaseColor;
    half    _Cutoff;
    half    _IsFace;

    half    _CelShadeMidPoint;
    half    _CelShadeSoftness;
    half    _DirectLightMultiplier;
    half    _ReceiveShadowStrength;
    half4   _ShadowColor;

    half    _AdditionalLightMultiplier;
    half    _AdditionalLightSoftness;
    half    _LightBands;

    half    _IndirectLightMultiplier;
    half    _IndirectLightFlatten;
    half4   _IndirectLightMinColor;

    half    _Smoothness;
    half    _Metallic;
    half    _SpecularStrength;
    half    _ReflectionStrength;

    half4   _RimColor;
    half    _RimStrength;
    half    _RimWidth;

    half4   _EmissionColor;
    half    _OcclusionStrength;

    float   _OutlineWidth;
    half4   _OutlineColor;
    float   _OutlineZOffset;
    float   _OutlineSmoothNormals;
CBUFFER_END

// Set by URP while rendering shadow maps.
float3 _LightDirection;
float3 _LightPosition;

struct ToonSurfaceData
{
    half3 albedo;
    half  alpha;
    half3 emission;
    half  occlusion;
    half  smoothness;
    half  metallic;
};

// ---------------------------------------------------------------------------------------------- vertex
float3 TransformPositionWSToOutlinePositionWS(float3 positionWS, float positionVS_Z, float3 normalWS)
{
    float outlineExpandAmount = _OutlineWidth * GetOutlineCameraFovAndDistanceFixMultiplier(positionVS_Z);
    return positionWS + normalWS * outlineExpandAmount;
}

Varyings VertexShaderWork(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);
    VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    float3 positionWS = vertexInput.positionWS;

#ifdef ToonShaderIsOutline
    // Hard-edged props have split normals; pushing along them opens gaps at every corner.
    // HotelToonShading stores an averaged normal in UV8 for those meshes.
    float3 outlineNormalWS = vertexNormalInput.normalWS;
    if (_OutlineSmoothNormals > 0.5 && dot(input.smoothNormalOS, input.smoothNormalOS) > 0.25)
        outlineNormalWS = TransformObjectToWorldNormal(input.smoothNormalOS);
    positionWS = TransformPositionWSToOutlinePositionWS(vertexInput.positionWS, vertexInput.positionVS.z, outlineNormalWS);
#endif

    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionWSAndFogFactor = float4(positionWS, ComputeFogFactor(vertexInput.positionCS.z));
    output.normalWS = vertexNormalInput.normalWS;
    output.positionCS = TransformWorldToHClip(positionWS);

#if !defined(ToonShaderIsOutline) && !defined(ToonShaderApplyShadowBiasFix)
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
    #ifdef USE_APV_PROBE_OCCLUSION
        OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
    #else
        float4 unusedProbeOcclusion = 0;
        OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, unusedProbeOcclusion);
    #endif
#endif

#ifdef ToonShaderIsOutline
    // Push the outline back a little so it never covers the surface it belongs to (eyes, mouths get extra).
    output.positionCS = NiloGetNewClipPosWithZOffset(output.positionCS, _OutlineZOffset + 0.03 * _IsFace);
#endif

#ifdef ToonShaderApplyShadowBiasFix
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, output.normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(positionCS);
#endif

    return output;
}

// ---------------------------------------------------------------------------------------------- surface
half4 GetFinalBaseColor(Varyings input)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
}

void DoClipTestToTargetAlphaValue(half alpha)
{
#if defined(_ALPHATEST_ON)
    clip(alpha - _Cutoff);
#endif
}

ToonSurfaceData InitializeSurfaceData(Varyings input)
{
    ToonSurfaceData output;
    half4 baseColor = GetFinalBaseColor(input);
    output.albedo = baseColor.rgb;
    output.alpha = baseColor.a;
    DoClipTestToTargetAlphaValue(output.alpha);

    // Emission: a black colour switches it off; the map defaults to white.
    output.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb * _EmissionColor.rgb;

    half occlusionTex = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, input.uv).r;
    output.occlusion = lerp(1, occlusionTex, _OcclusionStrength);
    output.smoothness = _Smoothness;
    output.metallic = _Metallic;
    return output;
}

#include "HotelToon_Lighting.hlsl"

// ---------------------------------------------------------------------------------------------- fragments
#if !defined(ToonShaderIsOutline) && !defined(ToonShaderApplyShadowBiasFix)
InputData InitializeToonInputData(Varyings input, half3 normalWS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWSAndFogFactor.xyz;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(inputData.positionWS);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.positionWSAndFogFactor.w);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

#if !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    #ifdef USE_APV_PROBE_OCCLUSION
        float4 probeOcclusion = input.probeOcclusion;
    #else
        float4 probeOcclusion = 1;
    #endif
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
    return inputData;
}

half4 ShadeFinalColor(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    ToonSurfaceData surfaceData = InitializeSurfaceData(input);

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    // Double-sided materials (cards, cloth) light their back side correctly.
    half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    normalWS = IS_FRONT_VFACE(isFrontFace, normalWS, -normalWS);

    InputData inputData = InitializeToonInputData(input, normalWS);
    half3 color = ShadeAllLights(surfaceData, inputData);
    color = MixFog(color, inputData.fogCoord);
    return half4(color, 1);
}
#endif

#ifdef ToonShaderIsOutline
half4 ShadeOutlineColor(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    ToonSurfaceData surfaceData = InitializeSurfaceData(input);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    half3 color = ShadeOutline(surfaceData, input.positionWSAndFogFactor.xyz);
    color = MixFog(color, InitializeInputDataFog(float4(input.positionWSAndFogFactor.xyz, 1.0), input.positionWSAndFogFactor.w));
    return half4(color, 1);
}
#endif

void AlphaClipAndLODTest(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    DoClipTestToTargetAlphaValue(GetFinalBaseColor(input).a);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
}

half DepthOnlyFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    AlphaClipAndLODTest(input);
    return input.positionCS.z;
}

void DepthNormalsFragment(
    Varyings input
    , FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    AlphaClipAndLODTest(input);

    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    normalWS = IS_FRONT_VFACE(isFrontFace, normalWS, -normalWS);

#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
    outNormalWS = half4(packedNormalWS, 0.0);
#else
    outNormalWS = half4(normalWS, 0.0);
#endif

#if defined(_WRITE_SMOOTHNESS)
    // URP screen space reflections read smoothness from here; scaled by the material's reflection strength.
    outNormalWS.a = _Smoothness * saturate(_ReflectionStrength);
#endif

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
