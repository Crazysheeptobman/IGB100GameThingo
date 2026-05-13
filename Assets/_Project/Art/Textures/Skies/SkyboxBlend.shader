Shader "Custom/SkyboxBlend"
{
    Properties
    {
        _FrontA  ("Front A",   2D) = "white" {}
        _BackA   ("Back A",    2D) = "white" {}
        _LeftA   ("Left A",    2D) = "white" {}
        _RightA  ("Right A",   2D) = "white" {}
        _UpA     ("Up A",      2D) = "white" {}
        _DownA   ("Down A",    2D) = "white" {}

        _FrontB  ("Front B",   2D) = "white" {}
        _BackB   ("Back B",    2D) = "white" {}
        _LeftB   ("Left B",    2D) = "white" {}
        _RightB  ("Right B",   2D) = "white" {}
        _UpB     ("Up B",      2D) = "white" {}
        _DownB   ("Down B",    2D) = "white" {}

        _Blend   ("Blend", Range(0,1)) = 0
        _Tint    ("Tint", Color) = (1,1,1,1)
        _Exposure("Exposure", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FrontA, _BackA, _LeftA, _RightA, _UpA, _DownA;
            sampler2D _FrontB, _BackB, _LeftB, _RightB, _UpB, _DownB;
            float _Blend;
            float4 _Tint;
            float _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 dir    : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

                        half4 SampleSide6A(float3 dir)
            {
                float ax = abs(dir.x), ay = abs(dir.y), az = abs(dir.z);
                float2 uv;

                if (ax >= ay && ax >= az)
                {
                    if (dir.x > 0) { uv = float2(-dir.z, dir.y) / ax * 0.4995 + 0.5005; return tex2D(_LeftA, uv); }
                    else           { uv = float2( dir.z, dir.y) / ax * 0.4995 + 0.5005; return tex2D(_RightA, uv); }
                }
                else if (ay >= ax && ay >= az)
                {
                    if (dir.y > 0) { uv = float2( dir.x,-dir.z) / ay * 0.4995 + 0.5005; return tex2D(_UpA,   uv); }
                    else           { uv = float2( dir.x, dir.z) / ay * 0.4995 + 0.5005; return tex2D(_DownA,  uv); }
                }
                else
                {
                    if (dir.z > 0) { uv = float2( dir.x, dir.y) / az * 0.4995 + 0.5005; return tex2D(_FrontA, uv); }
                    else           { uv = float2(-dir.x, dir.y) / az * 0.4995 + 0.5005; return tex2D(_BackA,  uv); }
                }

                return half4(0,0,0,1);
            }

            half4 SampleSide6B(float3 dir)
            {
                float ax = abs(dir.x), ay = abs(dir.y), az = abs(dir.z);
                float2 uv;

                if (ax >= ay && ax >= az)
                {
                    if (dir.x > 0) { uv = float2(-dir.z, dir.y) / ax * 0.4995 + 0.5005; return tex2D(_LeftB, uv); }
                    else           { uv = float2( dir.z, dir.y) / ax * 0.4995 + 0.5005; return tex2D(_RightB, uv); }
                }
                else if (ay >= ax && ay >= az)
                {
                    if (dir.y > 0) { uv = float2( dir.x,-dir.z) / ay * 0.4995 + 0.5005; return tex2D(_UpB,   uv); }
                    else           { uv = float2( dir.x, dir.z) / ay * 0.4995 + 0.5005; return tex2D(_DownB,  uv); }
                }
                else
                {
                    if (dir.z > 0) { uv = float2( dir.x, dir.y) / az * 0.4995 + 0.5005; return tex2D(_FrontB, uv); }
                    else           { uv = float2(-dir.x, dir.y) / az * 0.4995 + 0.5005; return tex2D(_BackB,  uv); }
                }

                return half4(0,0,0,1);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                half4 colA = SampleSide6A(dir);
                half4 colB = SampleSide6B(dir);
                half4 col  = lerp(colA, colB, _Blend);
                col.rgb   *= _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                return col;
            }
            ENDCG
        }
    }
}