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

// Sky light scattered down the open route. It arrives from above: floors and upward faces
// catch it, walls get about half, overhangs none, so a shaft keeps a natural top-down gradient
// instead of an even fill. Zero at the surface and faded in over the first metres. It is scaled
// by the route's daylight here and again through occlusion, so it dies out faster than direct
// daylight and never reaches sealed rooms.
half3 ExcavationBounce(float3 positionWS, half3 normalWS)
{
    if (_ExcavationDaylightEnabled < 0.5) return 0;
    float3 p = mul(_ExcavationDaylightWorldToLocal, float4(positionWS, 1)).xyz;
    if (p.x < 0 || p.z < 0 || p.x > _ExcavationDaylightExtent.x || p.z > _ExcavationDaylightExtent.z) return 0;
    half below = saturate((_ExcavationDaylightExtent.y - p.y - 0.5) / 1.5);
    half facing = saturate(0.5 + 0.5 * normalWS.y);
    return SampleSH(half3(0, 1, 0)) * (_ExcavationBounce * below * facing * ExcavationAmbient(positionWS, normalWS));
}

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

#define GetMainLight ExcavationMainLight
#define GetAdditionalLight ExcavationAdditionalLight
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#undef GetAdditionalLight
#undef GetMainLight

#endif
