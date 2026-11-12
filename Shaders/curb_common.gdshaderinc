// Meio-fio pintado (v0.9.8): concreto cinza com tinta gasta por ruído, junta entre as peças
// de 1 m e sujeira subindo da sarjeta. Usado pela calçada (faixas brancas intercaladas) e
// pelos trechos amarelos de proibido estacionar.

float curb_hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float curb_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(curb_hash(i), curb_hash(i + vec2(1.0, 0.0)), u.x),
			mix(curb_hash(i + vec2(0.0, 1.0)), curb_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

/** along = posição ao longo do meio-fio (m); h01 = altura 0 (sarjeta) a 1 (topo); paint = 0 sem tinta, 1 pintado. */
vec3 curb_color(float along, float h01, vec3 paint_color, float paint, float side) {
	float grain = curb_hash(floor(vec2(along, h01) * vec2(40.0, 8.0))) * 0.06 - 0.03;
	vec3 concrete = vec3(0.3, 0.3, 0.29) + grain;
	// Tinta descascada: manchas de ruído em duas escalas (mais gasta perto do chão).
	float worn = curb_noise(vec2(along * 2.3, h01 * 3.0)) * 0.7 + curb_noise(vec2(along * 9.0, h01 * 11.0)) * 0.3;
	float keep = smoothstep(0.22 + (1.0 - h01) * 0.18 * side, 0.32 + (1.0 - h01) * 0.18 * side, worn);
	vec3 col = mix(concrete, paint_color * (0.94 + grain), paint * keep);
	// Junta entre as peças de concreto (a cada 1 m).
	float joint = fract(along);
	col *= (joint < 0.012 || joint > 0.988) ? 0.55 : 1.0;
	// Sujeira e água da sarjeta escurecem a parte de baixo.
	col *= mix(0.62, 1.0, smoothstep(0.0, 0.5, h01 + (1.0 - side)));
	return col;
}
