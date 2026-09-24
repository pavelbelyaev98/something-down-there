#ifndef EXCAVATION_LIGHTING_INCLUDED
#define EXCAVATION_LIGHTING_INCLUDED

// Load URP 17.6's dependencies before redirecting its fragment main-light lookup.
// The standard BRDF, shadowing, reflections and additional-light loops stay owned
// by URP; daylight is limited by the excavated air route. Applying this to
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

// Diffused work lights have a finite source size. Bound the near-field irradiance
// smoothly instead of letting inverse-square attenuation blow out nearby soil.
// Distant falloff, range and URP shadow occlusion remain intact for both receivers.
Light ExcavationDiffuseLight(Light light)
{
    light.distanceAttenuation /= 1.0 + 4.0 * light.distanceAttenuation;
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

#define GetMainLight ExcavationMainLight
#define GetAdditionalLight ExcavationAdditionalLight
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#undef GetAdditionalLight
#undef GetMainLight

#endif
