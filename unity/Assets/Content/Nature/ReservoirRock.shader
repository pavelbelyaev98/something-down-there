Shader "Something Down There/Reservoir Rock"
{
    Properties
    {
        _BaseMap("Purchased rock albedo", 2D) = "white" {}
        _BumpMap("Purchased rock normal", 2D) = "bump" {}
        _BaseColor("Rock tint", Color) = (1,1,1,1)
        _BumpScale("Normal strength", Range(0,1)) = .65
        _Waterline("Former water height", Float) = 5.2
        _StainStrength("Mineral stain", Range(0,1)) = .65
        _StainColor("Mineral color", Color) = (.76,.71,.57,1)
        _HazeColor("Distant air", Color) = (.55,.7,.78,1)
        _HazeStart("Haze start", Float) = 65
        _HazeEnd("Haze end", Float) = 360
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST, _BaseColor, _StainColor, _HazeColor;
        float _BumpScale, _Waterline, _StainStrength, _HazeStart, _HazeEnd;
        CBUFFER_END
        float3 _LightDirection;
        float3 _LightPosition;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input,output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs n = GetVertexNormalInputs(input.normalOS,input.tangentOS);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            output.normalWS = n.normalWS;
            output.tangentWS = half4(n.tangentWS,input.tangentOS.w * GetOddNegativeScale());
            output.uv = TRANSFORM_TEX(input.uv,_BaseMap);
            return output;
        }
        Varyings ShadowVert(Attributes input)
        {
            Varyings output = Vert(input);
            float3 lightDirection = _LightDirection;
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                lightDirection = normalize(_LightPosition-output.positionWS);
            #endif
            output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,lightDirection));
            output.positionCS = ApplyShadowClamping(output.positionCS);
            return output;
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        half4 NormalFrag(Varyings input) : SV_Target { return half4(normalize(input.normalWS),0); }
        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_BumpScale);
            half3 bitangent = cross(input.normalWS,input.tangentWS.xyz)*input.tangentWS.w;
            half3 normal = normalize(TransformTangentToWorld(normalTS,half3x3(input.tangentWS.xyz,bitangent,input.normalWS)));
            half3 albedo = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
            // One level across separate cliffs, with small erosion breaks instead of a painted ruler line.
            float height = input.positionWS.y + .07*sin(input.positionWS.x*.8) + .04*sin(input.positionWS.z*1.7);
            half band = 1-smoothstep(.16,.48,abs(height-_Waterline));
            half below = 1-smoothstep(_Waterline-.5,_Waterline+.1,height);
            albedo = lerp(albedo,albedo*half3(1.12,1.02,.85),below*_StainStrength*.5);
            albedo = lerp(albedo,albedo*.65+_StainColor.rgb*.45,band*_StainStrength);
            half moss = smoothstep(_Waterline+1,_Waterline+3,height)*smoothstep(.55,.85,input.normalWS.y)*_StainStrength;
            albedo = lerp(albedo,albedo*half3(.48,.68,.28),moss*.7);
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.normalWS = normal;
            lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            lighting.bakedGI = SampleSH(normal);
            lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            lighting.shadowMask = half4(1,1,1,1);
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = albedo;
            surface.normalTS = normalTS;
            surface.smoothness = .12;
            surface.occlusion = 1;
            surface.alpha = 1;
            half4 color = UniversalFragmentPBR(lighting,surface);
            // Local to scenery: global fog would lift the black level in excavated tunnels.
            half haze = smoothstep(_HazeStart,_HazeEnd,distance(_WorldSpaceCameraPos,input.positionWS));
            color.rgb = lerp(color.rgb,_HazeColor.rgb,haze*.8);
            return color;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
