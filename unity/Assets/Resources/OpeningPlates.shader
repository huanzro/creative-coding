Shader "Hidden/Echoes/OpeningPlates"
{
    Properties
    {
        _MainTex ("Forest", 2D) = "black" {}
        _NextPlate ("Ruins or sky", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragment
            #include "UnityCG.cginc"
            sampler2D _MainTex, _NextPlate;
            float4 _ForestUV, _NextUV;
            float4 _Boundary;
            float3 _MistedTint;

            float4 fragment(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float3 forest = tex2D(_MainTex, uv * _ForestUV.xy + _ForestUV.zw).rgb;
                if (_Boundary.x <= .002) return float4(forest, 1);
                float3 next = tex2D(_NextPlate, uv * _NextUV.xy + _NextUV.zw).rgb;
                if (_Boundary.x >= .998) return float4(next, 1);
                // Let the underlying world enter from the same vertical direction as the camera's travel.
                float y = lerp(uv.y, 1 - uv.y, _Boundary.y);
                y += .048 * sin(uv.x * 5.8 + _Boundary.z) + .028 * sin(uv.x * 15.7 - _Boundary.z * .41);
                float center = lerp(-.16, 1.16, _Boundary.x);
                float w = lerp(.105, .155, _Boundary.y);
                float reveal = 1 - smoothstep(-w, w, y - center);
                float mist = exp(-abs(y - center) * lerp(10, 8, _Boundary.y)) * _Boundary.w;
                float3 result = lerp(forest, next, reveal);
                return float4(lerp(result, _MistedTint, mist), 1);
            }
            ENDCG
        }
    }
}
