Shader "Custom Shaders/Dissolving"
{
 Properties {
  _MainTex("Texture", 2D) = "white" {}
  _SliceGuide("Slice Guide", 2D) = "white" {}
  _SliceAmount("Slice Amount", Range(0,1)) = 0.5
 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
  Cull Off
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
  TEXTURE2D(_SliceGuide); SAMPLER(sampler_SliceGuide);
  CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST, _SliceGuide_ST;
   float _SliceAmount;
  CBUFFER_END
  struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
  struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 sliceUV : TEXCOORD1; float3 normalWS : TEXCOORD2; float3 positionWS : TEXCOORD3; half fog : TEXCOORD4; };
  Varyings Vert(Attributes v) {
   Varyings o;
   o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
   o.positionCS = TransformWorldToHClip(o.positionWS);
   o.normalWS = TransformObjectToWorldNormal(v.normalOS);
   o.uv = TRANSFORM_TEX(v.uv, _MainTex);
   o.sliceUV = TRANSFORM_TEX(v.uv, _SliceGuide);
   o.fog = ComputeFogFactor(o.positionCS.z);
   return o;
  }
  void Dissolve(Varyings i) { clip(SAMPLE_TEXTURE2D(_SliceGuide, sampler_SliceGuide, i.sliceUV).r - _SliceAmount); }
  ENDHLSL
  Pass {
   Name "ForwardLit"
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   half4 Frag(Varyings i) : SV_Target {
    Dissolve(i);
    half3 normal = normalize(i.normalWS);
    Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
    half3 color = albedo * (SampleSH(normal) + light.color * saturate(dot(normal,light.direction)) * light.shadowAttenuation);
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster"
   Tags { "LightMode"="ShadowCaster" }
   ZWrite On ColorMask 0
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment FragDepth
   half4 FragDepth(Varyings i) : SV_Target { Dissolve(i); return 0; }
   ENDHLSL
  }
  Pass {
   Name "DepthOnly"
   Tags { "LightMode"="DepthOnly" }
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment FragDepth
   half4 FragDepth(Varyings i) : SV_Target { Dissolve(i); return 0; }
   ENDHLSL
  }
 }
}
