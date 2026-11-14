# Launcher do Dead Frequency.
# 1ª vez: pergunta a pasta de instalação (cria a pasta) e se você quer atualizações automáticas.
# Depois: confere o último release no GitHub; se o jogo não estiver instalado, pergunta se quer baixar;
# se houver versão nova, baixa sozinho (ou pergunta, se as automáticas estiverem desligadas).
# Barra de progresso no download e na instalação, botão JOGAR e engrenagem com as configurações.
# Configurações em user://launcher.cfg; a versão instalada fica em <pasta>/version.txt.
# Canal de atualização: estável (releases públicos da main) ou pré-release (builds vX.Y.Z.N-pre da Internal,
# instáveis e não testados; a pessoa confirma um aviso antes de ligar).
# Autoatualização: cada release publica launcher-version.txt. Se o número for maior que LAUNCHER_VERSION,
# o launcher baixa o executável novo, confere o SHA256 (SHA256SUMS.txt), troca o próprio arquivo e reabre.
# Suba LAUNCHER_VERSION só quando mudar o launcher: sem mudança, o CI reaproveita o launcher da release anterior.
extends Control

const REPO := "dieguesmosken/dead-frequency"
## Versão do launcher (inteiro). Suba quando mudar este app: é o que faz os launchers antigos se atualizarem.
const LAUNCHER_VERSION := 2
const API := "https://api.github.com/repos/%s/releases/latest" % REPO
const API_ALL := "https://api.github.com/repos/%s/releases?per_page=20" % REPO
const RELEASES := "https://github.com/%s/releases" % REPO
const CFG := "user://launcher.cfg"

const GREEN := Color(0.55, 1.0, 0.62)
const GREEN_DIM := Color(0.28, 0.55, 0.33)
const AMBER := Color(1.0, 0.72, 0.25)
const CYAN := Color(0.35, 0.9, 1.0)
const RED := Color(1.0, 0.4, 0.35)
const BG := Color(0.03, 0.06, 0.05)

var cfg := ConfigFile.new()
var install_dir := ""
var auto_update := true
## Recebe as pré-releases da Internal (instáveis).
var prerelease := false
var installed := ""
var latest := ""
var asset_url := ""
var asset_size := 0
var notes := ""

var http_api: HTTPRequest
var http_dl: HTTPRequest
var http_self: HTTPRequest
var _self_step := ""
var _self_url := ""
var _self_sha := ""
var _pending_release = null
var downloading := false
var dl_started := 0.0
var installing := false
var _resolving := false
var zip: ZIPReader
var zip_files: PackedStringArray
var zip_index := 0
var zip_root := ""

var version_label: Label
var status: Label
var bar: ProgressBar
var play: Button
var gear: Button
var dialog: PanelContainer
var dialog_box: VBoxContainer
var settings_open := false


func _ready() -> void:
	get_window().title = "Dead Frequency Launcher"
	_build_ui()
	_cleanup_old_self()
	_load_cfg()
	_use_system_proxy()
	if install_dir == "":
		_ask_folder(true)
	elif not cfg.has_section_key("launcher", "auto_update"):
		_ask_auto_update()
	else:
		_check()


# ================================================================ interface

