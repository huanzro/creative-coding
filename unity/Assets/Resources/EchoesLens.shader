Shader "Hidden/Echoes/WorldLens"
{
    Properties
    {
        _MainTex ("Scene",2D)="black" {}
        _BackgroundBrightness ("Scenery brightness", Float) = 1
    }
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
            // Actor UV and radii in units of output height. Brightness and radial gain stay independent.
            float4 _ProjectionProfile;
            float4 _ProjectionGrade;
            float _BackgroundBrightness;

            float3 highlights(float2 uv)
            {
                float4 tap=tex2D(_MainTex,uv);
                float3 value=tap.rgb*lerp(1,_BackgroundBrightness,saturate(tap.a));
                return max(0,value-.58) * smoothstep(.58,1.18,max(value.r,max(value.g,value.b)));
            }
            float4 frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv;
                float2 px=_MainTex_TexelSize.xy*float2(4.6,4.6);
                float4 scene=tex2D(_MainTex,uv);
                float3 baseColor=scene.rgb;
                float3 bloom=highlights(uv+float2(px.x,0))+highlights(uv-float2(px.x,0))+
                    highlights(uv+float2(0,px.y))+highlights(uv-float2(0,px.y));
                bloom+=(highlights(uv+px*2)+highlights(uv-px*2)+highlights(uv+float2(px.x,-px.y)*2)+
                    highlights(uv+float2(-px.x,px.y)*2))*.58;
                baseColor += bloom*(.13+.045*_Lens.y);
                float2 q=(uv-.5)*float2(1.13,.90);
                float vignette=smoothstep(.18,.61,length(q));
                baseColor=baseColor/(.86+.24*baseColor);
                float grain = frac(sin(dot(floor(uv/_MainTex_TexelSize.xy),float2(12.9898,78.233)))*43758.5453)-.5;
                float3 color=(baseColor+grain*.008)*(1-vignette*(.31+_Lens.y*.035)*(1-_ProjectionGrade.y));
                float headroom=1-saturate(max(color.r,max(color.g,color.b)));
                float exposure=min(1,_ProjectionGrade.x)+max(0,_ProjectionGrade.x-1)*headroom*headroom;
                float2 fromActor=(uv-_ProjectionProfile.xy)*float2(_ProjectionGrade.w,1);
                float edge=smoothstep(_ProjectionProfile.z,_ProjectionProfile.z+_ProjectionProfile.w,length(fromActor));
                // Performers write zero coverage and keep their own level over the graded scenery.
                float coverage=saturate(scene.a);
                float radial=lerp(1,lerp(_ProjectionGrade.z,1,edge),_ProjectionGrade.y*coverage);
                float backdrop=lerp(1,_BackgroundBrightness,coverage);
                return float4(saturate(color*exposure*radial*backdrop*_Lens.x),1);
            }
            ENDCG
        }
    }
}
