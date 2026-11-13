// Shaders/Characters/meshy_recolor.gdshader
// Jogador do Meshy (v0.10): pinta camiseta, calça, pele e cabelo com as cores do menu Personagem.
// A máscara (R = camiseta, G = calça, B = pele, A = cabelo) foi gerada uma vez a partir da textura
// original; a cor nova é multiplicada pelo brilho do pixel em relação à média da região, então as
// sombras e as facetas do low-poly continuam. Textura pequena e sem filtro = cara de PS1.
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
#include "res://Shaders/ps1_common.gdshaderinc"

uniform sampler2D base_tex : source_color, filter_nearest;
uniform sampler2D mask_tex : filter_nearest;
uniform vec3 shirt_color : source_color = vec3(0.2, 0.6, 0.25);
uniform vec3 pants_color : source_color = vec3(0.15, 0.25, 0.6);
uniform vec3 skin_color : source_color = vec3(0.78, 0.6, 0.45);
uniform vec3 hair_color : source_color = vec3(0.2, 0.12, 0.07);
/// Brilho médio (linear) de cada região na textura original: camiseta, calça, pele, cabelo.
uniform vec4 ref_lum = vec4(0.1, 0.065, 0.35, 0.02);

vec3 tint(vec3 target, float lum, float ref) {
	return target * clamp(lum / max(ref, 0.001), 0.35, 1.8);
}

void vertex() {
	POSITION = ps1_snap(PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)), VIEWPORT_SIZE);
}

void fragment() {
	vec3 c = texture(base_tex, UV).rgb;
	vec4 m = texture(mask_tex, UV);
	float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
	vec3 o = c;
	o = mix(o, tint(shirt_color, l, ref_lum.x), m.r);
	o = mix(o, tint(pants_color, l, ref_lum.y), m.g);
	o = mix(o, tint(skin_color, l, ref_lum.z), m.b);
	o = mix(o, tint(hair_color, l, ref_lum.w), m.a);
	ALBEDO = o;
	ROUGHNESS = 0.9;
}
