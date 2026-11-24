Shader "Hidden/Echoes/IllustratedSpirit"
{
    Properties
    {
        _MainTex ("Original painted sylph", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            CGPROGRAM
            #pragma vertex vert_img_world
            #pragma fragment fragment
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _CastLight;
            float _Exposure, _Lids;
            v2f_img vert_img_world(appdata_img input)
            {
                v2f_img output;
                UNITY_INITIALIZE_OUTPUT(v2f_img, output);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                return output;
            }
            float4 fragment(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float eye = max(1 - smoothstep(.027, .049, length((uv - float2(.479, .828)) * float2(1, 1.7))),
                                1 - smoothstep(.023, .046, length((uv - float2(.588, .845)) * float2(1, 1.7))));
                uv.y += .012 * eye * (1 - _Lids);
                float4 art = tex2D(_MainTex, uv);
                float alpha = smoothstep(.045, .91, art.a);
                float luma = dot(art.rgb, float3(.2126, .7152, .0722));
                float3 rgb = lerp(luma.xxx, art.rgb, 1.08) * _Exposure;
                rgb = lerp(rgb, rgb * (.69 + _CastLight.rgb * .40), .55);
                float palm = exp(-28 * length(uv - float2(.855, .566)));
                rgb += _CastLight.rgb * _CastLight.a * (palm * .32 + .036);
                return float4(saturate(rgb), alpha);
            }
            ENDCG
        }
    }
}
