Shader "Hidden/Echoes/Spirit3D"
{
    Properties
    {
        _Tint ("Material color and emission", Vector) = (1,1,1,0)
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite On ZTest LEqual Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4x4 _AvatarViewProjection;
            float4 _Tint;
            struct Vertex
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float shade : TEXCOORD2;
            };

            Varyings vert(Vertex input)
            {
                Varyings output;
                float4 world = mul(unity_ObjectToWorld, input.position);
                output.position = mul(_AvatarViewProjection, world);
                output.world = world.xyz;
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.shade = input.color.r;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float3 eye = normalize(float3(0, 1.16, -5.6) - input.world);
                float3 key = normalize(float3(-.53, .78, -.63));
                float3 fill = normalize(float3(.70, .23, .39));
                float diffuse = saturate(dot(normal, key));
                float cool = saturate(dot(normal, fill));
                float rim = pow(1 - saturate(dot(normal, eye)), 2.2);
                float sheen = pow(saturate(dot(normal, normalize(key + eye))), 20);
                float3 color = _Tint.rgb * input.shade *
                    (float3(.44,.43,.49) + diffuse * float3(.68,.61,.48) + cool * float3(.15,.27,.33));
                color += rim * float3(.085,.13,.16) + sheen * float3(.10,.086,.068) + _Tint.rgb * _Tint.a * .44;
                return float4(saturate(color), 1);
            }
            ENDCG
        }
    }
}
