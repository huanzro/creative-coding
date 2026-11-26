Shader "Hidden/Echoes/StarTrailInk"
{
    Properties { _MainTex ("Ink", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        CGINCLUDE
            #include "UnityCG.cginc"
            float4 _InkViewport;
            float4 _InkCanvas, _InkAxes;
            float _InkDepth;
            struct Vertex { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Ribbon { float4 vertex : POSITION; float3 direction : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Pixel { float4 position : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            Pixel vert(Vertex v)
            {
                Pixel o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            float4 portraitClip(float2 pixel)
            {
                float2 clipPos = pixel * _InkViewport.xy + _InkViewport.zw;
                #if UNITY_UV_STARTS_AT_TOP
                clipPos.y = -clipPos.y;
                #endif
                return float4(clipPos, 0, 1);
            }
            Pixel vertPortrait(Vertex v)
            {
                Pixel o;
                o.position = portraitClip(v.vertex.xy);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            Pixel vertWorldRibbon(Ribbon v)
            {
                float3 fromEye = v.vertex.xyz - float3(0, 0, _InkDepth);
                float3 across = cross(fromEye, v.direction);
                float magnitude = dot(across, across);
                across = magnitude > .000001 ? across * rsqrt(magnitude) : float3(0, 1, 0);
                float3 expanded = v.vertex.xyz + across * ((v.uv.x * 2 - 1) * .078);
                Pixel o;
                o.position = UnityObjectToClipPos(float4(expanded, 1));
                o.color = v.color;
                o.color.a *= smoothstep(.45, 2.4, fromEye.z);
                o.uv = v.uv;
                return o;
            }
            float2 projectRibbon(float3 center)
            {
                float2 plane = float2(dot(center.xy, _InkAxes.xy), dot(center.xy, _InkAxes.zw));
                return _InkCanvas.xy + plane * (_InkCanvas.z / max(2.5, center.z - _InkDepth));
            }
            Pixel vertPortraitRibbon(Ribbon v)
            {
                float2 center = projectRibbon(v.vertex.xyz);
                float2 along = projectRibbon(v.vertex.xyz + v.direction * .045) - center;
                float magnitude = dot(along, along);
                float2 side = magnitude > .000001 ? float2(-along.y, along.x) * rsqrt(magnitude) : float2(0, 1);
                Pixel o;
                o.position = portraitClip(center + side * ((v.uv.x * 2 - 1) * _InkCanvas.w));
                o.color = v.color;
                o.color.a *= smoothstep(.45, 2.4, v.vertex.z - _InkDepth);
                o.uv = v.uv;
                return o;
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
        ENDCG
        Pass
        {
            Name "WorldInk"
            ZTest LEqual
            Blend One One, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment glow
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
            #pragma fragment glow
            ENDCG
        }
        Pass
        {
            Name "PortraitRibbon"
            ZTest Always
            Blend One One, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertPortraitRibbon
            #pragma fragment glow
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
