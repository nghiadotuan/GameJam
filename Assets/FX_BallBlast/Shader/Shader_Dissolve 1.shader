Shader "Custom/SmokeDissolve"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
        _TilingOffset ("TilingOffset", Vector) = (1, 1, 0, 0)
        
        [NoScaleOffset] _DissolveTex ("DissolveTex", 2D) = "white" {}
        _Smooth ("Smooth", Float) = 5
        _DissolveTexTiling ("DissolveTexTiling", Vector) = (0.8, 0.8, 0, 0)
    }

    SubShader
    {
        // Render Queue: Transparent-1 (2999)
        Tags { "Queue"="Transparent-1" "IgnoreProjector"="True" "RenderType"="Transparent" }
        
        // Alpha Blending
        Blend SrcAlpha OneMinusSrcAlpha
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
                fixed4 color : COLOR; // Vertex Color from Particle System
            };

            struct v2f
            {
                float2 uvMain : TEXCOORD0;
                float2 uvDissolve : TEXCOORD1;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _TilingOffset;
            
            sampler2D _DissolveTex;
            float4 _DissolveTexTiling;
            
            float _Smooth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Calculate UVs with Tiling and Offset
                o.uvMain = v.uv * _TilingOffset.xy + _TilingOffset.zw;
                o.uvDissolve = v.uv * _DissolveTexTiling.xy + _DissolveTexTiling.zw;
                
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample Main Texture
                fixed4 col = tex2D(_MainTex, i.uvMain);
                
                // Sample Dissolve Texture
                fixed4 noise = tex2D(_DissolveTex, i.uvDissolve);
                
                // Multiply with Vertex Color
                col *= i.color;
                
                // Apply Dissolve/Noise to Alpha
                // _Smooth controls density/contrast
                col.a *= noise.r * _Smooth;
                
                // Clamp Alpha
                col.a = saturate(col.a);
                
                return col;
            }
            ENDCG
        }
    }
}