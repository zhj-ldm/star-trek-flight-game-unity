Shader "StarTrek/ShieldBubble"
{
    Properties
    {
        _Color ("Shield Color", Color) = (0.3, 0.6, 1, 0.12)
        _EdgeColor ("Edge Color", Color) = (0.5, 0.8, 1, 0.5)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _HitPos ("Hit Position", Vector) = (0,0,0,0)
        _HitTime ("Hit Time", Float) = -1
        _Opacity ("Opacity Mult", Range(0, 3)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            float4 _Color;
            float4 _EdgeColor;
            float _FresnelPower;
            float3 _HitPos;
            float _HitTime;
            float _Opacity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                o.worldPos = worldPos;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float fresnel = 1.0 - saturate(dot(normalize(i.viewDir), normalize(i.worldNormal)));
                fresnel = pow(fresnel, _FresnelPower);

                float ripple = 0;
                if (_HitTime > 0)
                {
                    float dist = distance(i.worldPos, _HitPos);
                    float t = _Time.y - _HitTime;
                    float waveRadius = t * 40;
                    float waveWidth = 8;
                    float wave = exp(-pow(dist - waveRadius, 2) / waveWidth);
                    ripple = wave * (1 - saturate(t * 1.5));
                }

                float3 col = lerp(_Color.rgb, _EdgeColor.rgb, fresnel) + ripple * float3(1,1,1);
                float alpha = (_Color.a + fresnel * _EdgeColor.a + ripple * 0.8) * _Opacity;
                alpha = saturate(alpha);

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
