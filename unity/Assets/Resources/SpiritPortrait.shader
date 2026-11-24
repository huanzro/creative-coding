Shader "Hidden/Echoes/SpiritPortrait"
{
    Properties
    {
        _MainTex ("Portrait", 2D) = "white" {}
        _BackShade ("Back lighting", Range(0,1)) = 1
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _BackShade;
            struct SpriteVertex { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(SpriteVertex input)
            {
                Varyings output;
                output.position = float4(input.vertex.xy, 0, 1);
                // This pass writes clip coordinates directly into a render texture.
                // Apply the render-target flip normally handled by Unity's camera matrix.
                #if UNITY_UV_STARTS_AT_TOP
                    output.position.y = -output.position.y;
                #endif
                output.uv = input.uv;
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float4 painted = tex2D(_MainTex, input.uv);
                float luminance = dot(painted.rgb, float3(.2126, .7152, .0722));
                float3 rgb = saturate(lerp(luminance.xxx, painted.rgb, 1.32));
                float headroom = 1 - max(rgb.r, max(rgb.g, rgb.b));
                rgb += min(.48 * luminance * (1 - luminance), .88 * headroom);
                return float4(rgb * _BackShade, painted.a);
            }
            ENDCG
        }
    }
}
