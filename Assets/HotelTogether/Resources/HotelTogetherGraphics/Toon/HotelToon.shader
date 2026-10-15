// Hotel Together toon shader.
// Based on "SimpleURPToonLitOutlineExample" by ColinLeung-NiloCat (MIT License, see LICENSE-NiloCat-ToonShaderExample.txt)
// https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
//
// Changes for Hotel Together (Unity 6.6 / URP 17.6, Forward+):
// - Forward+ cluster light loop (_CLUSTER_LIGHT_LOOP) so lamps and spot lights light toon surfaces.
// - Full URP shadow/shadow-mask/SSAO/light-layer/light-cookie inputs through InputData.
// - Ambient from light probes / lightmaps (flattened for a cartoon look) instead of a constant.
// - Banded lamp falloff, stylised specular highlight, rim light, reflection probes + screen space reflections.
// - Outline can use smoothed normals stored in UV8 (TEXCOORD7) so hard-edged props get unbroken outlines.
// - Depth-normals pass writes smoothness for URP screen space reflections.
// The materials are created at runtime by HotelToonShading.cs from the game's URP Lit / glTF materials.
Shader "Hotel Together/Toon"
{
    Properties
    {
        [Header(Base Color)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [HDR][MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("    Cutoff", Range(0.0, 1.0)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face", Float) = 2
        [ToggleUI] _IsFace("Is Face? (eyes/mouth)", Float) = 0

        [Header(Cel Shading)]
        _CelShadeMidPoint("Mid Point", Range(-1,1)) = -0.05
        _CelShadeSoftness("Softness", Range(0,1)) = 0.06
        _DirectLightMultiplier("Sun Brightness", Range(0,2)) = 0.95
        _ReceiveShadowStrength("Receive Shadows", Range(0,1)) = 0.9
        _ShadowColor("Shadow Tint", Color) = (0.86,0.84,0.97,1)

        [Header(Lamps)]
        _AdditionalLightMultiplier("Lamp Brightness", Range(0,2)) = 0.9
        _AdditionalLightSoftness("Lamp Softness", Range(0,1)) = 0.3
        _LightBands("Lamp Falloff Bands (0 = smooth)", Range(0,6)) = 3

        [Header(Ambient)]
        _IndirectLightMultiplier("Ambient", Range(0,2)) = 1
        _IndirectLightFlatten("Flatten Ambient", Range(0,1)) = 0.5
        _IndirectLightMinColor("Ambient Floor", Color) = (0.06,0.06,0.08,1)

        [Header(Surface)]
        _Smoothness("Smoothness", Range(0,1)) = 0.3
        _Metallic("Metallic", Range(0,1)) = 0
        _SpecularStrength("Toon Highlight", Range(0,2)) = 0.7
        _ReflectionStrength("Reflections", Range(0,2)) = 1

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1,0.95,0.86,1)
        _RimStrength("Rim Strength", Range(0,2)) = 0.2
        _RimWidth("Rim Width", Range(0,1)) = 0.28

        [Header(Emission)]
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,1)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}

        [Header(Occlusion)]
        [NoScaleOffset] _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 0

        [Header(Outline)]
        _OutlineWidth("Width", Range(0,4)) = 1
        _OutlineColor("Color (multiplies surface)", Color) = (0.32,0.25,0.28,1)
        _OutlineZOffset("Z Offset (view space)", Range(0,1)) = 0.0001
        [ToggleUI] _OutlineSmoothNormals("Use Smoothed Normals (UV8)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "ComplexLit"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #pragma shader_feature_local_fragment _ALPHATEST_ON
        ENDHLSL

        // ------------------------------------------------------------------ toon surface
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertexShaderWork
            #pragma fragment ShadeFinalColor

            // Universal Pipeline keywords (URP 17.6 Lit.shader)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_REFLECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "HotelToon_Shared.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ inverted hull outline
        // No LightMode tag: URP draws it as "SRPDefaultUnlit" in the opaque queue.
        // HotelToonShading turns this pass off (SetShaderPassEnabled) for walls, floors and other large surfaces.
        Pass
        {
            Name "Outline"

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertexShaderWork
            #pragma fragment ShadeOutlineColor

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #define ToonShaderIsOutline
            #include "HotelToon_Shared.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertexShaderWork
            #pragma fragment AlphaClipAndLODTest

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #define ToonShaderApplyShadowBiasFix
            #include "HotelToon_Shared.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ depth prepass (outline included)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertexShaderWork
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "HotelToon_Shared.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------ depth + normals (SSAO, SSR)
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertexShaderWork
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _WRITE_SMOOTHNESS
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "HotelToon_Shared.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
