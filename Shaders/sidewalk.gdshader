// Calçada de placas de concreto com junta a cada 1,5 m. Meio-fio (v0.9.8): concreto cinza com
// faixas brancas intercaladas de 1 m, tinta gasta e sujeira da sarjeta (Shaders/curb_common).
shader_type spatial;
render_mode diffuse_lambert;
#include "res://Shaders/curb_common.gdshaderinc"

uniform vec3 tile_color : source_color = vec3(0.36, 0.35, 0.33);
uniform float tile_size = 1.5;
/** Placa de concreto (1 tile = 1 placa) e pedra portuguesa do calçadão (v0.9.7). */
uniform sampler2D slab_tex : source_color, filter_nearest_mipmap, repeat_enable;
uniform sampler2D pt_tex : source_color, filter_nearest_mipmap, repeat_enable;
uniform float block_period = 48.0;
uniform float block_half = 18.0;
uniform float curb_height = 0.15;
/** Largura da faixa de cima do meio-fio (pintada junto com a lateral). */
uniform float curb_top = 0.14;
uniform vec3 curb_paint : source_color = vec3(0.88, 0.88, 0.85);

varying vec3 world_pos;
varying vec3 world_normal;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

/** Faixas brancas intercaladas: 1 m pintado, 1 m concreto. */
float curb_band(float along) {
	return fract(along * 0.5) < 0.5 ? 1.0 : 0.0;
}

void fragment() {
	vec3 col = tile_color;
	vec2 local = world_pos.xz - round(world_pos.xz / block_period) * block_period;
	float edge = block_half - max(abs(local.x), abs(local.y));
	if (world_normal.y > 0.5 && edge < curb_top) {
		// Topo do meio-fio: mesma pintura da lateral.
		float along = abs(local.x) > abs(local.y) ? world_pos.z : world_pos.x;
		col = curb_color(along, 1.0, curb_paint, curb_band(along), 0.0);
	} else if (world_normal.y > 0.5) {
		vec2 cell = world_pos.xz / tile_size;
		vec2 f = fract(cell);
		float h = hash(floor(cell));
		// Espelha/gira cada placa pelo sorteio pra rachadura não se repetir em fila.
		if (h > 0.5) f.x = 1.0 - f.x;
		if (fract(h * 7.0) > 0.5) f = f.yx;
		col = tile_color * textureGrad(slab_tex, f, dFdx(cell), dFdy(cell)).rgb * 1.8 + (h * 0.06 - 0.03);
		// Calçadão de pedra portuguesa em alguns quarteirões.
		if (hash(floor(world_pos.xz / block_period + 0.5) + 3.1) > 0.78) {
			col = texture(pt_tex, world_pos.xz / 3.0).rgb * 0.85;
		}
	} else {
		float along = abs(world_normal.x) > 0.5 ? world_pos.z : world_pos.x;
		float h01 = clamp((world_pos.y - (curb_height - 0.15)) / 0.15, 0.0, 1.0);
		col = curb_color(along, h01, curb_paint, curb_band(along), 1.0);
	}
	ALBEDO = col;
	ROUGHNESS = 0.95;
}
