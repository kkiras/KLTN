Shader "UI/KLTN/CardReviveDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Reveal ("Reveal", Range(0,1)) = 1
        _EdgeColor ("Reconstruction Edge", Color) = (0.35,0.9,1,1)
        _EdgeWidth ("Edge Width", Range(0.001,0.2)) = 0.08
        _Softness ("Softness", Range(0.001,0.2)) = 0.035
        _CardCenter ("Card Center", Vector) = (0.5,0.5,0,0)
        _CardHalfSize ("Card Half Size", Vector) = (0.1,0.2,0,0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _EdgeColor;
            float _Reveal;
            float _EdgeWidth;
            float _Softness;
            float4 _CardCenter;
            float4 _CardHalfSize;
            float4 _ClipRect;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.screenPosition = ComputeScreenPos(output.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                float2 cell = floor(screenUV * float2(270, 160));
                float grain = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                float2 relative = (screenUV - _CardCenter.xy) / max(_CardHalfSize.xy, 0.001);
                float radial = saturate(length(relative) * 0.7071);
                float noise = grain * 0.38 + radial * 0.62;
                float reveal = smoothstep(noise - _Softness, noise + _Softness, _Reveal);
                float edge = saturate(1 - abs(noise - _Reveal) / _EdgeWidth);

                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge * 0.8);
                color.a *= saturate(reveal + edge * 0.7);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