func _build_ui() -> void:
	var bg := ColorRect.new()
	bg.color = BG
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var glow := ColorRect.new()
	glow.set_anchors_preset(Control.PRESET_FULL_RECT)
	var sh := Shader.new()
	sh.code = """
shader_type canvas_item;
void fragment() {
	vec2 uv = UV;
	float g = smoothstep(0.9, 0.0, distance(uv, vec2(0.85, 0.15)));
	float line = step(0.5, fract(FRAGCOORD.y * 0.5)) * 0.06;
	float vig = smoothstep(0.45, 0.95, distance(uv, vec2(0.5)));
	COLOR = vec4(vec3(0.05, 0.22, 0.14) * g, g * 0.6) + vec4(0.0, 0.0, 0.0, line + vig * 0.5);
}"""
	var mat := ShaderMaterial.new()
	mat.shader = sh
	glow.material = mat
	glow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(glow)

	var title := _label("DEAD", 84, Color.WHITE)
	title.position = Vector2(44, 52)
	title.add_theme_constant_override("outline_size", 14)
	title.add_theme_color_override("font_outline_color", Color(0, 0, 0))
	add_child(title)
	var title2 := _label("FREQUENCY", 46, GREEN)
	title2.position = Vector2(48, 148)
	title2.add_theme_constant_override("outline_size", 10)
	title2.add_theme_color_override("font_outline_color", Color(0, 0, 0))
	add_child(title2)
	var tag := _label("cidade simulada na zona morta · sintonize a 96.6", 15, AMBER)
	tag.position = Vector2(50, 206)
	add_child(tag)

	version_label = _label("", 15, GREEN_DIM)
	version_label.position = Vector2(500, 16)
	version_label.size = Vector2(170, 24)
	version_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	add_child(version_label)
	add_child(_alpha_badge(Vector2(678, 14)))

	gear = _button("⚙", 22)
	gear.tooltip_text = "Configurações do launcher"
	gear.position = Vector2(16, 12)
	gear.size = Vector2(44, 40)
	gear.pressed.connect(_toggle_settings)
	add_child(gear)
	gear.text = ""
	gear.icon = _gear_icon()

	status = _label("", 16, Color(0.75, 0.82, 0.8))
	status.position = Vector2(44, 312)
	status.size = Vector2(440, 40)
	status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(status)

	bar = ProgressBar.new()
	bar.position = Vector2(44, 368)
	bar.size = Vector2(440, 16)
	bar.show_percentage = false
	var fill := StyleBoxFlat.new()
	fill.bg_color = CYAN
	fill.set_corner_radius_all(8)
	var back := StyleBoxFlat.new()
	back.bg_color = Color(0.1, 0.14, 0.15)
	back.set_corner_radius_all(8)
	bar.add_theme_stylebox_override("fill", fill)
	bar.add_theme_stylebox_override("background", back)
	add_child(bar)

	play = _button("▶  JOGAR", 26)
	play.position = Vector2(516, 344)
	play.size = Vector2(200, 64)
	play.disabled = true
	play.pressed.connect(_launch)
	add_child(play)

	# Caixa central para perguntas e configurações.
	dialog = PanelContainer.new()
	var st := StyleBoxFlat.new()
	st.bg_color = Color(0.04, 0.08, 0.07, 0.97)
	st.border_color = GREEN_DIM
	st.set_border_width_all(2)
	st.set_corner_radius_all(10)
	st.set_content_margin_all(18)
	dialog.add_theme_stylebox_override("panel", st)
	dialog.position = Vector2(110, 60)
	dialog.size = Vector2(540, 300)
	dialog.custom_minimum_size = Vector2(540, 0)
	dialog.visible = false
	add_child(dialog)
	dialog_box = VBoxContainer.new()
	dialog_box.add_theme_constant_override("separation", 12)
	dialog.add_child(dialog_box)

	http_api = HTTPRequest.new()
	http_api.timeout = 15
	http_api.request_completed.connect(_on_api)
	add_child(http_api)
	http_dl = HTTPRequest.new()
	http_dl.use_threads = true
	http_dl.max_redirects = 10
	http_dl.request_completed.connect(_on_download)
	add_child(http_dl)
	http_self = HTTPRequest.new()
	http_self.max_redirects = 10
	http_self.timeout = 120
	http_self.request_completed.connect(_on_self)
	add_child(http_self)


## Etiqueta amarela "ALPHA" ao lado da versão (o jogo está em alfa).
func _alpha_badge(pos: Vector2) -> Label:
	var l := _label("ALPHA", 13, Color(0.1, 0.08, 0.02))
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(1.0, 0.8, 0.16)
	sb.set_corner_radius_all(3)
	sb.content_margin_left = 6
	sb.content_margin_right = 6
	sb.content_margin_top = 1
	sb.content_margin_bottom = 1
	l.add_theme_stylebox_override("normal", sb)
	l.position = pos
	return l


