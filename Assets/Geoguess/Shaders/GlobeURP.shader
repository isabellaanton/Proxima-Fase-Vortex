// Earth surface shader for URP: albedo + normal map + simple sun lighting,
// plus per-pixel country highlighting driven by the country ID map.
//  - _HighlightID : pulsing fill + glowing outline (the round's target / answer)
//  - _HoverID     : steady tint + outline (mouse-over in Find-It mode)
//  - faint borders are drawn around every ID-map region.
// The ID map MUST be imported: Filter=Point, sRGB OFF, Compression=None, no mipmaps, Wrap=Repeat.
Shader "Geoguess/GlobeURP"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap("Earth Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,3)) = 1
        _MinLight("Night-side Light", Range(0,1)) = 0.15

        [NoScaleOffset] _IDMap("Country ID Map", 2D) = "black" {}
        _IDMapSize("ID Map Size (set by script)", Vector) = (2048,1024,0,0)
        _BorderStrength("Border Darkening", Range(0,1)) = 0.35
        _OutlineWidth("Outline Width (ID-map texels)", Range(0.5,6)) = 1.5

        _HighlightID("Highlight ID (rgb, w=active)", Vector) = (0,0,0,0)
        [HDR] _HighlightColor("Highlight Colour", Color) = (0.3,1.2,1.7,1)
        _FillStrength("Highlight Fill", Range(0,1)) = 0.45
        _PulseSpeed("Pulse Speed", Float) = 4

        _HoverID("Hover ID (rgb, w=active)", Vector) = (0,0,0,0)
        [HDR] _HoverColor("Hover Colour", Color) = (1.6,1.4,0.4,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_IDMap);   SAMPLER(sampler_IDMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _BumpScale, _MinLight;
                float4 _IDMapSize;
                float  _BorderStrength, _OutlineWidth;
                float4 _HighlightID; float4 _HighlightColor; float _FillStrength, _PulseSpeed;
                float4 _HoverID;     float4 _HoverColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 tangentWS : TEXCOORD2; float3 bitangentWS : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS; o.uv = v.uv;
                o.normalWS = n.normalWS; o.tangentWS = n.tangentWS; o.bitangentWS = n.bitangentWS;
                return o;
            }

            #define ID_EPS 0.008
            float3 SampleID(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_IDMap, sampler_IDMap, uv, 0).rgb; }
            float IsID(float3 c, float4 t) { return t.w * step(distance(c, t.rgb), ID_EPS); }
            float Differs(float3 a, float3 b) { return step(ID_EPS, distance(a, b)); }

            // 1 on pixels that touch the border of region 't' (either side of the border).
            float EdgeOf(float3 c, float3 s0, float3 s1, float3 s2, float3 s3, float4 t)
            {
                float cC = IsID(c, t);
                float e = Differs(c, s0) * max(cC, IsID(s0, t));
                e = max(e, Differs(c, s1) * max(cC, IsID(s1, t)));
                e = max(e, Differs(c, s2) * max(cC, IsID(s2, t)));
                e = max(e, Differs(c, s3) * max(cC, IsID(s3, t)));
                return e;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                float3 nWS = normalize(TransformTangentToWorld(nTS, float3x3(i.tangentWS, i.bitangentWS, i.normalWS)));

                // ---- country ID lookups (centre + 4 neighbours for edge detection)
                float2 t = _OutlineWidth / _IDMapSize.xy;
                float3 c0 = SampleID(i.uv);
                float3 s0 = SampleID(i.uv + float2( t.x, 0));
                float3 s1 = SampleID(i.uv + float2(-t.x, 0));
                float3 s2 = SampleID(i.uv + float2(0,  t.y));
                float3 s3 = SampleID(i.uv + float2(0, -t.y));

                float border = max(max(Differs(c0, s0), Differs(c0, s1)), max(Differs(c0, s2), Differs(c0, s3)));
                albedo *= 1.0 - border * _BorderStrength;

                float hi = IsID(c0, _HighlightID), ho = IsID(c0, _HoverID);
                float hiEdge = EdgeOf(c0, s0, s1, s2, s3, _HighlightID);
                float hoEdge = EdgeOf(c0, s0, s1, s2, s3, _HoverID);
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);

                albedo = lerp(albedo, saturate(_HighlightColor.rgb), hi * _FillStrength * (0.4 + 0.6 * pulse));
                albedo = lerp(albedo, saturate(_HoverColor.rgb), ho * 0.35);

                // ---- lighting
                Light L = GetMainLight();
                float ndl = saturate(dot(nWS, L.direction));
                float3 lit = albedo * (L.color * ndl + _MinLight);

                // ---- emission (HDR => Bloom makes it glow)
                float3 emis = _HighlightColor.rgb * (hiEdge * (1.0 + pulse) + hi * 0.25 * pulse) + _HoverColor.rgb * hoEdge * 1.2;
                return half4(lit + emis, 1);
            }
            ENDHLSL
        }
    }
}
