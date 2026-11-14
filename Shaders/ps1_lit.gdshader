// Shaders/ps1_lit.gdshader
// Material simples dos modelos do Meshy sem recolor (caminhonete, barracas, viaturas): textura sem filtro,
// luz difusa e o tremor de vértices do PS1 (ps1_common).
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
#include "res://Shaders/ps1_common.gdshaderinc"

uniform sampler2D base_tex : source_color, filter_nearest;
/** Multiplica a textura (carros de cenário pintados de cores diferentes). */
uniform vec3 tint : source_color = vec3(1.0);

void vertex() {
	POSITION = ps1_snap(PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)), VIEWPORT_SIZE);
}

void fragment() {
	ALBEDO = texture(base_tex, UV).rgb * tint;
	ROUGHNESS = 0.8;
}
