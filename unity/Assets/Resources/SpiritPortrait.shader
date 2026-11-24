Shader "Hidden/Echoes/Spirit3D"
{
    Properties
    {
        _Tint ("Material color and emission", Vector) = (1,1,1,0)
        _SceneLight ("Surrounding light", Vector) = (1,.82,.55,.2)
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

            float4 _Tint;
            float4 _SceneLight;
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
                output.position = UnityObjectToClipPos(input.position);
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
                float sheen = pow(saturate(dot(normal, normalize(key + eye))), 25);
                float scattered = saturate(dot(normal, key) * .5 + .44);
                float grain = 1 + .023 * sin(218 * input.world.x + 81 * input.world.y) *
                    sin(143 * input.world.z - 164 * input.world.y);
                float3 color = _Tint.rgb * input.shade *
                    (float3(.49,.49,.55) + diffuse * float3(.55,.52,.48) + cool * float3(.15,.23,.29));
                color *= grain;
                color += (rim * .24 + scattered * .36) * _SceneLight.rgb * _SceneLight.a;
                color += rim * float3(.049,.073,.082) + sheen * float3(.07,.065,.052) + _Tint.rgb * _Tint.a * .38;
                return float4(saturate(color), 1);
            }
            ENDCG
        }
    }
}
