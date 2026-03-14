Shader "Effect/SoftDissolve_Additive_BuiltIn"
{
    Properties
    {
        [Header(Color Settings)]
        _ColorIntensity("Color Intensity", Float) = 1
        _Color("Main Color", Color) = (1,1,1,1)
        
        [Header(Blending and Culling)]
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Blend Mode (Dst)", Int) = 1 // 1 = Additive (One), 10 = AlphaBlend (OneMinusSrcAlpha)
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Int) = 0 // 0 = Off, 2 = Back
        
        [Header(Textures)]
        _MainTex("Main Texture", 2D) = "white" {}
        _DissolveTex("Dissolve Texture", 2D) = "white" {}
        
        [Header(Dissolve Settings)]
        _DissolveProgress("Dissolve Progress", Range(0, 1)) = 0
        _Hardness("Hardness", Range(0.5, 1)) = 0.5
        
        [Header(Custom Data)]
        [Toggle] _Custom1xy("Use Custom Data (UV Pan)", Float) = 0
    }
    
    SubShader
    {
        // FIX 1: Updated Tags for correct sorting order to prevent Grid occlusion
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "PreviewType"="Plane" 
        }

        LOD 100

        // Render States
        Blend SrcAlpha [_Dst]
        AlphaToMask Off
        Cull [_CullMode]
        ColorMask RGBA
        ZWrite Off
        ZTest LEqual
        
        // FIX 2: Offset to prevent Z-fighting with the ground/grid
        Offset -1, -1
        
        Pass
        {
            Name "Forward"
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0; // Main UV
                float4 customData : TEXCOORD1; // Custom Data from Particle System (xy = offset, z = dissolve offset)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uvMain : TEXCOORD0;
                float2 uvDissolve : TEXCOORD1;
                float4 customData : TEXCOORD2; // Passed from vertex to fragment
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Uniforms
            uniform int _Dst;
            uniform int _CullMode;
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            sampler2D _DissolveTex;
            float4 _DissolveTex_ST;
            
            float4 _Color;
            float _ColorIntensity;
            float _DissolveProgress;
            float _Hardness;
            float _Custom1xy;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Transform Vertex Position
                o.vertex = UnityObjectToClipPos(v.vertex);

                // Pass Vertex Color
                o.color = v.color;

                // Transform UVs
                o.uvMain = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.uvDissolve = TRANSFORM_TEX(v.texcoord, _DissolveTex);

                // Pass Custom Data (from Particle System)
                o.customData = v.customData;

                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // --- 1. Main Color Calculation ---
                float4 mainTexColor = tex2D(_MainTex, i.uvMain);
                
                // Combine: Prop Color * Texture * Intensity * Vertex Color
                float3 finalRGB = (_Color.rgb * mainTexColor.rgb * _ColorIntensity * i.color.rgb);

                // --- 2. Dissolve Calculation ---
                // Calculate UVs for Dissolve: Blend between original UV and (UV + CustomData.xy)
                float2 dissolveUV = lerp(i.uvDissolve, i.uvDissolve + i.customData.xy, _Custom1xy);
                
                // Sample Dissolve Texture
                float dissolveSample = tex2D(_DissolveTex, dissolveUV).r;

                // Calculate Dissolve Alpha Mask
                // Logic recreated from Original ASE: saturate((Red + 1.0) - (CustomData.z + Progress) * 2.0)
                float dissolveBase = (dissolveSample + 1.0) - ((i.customData.z + _DissolveProgress) * 2.0);
                
                // Apply Hardness/Softness
                float alphaMask = smoothstep(1.0 - _Hardness, _Hardness, saturate(dissolveBase));

                // --- 3. Final Output ---
                // Alpha = Texture Alpha * Calculated Mask * Vertex Alpha
                float finalAlpha = mainTexColor.a * alphaMask * i.color.a;

                return fixed4(finalRGB, finalAlpha);
            }
            ENDCG
        }
    }
    
    // Fallback for older hardware
    FallBack "Mobile/Particles/Additive"
}