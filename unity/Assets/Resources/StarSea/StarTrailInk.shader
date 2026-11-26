Shader "Hidden/Echoes/StarTrailInk"
{
    Properties { _MainTex ("Ink", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        CGINCLUDE
            #include "UnityCG.cginc"
            float4 _InkWindow, _InkProject, _InkOrientation;
            float3 _InkEye;
            float _InkTime;
            struct Vertex { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Ribbon { float4 vertex : POSITION; float3 direction : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; float3 reveal : TEXCOORD1; };
            struct Pixel { float4 position : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float3 reveal : TEXCOORD1; float3 ray : TEXCOORD2; };

            float3 stageRay(float3 vertex)
            {
                float3 relative = vertex - _InkEye;
                return float3(relative.xy * _InkProject.xy, relative.z);
            }
            float2 stageUV(float3 ray)
            {
                return .5 + ray.xy / max(.095, ray.z);
            }
            float4 portraitClip(float3 ray)
            {
                float2 local = (stageUV(ray) - _InkWindow.xy) / _InkWindow.zw - .5;
                float2 clipPos = float2(dot(local, _InkOrientation.xy), dot(local, _InkOrientation.zw)) * float2(2, -2);
                #if UNITY_UV_STARTS_AT_TOP
                clipPos.y = -clipPos.y;
                #endif
                float depth = max(.095, ray.z);
                return float4(clipPos * depth, .5 * depth, depth);
            }
            Pixel projectInk(float3 at, fixed4 color, float2 uv, bool portrait)
            {
                Pixel o;
                o.ray = stageRay(at);
                o.position = portrait ? portraitClip(o.ray) : UnityObjectToClipPos(float4(at, 1));
                o.color = color;
                o.uv = uv;
                o.reveal = 0;
                return o;
            }
            Pixel vert(Vertex v) { return projectInk(v.vertex.xyz, v.color, v.uv, false); }
            Pixel vertPortrait(Vertex v) { return projectInk(v.vertex.xyz, v.color, v.uv, true); }

            Pixel projectRibbon(Ribbon v, bool portrait)
            {
                float3 fromEye = v.vertex.xyz - _InkEye;
                float3 across = cross(fromEye, v.direction);
                float magnitude = dot(across, across);
                across = magnitude > .000001 ? across * rsqrt(magnitude) : float3(0, 1, 0);
                float3 expanded = v.vertex.xyz + across * ((v.uv.x * 2 - 1) * lerp(.078, .063, v.reveal.z));
                Pixel o = projectInk(expanded, v.color, v.uv, portrait);
                o.color.a *= smoothstep(.45, 2.4, fromEye.z);
                o.reveal = v.reveal;
                return o;
            }
            Pixel vertWorldRibbon(Ribbon v) { return projectRibbon(v, false); }
            Pixel vertPortraitRibbon(Ribbon v) { return projectRibbon(v, true); }

            void cropMain(Pixel i)
            {
                if (_InkWindow.z > .0001)
                {
                    float2 fromStart = stageUV(i.ray) - _InkWindow.xy;
                    float2 fromEnd = fromStart - _InkWindow.zw;
                    clip(max(max(-fromStart.x, -fromStart.y), max(fromEnd.x, fromEnd.y)));
                }
            }
            fixed4 glow(Pixel i) : SV_Target
            {
                bool petal = i.uv.x >= 1.99;
                float2 local = float2(i.uv.x - (petal ? 2 : 0), i.uv.y);
                float2 d = local * 2 - 1;
                float radiusSquared = d.x * d.x + d.y * d.y;
                if (petal)
                {
                    float contour = 1 - smoothstep(.43, 1, radiusSquared);
                    float core = 1 - smoothstep(0, .56, radiusSquared);
                    float opacity = contour * (.50 + .28 * core) * i.color.a;
                    float3 tint = lerp(i.color.rgb, float3(1, .92, .62), core * .44);
                    return fixed4(tint * opacity, opacity);
                }
                float halo = .34 * exp(-3.8 * radiusSquared) * (1 - smoothstep(.66, 1, radiusSquared));
                float core = exp(-68 * radiusSquared);
                float alpha = saturate((halo + core * .90) * i.color.a);
                float3 tint = lerp(i.color.rgb, float3(.94, .98, 1), core * .57);
                return fixed4(tint * alpha, alpha);
            }
            fixed4 glowRibbon(Pixel i) : SV_Target
            {
                if (i.reveal.z > .5)
                {
                    float arrival = max(_InkTime - i.reveal.x, (_InkEye.z - i.reveal.y) * .68);
                    i.color.a *= smoothstep(0, .040, arrival);
                    i.color.rgb = lerp(i.color.rgb, float3(.97, .96, 1), .36 * (1 - smoothstep(.03, .17, arrival)));
                }
                return glow(i);
            }
            fixed4 worldGlow(Pixel i) : SV_Target { cropMain(i); return glow(i); }
            fixed4 worldGlowRibbon(Pixel i) : SV_Target { cropMain(i); return glowRibbon(i); }
        ENDCG
        Pass
        {
            Name "WorldInk"
            ZTest LEqual
            Blend One One, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment worldGlow
            ENDCG
        }
        Pass
        {
            Name "PortraitInk"
            ZTest Always
            Blend One One, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertPortrait
            #pragma fragment glow
            ENDCG
        }
        Pass
        {
            Name "WorldRibbon"
            ZTest LEqual
            Blend One One, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertWorldRibbon
            #pragma fragment worldGlowRibbon
            ENDCG
        }
        Pass
        {
            Name "PortraitRibbon"
            ZTest Always
            Blend One One, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertPortraitRibbon
            #pragma fragment glowRibbon
            ENDCG
        }
        Pass
        {
            Name "StraightAlpha"
            ZTest Always
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 ink = tex2D(_MainTex, i.uv);
                return fixed4(ink.rgb / max(.0039, ink.a), ink.a);
            }
            ENDCG
        }
    }
}
