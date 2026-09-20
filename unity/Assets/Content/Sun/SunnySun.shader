Shader "Something Down There/Sunny Sun Sky"
{
    Properties
    {
        _SunMap("Approved sun", 2D) = "black" {}
        _SkyColor("Bright cyan sky", Color) = (.12,.77,.85,1)
        _HorizonColor("Light cyan horizon", Color) = (.42,.88,.9,1)
        _SunDirection("Direction toward sun", Vector) = (0,1,0,0)
        _SunTangentRadius("Sun texture tangent half-width", Float) = .08749
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SunMap); SAMPLER(sampler_SunMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _SkyColor, _HorizonColor;
            float4 _SunDirection;
            float _SunTangentRadius;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float3 view = normalize(input.direction);
                float3 sun = normalize(_SunDirection.xyz);
                float3 sky = lerp(_HorizonColor.rgb, _SkyColor.rgb, smoothstep(0, .85, view.y));
                float forward = dot(view,sun);
                float3 axis = abs(sun.y) < .99 ? float3(0,1,0) : float3(0,0,1);
                float3 right = normalize(cross(axis,sun));
                float3 up = cross(sun,right);
                float2 sunUv = float2(dot(view,right),dot(view,up)) /
                    (max(forward,.0001) * max(_SunTangentRadius,.001) * 2) + .5;
                float4 disc = SAMPLE_TEXTURE2D_GRAD(_SunMap,sampler_SunMap,saturate(sunUv),ddx(sunUv),ddy(sunUv));
                float sunCoverage = step(0,forward) * step(0,sunUv.x) * step(sunUv.x,1) * step(0,sunUv.y) * step(sunUv.y,1);
                sky = lerp(sky,disc.rgb,disc.a * sunCoverage);

                return float4(sky,1);
            }
            ENDHLSL
        }
    }
}
