Shader "Custom/DistortAdd"
{
    Properties
    {
        [Header(Main Settings)]
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture", 2D) = "white" {}
        _TilingOffset ("Tiling & Offset", Vector) = (3, 1, 0, 0)
        
        [Header(Distortion Settings)]
        _DistortTex ("Distort Texture", 2D) = "white" {}
        _DistortStrength ("Distort Strength", Float) = 0.2
        _UOffsetSpeed ("U Offset Speed", Float) = 0.4
        _VOffsetSpeed ("V Offset Speed", Float) = 0.4
        _DistortTilingOffset ("Distort Tiling & Offset", Vector) = (3, 1, 0, 0)

        [Header(Mask Settings)]
        _MaskTex ("Mask Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent-68" "RenderType"="Transparent" "PreviewType"="Plane" }
        
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            float4 _Color;
            sampler2D _MainTex;
            float4 _TilingOffset;
            
            sampler2D _DistortTex;
            float4 _DistortTilingOffset;
            float _DistortStrength;
            float _UOffsetSpeed;
            float _VOffsetSpeed;
            
            sampler2D _MaskTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 distUV = i.uv * _DistortTilingOffset.xy + _DistortTilingOffset.zw;
                float2 scroll = float2(_UOffsetSpeed, _VOffsetSpeed) * _Time.y;
                distUV += scroll;

                fixed4 distortColor = tex2D(_DistortTex, distUV);
                float2 distortion = (distortColor.rg - 0.5) * 2.0 * _DistortStrength;

                float2 mainUV = i.uv * _TilingOffset.xy + _TilingOffset.zw;
                mainUV += distortion; 

                fixed4 mainCol = tex2D(_MainTex, mainUV);
                fixed4 maskCol = tex2D(_MaskTex, i.uv);

                fixed4 finalColor = _Color * mainCol * maskCol * i.color;
                
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}