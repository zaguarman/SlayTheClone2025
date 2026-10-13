// The battle arrows (see ArrowIndicator): flat vertex-colored shapes, blended over the table. Each arrow writes its own
// stencil value and never draws over it again, so where an arrow's parts overlap (branches joining a trunk, a body
// meeting its head) it shows as one translucent shape, never a darker seam. Other arrows still blend over it.
Shader "SlayTheClone/TacticalArrow" {
    Properties {
        _StencilRef ("Stencil Ref", Int) = 1
    }
    SubShader {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Stencil {
            Ref [_StencilRef]
            Comp NotEqual
            Pass Replace
        }

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f {
                float4 position : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v) {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                return i.color;
            }
            ENDCG
        }
    }
}
