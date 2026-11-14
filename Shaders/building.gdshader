// Fachada procedural: janelas calculadas pela posição no mundo, então qualquer
// tamanho de prédio funciona com um único material. Cor e sorteio por instância.
shader_type spatial;
render_mode diffuse_lambert, specular_schlick_ggx;

instance uniform vec4 wall_color : source_color = vec4(0.45, 0.42, 0.38, 1.0);
instance uniform float light_seed = 0.0;
instance uniform float lit_ratio = 0.3;
/** Acabamento da fachada (v0.9.7): 0 reboco, 1 tijolo, 2 pastilha, 3 chapisco. */
instance uniform float wall_kind = 0.0;

/** Atlas 2x2 de acabamentos (Assets/Textures/City/wall_atlas.png), cada tile = 2 m. */
uniform sampler2D wall_atlas : source_color, filter_nearest_mipmap, repeat_enable;
uniform float tile_meters = 2.0;

/** 1 = noite (muitas janelas acesas), 0 = dia (poucas, fracas). */
uniform float night : hint_range(0.0, 1.0) = 1.0;
uniform float floor_height = 3.2;
uniform float window_spacing = 2.6;
uniform vec3 warm_light : source_color = vec3(1.0, 0.78, 0.45);
uniform vec3 cold_light : source_color = vec3(0.55, 0.85, 1.0);

varying vec3 world_pos;
varying vec3 world_normal;
varying vec3 local_pos;
varying vec3 local_normal;
/** Meia largura da caixa (BoxMesh sem subdivisão: os 4 vértices de cada face têm o mesmo |x| e |z|). */
varying vec2 half_ext;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	local_pos = VERTEX;
	local_normal = NORMAL;
	half_ext = abs(VERTEX.xz);
}

void fragment() {
	vec3 col = wall_color.rgb;
	vec3 emit = vec3(0.0);
	float rough = 0.92;
	float kind = floor(wall_kind + 0.5);

	// Grão estilo PS1 (ruído em blocos de 25 cm).
	float grain = hash(floor(world_pos.xz * 4.0) + floor(world_pos.y * 4.0)) * 0.08 - 0.04;
	col += grain;

	if (abs(world_normal.y) < 0.5) {
		float u = abs(world_normal.x) > 0.5 ? world_pos.z : world_pos.x;
		// Textura do acabamento: tile do atlas escolhido por wall_kind, multiplicado pela cor.
		vec2 full = vec2(u, -world_pos.y) / tile_meters;
		vec2 tile = vec2(mod(kind, 2.0), floor(kind / 2.0));
		// textureGrad com a derivada da coordenada sem fract: evita a linha de mipmap na emenda do tile.
		vec3 tex = textureGrad(wall_atlas, (tile + fract(full)) * 0.5, dFdx(full) * 0.5, dFdy(full) * 0.5).rgb;
		col = kind == 1.0 ? mix(col * tex * 1.8, tex, 0.75) : col * tex * 1.8;
		col += grain * 0.5;
		// Janelas: número inteiro por face e centradas, para nenhuma ficar cortada na quina (v0.10.1).
		bool along_z = abs(local_normal.x) > 0.5;
		float half_w = along_z ? half_ext.y : half_ext.x;
		float lu = along_z ? local_pos.z : local_pos.x;
		float n = max(1.0, floor(2.0 * half_w / window_spacing));
		float spacing = 2.0 * half_w / n;
		vec2 cell = vec2((lu + half_w) / spacing, world_pos.y / floor_height);
		vec2 f = fract(cell);
		vec2 id = floor(cell);
		// Faixa entre andares.
		if (f.y < 0.08) {
			col *= 0.75;
		}
		bool window_area = f.x > 0.2 && f.x < 0.8 && f.y > 0.32 && f.y < 0.82 && id.y >= 1.0;
		// Peitoril claro embaixo da janela.
		if (id.y >= 1.0 && f.x > 0.17 && f.x < 0.83 && f.y > 0.28 && f.y < 0.32) {
			col = vec3(0.62, 0.6, 0.56);
		}
		if (window_area) {
			float h = hash(id + vec2(light_seed, light_seed * 1.7) + sign(world_normal.x + world_normal.z) * 13.0);
			col = vec3(0.05, 0.06, 0.07);
			rough = 0.25;
			float ratio = lit_ratio * mix(0.08, 1.0, night);
			if (h < ratio) {
				vec3 lamp = h < ratio * 0.18 ? cold_light : warm_light;
				// Algumas janelas piscam devagar (TV ligada).
				float flicker = h < ratio * 0.06 ? 0.6 + 0.4 * sin(TIME * 7.0 + h * 40.0) : 1.0;
				col = lamp * 0.6;
				emit = lamp * 1.4 * flicker * mix(0.2, 1.0, night);
			}
		}
		// Térreo: faixa escura de lojas fechadas.
		if (id.y < 1.0 && f.y > 0.15 && f.y < 0.75) {
			col = vec3(0.07, 0.07, 0.08);
		}
	} else {
		col *= 0.55; // telhado
	}

	ALBEDO = col;
	EMISSION = emit;
	ROUGHNESS = rough;
}