func _label(text: String, size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	return l


func _button(text: String, size := 18, primary := true) -> Button:
	var b := Button.new()
	b.text = text
	b.add_theme_font_size_override("font_size", size)
	for state in ["normal", "hover", "pressed", "disabled", "focus"]:
		var sb := StyleBoxFlat.new()
		sb.set_corner_radius_all(10)
		sb.set_content_margin_all(10)
		match state:
			"normal": sb.bg_color = Color(0.12, 0.35, 0.22) if primary else Color(0.1, 0.13, 0.14)
			"hover": sb.bg_color = Color(0.18, 0.5, 0.3) if primary else Color(0.16, 0.2, 0.21)
			"pressed": sb.bg_color = Color(0.1, 0.28, 0.18)
			"disabled": sb.bg_color = Color(0.1, 0.12, 0.13)
			"focus":
				sb.bg_color = Color(0, 0, 0, 0)
				sb.border_color = GREEN
				sb.set_border_width_all(2)
		b.add_theme_stylebox_override(state, sb)
	b.add_theme_color_override("font_color", Color.WHITE)
	b.add_theme_color_override("font_disabled_color", Color(0.45, 0.5, 0.5))
	return b


func _gear_icon() -> Texture2D:
	var s := 26
	var img := Image.create_empty(s, s, false, Image.FORMAT_RGBA8)
	for y in s:
		for x in s:
			var d := Vector2(x - s * 0.5 + 0.5, y - s * 0.5 + 0.5)
			var r := d.length()
			var outer := 12.0 if cos(atan2(d.y, d.x) * 8.0) > 0.2 else 9.0
			img.set_pixel(x, y, GREEN if r <= outer and r >= 4.0 else Color(0, 0, 0, 0))
	return ImageTexture.create_from_image(img)


func _set_status(text: String, color := Color(0.75, 0.82, 0.8)) -> void:
	status.text = text
	status.add_theme_color_override("font_color", color)


func _refresh_version() -> void:
	installed = _read_installed()
	version_label.text = (installed + ("  (pré-release)" if installed.ends_with("-pre") else "")) if installed != "" else "não instalado"
	play.disabled = not _game_exists() or downloading or installing


func _open_dialog(title: String, text: String) -> void:
	for c in dialog_box.get_children():
		c.queue_free()
	dialog_box.add_child(_label(title, 22, GREEN))
	if text != "":
		var l := _label(text, 15, Color(0.8, 0.86, 0.84))
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		l.custom_minimum_size = Vector2(500, 0)
		dialog_box.add_child(l)
	dialog.visible = true
	dialog.size = Vector2(540, 0)
	dialog.position = Vector2((760 - 540) * 0.5, 40)
	dialog.call_deferred("reset_size")


func _dialog_buttons(buttons: Array) -> void:
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_END
	row.add_theme_constant_override("separation", 10)
	for b in buttons:
		row.add_child(b)
	dialog_box.add_child(row)
	dialog.call_deferred("reset_size")


func _close_dialog() -> void:
	dialog.visible = false
	settings_open = false


# ================================================================ configuração

func _default_dir() -> String:
	# Quem usava o launcher antigo (pasta "game" ao lado do executável) continua com a mesma pasta.
	var legacy := OS.get_executable_path().get_base_dir().path_join("game")
	if FileAccess.file_exists(legacy.path_join("version.txt")):
		return legacy
	var home := OS.get_environment("USERPROFILE") if OS.get_name() == "Windows" else OS.get_environment("HOME")
	if home == "":
		home = OS.get_user_data_dir()
	return home.path_join("Games").path_join("Dead Frequency")


func _load_cfg() -> void:
	cfg.load(CFG)
	install_dir = cfg.get_value("launcher", "install_dir", "")
	auto_update = cfg.get_value("launcher", "auto_update", true)
	prerelease = cfg.get_value("launcher", "prerelease", false)
	_refresh_version()


func _save_cfg(save_auto := false) -> void:
	cfg.set_value("launcher", "install_dir", install_dir)
	if save_auto or cfg.has_section_key("launcher", "auto_update"):
		cfg.set_value("launcher", "auto_update", auto_update)
	cfg.set_value("launcher", "prerelease", prerelease)
	cfg.save(CFG)


func _use_system_proxy() -> void:
	# Respeita HTTPS_PROXY/HTTP_PROXY (redes de empresa/escola).
	var p := OS.get_environment("HTTPS_PROXY")
	if p == "":
		p = OS.get_environment("https_proxy")
	if p == "":
		return
	p = p.replace("http://", "").replace("https://", "").trim_suffix("/")
	var host := p.get_slice(":", 0)
	var port := int(p.get_slice(":", 1)) if p.contains(":") else 8080
	for h in [http_api, http_dl]:
		h.set_https_proxy(host, port)
		h.set_http_proxy(host, port)


func _exe_name() -> String:
	return "DeadFrequency.exe" if OS.get_name() == "Windows" else "DeadFrequency.x86_64"


func _game_exists() -> bool:
	return install_dir != "" and FileAccess.file_exists(install_dir.path_join(_exe_name()))


func _read_installed() -> String:
	if install_dir == "":
		return ""
	var f := FileAccess.open(install_dir.path_join("version.txt"), FileAccess.READ)
	return f.get_as_text().strip_edges() if f else ""


# ================================================================ perguntas

func _ask_folder(first_run: bool) -> void:
	_open_dialog("Pasta de instalação", "Onde o Dead Frequency vai ficar? A pasta é criada se não existir. Seus saves ficam guardados à parte e não se perdem nas atualizações.")
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	var edit := LineEdit.new()
	edit.text = install_dir if install_dir != "" else _default_dir()
	edit.custom_minimum_size = Vector2(380, 38)
	edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(edit)
	var browse := _button("Procurar...", 15, false)
	browse.pressed.connect(func():
		var fd := FileDialog.new()
		fd.file_mode = FileDialog.FILE_MODE_OPEN_DIR
		fd.access = FileDialog.ACCESS_FILESYSTEM
		fd.use_native_dialog = true
		fd.title = "Escolha a pasta de instalação"
		fd.current_dir = edit.text.get_base_dir() if not DirAccess.dir_exists_absolute(edit.text) else edit.text
		fd.dir_selected.connect(func(d): edit.text = d.path_join("Dead Frequency") if not d.ends_with("Dead Frequency") else d)
		add_child(fd)
		fd.popup_centered(Vector2i(640, 420)))
	row.add_child(browse)
	dialog_box.add_child(row)
	var err := _label("", 14, RED)
	dialog_box.add_child(err)
	var ok := _button("Criar pasta e continuar", 17)
	ok.pressed.connect(func():
		var path := edit.text.strip_edges()
		if path == "":
			err.text = "Escolha uma pasta."
			return
		var e := DirAccess.make_dir_recursive_absolute(path)
		if e != OK and not DirAccess.dir_exists_absolute(path):
			err.text = "Não deu para criar a pasta (%s). Escolha outra." % error_string(e)
			return
		install_dir = path
		_save_cfg()
		_refresh_version()
		_close_dialog()
		if first_run and not cfg.has_section_key("launcher", "auto_update"):
			_ask_auto_update()
		else:
			_check())
	var buttons := [ok]
	if not first_run:
		var cancel := _button("Cancelar", 17, false)
		cancel.pressed.connect(_close_dialog)
		buttons.push_front(cancel)
	_dialog_buttons(buttons)


func _ask_auto_update() -> void:
	_open_dialog("Atualizações automáticas", "Quer que o launcher baixe e instale as versões novas sozinho sempre que abrir? Dá para mudar depois na engrenagem.")
	var yes := _button("Sim, atualizar sozinho", 17)
	var no := _button("Não, perguntar antes", 17, false)
	yes.pressed.connect(func(): _set_auto(true))
	no.pressed.connect(func(): _set_auto(false))
	_dialog_buttons([no, yes])


func _set_auto(value: bool) -> void:
	auto_update = value
	_save_cfg(true)
	_close_dialog()
	_check()


func _ask_download(update: bool) -> void:
	var mb := "%.0f MB" % (asset_size / 1048576.0)
	if update:
		_open_dialog("Atualização disponível", "A versão %s saiu (você tem a %s). Baixar agora? (%s)\n\n%s" % [latest, installed, mb, notes])
	else:
		_open_dialog("Instalar o Dead Frequency", "O jogo ainda não está instalado nesta pasta:\n%s\n\nBaixar a versão %s agora? (%s)" % [install_dir, latest, mb])
	var yes := _button("Atualizar" if update else "Baixar e instalar", 17)
	var no := _button("Jogar assim mesmo" if update else "Agora não", 17, false)
	yes.pressed.connect(func():
		_close_dialog()
		_start_download())
	no.pressed.connect(func():
		_close_dialog()
		_set_status("Atualização adiada. Você está na %s." % installed if update else "Clique na engrenagem quando quiser instalar.", AMBER))
	_dialog_buttons([no, yes])


func _toggle_settings() -> void:
	if settings_open:
		_close_dialog()
		return
	if downloading or installing:
		return
	settings_open = true
	_open_dialog("Configurações", "")
	dialog_box.add_child(_label("Pasta de instalação:", 15, GREEN_DIM))
	var path := _label(install_dir if install_dir != "" else "(nenhuma)", 15, Color.WHITE)
	path.autowrap_mode = TextServer.AUTOWRAP_ARBITRARY
	path.custom_minimum_size = Vector2(500, 0)
	dialog_box.add_child(path)
	var auto := CheckButton.new()
	auto.text = "Atualizar automaticamente ao abrir"
	auto.button_pressed = auto_update
	auto.add_theme_font_size_override("font_size", 16)
	auto.toggled.connect(func(v):
		auto_update = v
		_save_cfg(true))
	dialog_box.add_child(auto)
	var pre := CheckButton.new()
	pre.text = "Receber pré-releases (instável)"
	pre.button_pressed = prerelease
	pre.add_theme_font_size_override("font_size", 16)
	pre.add_theme_color_override("font_color", AMBER)
	pre.toggled.connect(func(v):
		if v:
			_ask_prerelease()
		else:
			_set_prerelease(false))
	dialog_box.add_child(pre)
	if prerelease:
		dialog_box.add_child(_label("⚠ Canal de pré-release ligado: builds de teste da Internal.", 13, AMBER))
	var row1 := HBoxContainer.new()
	row1.add_theme_constant_override("separation", 8)
	var change := _button("Trocar pasta", 15, false)
	change.pressed.connect(func():
		_close_dialog()
		_ask_folder(false))
	var open := _button("Abrir pasta", 15, false)
	open.pressed.connect(func():
		if install_dir != "":
			DirAccess.make_dir_recursive_absolute(install_dir)
			OS.shell_open(install_dir))
	var check := _button("Verificar agora", 15, false)
	check.pressed.connect(func():
		_close_dialog()
		_check())
	var reinstall := _button("Reinstalar", 15, false)
	reinstall.pressed.connect(func():
		_close_dialog()
		if asset_url != "":
			_start_download()
		else:
			_check(true))
	for b in [change, open, check, reinstall]:
		row1.add_child(b)
	dialog_box.add_child(row1)
	var site := _button("Página de releases", 15, false)
	site.pressed.connect(func(): OS.shell_open(RELEASES))
	var close := _button("Fechar", 17)
	close.pressed.connect(_close_dialog)
	_dialog_buttons([site, close])


func _ask_prerelease() -> void:
	settings_open = false
	_open_dialog("⚠ Pré-releases: instável e não testado",
		"As pré-releases são builds automáticas de cada mudança em desenvolvimento (canal Internal). "
		+ "Elas NÃO foram testadas: podem travar, perder o save, quebrar o online ou nem abrir. "
		+ "Faça backup do seu save antes. Para voltar, desligue esta opção: o launcher reinstala a versão estável.")
	var no := _button("Cancelar", 16, false)
	no.pressed.connect(func():
		_close_dialog()
		_toggle_settings())
	var yes := _button("Entendi, quero testar", 16)
	yes.add_theme_color_override("font_color", AMBER)
	yes.pressed.connect(func():
		_close_dialog()
		_set_prerelease(true))
	_dialog_buttons([no, yes])


func _set_prerelease(value: bool) -> void:
	prerelease = value
	_save_cfg(true)
	_close_dialog()
	if value:
		_set_status("Canal de pré-release ligado (instável).", AMBER)
		_check()
	elif installed.ends_with("-pre"):
		# Estava numa pré-release: volta para a estável mais nova.
		_set_status("Voltando para a versão estável...", AMBER)
		_check(true)
	else:
		_set_status("Canal estável.", GREEN)


# ================================================================ GitHub

func _check(force_download := false) -> void:
	_refresh_version()
	_set_status("Procurando atualizações...")
	bar.value = 0
	var err := http_api.request(API_ALL if prerelease else API, ["User-Agent: DeadFrequencyLauncher", "Accept: application/vnd.github+json"])
	if err != OK:
		_offline("Não deu para procurar atualizações (%s)." % error_string(err))
	set_meta("force", force_download)


func _offline(msg: String) -> void:
	if _game_exists():
		_set_status(msg + " Jogando a versão instalada.", AMBER)
	else:
		_set_status(msg + " Confira a internet e clique na engrenagem > Verificar agora.", RED)
	_refresh_version()


func _on_api(result: int, code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	if result != HTTPRequest.RESULT_SUCCESS or code != 200:
		_offline("Sem conexão com o GitHub.")
		return
	var data = JSON.parse_string(body.get_string_from_utf8())
	if typeof(data) == TYPE_ARRAY:
		data = _pick_release(data)
	if typeof(data) != TYPE_DICTIONARY:
		_offline("Resposta estranha do GitHub.")
		return
	if _start_self_update(data):
		return # depois de ver o launcher novo (ou desistir), continua em _handle_release
	_handle_release(data)


func _handle_release(data: Dictionary) -> void:
	latest = str(data.get("tag_name", ""))
	notes = str(data.get("body", "")).split("\n---")[0].strip_edges().left(420)
	asset_url = ""
	var platform := "windows" if OS.get_name() == "Windows" else "linux"
	for a in data.get("assets", []):
		var n := str(a.get("name", ""))
		if n.begins_with("DeadFrequency-") and n.contains(platform) and n.ends_with(".zip"):
			asset_url = str(a.get("browser_download_url", ""))
			asset_size = int(a.get("size", 0))
	if asset_url == "":
		_offline("O release %s não tem o arquivo para %s." % [latest, platform])
		return
	var force: bool = get_meta("force", false)
	if not _game_exists():
		_set_status("Jogo não instalado.", AMBER)
		_ask_download(false)
	elif force:
		_start_download()
	elif _compare(latest, installed) > 0:
		if auto_update:
			_start_download()
		else:
			_set_status("Versão nova disponível: %s." % latest, AMBER)
			_ask_download(true)
	else:
		_set_status("Tudo atualizado — %s. Bom turno!" % installed, GREEN)
		bar.value = 100
	_refresh_version()


## Canal de pré-release: a release mais nova da lista (pré ou estável), sem rascunhos.
func _pick_release(list: Array):
	var best = null
	for r in list:
		if typeof(r) != TYPE_DICTIONARY or r.get("draft", false):
			continue
		if best == null or _compare(str(r.get("tag_name", "")), str(best.get("tag_name", ""))) > 0:
			best = r
	return best


## Compara vX.Y.Z[.N-pre]: a estável conta como build 0, então v0.10.0.5-pre > v0.10.0 e v0.10.1 > v0.10.0.9-pre.
func _compare(a: String, b: String) -> int:
	var x := a.trim_prefix("v").trim_suffix("-pre").split(".")
	var y := b.trim_prefix("v").trim_suffix("-pre").split(".")
	for i in 4:
		var xi := int(x[i]) if i < x.size() else 0
		var yi := int(y[i]) if i < y.size() else 0
		if xi != yi:
			return 1 if xi > yi else -1
	return 0


# ================================================================ autoatualização do launcher

func _self_exe() -> String:
	return OS.get_executable_path()


## Começa a conferência do launcher novo. false = nada a fazer (segue direto para o jogo).
func _start_self_update(data: Dictionary) -> bool:
	if OS.has_feature("editor") or _self_step != "":
		return false
	var want := "DeadFrequencyLauncher-windows.exe" if OS.get_name() == "Windows" else "DeadFrequencyLauncher-linux"
	var ver_url := ""
	var sums_url := ""
	_self_url = ""
	for a in data.get("assets", []):
		var n := str(a.get("name", ""))
		var u := str(a.get("browser_download_url", ""))
		if n == "launcher-version.txt":
			ver_url = u
		elif n == "SHA256SUMS.txt":
			sums_url = u
		elif n == want:
			_self_url = u
	if ver_url == "" or sums_url == "" or _self_url == "":
		return false
	_pending_release = data
	set_meta("sums_url", sums_url)
	_self_step = "version"
	if http_self.request(ver_url, ["User-Agent: DeadFrequencyLauncher"]) != OK:
		_self_done()
	return true


func _on_self(result: int, code: int, _h: PackedStringArray, body: PackedByteArray) -> void:
	if result != HTTPRequest.RESULT_SUCCESS or code != 200:
		_self_done()
		return
	match _self_step:
		"version":
			var remote := int(body.get_string_from_utf8().strip_edges())
			if remote <= LAUNCHER_VERSION:
				_self_done()
				return
			_set_status("Atualizando o launcher (versão %d)..." % remote, CYAN)
			_self_step = "sums"
			http_self.request(str(get_meta("sums_url")), ["User-Agent: DeadFrequencyLauncher"])
		"sums":
			var want := _self_url.get_file()
			_self_sha = ""
			for line in body.get_string_from_utf8().split("\n"):
				var parts := line.strip_edges().split(" ", false)
				if parts.size() >= 2 and parts[parts.size() - 1].trim_prefix("*") == want:
					_self_sha = parts[0].to_lower()
			if _self_sha == "":
				_self_done()
				return
			_self_step = "exe"
			http_self.download_file = _self_exe() + ".new"
			http_self.request(_self_url, ["User-Agent: DeadFrequencyLauncher"])
		"exe":
			http_self.download_file = ""
			_swap_self()


## Troca o executável: o atual vira .old (no Windows dá para renomear um .exe aberto), o novo assume e reabre.
func _swap_self() -> void:
	var exe := _self_exe()
	var fresh := exe + ".new"
	if FileAccess.get_sha256(fresh).to_lower() != _self_sha:
		DirAccess.remove_absolute(fresh)
		_set_status("Launcher novo veio corrompido; seguindo com este.", AMBER)
		_self_done()
		return
	DirAccess.remove_absolute(exe + ".old")
	if DirAccess.rename_absolute(exe, exe + ".old") != OK:
		DirAccess.remove_absolute(fresh)
		_set_status("Sem permissão para atualizar o launcher nesta pasta. Baixe o novo na página de releases.", AMBER)
		_self_done()
		return
	if DirAccess.rename_absolute(fresh, exe) != OK:
		DirAccess.rename_absolute(exe + ".old", exe)
		_self_done()
		return
	if OS.get_name() != "Windows":
		OS.execute("chmod", ["+x", exe])
	OS.create_process(exe, [])
	get_tree().quit()


## Sem launcher novo (ou deu errado): continua o fluxo normal do jogo.
func _self_done() -> void:
	_self_step = "skip"
	http_self.download_file = ""
	if _pending_release != null:
		var d = _pending_release
		_pending_release = null
		_handle_release(d)


## Limpa o .old da atualização anterior.
func _cleanup_old_self() -> void:
	if not OS.has_feature("editor"):
		var old := _self_exe() + ".old"
		if FileAccess.file_exists(old):
			DirAccess.remove_absolute(old)


# ================================================================ download e instalação

func _zip_path() -> String:
	return install_dir.path_join(".download.zip")


func _start_download() -> void:
	DirAccess.make_dir_recursive_absolute(install_dir)
	downloading = true
	dl_started = Time.get_ticks_msec() / 1000.0
	# O GitHub responde com um redirecionamento para o servidor de arquivos: pegamos o endereço
	# final primeiro (sem baixar nada) e depois baixamos direto de lá.
	http_dl.download_file = ""
	http_dl.max_redirects = 0
	_resolving = true
	var err := http_dl.request(asset_url, ["User-Agent: DeadFrequencyLauncher"])
	if err != OK:
		downloading = false
		_set_status("Falha ao iniciar o download (%s)." % error_string(err), RED)
	_refresh_version()


func _process(_delta: float) -> void:
	if downloading:
		var done := http_dl.get_downloaded_bytes()
		var total := http_dl.get_body_size()
		if total <= 0:
			total = asset_size
		var secs := maxf(0.1, Time.get_ticks_msec() / 1000.0 - dl_started)
		bar.value = 100.0 * done / maxf(1.0, total)
		_set_status("Baixando %s  ·  %.0f / %.0f MB  ·  %.1f MB/s" % [latest, done / 1048576.0, total / 1048576.0, done / 1048576.0 / secs])
	elif installing:
		_install_step()


func _on_download(result: int, code: int, headers: PackedStringArray, _b: PackedByteArray) -> void:
	if _resolving:
		_resolving = false
		var url := asset_url
		if code >= 300 and code < 400:
			for h in headers:
				if h.to_lower().begins_with("location:"):
					url = h.substr(9).strip_edges()
		http_dl.max_redirects = 10
		http_dl.download_file = _zip_path()
		dl_started = Time.get_ticks_msec() / 1000.0
		var err := http_dl.request(url, ["User-Agent: DeadFrequencyLauncher"])
		if err != OK:
			downloading = false
			_set_status("Falha ao iniciar o download (%s)." % error_string(err), RED)
			_refresh_version()
		return
	downloading = false
	if result != HTTPRequest.RESULT_SUCCESS or code != 200:
		DirAccess.remove_absolute(_zip_path())
		_set_status("O download falhou (%d / %d). Tente de novo pela engrenagem." % [result, code], RED)
		_refresh_version()
		return
	zip = ZIPReader.new()
	if zip.open(_zip_path()) != OK:
		_set_status("O arquivo baixado está corrompido. Tente de novo.", RED)
		_refresh_version()
		return
	zip_files = zip.get_files()
	zip_index = 0
	# Se o zip tiver uma pasta por fora, tira ela.
	zip_root = ""
	for f in zip_files:
		if f.get_file() == _exe_name() and f.contains("/"):
			zip_root = f.get_base_dir() + "/"
	installing = true
	_refresh_version()


func _install_step() -> void:
	# Um arquivo por quadro, para a barra andar e a janela não travar.
	if zip_index >= zip_files.size():
		zip.close()
		installing = false
		DirAccess.remove_absolute(_zip_path())
		var vf := FileAccess.open(install_dir.path_join("version.txt"), FileAccess.WRITE)
		if vf:
			vf.store_string(latest)
			vf.close()
		if OS.get_name() != "Windows":
			OS.execute("chmod", ["+x", install_dir.path_join(_exe_name())])
		bar.value = 100
		_set_status("Pronto! Dead Frequency %s instalado." % latest, GREEN)
		_refresh_version()
		return
	var name := zip_files[zip_index]
	zip_index += 1
	bar.value = 100.0 * zip_index / zip_files.size()
	_set_status("Instalando %s  ·  %d/%d" % [latest, zip_index, zip_files.size()])
	var rel := name.trim_prefix(zip_root)
	if rel == "" or rel.ends_with("/"):
		DirAccess.make_dir_recursive_absolute(install_dir.path_join(rel))
		return
	var target := install_dir.path_join(rel)
	DirAccess.make_dir_recursive_absolute(target.get_base_dir())
	var f := FileAccess.open(target, FileAccess.WRITE)
	if f == null:
		installing = false
		zip.close()
		_set_status("Não deu para gravar %s (o jogo está aberto?). Feche o jogo e tente de novo." % rel, RED)
		_refresh_version()
		return
	f.store_buffer(zip.read_file(name))
	f.close()


# ================================================================ jogar

func _launch() -> void:
	if not _game_exists():
		return
	var pid := OS.create_process(install_dir.path_join(_exe_name()), ["--from-launcher"])
	if pid <= 0:
		_set_status("Não deu para abrir o jogo.", RED)
		return
	_set_status("Abrindo o jogo... boa entrega!", GREEN)
	await get_tree().create_timer(1.0).timeout
	get_tree().quit()
