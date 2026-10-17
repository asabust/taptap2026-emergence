Shader "CellFusion/OrganicLevelBoard"
{
    Properties
    {
        [PerRendererData] _MainTex("Texture", 2D) = "white" {}
        [HideInInspector] _LifeTex("Life state", 2D) = "black" {}
        [HideInInspector] _BoardTex("Terrain and goals", 2D) = "black" {}
        [HideInInspector] _GridSize("Grid size", Vector) = (1,1,0,0)
        _Color("Tint", Color) = (1,1,1,1)
        _Fusion("Membrane fusion", Range(0.02,0.5)) = 0.3
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _LifeTex, _BoardTex;
            float4 _GridSize, _Player, _PlayerTail, _PlayerPrevious, _Color, _ClipRect;
            float _Fusion, _Progress;
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float4 local : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv;
                o.local = v.vertex; o.color = v.color * _Color; return o;
            }
            bool Inside(float2 cell) { return all(cell >= 0) && all(cell < _GridSize.xy); }
            float4 ReadLife(float2 cell) { return tex2Dlod(_LifeTex, float4((cell + 0.5) / _GridSize.xy,0,0)); }
            void Merge(float nextD, float3 nextColor, inout float d, inout float3 color)
            {
                float h = saturate(0.5 + 0.5 * (nextD - d) / max(_Fusion, 0.001));
                d = lerp(nextD, d, h) - _Fusion * h * (1 - h);
                color = lerp(nextColor, color, h);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * _GridSize.xy - 0.5;
                float2 tile = floor(p + 0.5);
                float4 ground = tex2D(_BoardTex, (tile + 0.5) / _GridSize.xy);
                float eased = smoothstep(0, 1, _Progress);
                float d = 100, nucleus = 0;
                float3 tint = float3(0.5,0.7,0.85);
                // Local 5x5 sampling keeps cost bounded as boards grow to 30x30.
                [loop] for (int dy = -2; dy <= 2; dy++)
                [loop] for (int dx = -2; dx <= 2; dx++)
                {
                    float2 cell = tile + float2(dx,dy);
                    if (!Inside(cell)) continue;
                    float4 data = ReadLife(cell); // previous life, next life, previous clone, next clone
                    float life = lerp(data.r, data.g, eased);
                    if (life < 0.001) continue;
                    float2 center = cell;
                    if (data.r < 0.5 && data.g > 0.5 && _Progress < 1)
                    {
                        float2 source = 0; float count = 0;
                        [loop] for (int sy=-1;sy<=1;sy++)
                        [loop] for (int sx=-1;sx<=1;sx++)
                        {
                            float2 neighbour=cell+float2(sx,sy);
                            if ((sx!=0 || sy!=0) && Inside(neighbour))
                            {
                                if (ReadLife(neighbour).r > 0.5 || all(abs(neighbour-_PlayerPrevious.xy)<0.01))
                                { source+=neighbour; count++; }
                            }
                        }
                        if (count>0) center=lerp(source/count,cell,eased);
                    }
                    float clone = lerp(data.b, data.a, eased);
                    float3 cellColor = lerp(float3(0.38 + min(cell.y,6) * 0.025,0.66,0.8 + min(cell.x,6) * 0.018), float3(0.32,0.48,0.96),clone);
                    Merge(length(p-center)-0.48*life, cellColor, d, tint);
                    float nucleusAlpha = data.r < data.g ? smoothstep(0,1,saturate((_Progress-0.35)/0.65)) : life;
                    nucleus = max(nucleus, exp(-dot(p-cell,p-cell)/0.012)*nucleusAlpha);
                }
                Merge(length(p-_Player.xy)-_Player.z,float3(1,0.43,0.48),d,tint);
                if (_PlayerTail.z > 0.001) Merge(length(p-_PlayerTail.xy)-_PlayerTail.z,float3(1,0.43,0.48),d,tint);
                float aa = max(fwidth(d),0.002);
                float inside = 1-smoothstep(-aa,aa,d);
                float rim = exp(-pow(abs(d)/0.035,2));
                float halo = exp(-pow(abs(d)/0.09,2));
                float3 bg = ground.r > 0.75 ? float3(0.92,0.95,0.92) : ground.r > 0.25 ? float3(0.28,0.34,0.4) : float3(0.055,0.08,0.13);
                float2 gd = abs(frac(p+0.5)-0.5);
                float grid = smoothstep(0.49-max(fwidth(p.x),fwidth(p.y)),0.49,max(gd.x,gd.y));
                if (ground.r > 0.75) bg = lerp(bg,float3(0.66,0.78,0.75),grid*0.5);
                float3 body = lerp(tint,float3(0.88,0.94,0.9),0.38);
                float3 color = lerp(bg,body,inside*0.65);
                color = lerp(color,tint,halo*0.18);
                color = lerp(color,float3(1,0.91,0.97),rim*0.8);
                color = lerp(color,float3(0.13,0.23,0.29),nucleus*0.9);
                float playerNucleus = exp(-dot(p-_Player.xy,p-_Player.xy)/0.012);
                color = lerp(color,float3(0.48,0.09,0.14),playerNucleus);
                float ring = 1-smoothstep(0.02,0.02+aa,abs(length(p-tile)-0.31));
                if (ground.g > 0.5) color = lerp(color,float3(0.85,0.62,0.16),ring);
                if (ground.b > 0.5) color = lerp(color,float3(0.58,0.3,0.8),ring);
                float selection = 1-smoothstep(0.018,0.018+aa,abs(max(abs(p.x-tile.x),abs(p.y-tile.y))-0.43));
                color = lerp(color,float3(1,0.75,0.04),selection*ground.a);
                half4 result = half4(color,1)*input.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.local.xy,_ClipRect);
                #endif
                return result;
            }
            ENDHLSL
        }
    }
}
