// The C4 preview's carve volume (026): the ball of ground a charge will take, drawn through the ground it lies in.
// A soft rim shows its shape, a bright line marks where it meets the scene (the crater's edge), and the part behind
// surfaces is fainter so the ball reads as inside the ground. Colour (valid or invalid) comes from _Color.
Shader "Something Down There/Blast Preview"
{
    Properties
    {
        _Color("Colour", Color) = (1, .62, .2, 1)
        _Fill("Fill", Range(0, 1)) = .05
        _Rim("Rim", Range(0, 1)) = .45
        _Edge("Edge line", Range(0, 2)) = 1.1
        _EdgeWidth("Edge width (m)", Range(.01, .3)) = .07
        _Hidden("Behind surfaces", Range(0, 1)) = .45
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fill, _Rim, _Edge, _EdgeWidth, _Hidden;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 toEye = normalize(_WorldSpaceCameraPos - input.positionWS);
                half facing = saturate(dot(normalize(input.normalWS), toEye));
                half rim = pow(1 - facing, 2.5);
                float scene = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)), _ZBufferParams);
                float here = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                half edge = saturate(1 - abs(scene - here) / _EdgeWidth);
                half hidden = here > scene + .01 ? _Hidden : 1;
                half alpha = saturate((_Fill + rim * _Rim) * hidden + edge * edge * _Edge) * _Color.a;
                return half4(_Color.rgb * (1 + edge), alpha);
            }
            ENDHLSL
        }
    }
}
