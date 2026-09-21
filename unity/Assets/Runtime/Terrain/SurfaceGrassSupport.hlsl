#ifndef SURFACE_GRASS_SUPPORT_INCLUDED
#define SURFACE_GRASS_SUPPORT_INCLUDED

TEXTURE2D(_SurfaceGrassSupport);
SAMPLER(sampler_SurfaceGrassSupport);
float4x4 _SurfaceGrassWorldToLocal;
float4 _SurfaceGrassSupportSize;
float _SurfaceGrassRadius, _SurfaceGrassEnabled;

void ClipExcavationGrass(float3 worldPosition)
{
    if (_SurfaceGrassEnabled < .5) return;
    float2 p = mul(_SurfaceGrassWorldToLocal, float4(worldPosition, 1)).xz;
    float2 extent = _SurfaceGrassSupportSize.zw;
    clip(min(min(p.x, p.y), min(extent.x - p.x, extent.y - p.y)));
    if (_SurfaceGrassRadius > 0)
        clip(_SurfaceGrassRadius - length(p - extent * .5));
    // Match the CPU density samples, including half-texel centres. Evaluate the
    // actual displaced fragment, so wind cannot put a leaf back over the hole.
    float2 uv = (p / extent * (_SurfaceGrassSupportSize.xy - 1) + .5) / _SurfaceGrassSupportSize.xy;
    clip(SAMPLE_TEXTURE2D_LOD(_SurfaceGrassSupport, sampler_SurfaceGrassSupport, uv, 0).r);
}

#endif
