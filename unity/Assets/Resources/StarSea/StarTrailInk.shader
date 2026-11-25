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
            struct Vertex { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Pixel { float4 position : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            Pixel vert(Vertex v)
            {
                Pixel o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            Pixel vertPortrait(Vertex v)
            {
                Pixel o;
                o.position = float4(v.vertex.xy * _InkViewport.xy + _InkViewport.zw, 0, 1);
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y = -o.position.y;
                #endif
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            fixed4 glow(Pixel i) : SV_Target
            {
                float2 d = i.uv * 2 - 1;
                float radiusSquared = d.x * d.x + d.y * d.y;
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
