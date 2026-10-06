#ifndef EXCAVATION_LIGHTING_INCLUDED
#define EXCAVATION_LIGHTING_INCLUDED

// Load URP 17.6's dependencies before redirecting its fragment main-light lookup.
// The standard BRDF, shadowing and additional-light loops stay owned by URP;
// daylight and the sky's reflection are limited by the excavated air route. Applying this to
// the final colour would incorrectly extinguish point/spot lamps underground.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "ExcavationDaylight.hlsl"

Light ExcavationMainLight(InputData inputData, half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
    Light light = GetMainLight(inputData, shadowMask, aoFactor);
    light.distanceAttenuation *= ExcavationAmbient(inputData.positionWS, inputData.normalWS);
    return light;
}

// URP's optional Meta Quest facing check requests just the light direction.
Light ExcavationMainLight() { return GetMainLight(); }

// Diffused work lights have a finite source size. Each light's irradiance saturates
// smoothly toward the same near ceiling however strong the light is, so a brighter lamp
// reaches further without blowing out nearby soil. Distant falloff, range and URP shadow
// occlusion remain intact for both receivers. Float math: near a lamp the product is large.
#define EXCAVATION_NEAR_IRRADIANCE 2.0
Light ExcavationDiffuseLight(Light light)
{
    float output = max(light.color.r, max(light.color.g, light.color.b));
    float attenuation = light.distanceAttenuation;
    light.distanceAttenuation = attenuation / (1.0 + attenuation * output / EXCAVATION_NEAR_IRRADIANCE);
    return light;
}

Light ExcavationAdditionalLight(uint index, float3 positionWS)
{
    return ExcavationDiffuseLight(GetAdditionalLight(index, positionWS));
}

Light ExcavationAdditionalLight(uint index, InputData inputData, half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
    return ExcavationDiffuseLight(GetAdditionalLight(index, inputData, shadowMask, aoFactor));
}

// The sky's reflection fades like its light (113: silver in a pit read sky blue): URP's environment reflection is
// scaled by the daylight the excavated air route lets in; the ambient light and the lamps' highlights stay.
half3 ExcavationGlobalIllumination(BRDFData brdfData, BRDFData brdfDataClearCoat, float clearCoatMask,
    half3 bakedGI, half occlusion, float3 positionWS, half3 normalWS, half3 viewDirectionWS, float2 normalizedScreenSpaceUV)
{
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    return GlobalIllumination(brdfData, brdfDataClearCoat, clearCoatMask, bakedGI, occlusion, positionWS, normalWS,
        viewDirectionWS, normalizedScreenSpaceUV);
#else
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half fresnelTerm = Pow4(1.0 - saturate(dot(normalWS, viewDirectionWS)));
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS, brdfData.perceptualRoughness, 1.0h,
        normalizedScreenSpaceUV) * ExcavationAmbient(positionWS, normalWS);
    half3 color = EnvironmentBRDF(brdfData, bakedGI, indirectSpecular, fresnelTerm);
    if (IsOnlyAOLightingFeatureEnabled()) color = half3(1, 1, 1);
    return color * occlusion;
#endif
}

#define GetMainLight ExcavationMainLight
#define GetAdditionalLight ExcavationAdditionalLight
#define GlobalIllumination ExcavationGlobalIllumination
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#undef GlobalIllumination
#undef GetAdditionalLight
#undef GetMainLight

#endif
