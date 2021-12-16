Shader "TileInstanced"
{
	Properties
	{
		_MainTex("Terrain Texture Array", 2DArray) = "white" {}
	}
		SubShader
		{
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			#pragma surface surf Standard addshadow
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
			#pragma require 2darray

			#include "UnityCG.cginc"

			UNITY_DECLARE_TEX2DARRAY(_MainTex);

			struct Input
			{
				float2 uv_MainTex;
			};

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			StructuredBuffer<int> tileBuffer;
			StructuredBuffer<float4> positionBuffer;
			StructuredBuffer<float4> colorBuffer;
#endif

			void setup()
			{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				float4 data = positionBuffer[unity_InstanceID];

				unity_ObjectToWorld._11_21_31_41 = float4(data.w, 0, 0, 0);
				unity_ObjectToWorld._12_22_32_42 = float4(0, data.w, 0, 0);
				unity_ObjectToWorld._13_23_33_43 = float4(0, 0, data.w, 0);
				unity_ObjectToWorld._14_24_34_44 = float4(data.xyz, 1);
				unity_WorldToObject = unity_ObjectToWorld;
				unity_WorldToObject._14_24_34 *= -1;
				unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
#endif
			}

			void surf(Input IN, inout SurfaceOutputStandard o)
			{
				int tile = 0;
				float4 col = 1.0f;

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
				tile = tileBuffer[unity_InstanceID];
				col = colorBuffer[unity_InstanceID];
#else
				col = float4(0, 0, 1, 1);
#endif

				fixed4 c = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(IN.uv_MainTex, (float)tile)) * col;
				o.Albedo = c.rgb;
				o.Alpha = c.a;
			}
			ENDCG
		}
		FallBack "Diffuse"
}