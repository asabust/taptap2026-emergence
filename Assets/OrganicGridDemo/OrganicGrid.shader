Shader "CellFusion/OrganicGrid"
{
    Properties { _Fusion("Membrane fusion", Range(0.02,0.5)) = 0.3 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Fusion;
            CBUFFER_END
            float4 _Cells[49], _Nuclei[49], _CellColors[49];
            float4 _Player, _PlayerTail;
            float _ShowGrid, _GoalReached;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            void Merge(float nextD, float3 nextColor, inout float d, inout float3 color)
            {
                float h = saturate(0.5 + 0.5 * (nextD - d) / max(_Fusion, 0.001));
                d = lerp(nextD, d, h) - _Fusion * h * (1 - h);
                color = lerp(nextColor, color, h);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 8;
                float d = 100;
                float3 tint = float3(0.5,0.7,0.85);
                float nucleus = 0;
                [loop] for (int i = 0; i < 49; i++)
                {
                    if (_Cells[i].w < 0.001) continue;
                    Merge(length(p - _Cells[i].xy) - _Cells[i].z, _CellColors[i].rgb, d, tint);
                    nucleus = max(nucleus, exp(-dot(p - _Nuclei[i].xy, p - _Nuclei[i].xy) / 0.012) * _Nuclei[i].z);
                }
                Merge(length(p - _Player.xy) - _Player.z, float3(1,0.43,0.48), d, tint);
                if (_PlayerTail.z > 0.001)
                    Merge(length(p - _PlayerTail.xy) - _PlayerTail.z, float3(1,0.43,0.48), d, tint);
                float aa = max(fwidth(d), 0.002);
                float inside = 1 - smoothstep(-aa, aa, d);
                float rim = exp(-pow(abs(d) / 0.035, 2));
                float halo = exp(-pow(abs(d) / 0.09, 2));
                float3 bg = float3(0.92,0.95,0.92);
                float2 gridDistance = abs(frac(p + 0.5) - 0.5);
                float grid = 1 - smoothstep(0.49 - max(fwidth(p.x),fwidth(p.y)), 0.49, max(gridDistance.x, gridDistance.y));
                float boardMask = 1 - smoothstep(3.49,3.51,max(abs(p.x),abs(p.y)));
                bg = lerp(bg, float3(0.66,0.78,0.75), (1-grid) * _ShowGrid * boardMask * 0.5);
                float3 body = lerp(tint, float3(0.88,0.94,0.9), 0.38);
                float3 color = lerp(bg, body, inside * 0.65);
                color = lerp(color, tint, halo * 0.18);
                color = lerp(color, float3(1,0.91,0.97), rim * 0.8);
                color = lerp(color, float3(0.13,0.23,0.29), nucleus * 0.9);
                float playerNucleus = exp(-dot(p - _Player.xy, p - _Player.xy) / 0.012);
                color = lerp(color, float3(0.48,0.09,0.14), playerNucleus);
                // Target is fixed at board (3,3), which maps to local (0,0).
                float ring = 1 - smoothstep(0.02,0.02+aa,abs(length(p)-0.31));
                color = lerp(color, lerp(float3(0.85,0.62,0.16),float3(0.2,0.65,0.4),_GoalReached), ring);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
