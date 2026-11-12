// Trecho de meio-fio pintado de uma cor só (amarelo: proibido estacionar), v0.9.8.
shader_type spatial;
render_mode diffuse_lambert;
#include "res://Shaders/curb_common.gdshaderinc"

uniform vec3 paint_color : source_color = vec3(0.95, 0.74, 0.08);
uniform float curb_height = 0.15;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	bool top = world_normal.y > 0.5;
	// Ao longo do trecho: a faixa é fina, então o eixo comprido é o que não é a normal.
	float along = abs(world_normal.x) > 0.5 ? world_pos.z : (abs(world_normal.z) > 0.5 ? world_pos.x : world_pos.x + world_pos.z);
	float h01 = top ? 1.0 : clamp(world_pos.y / curb_height, 0.0, 1.0);
	ALBEDO = curb_color(along, h01, paint_color, 1.0, top ? 0.0 : 1.0);
	ROUGHNESS = 0.8;
}
