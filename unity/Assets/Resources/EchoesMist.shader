Shader "Hidden/Echoes/WorldMist"
{
    SubShader
    {
        Tags { "Queue"="Transparent+7" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha One ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _EchoesWeather;
            struct Ray { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:COLOR; };
            Ray vert(float4 vertex:POSITION, float3 normal:NORMAL, float4 color:COLOR)
            {
                Ray o;
                o.pos=UnityObjectToClipPos(vertex);
                o.world=mul(unity_ObjectToWorld,vertex).xyz;
                o.normal=UnityObjectToWorldNormal(normal);
                o.color=color;
                return o;
            }
            float4 frag(Ray i):SV_Target
            {
                float3 direction=normalize(_WorldSpaceCameraPos-i.world);
                float face=abs(dot(direction,normalize(i.normal)));
                float haze=(1-(1-face)*(1-face)) *
                    (.46+.54*sin(i.world.y*.72+i.world.z*.46+_EchoesWeather.x*.19)*.5);
                return float4(i.color.rgb,i.color.a * haze * .158);
            }
            ENDCG
        }
    }
}
