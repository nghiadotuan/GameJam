Shader "Custom/Shader_DissolveAdd"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
        _TilingOffset ("TilingOffset", Vector) = (1, 1, 0, 0)
        
        [NoScaleOffset] _DissolveTex ("DissolveTex", 2D) = "white" {}
        _Smooth ("Smooth", Float) = 5
        _DissolveTexTiling ("DissolveTexTiling", Vector) = (0.8, 0.8, 0, 0)

        [HideInInspector] _Add ("Add Color", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        // Thay đổi Queue lên cao hơn một chút để vẽ đè lên các vật khác
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        
        // --- QUAN TRỌNG: Đổi sang chế độ Additive (Cộng màu) ---
        // SrcAlpha One: Màu đen (0) cộng vào nền = không đổi (trong suốt)
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
                float2 uvMain : TEXCOORD0;
                float2 uvDissolve : TEXCOORD1;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _TilingOffset;
            
            sampler2D _DissolveTex;
            float4 _DissolveTexTiling;
            
            float _Smooth;
            fixed4 _Add;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uvMain = v.uv * _TilingOffset.xy + _TilingOffset.zw;
                o.uvDissolve = v.uv * _DissolveTexTiling.xy + _DissolveTexTiling.zw;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uvMain);
                col *= _Color * i.color;

                fixed4 noise = tex2D(_DissolveTex, i.uvDissolve);

                // Tính toán Alpha
                col.a *= noise.r * _Smooth;
                col.a = saturate(col.a);

                // Cộng thêm màu Add
                col.rgb += _Add.rgb;
                
                // Trong chế độ Additive, chúng ta thường nhân màu với Alpha 
                // để đảm bảo chỗ nào Alpha=0 thì màu cũng đen (không cộng ánh sáng)
                col.rgb *= col.a;

                return col;
            }
            ENDCG
        }
    }
}