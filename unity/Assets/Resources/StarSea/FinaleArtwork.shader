Shader "Hidden/Echoes/FinaleArtwork"
{
    Properties { _MainTex("Blank canvas", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment background
            #include "UnityCG.cginc"
            float4 _Mood;
            float randomAt(float2 p) { return frac(sin(dot(p,float2(133.9,312.1))) * 41376.31); }
            float4 background(v2f_img i) : SV_Target
            {
                float paper = _Mood.x;
                float2 p = i.uv - .5;
                float2 d = p * float2(1.55,1);
                float vignette = exp(-4.7 * dot(d,d));
                float dust = randomAt(floor(i.uv * float2(1580,900)));
                float2 cell = i.uv * float2(147,83);
                float star = step(.954,randomAt(floor(cell))) * (1-smoothstep(.04,.15,length(frac(cell)-.5)));
                float field = (.37 + .63 * vignette) * (.83 + dust * .27);
                float3 ink = lerp(float3(.029,.055,.137), float3(.16,.080,.034), paper) * field;
                float corner = step(abs(p.x),.477) * step(abs(p.y),.455) *
                    step(.28,abs(p.x)) * step(.31,abs(p.y));
                float2 rim = abs(abs(p) - float2(.475,.451));
                float border = corner * (1-smoothstep(.0005,.0014,min(rim.x,rim.y)));
                float glint = 1-smoothstep(.0,.012,abs(abs(p.x)-.475) + abs(abs(p.y)-.451)*.57);
                ink += lerp(float3(.54,.64,.94),float3(.86,.61,.26),paper) * (star * .37 + paper * (border * .32 + glint * .32));
                return float4(ink * _Mood.y,1);
            }
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex lineVertex
            #pragma fragment lineFragment
            #include "UnityCG.cginc"
            float4 _Mood, _Ink;
            float3 _Eye, _ViewRight, _ViewUp, _ViewForward, _PrintCenter, _PrintRight, _PrintUp;
            struct Vertex { float4 position : POSITION; float3 tangent : NORMAL; float2 width : TEXCOORD0; fixed4 color : COLOR; };
            struct Pixel { float4 position : SV_POSITION; float2 width : TEXCOORD0; fixed4 color : COLOR; float visibility : TEXCOORD1; };
            float2 projectVertex(float3 location)
            {
                float3 v = location - _Eye;
                float distance = max(.21,dot(v,_ViewForward));
                float2 spatial = clamp(float2(dot(v,_ViewRight),dot(v,_ViewUp)) * _Ink.xy / distance,-5,5);
                float3 sheet = location - _PrintCenter;
                float2 flat = float2(dot(sheet,_PrintRight),dot(sheet,_PrintUp)) + float2(0,.04);
                return lerp(spatial, flat, _Mood.x);
            }
            Pixel lineVertex(Vertex v)
            {
                Pixel p;
                float3 at = v.position.xyz;
                float2 center = projectVertex(at);
                float2 tangent = (projectVertex(at + v.tangent * .08) - projectVertex(at - v.tangent * .08)) * float2(_Ink.z,1);
                float2 across = normalize(float2(-tangent.y,tangent.x) + float2(0,.000002));
                center += across * float2(1/_Ink.z,1) * (v.width.x * 2 - 1) * _Ink.w * v.width.y;
                #if UNITY_UV_STARTS_AT_TOP
                center.y = -center.y;
                #endif
                p.position = float4(center,.5,1);
                p.width = v.width;
                p.color = v.color;
                p.visibility = lerp(smoothstep(.2,2.8,dot(at-_Eye,_ViewForward)),1,_Mood.x);
                return p;
            }
            fixed4 lineFragment(Pixel p) : SV_Target
            {
                float edge = abs(p.width.x * 2 - 1);
                float core = exp(-edge * edge * 23);
                float halo = exp(-edge * edge * 4.8) * (1-smoothstep(.72,1,edge));
                float gold = _Mood.x;
                float3 color = lerp(p.color.rgb, lerp(float3(.83,.38,.061),float3(1,.83,.40),core),gold);
                return fixed4(color * (core * .82 + halo * .24) * p.visibility * _Mood.y,0);
            }
            ENDCG
        }
    }
}
