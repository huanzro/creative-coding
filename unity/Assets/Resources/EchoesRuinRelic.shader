Shader "Hidden/Echoes/RuinRelic"
{
    Properties
    {
        _Reveal ("Exposed material", Range(0,1)) = 1
        _Luminance ("Inlay brightness", Range(0,2)) = 1
        _Concealed ("Use concealment mask", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Reveal, _Luminance, _Concealed;
            float4 _EchoesKey, _EchoesAir, _EchoesAmbience;
            struct Vertex { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
            struct Surface
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                float3 objectPoint:TEXCOORD1;
                half3 normal:TEXCOORD2;
                half4 color:COLOR;
            };

            Surface vert(Vertex v)
            {
                Surface o;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.objectPoint=v.vertex.xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.color=v.color;
                return o;
            }

            half4 frag(Surface i, fixed facing:VFACE):SV_Target
            {
                half inlay=1-i.color.a;
                float exposure=i.objectPoint.x*.29+i.objectPoint.y*.46+i.objectPoint.z*.21+
                    .14*sin(i.objectPoint.y*46+i.objectPoint.x*14)*sin(i.objectPoint.z*33-i.objectPoint.x*27);
                if (_Concealed>.5 && inlay<.52) clip(_Reveal*.91-.47+exposure+inlay*.29);

                float3 view=_WorldSpaceCameraPos-i.world;
                float range=length(view);
                view/=max(.025,range);
                half3 normal=normalize(i.normal)*(facing<0?-1:1);
                half3 key=normalize(_EchoesKey.xyz+float3(.18,.42,-.34));
                half diffuse=saturate(dot(normal,key));
                half face=saturate(dot(normal,view));
                half edge=pow(1-face,2.8);
                half spec=pow(saturate(dot(normal,normalize(key+view))),26);
                float trace=sin(i.objectPoint.y*95+i.objectPoint.x*111)*
                    cos(i.objectPoint.z*72-i.objectPoint.x*144);
                half3 baseColor=i.color.rgb*(.93+.07*trace);
                half3 surface=baseColor*(.37+diffuse*.59+face*.20)+
                    baseColor*_EchoesAmbience.rgb*.27+spec*half3(.28,.30,.32)+edge*half3(.042,.067,.081);
                half3 lit=surface+baseColor*inlay*(.43+_Luminance*(.65+edge*.30));
                float fog=1-exp(-max(0,range-1.5)*max(.018,_EchoesAir.w));
                return half4(lerp(lit,_EchoesAir.rgb,fog*(.85-inlay*.40)),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
