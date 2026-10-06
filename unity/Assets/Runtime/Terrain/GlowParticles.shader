// Glints that light themselves (115): a texture (the Crystal Caverns pack's sparkle) in the particle's colour, added
// to what is behind it and faded where it nears a surface, so no hard edge cuts it. Visual only.
Shader "Something Down There/Glow Particles"
{
    Properties
    {
        _MainTex("Glint", 2D) = "white" {}
        _Intensity("Intensity", Range(0, 8)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Intensity;
            CBUFFER_END
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 glint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float scene = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)), _ZBufferParams);
                half fade = saturate((scene - LinearEyeDepth(input.positionCS.z, _ZBufferParams)) / .1);
                half3 color = glint.rgb * glint.a * input.color.rgb * input.color.a * _Intensity * fade;
                return half4(MixFogColor(color, half3(0, 0, 0), input.fog), 0);
            }
            ENDHLSL
        }
    }
}
