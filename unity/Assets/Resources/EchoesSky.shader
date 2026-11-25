Shader "Hidden/Echoes/WorldSky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _EchoesAir, _EchoesSkyHigh, _EchoesKey, _EchoesWeather, _EchoesAmbience;
            struct Ray { float4 pos:SV_POSITION; float3 world:TEXCOORD0; };
            Ray vert(float4 vertex:POSITION)
            {
                Ray o;
                o.pos=UnityObjectToClipPos(vertex);
                o.world=mul(unity_ObjectToWorld,vertex).xyz;
                return o;
            }
            float cloud(float2 p)
            {
                float veins=sin(dot(p,float2(1.13,1.88)) + sin(p.x*.89 + p.y*2.38)*1.17);
                return .5+.5*sin(p.x*1.2+p.y*2.13 + veins*1.95);
            }
            float4 frag(Ray i):SV_Target
            {
                float3 d=normalize(i.world-_WorldSpaceCameraPos);
                float horizon=pow(saturate(1-abs(d.y+.02)),3);
                float3 color=lerp(_EchoesSkyHigh.rgb,_EchoesAir.rgb,horizon*.94);
                float toward=saturate(dot(d,normalize(_EchoesKey.xyz)));
                float earth=_EchoesWeather.w;
                float sun=pow(toward,9)*.168 + pow(toward,204)*.45 + pow(toward,1802)*1.8;
                color += sun * float3(1,.72,.42)*_EchoesKey.w*earth;
                float band=saturate(1-abs(d.y+.44*d.x-.16)*2.0);
                float veil=cloud(d.xz*10 + d.y*3)*cloud(d.xz*26+d.y*12);
                color += (1-earth)*pow(band,2.6)*(.040+.32*veil) *
                    lerp(float3(.26,.45,.88),float3(.54,.28,.72),saturate(d.x+.5));
                return float4(color,1);
            }
            ENDCG
        }
    }
}
