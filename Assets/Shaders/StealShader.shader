Shader "Masquerade/SpriteGlitch"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _GlitchIntensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                float bands = floor(uv.y * 20.0);
                
                float noise = random(float2(bands, _Time.y * 10.0));
                
                noise = noise * 2.0 - 1.0; 

                uv.x += noise * _GlitchIntensity * 0.15;

                float colorR = tex2D(_MainTex, uv + float2(0.1 * _GlitchIntensity, 0)).r;
                float colorG = tex2D(_MainTex, uv).g;
                float colorB = tex2D(_MainTex, uv - float2(0.1 * _GlitchIntensity, 0)).b;
                float alpha  = tex2D(_MainTex, uv).a;

                fixed4 finalColor = fixed4(colorR, colorG, colorB, alpha);
                
                finalColor *= IN.color;
                
                finalColor.rgb *= finalColor.a; 

                return finalColor;
            }
            ENDCG
        }
    }
}