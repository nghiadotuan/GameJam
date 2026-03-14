Shader "Shader Graphs/Shader_UVOffsetCDAdd_Motion"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _InnerColor ("InnerColor", Color) = (1, 1, 1, 1)
        [HDR] _OuterColor ("OuterColor", Color) = (1, 0.5, 0, 1)
        
        [Header(Main Texture)]
        _MainTex ("MainTex", 2D) = "white" {}
        _TilingOffset ("TilingOffset", Vector) = (1, 1, 0, 0)
        _UOffsetSpeed ("Main U Speed", Float) = 0
        _VOffsetSpeed ("Main V Speed", Float) = 0
        
        [Header(Mask Settings)]
        _MaskTex ("MaskTex (Gradient)", 2D) = "white" {}
        _MaskUSpeed ("Mask U Speed", Float) = 0
        _MaskVSpeed ("Mask V Speed", Float) = 0
        
        [Toggle] _IsAlpha ("IsAlpha", Float) = 0
        [Enum(UnityEngine.Rendering.RenderQueue)] _QueueOffset ("Render Queue", Float) = 3000
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        
        // Additive Blending (Sáng rực)
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

            sampler2D _MainTex; float4 _TilingOffset;
            float _UOffsetSpeed; float _VOffsetSpeed;

            sampler2D _MaskTex; float4 _MaskTex_ST;
            float _MaskUSpeed; float _MaskVSpeed; // Thêm biến tốc độ cho Mask
            
            float4 _InnerColor;
            float4 _OuterColor;
            float _IsAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 1. Tính toán UV cuộn cho MainTex (Hiệu ứng dòng chảy bên trong)
                float2 baseUV = v.uv * _TilingOffset.xy + _TilingOffset.zw;
                float2 mainScroll = float2(_UOffsetSpeed, _VOffsetSpeed) * _Time.y;
                o.uv = baseUV + mainScroll;
                
                // 2. Tính toán UV cuộn cho Mask (Hiệu ứng tia lao đi/xuất hiện)
                float2 maskScroll = float2(_MaskUSpeed, _MaskVSpeed) * _Time.y;
                o.uvMask = TRANSFORM_TEX(v.uv, _MaskTex) + maskScroll;
                
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 mainTex = tex2D(_MainTex, i.uv);
                fixed4 maskTex = tex2D(_MaskTex, i.uvMask);

                // --- LOGIC MÀU (Lõi vs Viền) ---
                // Dùng kênh Red của MainTex để nội suy giữa viền (cam) và lõi (trắng)
                fixed3 mixedColor = lerp(_OuterColor.rgb, _InnerColor.rgb, mainTex.r);

                fixed4 finalColor;
                
                // RGB = Màu đã pha * Độ sáng texture * Vertex Color (từ Particle System)
                // Nhân với mainTex.r để vùng đen của texture thực sự biến mất (Fix lỗi viền đen/cam)
                finalColor.rgb = mixedColor * mainTex.r * i.color.rgb;
                
                // --- XỬ LÝ ALPHA & MASK ---
                // Tính Alpha tổng hợp
                float alpha = mainTex.a * maskTex.a * i.color.a;
                
                // Áp dụng Mask vào màu RGB luôn (để cắt gọn tia)
                finalColor.rgb *= maskTex.rgb;

                // Kỹ thuật "Premultiplied Alpha" giả lập cho Additive:
                // Nhân màu với Alpha để các đoạn mờ dần sẽ đen đi (hòa trộn tốt hơn với nền)
                finalColor.rgb *= alpha;
                
                // Gán alpha cuối cùng
                finalColor.a = alpha;

                return finalColor;
            }
            ENDCG
        }
    }
}