Shader "Something Down There/Recovery Mark"
{
    Properties { _Progress ("Hold progress", Range(0,1)) = 0 _Placed ("Confirmed", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Progress, _Placed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; return output;
            }
            float Stroke(float2 p, float2 a, float2 b)
            {
                float2 edge = b-a;
                return length(p-a-edge*saturate(dot(p-a,edge)/dot(edge,edge)));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = float2(1-input.uv.x*2,input.uv.y*2-1);
                float angle = frac(atan2(p.x,p.y)/6.2831853+1);
                float ring = abs(length(p)-.76);
                // Broken brackets become a closed ring; the check also confirms
                // success without relying on the white-to-amber colour change.
                if (_Placed < .5 && angle > _Progress && frac(angle*4) < .18) ring = 2;
                float glyph = _Placed > .5
                    ? min(Stroke(p,float2(-.32,0),float2(-.08,-.23)),Stroke(p,float2(-.08,-.23),float2(.35,.28)))
                    : min(Stroke(p,float2(-.16,0),float2(.16,0)),Stroke(p,float2(0,-.16),float2(0,.16)));
                float distance = min(ring,glyph);
                clip(.082-distance);
                half3 colour = _Placed > .5 || angle < _Progress ? half3(1,.65,.12) : half3(.86,.96,1);
                return half4(lerp(colour,half3(.025,.035,.04),smoothstep(.039,.053,distance)),1);
            }
            ENDHLSL
        }
    }
}
