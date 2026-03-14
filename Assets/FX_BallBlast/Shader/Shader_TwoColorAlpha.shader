Shader "Custom/Shader_TwoColorAlpha"
{
    Properties
    {
        // Khai báo các biến khớp với hình ảnh
        [MainTexture] _MainTex ("MainTex", 2D) = "white" {}
        _LightColor ("LightColor", Color) = (1,1,1,1)
        _DarkColor ("DarkColor", Color) = (1, 0.97, 0.87, 1)
        _ColorScale ("ColorScale", Float) = 1.6
        [Toggle] _IsAlpha ("IsAlpha", Float) = 1
        
        // Để hỗ trợ chỉnh sửa Render Queue trong Inspector nếu cần
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4 // LLessEqual
    }

    SubShader
    {
        // Thiết lập Tags giống trong hình: Transparent
        // Queue = Transparent - 3 (3000 - 3 = 2997)
        Tags { "RenderType"="Transparent" "Queue"="Transparent-3" "IgnoreProjector"="True" "PreviewType"="Plane" }
        
        Blend SrcAlpha OneMinusSrcAlpha // Chế độ hòa trộn Alpha thông thường
        ZWrite Off // Khói thường không ghi vào Z-Buffer
        Cull Off   // Render 2 mặt (tùy chọn, thường particle là quad 2D)

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Hỗ trợ instance cho Particle System (quan trọng vì dòng thông báo MaterialPropertyBlock)
            #pragma multi_compile_instancing 
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; // Lấy màu từ Particle System
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            // Khai báo biến để nhận dữ liệu từ Properties
            fixed4 _LightColor;
            fixed4 _DarkColor;
            float _ColorScale;
            float _IsAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color; // Truyền màu vertex (màu hạt) xuống fragment
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Lấy mẫu texture
                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                // 2. Logic TwoColor: Dùng độ sáng của texture (kênh Red hoặc Alpha) để trộn màu
                // Giả sử texture khói đen trắng: Đen -> DarkColor, Trắng -> LightColor
                float lerpFactor = texColor.r; 
                
                fixed4 finalColor = lerp(_DarkColor, _LightColor, lerpFactor);
                
                // 3. Áp dụng ColorScale (tăng độ sáng)
                finalColor.rgb *= _ColorScale;

                // 4. Nhân với màu của Particle (Vertex Color) để hệ thống hạt có thể đổi màu/fade
                finalColor *= i.color;

                // 5. Xử lý Alpha
                // Nếu _IsAlpha được bật (1), ta dùng Alpha của texture gốc
                // Nếu không, ta có thể lấy Alpha từ độ sáng hoặc set cứng bằng 1
                float alphaSource = (_IsAlpha > 0.5) ? texColor.a : 1.0;
                
                finalColor.a = alphaSource * i.color.a;

                return finalColor;
            }
            ENDCG
        }
    }
}