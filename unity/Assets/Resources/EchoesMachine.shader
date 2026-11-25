Shader "Hidden/Echoes/MachineGlass"
{
    Properties
    {
        _MainTex ("Inlay", 2D) = "white" {}
        _Tint ("Color", Color) = (1,1,1,1)
        _Glow ("Light", Float) = .4
        _Opacity ("Opacity", Range(0,1)) = .4
        _Sign ("Inlay mode", Float) = 0
        _Pool ("Mirror liquid mode", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Tint, _EchoesAir, _EchoesWeather;
            float _Glow, _Opacity, _Sign, _Pool;
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output
            {
                float4 pos:SV_POSITION;
                float3 world:TEXCOORD0;
                float3 normal:TEXCOORD1;
                float4 color:COLOR;
                float2 uv:TEXCOORD2;
            };
            Output vert(Input v)
            {
                Output o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color * _Tint;
                o.uv = v.uv;
                return o;
            }
            float4 frag(Output i):SV_Target
            {
                float4 glyph = tex2D(_MainTex,i.uv);
                if (_Sign > .5) return float4(glyph.rgb * i.color.rgb, glyph.a);
                float3 view = _WorldSpaceCameraPos - i.world;
                float rank = length(view);
                float edge = pow(saturate(1-abs(dot(normalize(i.normal),view/max(rank,.05)))),2.3);
                float wave = .5+.5*sin(i.world.y*10.2+i.world.z*3.6-_EchoesWeather.x*5.0);
                float fog = 1-exp(-max(0,rank-1.0)*_EchoesAir.w);
                float3 light = i.color.rgb * (.18+_Glow*(.37+.89*edge)+.26*wave);
                if (_Pool > .5)
                {
                    float2 p=i.world.xz;
                    float tide=sin(dot(p,float2(11.2,8.7))-_EchoesWeather.x*3.0) *
                        sin(dot(p,float2(-6.8,13.5))+_EchoesWeather.x*2.4);
                    float reflection=pow(saturate(.47+.48*edge+.15*tide),2.6);
                    float3 depthTint=float3(.047,.082,.19);
                    light=lerp(depthTint,i.color.rgb*(.30+_Glow*.62),reflection);
                    light+=float3(.13,.22,.29)*pow(saturate(tide),8)*(.35+edge*.62);
                    return float4(lerp(light,_EchoesAir.rgb,fog*.59),
                        i.color.a*_Opacity*(.59+.32*edge+.09*tide)*(1-.43*fog));
                }
                return float4(lerp(light,_EchoesAir.rgb,fog*.63),
                    i.color.a * _Opacity * (.36+.51*edge+.13*wave) * (1-.58*fog));
            }
            ENDCG
        }
    }
}
