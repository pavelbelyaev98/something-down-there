Shader "Something Down There/Soil Break"
{
    Properties
    {
        _Dust("Soft dust", Range(0, 1)) = 0
        _Solid("Solid clods (mesh normals)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "ExcavationLighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Dust, _Solid;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2; half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Lit like the ground it came from: the sky only as far as the excavation's daylight reaches,
            // sun with its shadows, and work lamps (diffused like on the soil). `wrap` softens dust.
            half3 SoilLight(float3 positionWS, half3 normalWS, float4 positionCS, half wrap)
            {
                half daylight = ExcavationAmbient(positionWS, normalWS);
                half3 light = SampleSH(normalWS) * daylight;
                Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
                light += sun.color * (sun.shadowAttenuation * daylight * saturate((dot(normalWS, sun.direction) + wrap) / (1 + wrap)));
                // Fine dust scatters sunlight forward: it glows when seen against the sun, as in a sunbeam.
                half toward = saturate(dot(normalize(positionWS - _WorldSpaceCameraPos), sun.direction));
                light += sun.color * (sun.shadowAttenuation * daylight * _Dust * 2.5 * pow(toward, 8));
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normalWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light lamp = ExcavationAdditionalLight(lightIndex, positionWS);
                    light += lamp.color * (lamp.distanceAttenuation * saturate((dot(normalWS, lamp.direction) + wrap) / (1 + wrap)));
                LIGHT_LOOP_END
            #endif
                return light;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = input.color.a;
                half3 normal;
                if (_Solid > 0.5)
                {
                    // A clod mesh: its own normals.
                    normal = normalize(input.normalWS);
                }
                else
                {
                    // A bevelled crumb silhouette or a soft puff, shaded as a little rounded lump so it
                    // reads as soil, not a flat card. No texture, glow, lights or collisions.
                    float2 p = input.uv * 2 - 1;
                    float edge = max(max(abs(p.x), abs(p.y)), abs(p.x * .7 + p.y * .6));
                    half chip = saturate((.9 - edge) / max(fwidth(edge), .015));
                    half dust = pow(saturate(1 - dot(p, p)), 2);
                    alpha *= lerp(chip, dust, _Dust);
                    // A dust puff thins out where it meets the ground instead of cutting a hard line.
                    float scene = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)), _ZBufferParams);
                    alpha *= lerp(1, saturate((scene - LinearEyeDepth(input.positionCS.z, _ZBufferParams)) / .25), _Dust);
                    float3 toCamera = normalize(_WorldSpaceCameraPos - input.positionWS);
                    float bulge = sqrt(saturate(1 - dot(p, p))) + lerp(.15, .9, _Dust);
                    normal = normalize(UNITY_MATRIX_V[0].xyz * p.x + UNITY_MATRIX_V[1].xyz * p.y + toCamera * bulge);
                }
                half3 color = input.color.rgb * SoilLight(input.positionWS, normal, input.positionCS, lerp(.15, .8, _Dust));
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
