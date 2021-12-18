Shader "Surface/Terrain"
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
			#pragma surface surf Standard fullforwardshadows vertex:vert
			#pragma target 3.0

			#pragma require 2darray
			UNITY_DECLARE_TEX2DARRAY(_MainTex);

			struct Input
			{
				float2 uv_MainTex;
				float4 color : COLOR;
				float terrain; // TODO convert mainTex to vec3 and add terrain as z?
			};

			half _Glossiness;
			half _Metallic;
			fixed4 _Color;

			void vert(inout appdata_full v, out Input data)
			{
				UNITY_INITIALIZE_OUTPUT(Input, data);
				data.terrain = v.texcoord.z;
			}

			void surf(Input IN, inout SurfaceOutputStandard o)
			{
				fixed4 c = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(IN.uv_MainTex, IN.terrain));
				o.Albedo = c.rgb * _Color;
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;
				o.Alpha = c.a;
			}
			ENDCG
		}
			FallBack "Diffuse"
}
