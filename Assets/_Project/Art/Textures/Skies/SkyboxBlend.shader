Shader "Custom/SkyboxBlend"
{
    Properties
    {
        _Tint      ("Tint Color", Color) = (.5,.5,.5,.5)
        [Gamma]
        _Exposure  ("Exposure", Range(0,8)) = 1.0
        _Rotation  ("Rotation", Range(0,360)) = 0
        _Blend     ("Blend", Range(0,1)) = 0

        [NoScaleOffset] _FrontTexA ("Front A [+Z]", 2D) = "grey" {}
        [NoScaleOffset] _BackTexA  ("Back A  [-Z]", 2D) = "grey" {}
        [NoScaleOffset] _LeftTexA  ("Left A  [+X]", 2D) = "grey" {}
        [NoScaleOffset] _RightTexA ("Right A [-X]", 2D) = "grey" {}
        [NoScaleOffset] _UpTexA    ("Up A    [+Y]", 2D) = "grey" {}
        [NoScaleOffset] _DownTexA  ("Down A  [-Y]", 2D) = "grey" {}

        [NoScaleOffset] _FrontTexB ("Front B [+Z]", 2D) = "grey" {}
        [NoScaleOffset] _BackTexB  ("Back B  [-Z]", 2D) = "grey" {}
        [NoScaleOffset] _LeftTexB  ("Left B  [+X]", 2D) = "grey" {}
        [NoScaleOffset] _RightTexB ("Right B [-X]", 2D) = "grey" {}
        [NoScaleOffset] _UpTexB    ("Up B    [+Y]", 2D) = "grey" {}
        [NoScaleOffset] _DownTexB  ("Down B  [-Y]", 2D) = "grey" {}
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        CGINCLUDE
        #include "UnityCG.cginc"

        half4  _Tint;
        half   _Exposure;
        float  _Rotation;
        half   _Blend;

        float3 RotateAroundYInDegrees(float3 vertex, float degrees)
        {
            float alpha = degrees * UNITY_PI / 180.0;
            float sina, cosa;
            sincos(alpha, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float3(mul(m, vertex.xz), vertex.y).xzy;
        }

        struct appdata_t
        {
            float4 vertex   : POSITION;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f
        {
            float4 vertex   : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        v2f vert(appdata_t v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
            o.vertex   = UnityObjectToClipPos(rotated);
            o.texcoord = v.texcoord;
            return o;
        }

        half4 BlendFaces(v2f i, sampler2D smpA, half4 hdrA, sampler2D smpB, half4 hdrB)
        {
            half4 texA = tex2D(smpA, i.texcoord);
            half4 texB = tex2D(smpB, i.texcoord);

            half3 colA = DecodeHDR(texA, hdrA);
            half3 colB = DecodeHDR(texB, hdrB);

            half3 c = lerp(colA, colB, _Blend);
            c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
            return half4(c, 1);
        }
        ENDCG

        // Front
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            sampler2D _FrontTexA; half4 _FrontTexA_HDR;
            sampler2D _FrontTexB; half4 _FrontTexB_HDR;
            half4 frag(v2f i) : SV_Target
            { return BlendFaces(i, _FrontTexA, _FrontTexA_HDR, _FrontTexB, _FrontTexB_HDR); }
            ENDCG
        }

        // Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            sampler2D _BackTexA; half4 _BackTexA_HDR;
            sampler2D _BackTexB; half4 _BackTexB_HDR;
            half4 frag(v2f i) : SV_Target
            { return BlendFaces(i, _BackTexA, _BackTexA_HDR, _BackTexB, _BackTexB_HDR); }
            ENDCG
        }

        // Left
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            sampler2D _LeftTexA; half4 _LeftTexA_HDR;
            sampler2D _LeftTexB; half4 _LeftTexB_HDR;
            half4 frag(v2f i) : SV_Target
            { return BlendFaces(i, _LeftTexA, _LeftTexA_HDR, _LeftTexB, _LeftTexB_HDR); }
            ENDCG
        }

        // Right
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            sampler2D _RightTexA; half4 _RightTexA_HDR;
            sampler2D _RightTexB; half4 _RightTexB_HDR;
            half4 frag(v2f i) : SV_Target
            { return BlendFaces(i, _RightTexA, _RightTexA_HDR, _RightTexB, _RightTexB_HDR); }
            ENDCG
        }

        // Up
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            sampler2D _UpTexA; half4 _UpTexA_HDR;
            sampler2D _UpTexB; half4 _UpTexB_HDR;
            half4 frag(v2f i) : SV_Target
            { return BlendFaces(i, _UpTexA, _UpTexA_HDR, _UpTexB, _UpTexB_HDR); }
            ENDCG
        }

        // Down
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            sampler2D _DownTexA; half4 _DownTexA_HDR;
            sampler2D _DownTexB; half4 _DownTexB_HDR;
            half4 frag(v2f i) : SV_Target
            { return BlendFaces(i, _DownTexA, _DownTexA_HDR, _DownTexB, _DownTexB_HDR); }
            ENDCG
        }
    }
}