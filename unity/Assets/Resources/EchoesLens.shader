Shader "Hidden/Echoes/WorldLens"
{
    Properties { _MainTex ("Scene",2D)="black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Lens;

            float3 highlights(float2 uv)
            {
                float3 value=tex2D(_MainTex,uv).rgb;
                return max(0,value-.58) * smoothstep(.58,1.18,max(value.r,max(value.g,value.b)));
            }
            float4 frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv;
                float2 px=_MainTex_TexelSize.xy*float2(4.6,4.6);
                float3 baseColor=tex2D(_MainTex,uv).rgb;
                float3 bloom=highlights(uv+float2(px.x,0))+highlights(uv-float2(px.x,0))+
                    highlights(uv+float2(0,px.y))+highlights(uv-float2(0,px.y));
                bloom+=(highlights(uv+px*2)+highlights(uv-px*2)+highlights(uv+float2(px.x,-px.y)*2)+
                    highlights(uv+float2(-px.x,px.y)*2))*.58;
                baseColor += bloom*(.13+.045*_Lens.y);
                float2 q=(uv-.5)*float2(1.13,.90);
                float vignette=smoothstep(.18,.61,length(q));
                baseColor=baseColor/(.86+.24*baseColor);
                float grain = frac(sin(dot(floor(uv/_MainTex_TexelSize.xy),float2(12.9898,78.233)))*43758.5453)-.5;
                return float4(saturate((baseColor+grain*.008)*(1-vignette*(.31+_Lens.y*.035))*_Lens.x),1);
            }
            ENDCG
        }
    }
}
