// Hotel Together toon lighting equation.
// Edit this file to change the look; everything else is plumbing.
// Started from "SimpleURPToonLitOutlineExample_LightingEquation.hlsl" by ColinLeung-NiloCat (MIT License).
#pragma once

// Smooth "staircase": n flat bands with short soft steps between them (cartoon pools of lamp light).
half SoftQuantize(half x, half bands)
{
    half y = x * bands;
    half i = floor(y);
    half f = smoothstep(0.42, 0.58, y - i);
    return (i + f) / bands;
}

// Ambient: probe / lightmap light, flattened towards its average so shapes read as flat cartoon colour.
half3 ShadeGI(ToonSurfaceData s, InputData inputData, AmbientOcclusionFactor aoFactor)
{
    half3 flatSH = max(half3(0, 0, 0), EvaluateAmbientProbeSRGB(half3(0, 0, 0)));
#if defined(LIGHTMAP_ON)
    half3 average = inputData.bakedGI; // lightmaps are already soft
#else
    half3 average = lerp(inputData.bakedGI, flatSH, 0.75);
#endif
    half3 gi = lerp(inputData.bakedGI, average, _IndirectLightFlatten);
    gi = max(gi, _IndirectLightMinColor.rgb);
    half occlusion = min(aoFactor.indirectAmbientOcclusion, s.occlusion);
    return gi * occlusion * _IndirectLightMultiplier * _ShadowColor.rgb;
}

// Small stylised highlight: a crisp disc whose size shrinks as the surface gets glossier.
half ToonSpecular(half3 N, half3 L, half3 V, half smoothness)
{
    half3 H = SafeNormalize(L + V);
    half NoH = saturate(dot(N, H));
    half size = lerp(0.10, 0.012, smoothness);
    half edge = 0.35 * size;
    half spot = smoothstep(1 - size - edge, 1 - size + edge, NoH);
    return spot * smoothstep(0.18, 0.75, smoothness);
}

// Sun / moon (main directional light): hard cel ramp, shadow map multiplies the lit side only.
half3 ShadeMainLight(ToonSurfaceData s, InputData inputData, Light light, out half3 specular, out half litMask)
{
    half3 N = inputData.normalWS;
    half NoL = dot(N, light.direction);
    half ramp = smoothstep(_CelShadeMidPoint - _CelShadeSoftness, _CelShadeMidPoint + _CelShadeSoftness, NoL);
    ramp = _IsFace > 0.5 ? lerp(0.65, 1, ramp) : ramp;
    half shadow = lerp(1, light.shadowAttenuation, _ReceiveShadowStrength);
    litMask = ramp * shadow * light.distanceAttenuation;

    half3 radiance = light.color * litMask;
    specular = radiance * ToonSpecular(N, light.direction, inputData.viewDirectionWS, s.smoothness);
    return radiance * _DirectLightMultiplier;
}

// Lamps (point / spot lights): softer cel band and stepped falloff so pools of light read as cartoon shapes.
half3 ShadeAdditionalLight(ToonSurfaceData s, InputData inputData, Light light, inout half3 specular)
{
    half3 N = inputData.normalWS;
    half NoL = dot(N, light.direction);
    half band = smoothstep(_CelShadeMidPoint - _AdditionalLightSoftness, _CelShadeMidPoint + _AdditionalLightSoftness, NoL);
    band = _IsFace > 0.5 ? lerp(0.6, 1, band) : band;

    half attenuation = min(light.distanceAttenuation, 3.0);
    if (_LightBands > 0.5 && attenuation < 1)
        attenuation = SoftQuantize(attenuation, _LightBands);

    half shadow = lerp(1, light.shadowAttenuation, _ReceiveShadowStrength);
    half3 radiance = light.color * (attenuation * band * shadow);
    specular += radiance * ToonSpecular(N, light.direction, inputData.viewDirectionWS, s.smoothness);
    return radiance * _AdditionalLightMultiplier;
}

half3 ShadeAllLights(ToonSurfaceData s, InputData inputData)
{
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, s.occlusion);
    uint meshRenderingLayers = GetMeshRenderingLayer();

    half3 indirect = ShadeGI(s, inputData, aoFactor);

    // ---- main light
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    half3 direct = 0;
    half3 specular = 0;
    half mainLit = 0;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        direct = ShadeMainLight(s, inputData, mainLight, specular, mainLit);
    }

    // ---- additional lights (Forward+ cluster loop or per-object list)
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
            direct += ShadeAdditionalLight(s, inputData, light, specular);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
            direct += ShadeAdditionalLight(s, inputData, light, specular);
    LIGHT_LOOP_END
#endif

    // ---- environment reflection: reflection probes + URP screen space reflections, with a toon fresnel
    half3 N = inputData.normalWS;
    half3 V = inputData.viewDirectionWS;
    half NoV = saturate(dot(N, V));
    half reflectivity = lerp(0.04, 1.0, s.metallic);
    half3 f0 = lerp(half3(0.04, 0.04, 0.04), s.albedo, s.metallic);
    half perceptualRoughness = 1 - s.smoothness;
    half roughness = max(perceptualRoughness * perceptualRoughness, HALF_MIN_SQRT);
    half fresnel = Pow4(1 - NoV);
    half grazing = saturate(s.smoothness + reflectivity);
    half3 envBRDF = lerp(f0, grazing.xxx, fresnel) / (roughness * roughness + 1.0);
    half3 reflectVector = reflect(-V, N);
    half3 environment = GlossyEnvironmentReflection(reflectVector, inputData.positionWS, perceptualRoughness,
                                                    min(aoFactor.indirectAmbientOcclusion, s.occlusion), inputData.normalizedScreenSpaceUV);
    half3 reflection = environment * envBRDF * _ReflectionStrength * smoothstep(0.05, 0.5, s.smoothness + s.metallic * 0.3);

    // ---- rim light: thin bright edge on the lit side, plus a faint one from the sky
    half rimBand = smoothstep(1 - _RimWidth - 0.04, 1 - _RimWidth + 0.04, 1 - NoV);
    half rimSide = saturate(dot(N, mainLight.direction) * 0.5 + 0.6);
    half3 rimLight = rimBand * _RimStrength * _RimColor.rgb * (mainLight.color * mainLit * rimSide + indirect * 1.5);

    // Metals keep most of their cartoon colour, but give some of it to reflections.
    half diffuseScale = lerp(1.0, 0.45, s.metallic);
    half3 specularColor = lerp(half3(1, 1, 1), s.albedo * 1.4 + 0.15, s.metallic);

    half3 color = s.albedo * diffuseScale * (indirect + direct + rimLight)
                + specular * specularColor * _SpecularStrength
                + reflection
                + s.emission;
    return color;
}

// Outline colour: a darker version of the surface, lit by the sun and sky only (cheap, no shadows).
half3 ShadeOutline(ToonSurfaceData s, float3 positionWS)
{
    Light mainLight = GetMainLight();
    half3 flatSH = max(half3(0, 0, 0), EvaluateAmbientProbeSRGB(half3(0, 0, 0)));
    half3 light = max(flatSH, _IndirectLightMinColor.rgb) * _IndirectLightMultiplier + mainLight.color * 0.35;
    return s.albedo * _OutlineColor.rgb * min(light, 1.5) + s.emission * 0.25;
}
