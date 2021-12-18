Shader "Tile/Instanced"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Terrain Texture Array", 2DArray) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
	}
		SubShader
		{
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			// Physically based Standard lighting model, and enable shadows on all light types
			#pragma surface surf Standard fullforwardshadows vertex:vert

			// Use shader model 3.0 target, to get nicer looking lighting
			#pragma target 3.0

			#pragma require 2darray
			//sampler2D _MainTex;
			UNITY_DECLARE_TEX2DARRAY(_MainTex);

			struct Input
			{
				float2 uv_MainTex;
				float4 color : COLOR;
				float terrain; // TODO convert mainTex to vec3 and add terrain as z?
			};

			half _Glossiness;
			half _Metallic;
			// fixed4 _Color;

			// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
			// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
			// #pragma instancing_options assumeuniformscaling
			UNITY_INSTANCING_BUFFER_START(Props)
				UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
			UNITY_INSTANCING_BUFFER_END(Props)

			void vert(inout appdata_full v, out Input data)
			{
				UNITY_INITIALIZE_OUTPUT(Input, data);
				data.terrain = v.texcoord.z;
			}

			void surf(Input IN, inout SurfaceOutputStandard o)
			{
				// Produces a good texture resolution when fully zoomed
				//float2 uv = IN.worldPos.xy;
				fixed4 c = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(IN.uv_MainTex, IN.terrain));
				o.Albedo = c.rgb * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;
				o.Alpha = c.a;
			}
			ENDCG
		}
			FallBack "Diffuse"
}