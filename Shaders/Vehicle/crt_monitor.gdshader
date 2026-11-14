// Monitor CRT de fósforo verde do painel da caminhonete. A imagem vem de um SubViewport (DashMonitor).
// power: 0 = desligado; interference: 0..1 (carga com interferência na caçamba: linhas tremem e chuvisco).
shader_type spatial;
render_mode unshaded, cull_back;

// v0.10.1: filtro linear (o texto em 512x384 não serrilha de longe) e varredura/vinheta mais fracas para ler melhor.
uniform sampler2D screen : source_color, filter_linear;
uniform float power = 1.0;
uniform float interference = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

void fragment() {
	// Tubo levemente abaulado.
	vec2 c = UV - 0.5;
	vec2 uv = UV + c * dot(c, c) * 0.1;
	float row = floor(uv.y * 96.0);
	float t = floor(TIME * 14.0);
	// Interferência: algumas linhas escorregam de lado e a imagem dá uns pulos.
	uv.x += (hash(vec2(row, t)) - 0.5) * 0.05 * interference * step(0.55, hash(vec2(row * 0.37, t)));
	uv.y += step(0.985, hash(vec2(t, 3.0))) * 0.04 * interference;
	vec3 col = vec3(0.0);
	if (uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0)
		col = texture(screen, uv).rgb;
	float lum = max(col.r, max(col.g, col.b));
	vec3 phosphor = vec3(0.22, 1.0, 0.38) * lum;
	float scan = 0.86 + 0.14 * sin(uv.y * 192.0 * 3.14159);
	float snow = hash(floor(uv * vec2(128.0, 96.0)) + t) * interference * 0.35;
	float vignette = 1.0 - 0.6 * smoothstep(0.45, 0.8, length(c));
	vec3 lit = (phosphor * scan + vec3(0.05, 0.3, 0.1) * snow + vec3(0.008, 0.03, 0.012)) * vignette;
	// Desligado: vidro escuro com um reflexo fraco.
	vec3 off = vec3(0.012, 0.02, 0.016) + vec3(0.03) * smoothstep(0.5, 0.0, length(UV - vec2(0.3, 0.25)));
	ALBEDO = mix(off, lit * 1.4, clamp(power, 0.0, 1.0));
}
