Shader "HedgePhysics/HomingTarget"
{
    Properties {
        _BaseMap("Target icon", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay" }
        Pass {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZTest Always ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o; }
            half4 Frag(Varyings i) : SV_Target { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor; }
            ENDHLSL
        }
    }
}
