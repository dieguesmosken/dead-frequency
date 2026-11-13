// Shaders/ps1_common.gdshaderinc
// Visual PS1 (Fase 6): "vertex jitter". O PS1 não tinha subpixel, então os vértices grudavam na grade de
// pixels e os modelos tremiam de leve ao se mexer. ps1_jitter = linhas da grade (0 = desligado), definido
// pelo GameSettings (Opções > Gráficos > Visual PS1).
global uniform float ps1_jitter;

vec4 ps1_snap(vec4 clip, vec2 viewport) {
	if (ps1_jitter < 1.0 || clip.w <= 0.0) {
		return clip;
	}
	vec2 grid = vec2(ps1_jitter * viewport.x / max(viewport.y, 1.0), ps1_jitter) * 0.5;
	clip.xy = round(clip.xy / clip.w * grid) / grid * clip.w;
	return clip;
}
