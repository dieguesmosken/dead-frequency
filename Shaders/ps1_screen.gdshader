// Shaders/ps1_screen.gdshader
// Visual PS1 (Fase 6) na tela toda: pixels grandes (múltiplo inteiro, sem borrão), cores de 15 bits
// (32 tons por canal) e dithering ordenado 4x4, como a saída de vídeo do PS1. Fica numa CanvasLayer
// abaixo da interface (GameSettings), então HUD e menus continuam nítidos.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
/// Linhas desejadas na vertical (360 = completo, 480 = leve). O pixel é sempre um múltiplo inteiro.
uniform float lines = 360.0;
uniform float levels = 32.0;
uniform float dither = 1.0;

const float BAYER[16] = float[16](
	0.0, 8.0, 2.0, 10.0,
	12.0, 4.0, 14.0, 6.0,
	3.0, 11.0, 1.0, 9.0,
	15.0, 7.0, 13.0, 5.0);

void fragment() {
	float height = 1.0 / SCREEN_PIXEL_SIZE.y;
	float px = max(1.0, floor(height / lines + 0.25));
	vec2 cell = floor(FRAGCOORD.xy / px);
	vec2 uv = (cell + 0.5) * px * SCREEN_PIXEL_SIZE;
	vec3 c = texture(screen_tex, uv).rgb;
	int bx = int(mod(cell.x, 4.0));
	int by = int(mod(cell.y, 4.0));
	float threshold = mix(0.5, (BAYER[by * 4 + bx] + 0.5) / 16.0, dither);
	c = floor(c * (levels - 1.0) + threshold) / (levels - 1.0);
	COLOR = vec4(c, 1.0);
}
