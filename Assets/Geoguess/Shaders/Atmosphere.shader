// Fresnel rim glow for a slightly larger sphere around the globe (radius ~1.06x). Additive.
Shader "Geoguess/Atmosphere"
{
    Properties
    {
        [HDR] _Color("Glow Colour", Color) = (0.25,0.55,1,1)
        _Power("Fresnel Power", Range(0.5,8)) = 3
        _Intensity("Intensity", Range(0,4)) = 1.6
        _SunBias("Lit-side Bias", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Power, _Intensity, _SunBias;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = _WorldSpaceCameraPos - posWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS), v = normalize(i.viewWS);
                float fres = pow(1.0 - saturate(dot(n, v)), _Power);
                float sun = lerp(1.0, saturate(dot(n, GetMainLight().direction) * 0.5 + 0.6), _SunBias);
                return half4(_Color.rgb, saturate(fres * _Intensity * sun));
            }
            ENDHLSL
        }
    }
}
