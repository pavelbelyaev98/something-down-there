#ifndef EXCAVATION_DAYLIGHT_INCLUDED
#define EXCAVATION_DAYLIGHT_INCLUDED
        TEXTURE3D(_ExcavationDaylight); SAMPLER(sampler_ExcavationDaylight);
        float4x4 _ExcavationDaylightWorldToLocal;
        float3 _ExcavationDaylightSize, _ExcavationDaylightExtent;
        float _ExcavationDaylightEnabled;

        half ExcavationAmbient(float3 position, half3 normal)
        {
            if (_ExcavationDaylightEnabled < 0.5) return 1;
            float3 p = mul(_ExcavationDaylightWorldToLocal, float4(position + normal * 0.18, 1)).xyz;
            if (p.y >= _ExcavationDaylightExtent.y || p.x < 0 || p.z < 0
                || p.x > _ExcavationDaylightExtent.x || p.z > _ExcavationDaylightExtent.z) return 1;
            float3 uv = (saturate(p / _ExcavationDaylightExtent) * (_ExcavationDaylightSize - 1) + 0.5) / _ExcavationDaylightSize;
            // No minimum fill: without a route to daylight, only local lights
            // may reveal the ground and finds.
            return SAMPLE_TEXTURE3D_LOD(_ExcavationDaylight, sampler_ExcavationDaylight, uv, 0).r;
        }

#endif
