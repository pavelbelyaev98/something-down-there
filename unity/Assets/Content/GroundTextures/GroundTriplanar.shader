Shader "Something Down There/Ground Triplanar"
{
    Properties
    {
        _SoilAlbedo("Soil colour", 2D) = "white" {}
        [Normal] _SoilNormal("Soil normal", 2D) = "bump" {}
        _SoilRoughness("Soil mask (see mask layout)", 2D) = "white" {}
        _SoilTint("Soil tint", Color) = (1,1,1,1)
        _ClayAlbedo("Clay colour", 2D) = "white" {}
        [Normal] _ClayNormal("Clay normal", 2D) = "bump" {}
        _ClayMask("Clay occlusion (G)", 2D) = "white" {}
        _ClayTint("Clay tint", Color) = (1,1,1,1)
        _ClayTileMetres("Clay tile metres", Float) = 3
        _ClayNormalStrength("Clay relief", Range(0, 2)) = 0.25
        _RockAlbedo("Rock colour", 2D) = "white" {}
        [Normal] _RockNormal("Rock normal", 2D) = "bump" {}
        _RockMask("Rock occlusion (G)", 2D) = "white" {}
        _RockTint("Rock tint", Color) = (0.4,0.4,0.4,1)
        _RockTileMetres("Rock tile metres", Float) = 3
        _RockNormalStrength("Rock relief", Range(0, 2)) = 0.65
        _GravelAlbedo("Gravel colour", 2D) = "white" {}
        [Normal] _GravelNormal("Gravel normal", 2D) = "bump" {}
        _GravelMask("Gravel occlusion (G)", 2D) = "white" {}
        _GravelTint("Gravel tint", Color) = (1,1,1,1)
        _GravelTileMetres("Gravel tile metres", Float) = 2
        _GravelNormalStrength("Gravel relief", Range(0, 2)) = 0.5
        _ConcreteAlbedo("Concrete colour", 2D) = "white" {}
        [Normal] _ConcreteNormal("Concrete normal", 2D) = "bump" {}
        _ConcreteMask("Concrete occlusion (G)", 2D) = "white" {}
        _ConcreteTint("Concrete tint", Color) = (1,1,1,1)
        _ConcreteTileMetres("Concrete tile metres", Float) = 2
        _ConcreteNormalStrength("Concrete relief", Range(0, 2)) = 0.35
        _PondClayTint("Pond clay tint (clay textures)", Color) = (0.6,0.66,0.72,1)
        _PondClayTileMetres("Pond clay tile metres", Float) = 3.5
        _PondClayNormalStrength("Pond clay relief", Range(0, 2)) = 0.12
        _ClayDeepTint("Clay tint below the rock-zone border", Color) = (1,1,1,1)
        _RockColdTint("Rock tint below the ancient-zone border", Color) = (1,1,1,1)
        _ZoneDepths("Zone borders (rock, ancient) and blend half-width in metres", Vector) = (75,112.5,3,0)
        _StrataStrength("Colour band strength", Range(0, 0.3)) = 0
        _StrataCool("Grey-blue share of clay bands", Range(0, 1)) = 0
        _FractureTileMetres("Fractured grain tile metres", Float) = 0.9
        _FractureShardMetres("Fractured shard size metres", Float) = 0.16
        _FractureLift("Fractured band paleness", Range(0, 1)) = 0.35
        _CrackColour("Crack line mineral colour", Color) = (0.86, 0.84, 0.78, 1)
        _BackfillTint("Backfill loose fill tint (gravel textures)", Color) = (0.62,0.5,0.4,1)
        _BackfillChunkMetres("Backfill chunk size", Float) = 0.4
        _TurfAlbedo("Turf colour", 2D) = "white" {}
        [Normal] _TurfNormal("Turf normal", 2D) = "bump" {}
        _TurfRoughness("Turf mask (see mask layout)", 2D) = "white" {}
        _TurfTint("Surface cap tint", Color) = (1,1,1,1)
        _BandAlbedo("Surrounding band colour", 2D) = "white" {}
        [Normal] _BandNormal("Surrounding band normal", 2D) = "bump" {}
        _BandMask("Surrounding band mask", 2D) = "white" {}
        _BandTint("Surrounding band tint", Color) = (1,1,1,1)
        _BandTileMetres("Surrounding band tile metres", Float) = 20
        _BandNormalStrength("Surrounding band relief", Float) = 1
        _BandMaskMin("Surrounding band mask minimum", Vector) = (0,0,0,0)
        _BandMaskMax("Surrounding band mask maximum", Vector) = (1,1,1,1)
        _DigEdge("Band share beyond the dig plot outline", 2D) = "black" {}
        _DigEdgeRect("Edge map origin (xy) and inverse size (zw)", Vector) = (0,0,1,1)
        [Toggle] _BandBlend("Blend the cap into the surrounding band", Float) = 0
        _CapGrain("Cap close-up grain from the soil", Range(0, 1)) = 0
        _RimMix("Depth the cap fades over across a hole's rounded mouth", Float) = 0
        _CapGrainMetres("Cap grain tile metres", Float) = 2
        [Enum(RoughnessContactStone,0,MetallicOcclusionSmoothness,1)] _MaskLayout("Mask layout", Float) = 0
        [Enum(RoughnessContactStone,0,MetallicOcclusionSmoothness,1)] _TurfMaskLayout("Turf mask layout", Float) = 0
        _MaxSmoothness("Dry ground maximum smoothness", Range(0, 1)) = 0.15
        _TileMetres("Turf tile metres", Float) = 1
        _SoilTileMetres("Soil tile metres", Float) = 2
        _NormalStrength("Soil relief", Range(0, 2)) = 0.8
        _StoneNormalStrength("Embedded stone relief", Range(0, 2)) = 0.85
        _SoilOcclusion("Soil texture occlusion", Range(0, 1)) = 1
        _TurfNormalStrength("Turf relief", Range(0, 2)) = 0.45
        _SurfaceHeight("Original surface height", Float) = 0
        _TurfDepth("Turf transition depth", Range(0.001, 0.1)) = 0.045
        _MacroVariation("Broad colour variation", Range(0, 0.4)) = 0.12
        [HideInInspector] _GroundOpacity("Ground opacity", Float) = 1
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _TileMetres;
            float _SoilTileMetres;
            float _NormalStrength;
            float _StoneNormalStrength;
            float _SoilOcclusion;
            float _TurfNormalStrength;
            float _SurfaceHeight;
            float _TurfDepth;
            float _MacroVariation;
            float _MaskLayout;
            float _TurfMaskLayout;
            float _MaxSmoothness;
            float _GroundOpacity;
            float4 _ClayTint, _RockTint, _TurfTint, _SoilTint;
            float4 _BandTint, _BandMaskMin, _BandMaskMax, _DigEdgeRect;
            float _BandTileMetres, _BandNormalStrength, _BandBlend, _CapGrain, _CapGrainMetres, _RimMix;
            float _ClayTileMetres, _RockTileMetres;
            float _ClayNormalStrength, _RockNormalStrength;
            float4 _GravelTint, _ConcreteTint;
            float _GravelTileMetres, _ConcreteTileMetres, _GravelNormalStrength, _ConcreteNormalStrength;
            float4 _PondClayTint, _ClayDeepTint, _RockColdTint, _ZoneDepths;
            float _PondClayTileMetres, _PondClayNormalStrength, _StrataStrength, _StrataCool;
            float _FractureTileMetres, _FractureShardMetres, _FractureLift;
            half4 _CrackColour;
            float4 _BackfillTint;
            float _BackfillChunkMetres;
        CBUFFER_END
        TEXTURE2D(_SoilAlbedo); SAMPLER(sampler_SoilAlbedo);
        TEXTURE2D(_SoilNormal); SAMPLER(sampler_SoilNormal);
        TEXTURE2D(_SoilRoughness); SAMPLER(sampler_SoilRoughness);
        TEXTURE2D(_TurfAlbedo); SAMPLER(sampler_TurfAlbedo);
        TEXTURE2D(_TurfNormal); SAMPLER(sampler_TurfNormal);
        TEXTURE2D(_TurfRoughness); SAMPLER(sampler_TurfRoughness);
        // The band samples the terrain layer's own textures with their own filtering.
        TEXTURE2D(_BandAlbedo); SAMPLER(sampler_BandAlbedo);
        TEXTURE2D(_BandNormal); TEXTURE2D(_BandMask);
        TEXTURE2D(_DigEdge); SAMPLER(sampler_DigEdge);
        // Identical repeat/trilinear imports share sampler states across layers.
        TEXTURE2D(_ClayAlbedo); TEXTURE2D(_ClayNormal); TEXTURE2D(_ClayMask);
        TEXTURE2D(_RockAlbedo); TEXTURE2D(_RockNormal); TEXTURE2D(_RockMask);
        TEXTURE2D(_GravelAlbedo); TEXTURE2D(_GravelNormal); TEXTURE2D(_GravelMask);
        TEXTURE2D(_ConcreteAlbedo); TEXTURE2D(_ConcreteNormal); TEXTURE2D(_ConcreteMask);
        #include "../../Runtime/Terrain/ExcavationDaylight.hlsl"

        struct GroundAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 materials : TEXCOORD2;
            float4 materials2 : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct GroundVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half fogFactor : TEXCOORD2;
            half4 materials : TEXCOORD3;
            half4 materials2 : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        GroundVaryings GroundVertex(GroundAttributes input)
        {
            GroundVaryings output = (GroundVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            output.materials = input.materials;
            output.materials2 = input.materials2;
            return output;
        }

        // Derivatives come from continuous world position, never the changing
        // projection sign. Sign boundaries must not select an unrelated coarse mip.
        #define GROUND_SAMPLE(tex, uv, dx, dy) SAMPLE_TEXTURE2D_GRAD(tex, sampler##tex, uv, dx, dy)

        #define SOIL_SAMPLE(channel, uv, dx, dy) SAMPLE_TEXTURE2D_GRAD(_Soil##channel, sampler_Soil##channel, uv, dx, dy)

        half3 DecodeGroundMask(half4 mask, float layout)
        {
            // Original masks: roughness/contact/stone coverage in RGB.
            // BK masks: metallic/occlusion/smoothness in R/G/A. Ground stays
            // non-metallic, and BK's B channel is not a stone-coverage mask.
            return lerp(mask.rgb, half3(1 - mask.a, mask.g, 0), layout);
        }

        half3 ProjectGroundNormal(half3 n, half3 weights, half3 axisSign,
            half3 nx, half3 ny, half3 nz)
        {
            half3 dx = half3(0, nx.y, nx.x * axisSign.x) / max(nx.z, 0.25);
            half3 dy = half3(ny.x * axisSign.y, 0, ny.y) / max(ny.z, 0.25);
            half3 dz = half3(-nz.x * axisSign.z, nz.y, 0) / max(nz.z, 0.25);
            half3 detail = dx * weights.x + dy * weights.y + dz * weights.z;
            return normalize(n + detail - n * dot(detail, n));
        }

        void SoilSurface(float3 position, half3 geometricNormal, float3 positionDx, float3 positionDy,
            out half3 colour, out half3 normal, out half roughness, out half occlusion)
        {
            half3 n = normalize(geometricNormal);
            half3 axis = abs(n);
            half bestAxis = max(axis.x, max(axis.y, axis.z));
            // Exclude stretched grazing projections from the soil blend.
            half3 weights = smoothstep(bestAxis - 0.16, bestAxis, axis);
            weights /= max(dot(weights, 1.0), 0.0001);
            float soilTileMetres = max(_SoilTileMetres, 0.05);
            float maskLayout = _MaskLayout;
            float normalStrength = _NormalStrength;
            float stoneNormalStrength = _StoneNormalStrength;
            float3 p = position / soilTileMetres;
            float3 pDx = positionDx / soilTileMetres;
            float3 pDy = positionDy / soilTileMetres;
            // One world-space origin across chunks and rim meshes. No mesh UVs
            // or tangents: fresh vertical cuts keep the same physical texel scale.
            half3 axisSign = half3(n.x < 0 ? -1 : 1, n.y < 0 ? -1 : 1, n.z < 0 ? -1 : 1);
            float2 uvX = float2(p.z * axisSign.x, p.y);
            float2 uvY = float2(p.x * axisSign.y, p.z);
            float2 uvZ = float2(-p.x * axisSign.z, p.y);
            float2 dxX = float2(pDx.z * axisSign.x, pDx.y), dyX = float2(pDy.z * axisSign.x, pDy.y);
            float2 dxY = float2(pDx.x * axisSign.y, pDx.z), dyY = float2(pDy.x * axisSign.y, pDy.z);
            float2 dxZ = float2(-pDx.x * axisSign.z, pDx.y), dyZ = float2(-pDy.x * axisSign.z, pDy.y);
            // Only projections that can carry weight are sampled. An ineligible one has zero
            // blend weight, mineral priority and stone coverage, so flat ground and straight
            // walls skip two of three texture sets with an unchanged result.
            float3 eligible = step(bestAxis - 0.16, axis);
            half3 cx = 0, cy = 0, cz = 0, maskX = 0, maskY = 0, maskZ = 0;
            half3 nx = half3(0, 0, 1), ny = half3(0, 0, 1), nz = half3(0, 0, 1);
            // Authored B coverage gives stones their own relief without
            // amplifying the accepted soil grain or adding texture lookups.
            [branch] if (eligible.x > 0)
            {
                cx = SOIL_SAMPLE(Albedo, uvX, dxX, dyX).rgb;
                maskX = DecodeGroundMask(SOIL_SAMPLE(Roughness, uvX, dxX, dyX), maskLayout);
                nx = UnpackNormalScale(SOIL_SAMPLE(Normal, uvX, dxX, dyX), lerp(normalStrength, stoneNormalStrength, maskX.b));
            }
            [branch] if (eligible.y > 0)
            {
                cy = SOIL_SAMPLE(Albedo, uvY, dxY, dyY).rgb;
                maskY = DecodeGroundMask(SOIL_SAMPLE(Roughness, uvY, dxY, dyY), maskLayout);
                ny = UnpackNormalScale(SOIL_SAMPLE(Normal, uvY, dxY, dyY), lerp(normalStrength, stoneNormalStrength, maskY.b));
            }
            [branch] if (eligible.z > 0)
            {
                cz = SOIL_SAMPLE(Albedo, uvZ, dxZ, dyZ).rgb;
                maskZ = DecodeGroundMask(SOIL_SAMPLE(Roughness, uvZ, dxZ, dyZ), maskLayout);
                nz = UnpackNormalScale(SOIL_SAMPLE(Normal, uvZ, dxZ, dyZ), lerp(normalStrength, stoneNormalStrength, maskZ.b));
            }
            // Coverage is a material boundary, not a transparent overlay. The
            // former multiplicative weighting still diluted whole stone faces
            // with another plane's dirt. Height-select one coherent material
            // through overlaps; reserve blending for its narrow filtered edge.
            float3 stoneCoverage = float3(maskX.b, maskY.b, maskZ.b);
            float3 priority = axis + stoneCoverage * 0.6 - (1 - eligible) * 2;
            float highest = max(priority.x, max(priority.y, priority.z));
            float blendWidth = clamp(fwidth(highest), 0.008, 0.025);
            float3 mineralWeights = max(priority - highest + blendWidth, 0);
            mineralWeights /= max(dot(mineralWeights, 1.0), 0.0001);
            float3 covered = stoneCoverage * eligible;
            float mineral = smoothstep(0.15, 0.65, max(covered.x, max(covered.y, covered.z)));
            weights = lerp(weights, mineralWeights, mineral);
            colour = (cx * weights.x + cy * weights.y + cz * weights.z) * _SoilTint.rgb;
            // Surface-gradient projection: a flat normal map reproduces the
            // density-gradient mesh normal exactly, including blended slopes.
            normal = ProjectGroundNormal(n, weights, axisSign, nx, ny, nz);
            half2 soilMask = maskX.rg * weights.x + maskY.rg * weights.y + maskZ.rg * weights.z;
            roughness = soilMask.r;
            // The texture's baked occlusion, scaled: under the daylight fill a full-strength pack map
            // reads as dark blotches across a dug wall.
            occlusion = lerp(1, soilMask.g, _SoilOcclusion);
            // Soil variation stays below the continuous surface cap.
            float2 macroUV = (position.xz + position.y * float2(0.37, 0.23)) * 0.073;
            float2 macroDx = (positionDx.xz + positionDx.y * float2(0.37, 0.23)) * 0.073;
            float2 macroDy = (positionDy.xz + positionDy.y * float2(0.37, 0.23)) * 0.073;
            half macro = SOIL_SAMPLE(Albedo, macroUV, macroDx, macroDy).r;
            colour *= 1 + (macro - 0.47) * _MacroVariation * 3;

            // Feather the surface cap (the lakebed sediment, "turf" below) into the
            // exposed soil over a shallow collar. A nearly
            // binary cutoff at the flat surface traced individual mesh triangles.
            float depth = max(0, _SurfaceHeight - position.y);
            // The cap stays on one continuous top projection across the rim.
            // Switching to a wall projection near tilted mesh normals produced
            // unrelated dark polygonal patches on the otherwise intact surface.
            float edgeWidth = max(_TurfDepth * 0.45, (abs(positionDx.y) + abs(positionDy.y)) * 0.65);
            if (depth < max(_TurfDepth * 1.5, _RimMix) + 0.02)
            {
                half3 grass, gy;
                half2 turfMask;
                [branch] if (_BandBlend > 0.5)
                {
                    // The cap is the terrain's own mud: the plot keeps the cap tint, and beyond the
                    // outline the permanent collar lightens toward the damp band with the share the
                    // terrain paint uses (LakebedSiteSetup.DigBandShare). Same texture, relief, mask
                    // remap and world mapping as the terrain layers, so collar and terrain join
                    // without a texture line.
                    half band = SAMPLE_TEXTURE2D_LOD(_DigEdge, sampler_DigEdge, (position.xz - _DigEdgeRect.xy) * _DigEdgeRect.zw, 0).r;
                    float bandScale = 1 / max(_BandTileMetres, 0.05);
                    float2 bandUV = position.xz * bandScale, bandDx = positionDx.xz * bandScale, bandDy = positionDy.xz * bandScale;
                    grass = SAMPLE_TEXTURE2D_GRAD(_BandAlbedo, sampler_BandAlbedo, bandUV, bandDx, bandDy).rgb * lerp(_TurfTint.rgb, _BandTint.rgb, band);
                    gy = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_BandNormal, sampler_BandAlbedo, bandUV, bandDx, bandDy), _BandNormalStrength);
                    half4 bandMask = SAMPLE_TEXTURE2D_GRAD(_BandMask, sampler_BandAlbedo, bandUV, bandDx, bandDy) * (_BandMaskMax - _BandMaskMin) + _BandMaskMin;
                    turfMask = half2(1 - bandMask.a, bandMask.g);
                    // The terrain layer is soft painted mud mapped over 20 m: up close it was a blurry
                    // smear beside the crisp dug soil (user, 2026-10-03). The soil's own grain, as a
                    // brightness ratio (its average is 1, so the cap keeps its colour and from afar
                    // nothing changes), makes the cap read as the top of that soil; it fades out
                    // across the collar to meet the terrain unchanged.
                    [branch] if (_CapGrain > 0)
                    {
                        float grainScale = 1 / max(_CapGrainMetres, 0.05);
                        float2 grainUV = position.zx * grainScale, grainDx = positionDx.zx * grainScale, grainDy = positionDy.zx * grainScale;
                        half3 luminance = half3(0.3, 0.59, 0.11);
                        half mean = dot(SAMPLE_TEXTURE2D_LOD(_SoilAlbedo, sampler_SoilAlbedo, float2(0.5, 0.5), 16).rgb, luminance);
                        half fine = dot(SOIL_SAMPLE(Albedo, grainUV, grainDx, grainDy).rgb, luminance);
                        half grain = _CapGrain * (1 - band);
                        grass *= lerp(1, fine / max(mean, 0.01), grain);
                        half3 grainNormal = UnpackNormalScale(SOIL_SAMPLE(Normal, grainUV, grainDx, grainDy), _NormalStrength);
                        gy = normalize(half3(gy.xy + grainNormal.yx * grain, gy.z));
                    }
                }
                else
                {
                    float turfScale = soilTileMetres / max(_TileMetres, 0.05);
                    grass = GROUND_SAMPLE(_TurfAlbedo, uvY * turfScale, dxY * turfScale, dyY * turfScale).rgb * _TurfTint.rgb;
                    gy = UnpackNormalScale(GROUND_SAMPLE(_TurfNormal, uvY * turfScale, dxY * turfScale, dyY * turfScale), _TurfNormalStrength);
                    turfMask = DecodeGroundMask(GROUND_SAMPLE(_TurfRoughness, uvY * turfScale, dxY * turfScale, dyY * turfScale), _TurfMaskLayout).rg;
                    turfMask.g = lerp(0.65, 1, turfMask.g);
                }
                half leaf = saturate((grass.g - grass.r * 0.7) * 3.5);
                half drift = GROUND_SAMPLE(_TurfAlbedo, position.xz * 0.61, positionDx.xz * 0.61, positionDy.xz * 0.61).r;
                float fringeDepth = _TurfDepth * (0.75 + leaf * 0.15 + drift * 0.1);
                // Texture variation stays within the blend so it breaks up the
                // contour without punching holes in the untouched flat surface.
                float edge = depth - fringeDepth;
                // The cap is the top face only: projected from above onto a rim's steep step
                // face it smeared down it and read as a thick slab (user, 2026-10-03). A hole's
                // rounded mouth passes, and the cap fades into the soil across it.
                half flat = smoothstep(0.35, 0.65, n.y);
                half turf = _RimMix > 0
                    ? 1 - smoothstep(_RimMix * 0.2, max(_RimMix, fringeDepth), depth + (drift - 0.5) * 0.05)
                    : 1 - smoothstep(-edgeWidth, edgeWidth, edge);
                turf *= flat;
                // The thin cap fringe shares the surface lighting. Following the
                // steep soil normal here draws a dark polygonal outline on each cut.
                half3 turfNormal = half3(0, 1, 0);
                normal = normalize(lerp(normal, ProjectGroundNormal(turfNormal, half3(0, 1, 0), axisSign, gy, gy, gy), turf));
                colour = lerp(colour, grass, turf);
                roughness = lerp(roughness, turfMask.r, turf);
                occlusion = lerp(occlusion, turfMask.g, turf);
            }
        }
        void DepositSurface(TEXTURE2D_PARAM(albedoMap, albedoSampler),
            TEXTURE2D_PARAM(normalMap, normalSampler), TEXTURE2D_PARAM(maskMap, maskSampler),
            float3 position, float3 positionDx, float3 positionDy, half3 n,
            float tileMetres, half3 tint, half strength, out half3 colour, out half3 normal, out half occlusion)
        {
            float scale = 1 / max(tileMetres, 0.05);
            float3 p = position * scale, dx = positionDx * scale, dy = positionDy * scale;
            half3 axis = abs(n), signs = half3(n.x < 0 ? -1 : 1, n.y < 0 ? -1 : 1, n.z < 0 ? -1 : 1);
            half best = max(axis.x, max(axis.y, axis.z));
            half3 weights = smoothstep(best - 0.16, best, axis);
            weights /= max(dot(weights, 1.0), 0.0001);
            float2 uvX = float2(p.z * signs.x, p.y), dxX = float2(dx.z * signs.x, dx.y), dyX = float2(dy.z * signs.x, dy.y);
            float2 uvY = float2(p.x * signs.y, p.z), dxY = float2(dx.x * signs.y, dx.z), dyY = float2(dy.x * signs.y, dy.z);
            float2 uvZ = float2(-p.x * signs.z, p.y), dxZ = float2(-dx.x * signs.z, dx.y), dyZ = float2(-dy.x * signs.z, dy.y);
            // Zero-weight projections contribute nothing; skip their lookups.
            colour = 0; occlusion = 0;
            half3 nx = half3(0, 0, 1), ny = half3(0, 0, 1), nz = half3(0, 0, 1);
            [branch] if (weights.x > 0)
            {
                colour += SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, uvX, dxX, dyX).rgb * weights.x;
                nx = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uvX, dxX, dyX), strength);
                occlusion += SAMPLE_TEXTURE2D_GRAD(maskMap, maskSampler, uvX, dxX, dyX).g * weights.x;
            }
            [branch] if (weights.y > 0)
            {
                colour += SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, uvY, dxY, dyY).rgb * weights.y;
                ny = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uvY, dxY, dyY), strength);
                occlusion += SAMPLE_TEXTURE2D_GRAD(maskMap, maskSampler, uvY, dxY, dyY).g * weights.y;
            }
            [branch] if (weights.z > 0)
            {
                colour += SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, uvZ, dxZ, dyZ).rgb * weights.z;
                nz = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uvZ, dxZ, dyZ), strength);
                occlusion += SAMPLE_TEXTURE2D_GRAD(maskMap, maskSampler, uvZ, dxZ, dyZ).g * weights.z;
            }
            colour *= tint;
            normal = ProjectGroundNormal(n, weights, signs, nx, ny, nz);
        }

        // Mesh weights (clay, rock, concrete, 1 - gravel) and (pond clay, ...); soil is the
        // remainder. A missing stream reads (0,0,0,1), so meshes without weights render as soil.
        void GroundSurface(float3 position, half3 geometricNormal, half4 materials, half4 materials2,
            out half3 colour, out half3 normal, out half roughness, out half occlusion)
        {
            half4 deposits = saturate(half4(materials.xyz, 1 - materials.w)); // clay, rock, concrete, gravel
            half pond = saturate(materials2.x);
            half3 weights = half3(saturate(1 - dot(deposits, 1.0) - pond), deposits.xy);
            half total = max(dot(weights, 1.0) + deposits.z + deposits.w + pond, 0.0001);
            weights /= total; deposits.zw /= total; pond /= total;
            // Zone palettes follow depth: clay turns rust-red in the deep stone, rock cools in the
            // ancient zone. The generated borders undulate; a soft blend covers them.
            float depth = _SurfaceHeight - position.y;
            half deepClay = smoothstep(_ZoneDepths.x - _ZoneDepths.z, _ZoneDepths.x + _ZoneDepths.z, depth);
            half coldRock = smoothstep(_ZoneDepths.y - _ZoneDepths.z, _ZoneDepths.y + _ZoneDepths.z, depth);
            half3 n = normalize(geometricNormal);
            // Calculate gradients before the layer branches so boundary pixels keep stable mip levels.
            float3 dx = ddx(position), dy = ddy(position);
            colour = 0; normal = 0; roughness = 0; occlusion = 0;
            half3 layerColour, layerNormal; half layerRoughness, layerOcclusion;
            [branch] if (weights.x > 0.0001)
            {
                SoilSurface(position, geometricNormal, dx, dy, layerColour, layerNormal, layerRoughness, layerOcclusion);
                colour += layerColour * weights.x; normal += layerNormal * weights.x;
                roughness += layerRoughness * weights.x; occlusion += layerOcclusion * weights.x;
            }
            [branch] if (weights.y > 0.0001)
            {
                DepositSurface(TEXTURE2D_ARGS(_ClayAlbedo, sampler_SoilAlbedo),
                    TEXTURE2D_ARGS(_ClayNormal, sampler_SoilNormal), TEXTURE2D_ARGS(_ClayMask, sampler_SoilRoughness),
                    position, dx, dy, n, _ClayTileMetres, _ClayTint.rgb * lerp((half3)1, _ClayDeepTint.rgb, deepClay), _ClayNormalStrength,
                    layerColour, layerNormal, layerOcclusion);
                colour += layerColour * weights.y; normal += layerNormal * weights.y;
                roughness += weights.y; occlusion += layerOcclusion * weights.y;
            }
            [branch] if (weights.z > 0.0001)
            {
                DepositSurface(TEXTURE2D_ARGS(_RockAlbedo, sampler_SoilAlbedo),
                    TEXTURE2D_ARGS(_RockNormal, sampler_SoilNormal), TEXTURE2D_ARGS(_RockMask, sampler_SoilRoughness),
                    position, dx, dy, n, _RockTileMetres, _RockTint.rgb * lerp((half3)1, _RockColdTint.rgb, coldRock), _RockNormalStrength,
                    layerColour, layerNormal, layerOcclusion);
                colour += layerColour * weights.z; normal += layerNormal * weights.z;
                roughness += weights.z; occlusion += layerOcclusion * weights.z;
            }
            [branch] if (deposits.z > 0.0001)
            {
                DepositSurface(TEXTURE2D_ARGS(_ConcreteAlbedo, sampler_SoilAlbedo),
                    TEXTURE2D_ARGS(_ConcreteNormal, sampler_SoilNormal), TEXTURE2D_ARGS(_ConcreteMask, sampler_SoilRoughness),
                    position, dx, dy, n, _ConcreteTileMetres, _ConcreteTint.rgb, _ConcreteNormalStrength,
                    layerColour, layerNormal, layerOcclusion);
                colour += layerColour * deposits.z; normal += layerNormal * deposits.z;
                roughness += deposits.z; occlusion += layerOcclusion * deposits.z;
            }
            [branch] if (deposits.w > 0.0001)
            {
                DepositSurface(TEXTURE2D_ARGS(_GravelAlbedo, sampler_SoilAlbedo),
                    TEXTURE2D_ARGS(_GravelNormal, sampler_SoilNormal), TEXTURE2D_ARGS(_GravelMask, sampler_SoilRoughness),
                    position, dx, dy, n, _GravelTileMetres, _GravelTint.rgb, _GravelNormalStrength,
                    layerColour, layerNormal, layerOcclusion);
                colour += layerColour * deposits.w; normal += layerNormal * deposits.w;
                roughness += deposits.w; occlusion += layerOcclusion * deposits.w;
            }
            [branch] if (pond > 0.0001)
            {
                // Old pond clay: the clay textures, smoother and grey-blue.
                DepositSurface(TEXTURE2D_ARGS(_ClayAlbedo, sampler_SoilAlbedo),
                    TEXTURE2D_ARGS(_ClayNormal, sampler_SoilNormal), TEXTURE2D_ARGS(_ClayMask, sampler_SoilRoughness),
                    position, dx, dy, n, _PondClayTileMetres, _PondClayTint.rgb, _PondClayNormalStrength,
                    layerColour, layerNormal, layerOcclusion);
                colour += layerColour * pond; normal += layerNormal * pond;
                roughness += pond; occlusion += layerOcclusion * pond;
            }
            normal = normalize(normal);
            // Colour bands: gentle strata on a slightly undulating depth, so one main ground never
            // reads as one repeated wall. Smooth sums of sines, never a sawtooth; the cap keeps its look.
            float undulation = sin(position.x * 0.21 + position.z * 0.13) * 0.6 + sin(position.z * 0.17 - position.x * 0.07) * 0.4;
            float strataDepth = depth + undulation;
            half strata = sin(strataDepth * 2.3) * 0.5 + sin(strataDepth * 0.83 + 1.7) * 0.35 + sin(strataDepth * 5.9 + 0.4) * 0.15;
            half below = saturate((depth - _TurfDepth) * 4);
            // Old sediment is layered: the clay's darker bands turn grey-blue between orange ones.
            colour = lerp(colour, colour * half3(0.72, 0.86, 1.05), saturate(-strata) * _StrataCool * weights.y * below);
            colour *= 1 + strata * _StrataStrength * below;
            // Cracks read by line and grain, not colour alone (the rock's own texture is full of dark
            // hairlines): the band beside a crack is paler, broken into angular shards that catch the
            // light differently; the crack itself is a pale mineral-filled line with thin dark edges.
            // Backfill reads by grain: blocky chunks of clay among dark loose stones break the banding.
            half backfill = saturate(1 - materials2.w);
            [branch] if (backfill > 0.001)
            {
                half3 stones, stonesNormal, lumps, lumpsNormal; half stonesOcclusion, lumpsOcclusion;
                DepositSurface(TEXTURE2D_ARGS(_GravelAlbedo, sampler_SoilAlbedo),
                    TEXTURE2D_ARGS(_GravelNormal, sampler_SoilNormal), TEXTURE2D_ARGS(_GravelMask, sampler_SoilRoughness),
                    position, dx, dy, n, _GravelTileMetres * 1.6, _BackfillTint.rgb, _GravelNormalStrength,
                    stones, stonesNormal, stonesOcclusion);
                DepositSurface(TEXTURE2D_ARGS(_ClayAlbedo, sampler_SoilAlbedo),
                    TEXTURE2D_ARGS(_ClayNormal, sampler_SoilNormal), TEXTURE2D_ARGS(_ClayMask, sampler_SoilRoughness),
                    position, dx, dy, n, _ClayTileMetres * .7, _ClayTint.rgb * 0.85, _ClayNormalStrength * 3,
                    lumps, lumpsNormal, lumpsOcclusion);
                // Warped cells give irregular lumps: clay chunks, dark loose fill and stones.
                float3 q = position / max(_BackfillChunkMetres, 0.05);
                q += sin(q.yzx * 1.7 + q.zxy * 0.9) * 0.35;
                float h = frac(sin(dot(floor(q), float3(12.9898, 78.233, 37.719))) * 43758.5453);
                half chunk = step(0.62, h), dark = step(h, 0.28);
                half3 fill = lerp(lerp(stones, stones * 0.55, dark), lumps, chunk);
                colour = lerp(colour, fill, backfill);
                normal = normalize(lerp(normal, lerp(stonesNormal, lumpsNormal, chunk), backfill));
                occlusion = lerp(occlusion, lerp(stonesOcclusion, lumpsOcclusion, chunk) * (1 - dark * 0.3), backfill);
                roughness = lerp(roughness, 1, backfill);
            }
            half fracture = saturate(materials2.y), crack = saturate(materials2.z);
            [branch] if (fracture > 0.001)
            {
                DepositSurface(TEXTURE2D_ARGS(_RockAlbedo, sampler_SoilAlbedo),
                    TEXTURE2D_ARGS(_RockNormal, sampler_SoilNormal), TEXTURE2D_ARGS(_RockMask, sampler_SoilRoughness),
                    position, dx, dy, n, _FractureTileMetres, (half3)1, _RockNormalStrength * 1.8,
                    layerColour, layerNormal, layerOcclusion);
                // Shards: warped cells, each tilted and lit on its own.
                float3 q = position / max(_FractureShardMetres, 0.02);
                q += sin(q.yzx * 1.3 + q.zxy * 0.7) * 0.45;
                float3 cell = floor(q);
                float3 h = frac(sin(float3(dot(cell, float3(12.9898, 78.233, 37.719)), dot(cell, float3(39.346, 11.135, 83.155)),
                    dot(cell, float3(73.156, 52.235, 9.151)))) * 43758.5453);
                half3 tilt = (half3)(h * 2 - 1) * 0.55;
                half3 shardNormal = normalize(lerp(layerNormal, normalize(layerNormal + tilt), 0.8));
                normal = normalize(lerp(normal, shardNormal, fracture * 0.9));
                half shade = lerp(0.8, 1.2, (half)h.x);
                half3 broken = lerp(layerColour, (half3)dot(layerColour, half3(0.3, 0.59, 0.11)), 0.5);
                broken = saturate(broken * (1 + _FractureLift) * shade + _FractureLift * 0.12);
                colour = lerp(colour, broken, fracture);
                half core = smoothstep(0.38, 0.7, crack), rim = smoothstep(0.1, 0.38, crack) * (1 - core);
                colour = lerp(colour * (1 - rim * 0.65), _CrackColour.rgb, core);
                roughness = lerp(roughness, 1, max(core, fracture * 0.5));
            }
        }
        ENDHLSL

        Pass
        {
            Name "GroundForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "../../Runtime/Terrain/ExcavationLighting.hlsl"
            half4 GroundFragment(GroundVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 albedo, normal;
                half roughness, occlusion;
                GroundSurface(input.positionWS, input.normalWS, input.materials, input.materials2, albedo, normal, roughness, occlusion);
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = normal;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.fogCoord = input.fogFactor;
                lighting.vertexLighting = VertexLighting(input.positionWS, normal);
                lighting.bakedGI = SampleSH(normal);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1, 1, 1, 1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.normalTS = half3(0, 0, 1);
                // Dry earth must not turn wet or crystalline even where a source
                // mask contains polished grains or was authored for damp mud.
                surface.smoothness = min(saturate(1 - roughness), _MaxSmoothness);
                half3 daylightNormal = normalize(input.normalWS);
                if (input.positionWS.y >= _SurfaceHeight - _TurfDepth - 0.02 && daylightNormal.y > 0)
                    daylightNormal = half3(0, 1, 0);
                surface.occlusion = occlusion * ExcavationAmbient(input.positionWS, daylightNormal);
                surface.alpha = 1;
                half4 result = UniversalFragmentPBR(lighting, surface);
                result.rgb = MixFog(result.rgb, input.fogFactor);
                result.a = _GroundOpacity;
                return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half DepthFragment(GroundVaryings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFragment(GroundVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 albedo, normal;
                half roughness, occlusion;
                GroundSurface(input.positionWS, input.normalWS, input.materials, input.materials2, albedo, normal, roughness, occlusion);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
