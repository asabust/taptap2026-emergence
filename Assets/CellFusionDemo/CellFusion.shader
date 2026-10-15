Shader "CellFusion/SoftCells"
{
    Properties
    {
        _Fusion ("Fusion distance", Range(0.001, 0.5)) = 0.22
        _RimWidth ("Rim width", Range(0.002, 0.15)) = 0.025
        _GlowWidth ("Glow width", Range(0.005, 0.25)) = 0.07
        _Opacity ("Body opacity", Range(0, 1)) = 0.55
        _Style ("White body style", Range(0, 1)) = 0
        _Nucleus ("Nucleus strength", Range(0, 1)) = 0.7
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Fusion, _RimWidth, _GlowWidth, _Opacity, _Style, _Nucleus;
            CBUFFER_END
            // xy = center in quad-local coordinates, z = radius.
            float4 _Cells[8];
            float4 _CellColors[8];
            float4 _Domain;
            int _CellCount;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * _Domain.xy;
                float d = 100;
                float3 tint = 0;
                float nucleus = 0;
                [loop] for (int i = 0; i < 8; i++)
                {
                    if (i >= _CellCount) break;
                    float r = length(p - _Cells[i].xy);
                    float nextD = r - _Cells[i].z;
                    // Polynomial smooth minimum: rounds the union and creates a neck.
                    float h = saturate(0.5 + 0.5 * (nextD - d) / max(_Fusion, 0.001));
                    d = lerp(nextD, d, h) - _Fusion * h * (1 - h);
                    tint = lerp(_CellColors[i].rgb, tint, h);
                    nucleus = max(nucleus, exp(-pow(r / max(_Cells[i].z * 0.22, 0.001), 2)));
                }
                float aa = max(fwidth(d), 0.0005);
                float inside = 1 - smoothstep(-aa, aa, d);
                float rim = exp(-pow(abs(d) / max(_RimWidth, aa), 2));
                float halo = exp(-pow(abs(d) / max(_GlowWidth, aa), 2));
                float depth = saturate(-d / 0.35);
                float3 pastel = lerp(tint, float3(0.82, 0.9, 0.88), 0.45);
                float3 body = lerp(pastel * (0.8 + 0.2 * depth), float3(1,1,1), _Style);
                body = lerp(body, float3(0.08,0.06,0.09), nucleus * _Nucleus * (1 - _Style));
                float3 edge = lerp(float3(1,0.88,0.96), tint * 1.25, _Style);
                float bodyAlpha = inside * lerp(_Opacity, 1, _Style);
                float edgeAlpha = saturate(rim * 0.8 + halo * 0.28);
                float alpha = edgeAlpha + bodyAlpha * (1 - edgeAlpha);
                float3 color = (edge * edgeAlpha + body * bodyAlpha * (1 - edgeAlpha)) / max(alpha, 0.0001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
