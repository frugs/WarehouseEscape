Shader "Sokoban/BloomFade"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 1.0, 0.2, 1.0)
        _BloomIntensity ("Bloom Intensity", Range(1.0, 5.0)) = 2.0
        _PulseSpeed ("Pulse Speed", Range(0.0, 5.0)) = 2.0
        _FadeHeight ("Fade Height (Y)", Range(0.0, 1.0)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BloomFade"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            float4 _Color;
            float _BloomIntensity;
            float _PulseSpeed;
            float _FadeHeight;

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.uv = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Pulsing bloom effect
                float pulse = sin(_Time.y * _PulseSpeed) * 0.3 + 0.7;
                float bloomAmount = _BloomIntensity * pulse;

                // Get world Y position and calculate fade
                float worldY = i.worldPos.y;
                float fadeT = saturate(worldY / _FadeHeight);
                // Start at 0.5 opacity and fade to 0
                float alpha = 0.1 * (1.0 - fadeT);

                // Start with base color
                float4 color = _Color;

                // Apply bloom intensity (scaled for visibility)
                color.rgb *= bloomAmount * 2.0;

                // Apply fade transparency
                color.a = alpha;

                return color;
            }
            ENDCG
        }
    }
}
