Shader "Shader Graphs/Shader_UVOffsetAdd"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("MainTex", 2D) = "white" {}
        
        // Vector4: X=TileX, Y=TileY, Z=OffsetX, W=OffsetY
        _TilingOffset ("TilingOffset", Vector) = (1, 1, 0, 0)
        
        _UOffsetSpeed ("UOffsetSpeed", Float) = 0
        _VOffsetSpeed ("VOffsetSpeed", Float) = 0
        
        _MaskTex ("MaskTex", 2D) = "white" {}
        
        [Toggle] _IsAlpha ("IsAlpha", Float) = 0
        
        [Enum(UnityEngine.Rendering.RenderQueue)] _QueueOffset ("Render Queue", Float) = 3000
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        
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
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uvMask : TEXCOORD1;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MaskTex;
            float4 _MaskTex_ST;
            
            float4 _Color;
            float4 _TilingOffset;
            float _UOffsetSpeed;
            float _VOffsetSpeed;
            float _IsAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                float2 baseUV = v.uv * _TilingOffset.xy + _TilingOffset.zw;
                float2 scroll = float2(_UOffsetSpeed, _VOffsetSpeed) * _Time.y;

                o.uv = baseUV + scroll;
                o.uvMask = TRANSFORM_TEX(v.uv, _MaskTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 mainTex = tex2D(_MainTex, i.uv);
                fixed4 maskTex = tex2D(_MaskTex, i.uvMask);

                fixed4 finalColor = mainTex * _Color * i.color;
                finalColor *= maskTex;

                if (_IsAlpha > 0.5)
                {
                    finalColor.rgb *= mainTex.a;
                }

                finalColor.a *= maskTex.a;
                return finalColor;
            }
            ENDCG
        }
    }
}