Shader "Something Down There/Soil Break"
{
    Properties { _Dust("Soft dust", Range(0, 1)) = 0 }
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Dust;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                // A bevelled crumb silhouette and a soft puff need no texture,
                // imported VFX, additive glow, lights or particle collisions.
                float edge = max(max(abs(p.x), abs(p.y)), abs(p.x * .7 + p.y * .6));
                half chip = saturate((.9 - edge) / max(fwidth(edge), .015));
                half dust = pow(saturate(1 - dot(p, p)), 2);
                half alpha = input.color.a * lerp(chip, dust, _Dust);
                return half4(MixFog(input.color.rgb, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
