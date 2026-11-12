// Tela da TV do apartamento: chuvisco, barras de cor, visualizador de áudio e fundo de telejornal.
// O texto dos canais é um Label3D por cima. mode: 0 desligada, 1 jornal, 2 clipe, 3 tempo, 4 fora do ar.
shader_type spatial;
render_mode unshaded, cull_back;

uniform int mode = 0;
uniform float seed = 0.0;
uniform vec3 tint : source_color = vec3(0.2, 0.4, 0.9);
// Chuvisco por cima dos canais (antena ruim). 0 = imagem limpa (palha de aço na antena).
uniform float snow = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233)) + seed) * 43758.5453); }

void fragment() {
	vec2 uv = UV;
	// Antena ruim: linhas tremem de lado de vez em quando.
	if (snow > 0.0 && mode >= 1 && mode <= 4)
		uv.x += (hash(vec2(floor(uv.y * 60.0), floor(TIME * 12.0))) - 0.5) * 0.04 * snow;
	vec3 col = vec3(0.0);
	float scan = 0.85 + 0.15 * sin(uv.y * 240.0 + TIME * 8.0);
	if (mode == 1) {
		// Jornal: fundo azul com faixa de manchete embaixo.
		col = mix(vec3(0.04, 0.08, 0.25), vec3(0.1, 0.25, 0.6), uv.y);
		if (uv.y > 0.78) col = vec3(0.75, 0.08, 0.08);
		if (uv.y > 0.06 && uv.y < 0.12 && uv.x < 0.3) col = vec3(0.9, 0.8, 0.1);
	} else if (mode == 2) {
		// Clipe da 96.6: barras pulando.
		float bars = floor(uv.x * 16.0);
		float h = 0.25 + 0.6 * abs(sin(TIME * (2.0 + hash(vec2(bars, 1.0)) * 4.0) + bars));
		col = vec3(0.02, 0.05, 0.03);
		if (1.0 - uv.y < h && fract(uv.x * 16.0) < 0.8) col = mix(vec3(0.2, 1.0, 0.4), vec3(1.0, 0.3, 0.8), 1.0 - uv.y);
	} else if (mode == 3) {
		col = mix(vec3(0.1, 0.5, 0.85), vec3(0.6, 0.85, 1.0), 1.0 - uv.y);
		vec2 c = uv - vec2(0.78, 0.35);
		if (length(c * vec2(1.6, 1.0)) < 0.12) col = vec3(1.0, 0.85, 0.2);
	} else if (mode == 4) {
		float b = floor(uv.x * 7.0);
		vec3 bars[7] = { vec3(0.8), vec3(0.8, 0.8, 0.0), vec3(0.0, 0.8, 0.8), vec3(0.0, 0.8, 0.0), vec3(0.8, 0.0, 0.8), vec3(0.8, 0.0, 0.0), vec3(0.0, 0.0, 0.8) };
		col = bars[int(clamp(b, 0.0, 6.0))];
		if (uv.y > 0.7) col = vec3(hash(floor(uv * vec2(160.0, 120.0)) + floor(TIME * 24.0)));
	} else if (mode == 5) {
		col = vec3(hash(floor(uv * vec2(160.0, 120.0)) + floor(TIME * 30.0)));
	}
	if (snow > 0.0 && mode >= 1 && mode <= 4) {
		float n = hash(floor(UV * vec2(160.0, 120.0)) + floor(TIME * 30.0));
		col = mix(col, vec3(n), snow);
	}
	ALBEDO = col * scan;
}
