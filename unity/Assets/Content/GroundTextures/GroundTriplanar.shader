Shader "Something Down There/Ground Triplanar"
{
    Properties
    {
        _SoilAlbedo("Soil colour", 2D) = "white" {}
        [Normal] _SoilNormal("Soil normal", 2D) = "bump" {}
        _SoilRoughness("Soil mask (see mask layout)", 2D) = "white" {}
        _TurfAlbedo("Turf colour", 2D) = "white" {}
        [Normal] _TurfNormal("Turf normal", 2D) = "bump" {}
        _TurfRoughness("Turf mask (see mask layout)", 2D) = "white" {}
        [Enum(RoughnessContactStone,0,MetallicOcclusionSmoothness,1)] _MaskLayout("Mask layout", Float) = 0
        [Enum(RoughnessContactStone,0,MetallicOcclusionSmoothness,1)] _TurfMaskLayout("Turf mask layout", Float) = 0
        [Toggle] _SoilComparison("Compare soil on the west half", Float) = 0
        _SoilSplitX("Soil comparison split (world X)", Float) = 0
        _ComparisonAlbedo("Comparison soil colour", 2D) = "white" {}
        [Normal] _ComparisonNormal("Comparison soil normal", 2D) = "bump" {}
        _ComparisonRoughness("Comparison soil mask", 2D) = "white" {}
        [Enum(RoughnessContactStone,0,MetallicOcclusionSmoothness,1)] _ComparisonMaskLayout("Comparison mask layout", Float) = 0
        _ComparisonTileMetres("Comparison soil tile metres", Float) = 2
        _ComparisonNormalStrength("Comparison soil relief", Range(0, 2)) = 0.55
        _ComparisonStoneNormalStrength("Comparison stone relief", Range(0, 2)) = 0.9
        _MaxSmoothness("Dry ground maximum smoothness", Range(0, 1)) = 0.15
        _TileMetres("Turf tile metres", Float) = 1
        _SoilTileMetres("Soil tile metres", Float) = 2
        _NormalStrength("Soil relief", Range(0, 2)) = 0.8
        _StoneNormalStrength("Embedded stone relief", Range(0, 2)) = 0.85
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
            float _TurfNormalStrength;
            float _SurfaceHeight;
            float _TurfDepth;
            float _MacroVariation;
            float _MaskLayout;
            float _TurfMaskLayout;
            float _SoilComparison;
            float _SoilSplitX;
            float _ComparisonMaskLayout;
            float _ComparisonTileMetres;
            float _ComparisonNormalStrength;
            float _ComparisonStoneNormalStrength;
            float _MaxSmoothness;
            float _GroundOpacity;
        CBUFFER_END
        TEXTURE2D(_SoilAlbedo); SAMPLER(sampler_SoilAlbedo);
        TEXTURE2D(_SoilNormal); SAMPLER(sampler_SoilNormal);
        TEXTURE2D(_SoilRoughness); SAMPLER(sampler_SoilRoughness);
        TEXTURE2D(_TurfAlbedo); SAMPLER(sampler_TurfAlbedo);
        TEXTURE2D(_TurfNormal); SAMPLER(sampler_TurfNormal);
        TEXTURE2D(_TurfRoughness); SAMPLER(sampler_TurfRoughness);
        TEXTURE2D(_ComparisonAlbedo); SAMPLER(sampler_ComparisonAlbedo);
        TEXTURE2D(_ComparisonNormal); SAMPLER(sampler_ComparisonNormal);
        TEXTURE2D(_ComparisonRoughness); SAMPLER(sampler_ComparisonRoughness);
        #include "../../Runtime/Terrain/ExcavationDaylight.hlsl"

        struct GroundAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct GroundVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half fogFactor : TEXCOORD2;
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
            return output;
        }

        // Derivatives come from continuous world position, never the changing
        // projection sign. Sign boundaries must not select an unrelated coarse mip.
        #define GROUND_SAMPLE(tex, uv, dx, dy) SAMPLE_TEXTURE2D_GRAD(tex, sampler##tex, uv, dx, dy)

        half4 SampleGroundSoil(TEXTURE2D_PARAM(primaryTexture, primarySampler),
            TEXTURE2D_PARAM(comparisonTexture, comparisonSampler),
            float2 uv, float2 dx, float2 dy, bool useComparison)
        {
            // Gradients are derived from continuous world position before the
            // split, so even a cut crossing it retains stable mip selection.
            half4 sampled = half4(0, 0, 0, 0);
            [branch] if (useComparison)
                sampled = SAMPLE_TEXTURE2D_GRAD(comparisonTexture, comparisonSampler, uv, dx, dy);
            else
                sampled = SAMPLE_TEXTURE2D_GRAD(primaryTexture, primarySampler, uv, dx, dy);
            return sampled;
        }
        #define SOIL_SAMPLE(channel, uv, dx, dy) SampleGroundSoil( \
            TEXTURE2D_ARGS(_Soil##channel, sampler_Soil##channel), \
            TEXTURE2D_ARGS(_Comparison##channel, sampler_Comparison##channel), uv, dx, dy, useComparison)

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

        void GroundSurface(float3 position, half3 geometricNormal,
            out half3 colour, out half3 normal, out half roughness, out half occlusion)
        {
            half3 n = normalize(geometricNormal);
            half3 axis = abs(n);
            half bestAxis = max(axis.x, max(axis.y, axis.z));
            // Exclude stretched grazing projections from the soil blend.
            half3 weights = smoothstep(bestAxis - 0.16, bestAxis, axis);
            weights /= max(dot(weights, 1.0), 0.0001);
            bool useComparison = _SoilComparison > 0.5 && position.x < _SoilSplitX;
            float soilTileMetres = max(useComparison ? _ComparisonTileMetres : _SoilTileMetres, 0.05);
            float maskLayout = useComparison ? _ComparisonMaskLayout : _MaskLayout;
            float normalStrength = useComparison ? _ComparisonNormalStrength : _NormalStrength;
            float stoneNormalStrength = useComparison ? _ComparisonStoneNormalStrength : _StoneNormalStrength;
            float3 p = position / soilTileMetres;
            float3 positionDx = ddx(position), positionDy = ddy(position);
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
            half3 cx = SOIL_SAMPLE(Albedo, uvX, dxX, dyX).rgb;
            half3 cy = SOIL_SAMPLE(Albedo, uvY, dxY, dyY).rgb;
            half3 cz = SOIL_SAMPLE(Albedo, uvZ, dxZ, dyZ).rgb;
            half3 maskX = DecodeGroundMask(SOIL_SAMPLE(Roughness, uvX, dxX, dyX), maskLayout);
            half3 maskY = DecodeGroundMask(SOIL_SAMPLE(Roughness, uvY, dxY, dyY), maskLayout);
            half3 maskZ = DecodeGroundMask(SOIL_SAMPLE(Roughness, uvZ, dxZ, dyZ), maskLayout);
            // Coverage is a material boundary, not a transparent overlay. The
            // former multiplicative weighting still diluted whole stone faces
            // with another plane's dirt. Height-select one coherent material
            // through overlaps; reserve blending for its narrow filtered edge.
            float3 stoneCoverage = float3(maskX.b, maskY.b, maskZ.b);
            float3 eligible = step(bestAxis - 0.16, axis);
            float3 priority = axis + stoneCoverage * 0.6 - (1 - eligible) * 2;
            float highest = max(priority.x, max(priority.y, priority.z));
            float blendWidth = clamp(fwidth(highest), 0.008, 0.025);
            float3 mineralWeights = max(priority - highest + blendWidth, 0);
            mineralWeights /= max(dot(mineralWeights, 1.0), 0.0001);
            float3 covered = stoneCoverage * eligible;
            float mineral = smoothstep(0.15, 0.65, max(covered.x, max(covered.y, covered.z)));
            weights = lerp(weights, mineralWeights, mineral);
            colour = cx * weights.x + cy * weights.y + cz * weights.z;
            // Authored B coverage gives stones their own relief without
            // amplifying the accepted soil grain or adding texture lookups.
            half3 nx = UnpackNormalScale(SOIL_SAMPLE(Normal, uvX, dxX, dyX), lerp(normalStrength, stoneNormalStrength, maskX.b));
            half3 ny = UnpackNormalScale(SOIL_SAMPLE(Normal, uvY, dxY, dyY), lerp(normalStrength, stoneNormalStrength, maskY.b));
            half3 nz = UnpackNormalScale(SOIL_SAMPLE(Normal, uvZ, dxZ, dyZ), lerp(normalStrength, stoneNormalStrength, maskZ.b));
            // Surface-gradient projection: a flat normal map reproduces the
            // density-gradient mesh normal exactly, including blended slopes.
            normal = ProjectGroundNormal(n, weights, axisSign, nx, ny, nz);
            half2 soilMask = maskX.rg * weights.x + maskY.rg * weights.y + maskZ.rg * weights.z;
            roughness = soilMask.r;
            occlusion = soilMask.g;
            // Soil variation stays below the continuous meadow cap.
            float2 macroUV = (position.xz + position.y * float2(0.37, 0.23)) * 0.073;
            float2 macroDx = (positionDx.xz + positionDx.y * float2(0.37, 0.23)) * 0.073;
            float2 macroDy = (positionDy.xz + positionDy.y * float2(0.37, 0.23)) * 0.073;
            half macro = SOIL_SAMPLE(Albedo, macroUV, macroDx, macroDy).r;
            colour *= 1 + (macro - 0.47) * _MacroVariation * 3;

            // Feather turf into the exposed soil over a shallow collar. A nearly
            // binary cutoff at the flat surface traced individual mesh triangles.
            float depth = max(0, _SurfaceHeight - position.y);
            // The meadow stays on one continuous top projection across the rim.
            // Switching to a wall projection near tilted mesh normals produced
            // unrelated dark polygonal patches on the otherwise intact lawn.
            float edgeWidth = max(_TurfDepth * 0.45, (abs(positionDx.y) + abs(positionDy.y)) * 0.65);
            if (depth < _TurfDepth * 1.5 + 0.02)
            {
                float turfScale = soilTileMetres / max(_TileMetres, 0.05);
                half3 grassY = GROUND_SAMPLE(_TurfAlbedo, uvY * turfScale, dxY * turfScale, dyY * turfScale).rgb;
                half3 grass = grassY;
                half leaf = saturate((grass.g - grass.r * 0.7) * 3.5);
                half drift = GROUND_SAMPLE(_TurfAlbedo, position.xz * 0.61, positionDx.xz * 0.61, positionDy.xz * 0.61).r;
                float fringeDepth = _TurfDepth * (0.75 + leaf * 0.15 + drift * 0.1);
                // Texture variation stays within the blend so it breaks up the
                // contour without punching holes in untouched flat grass.
                float edge = depth - fringeDepth;
                half turf = (1 - smoothstep(-edgeWidth, edgeWidth, edge))
                    * smoothstep(-0.2, -0.05, n.y);
                half3 gy = UnpackNormalScale(GROUND_SAMPLE(_TurfNormal, uvY * turfScale, dxY * turfScale, dyY * turfScale), _TurfNormalStrength);
                // The thin turf fringe shares the lawn's lighting. Following the
                // steep soil normal here draws a dark polygonal outline on each cut.
                half3 turfNormal = half3(0, 1, 0);
                normal = normalize(lerp(normal, ProjectGroundNormal(turfNormal, half3(0, 1, 0), axisSign, gy, gy, gy), turf));
                colour = lerp(colour, grass, turf);
                half2 turfMask = DecodeGroundMask(GROUND_SAMPLE(_TurfRoughness, uvY * turfScale, dxY * turfScale, dyY * turfScale), _TurfMaskLayout).rg;
                roughness = lerp(roughness, turfMask.r, turf);
                occlusion = lerp(occlusion, lerp(0.65, 1, turfMask.g), turf);
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
                GroundSurface(input.positionWS, input.normalWS, albedo, normal, roughness, occlusion);
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
                GroundSurface(input.positionWS, input.normalWS, albedo, normal, roughness, occlusion);
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
