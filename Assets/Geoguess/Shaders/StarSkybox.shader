// Procedural starfield skybox (no texture needed). Assign via Window > Rendering > Lighting > Environment.
Shader "Geoguess/StarSkybox"
{
    Properties
    {
        _Density("Star Density (higher = fewer)", Range(0.9,0.999)) = 0.975
        _Scale("Star Grid Scale", Float) = 220
        _Brightness("Brightness", Range(0,4)) = 1.6
        _SpaceColor("Space Colour", Color) = (0.008,0.012,0.03,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Density, _Scale, _Brightness; float4 _SpaceColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.dir = v.positionOS.xyz; return o; }

            float hash13(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }

            half4 frag(Varyings i) : SV_Target
            {
                float3 p = normalize(i.dir) * _Scale;
                float3 cell = floor(p), f = frac(p) - 0.5;
                float h = hash13(cell);
                float star = step(_Density, h) * smoothstep(0.35, 0.0, length(f)) * lerp(0.4, 1.0, hash13(cell + 7.7));
                float3 tint = lerp(float3(0.7, 0.8, 1.0), float3(1.0, 0.9, 0.7), hash13(cell + 3.1));
                return half4(_SpaceColor.rgb + star * tint * _Brightness, 1);
            }
            ENDHLSL
        }
    }
}
