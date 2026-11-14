// Shaders/comfort_vignette.gdshader
// Vinheta de conforto (v0.10): escurece as bordas da tela quando o carro anda rápido. Menos movimento na visão
// periférica reduz o enjoo (técnica de "restrição de campo de visão" usada em jogos e em VR).
shader_type canvas_item;

uniform float strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	float d = length((SCREEN_UV - 0.5) * vec2(1.0, 0.8));
	float edge = smoothstep(mix(0.75, 0.32, strength), 0.78, d);
	COLOR = vec4(0.0, 0.0, 0.0, edge * strength * 0.85);
}
