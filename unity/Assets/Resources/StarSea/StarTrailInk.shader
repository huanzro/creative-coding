Shader "Hidden/Echoes/StarTrailInk"
{
    Properties { _MainTex ("Ink", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct InkVertex { float4 position : POSITION; fixed4 color : COLOR; };
            struct InkFragment { float4 position : SV_POSITION; fixed4 color : COLOR; };
            InkFragment vert(InkVertex value)
            {
                InkFragment output;
                output.position = UnityObjectToClipPos(value.position);
                output.color = value.color;
                return output;
            }
            fixed4 frag(InkFragment IN) : SV_Target
            {
                return fixed4(IN.color.rgb * IN.color.a, IN.color.a);
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _InkProjection;
            float2 _InkCenterExposure;
            fixed4 frag(v2f_img IN) : SV_Target
            {
                fixed4 ink = tex2D(_MainTex, IN.uv);
                float2 offset = (IN.uv - _InkProjection.xy) * float2(16.0/9, 1);
                float edge = smoothstep(_InkProjection.z, _InkProjection.z + _InkProjection.w, length(offset));
                float gain = lerp(_InkCenterExposure.x, 1, edge);
                return fixed4(ink.rgb / max(.0039, ink.a), ink.a * gain * _InkCenterExposure.y);
            }
            ENDCG
        }
    }
}
