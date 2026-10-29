extends Control
## GA-VS03: fine physical tactical tiles own ALL worker activity.
## The strategic Goldberg view only groups and displays derived totals.
## No city, settlement, site founding, coarse workforce or fake game turn.

const SCHEMA := "GA-VS03-PHYSICAL-1"
const RATE := 0.000001
const JOBS := [1, 2, 4]
const JOB_NAMES := ["Agricultura", "Pedra", "Madeira"]

var seed: int = 0
var focus: int = 1
var actions: Array[String] = []
var snapshot: Dictionary = {}
var mode: String = "strategic"

var selected_coarse: int = 0
var selected_fine: int = 0
var source_fine: int = 0
var destination_fine: int = 0
var dragging: bool = false
var drag_distance: float = 0.0
var camera_distance: float = 3.35

var globe: Node3D
var view_container: SubViewportContainer
var view: SubViewport
var camera: Camera3D

var title_label: Label
var status_label: Label
var info: RichTextLabel
var open_button: Button
var overview_button: Button
var move_button: Button
var preview_button: Button
var amount: SpinBox
var from_job: OptionButton
var to_job: OptionButton

func _ready() -> void:
	_build_ui()
	if not _load():
		return
	for cell in snapshot.get("coarse", []):
		if int(cell["workers"]) > 0:
			focus = int(cell["id"])
			selected_coarse = focus
			break
	if focus != 1 and not _load():
		return
	source_fine = int(snapshot["initialTile"])
	selected_fine = source_fine
	_rebuild()
	_update_hud()

func _build_ui() -> void:
	var root := HBoxContainer.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_theme_constant_override("separation", 14)
	add_child(root)

	var square := AspectRatioContainer.new()
	square.ratio = 1.0
	square.stretch_mode = AspectRatioContainer.STRETCH_FIT
	square.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	square.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(square)

	view_container = SubViewportContainer.new()
	view_container.stretch = true
	view_container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	view_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	view_container.gui_input.connect(_on_globe_input)
	square.add_child(view_container)

	view = SubViewport.new()
	view.size = Vector2i(760, 760)
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	view.handle_input_locally = true
	view_container.add_child(view)

	var environment := WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.025, 0.04, 0.052)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.62, 0.72, 0.78)
	env.ambient_light_energy = 0.75
	environment.environment = env
	view.add_child(environment)

	globe = Node3D.new()
	view.add_child(globe)
	camera = Camera3D.new()
	camera.position = Vector3(0, 0, camera_distance)
	camera.fov = 48.0
	camera.current = true
	view.add_child(camera)
	camera.look_at(Vector3.ZERO, Vector3.UP)

	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-42, -27, 0)
	light.light_energy = 0.9
	view.add_child(light)

	var panel := PanelContainer.new()
	panel.custom_minimum_size.x = 377
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(panel)
	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 9)
	panel.add_child(content)

	title_label = Label.new()
	title_label.text = "GLOBAL ARENA  |  VS03"
	title_label.add_theme_font_size_override("font_size", 21)
	content.add_child(title_label)

	var subtitle := Label.new()
	subtitle.text = "Trabalhadores em tiles físicos • sem cidades"
	subtitle.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(subtitle)

	status_label = Label.new()
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(status_label)

	open_button = Button.new()
	open_button.text = "Abrir malha tática do setor selecionado"
	open_button.pressed.connect(_open_tactical)
	content.add_child(open_button)

	overview_button = Button.new()
	overview_button.text = "Voltar à visão estratégica"
	overview_button.pressed.connect(_open_strategic)
	content.add_child(overview_button)

	info = RichTextLabel.new()
	info.bbcode_enabled = true
	info.size_flags_vertical = Control.SIZE_EXPAND_FILL
	info.custom_minimum_size = Vector2(320, 300)
	content.add_child(info)

	var transfer_label := Label.new()
	transfer_label.text = "Transferir trabalhadores entre tiles vizinhos:"
	content.add_child(transfer_label)

	var jobbar := HBoxContainer.new()
	content.add_child(jobbar)
	from_job = OptionButton.new()
	from_job.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	to_job = OptionButton.new()
	to_job.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for j in range(JOBS.size()):
		from_job.add_item(JOB_NAMES[j], JOBS[j])
		to_job.add_item(JOB_NAMES[j], JOBS[j])
	jobbar.add_child(from_job)
	jobbar.add_child(to_job)
	from_job.select(0)
	to_job.select(0)

	var countbar := HBoxContainer.new()
	content.add_child(countbar)
	var lbl := Label.new()
	lbl.text = "Quantidade:"
	countbar.add_child(lbl)
	amount = SpinBox.new()
	amount.min_value = 1
	amount.max_value = 8
	amount.step = 1
	amount.value = 3
	countbar.add_child(amount)

	move_button = Button.new()
	move_button.text = "Alocar trabalhadores no tile tático"
	move_button.pressed.connect(_move_workers)
	content.add_child(move_button)

	preview_button = Button.new()
	preview_button.text = "Recalcular prévia de oferta e consumo"
	preview_button.pressed.connect(_advance_preview)
	content.add_child(preview_button)

	var note := Label.new()
	note.text = "Protótipo: sem cidades, estoques liquidados, save ou turno canônico. A visão estratégica não contém moradores."
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	note.modulate = Color(0.78, 0.76, 0.61)
	content.add_child(note)

func _load() -> bool:
	var dll := ProjectSettings.globalize_path(
		"res://../GlobalArena.Console/bin/Release/net10.0/GlobalArena.Console.dll")
	if not FileAccess.file_exists(dll):
		status_label.text = "ERRO: compile primeiro GlobalArena.Console em Release."
		push_error(status_label.text)
		return false
	var cmd := PackedStringArray([dll, "tactical-bridge", str(seed), str(focus)])
	for item in actions:
		cmd.append(item)
	var output: Array = []
	var code := OS.execute("dotnet", cmd, output, false)
	if code != 0 or output.is_empty():
		status_label.text = "ERRO: o núcleo C# recusou a ordem. Histórico não foi alterado."
		push_error(status_label.text)
		return false
	var parser := JSON.new()
	if parser.parse(str(output[0])) != OK or typeof(parser.data) != TYPE_DICTIONARY:
		status_label.text = "ERRO: resposta inválida da ponte C#."
		push_error(status_label.text)
		return false
	if parser.data.get("schema", "") != SCHEMA:
		status_label.text = "ERRO: versão incompatível do modelo físico."
		return false
	snapshot = parser.data
	return true

func _vector(coords: Array) -> Vector3:
	return Vector3(float(coords[0]), float(coords[1]), float(coords[2]))

func _visible_cells() -> Array:
	return snapshot.get("coarse", []) if mode == "strategic" else snapshot.get("physical", [])

func _color(cell: Dictionary) -> Color:
	if mode == "strategic":
		if int(cell["id"]) == selected_coarse:
			return Color(0.86, 0.81, 0.42)
		if int(cell["workers"]) > 0:
			return Color(0.30, 0.67, 0.49)
	else:
		var id := int(cell["id"])
		if id == selected_fine:
			return Color(0.94, 0.81, 0.38)
		if id == destination_fine:
			return Color(0.95, 0.62, 0.28)
		if int(cell["workers"]) > 0:
			return Color(0.29, 0.78, 0.50)
		if _is_adjacent_to_source(id) and bool(cell["workable"]):
			return Color(0.65, 0.72, 0.35)
	if not bool(cell["land"]):
		return Color(0.10, 0.30, 0.52)
	match str(cell["biome"]):
		"Forest", "Rainforest":
			return Color(0.18, 0.42, 0.28)
		"Desert":
			return Color(0.71, 0.59, 0.40)
		"Highland":
			return Color(0.47, 0.50, 0.48)
		"Tundra", "PolarIce":
			return Color(0.64, 0.72, 0.76)
		_:
			return Color(0.39, 0.58, 0.36)

func _is_adjacent_to_source(id: int) -> bool:
	for cell in snapshot.get("physical", []):
		if int(cell["id"]) == source_fine:
			for adjacent in cell.get("adjacent", []):
				if int(adjacent) == id:
					return true
	return false

func _rebuild() -> void:
	for child in globe.get_children():
		globe.remove_child(child)
		child.queue_free()
	for cell in _visible_cells():
		var geometry := SurfaceTool.new()
		geometry.begin(Mesh.PRIMITIVE_TRIANGLES)
		var center := _vector(cell["center"]) * 1.004
		var boundary: Array = cell["boundary"]
		for i in range(boundary.size()):
			var a := _vector(boundary[i]) * 1.004
			var b := _vector(boundary[(i + 1) % boundary.size()]) * 1.004
			var normal := (a - center).cross(b - center).normalized()
			for vertex in [center, a, b]:
				geometry.set_normal(normal)
				geometry.add_vertex(vertex)
		var mesh := geometry.commit()
		var node := MeshInstance3D.new()
		node.mesh = mesh
		var material := StandardMaterial3D.new()
		material.albedo_color = _color(cell)
		material.cull_mode = BaseMaterial3D.CULL_DISABLED
		material.roughness = 0.9
		node.material_override = material
		globe.add_child(node)

		var outline := SurfaceTool.new()
		outline.begin(Mesh.PRIMITIVE_LINES)
		for i in range(boundary.size()):
			outline.add_vertex(_vector(boundary[i]) * 1.012)
			outline.add_vertex(_vector(boundary[(i + 1) % boundary.size()]) * 1.012)
		var border := MeshInstance3D.new()
		border.mesh = outline.commit()
		var ink := StandardMaterial3D.new()
		ink.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		ink.albedo_color = Color(0.04, 0.08, 0.09)
		border.material_override = ink
		globe.add_child(border)

func _focus_on_cell(cell_id: int) -> void:
	for cell in _visible_cells():
		if int(cell["id"]) == cell_id:
			var center := _vector(cell["center"])
			globe.quaternion = Quaternion(center, Vector3.BACK)
			return

func _open_tactical() -> void:
	mode = "tactical"
	focus = selected_coarse if selected_coarse > 0 else focus
	if not _load():
		return
	var source_visible := false
	for cell in snapshot.get("physical", []):
		if int(cell["id"]) == source_fine:
			source_visible = true
			break
	if not source_visible:
		source_fine = 0
		for row in snapshot.get("assignments", []):
			for tile in snapshot.get("physical", []):
				if int(tile["id"]) == int(row["tile"]):
					source_fine = int(row["tile"])
					break
	selected_fine = source_fine
	destination_fine = 0
	camera_distance = 2.25
	camera.position.z = camera_distance
	_rebuild()
	_focus_on_cell(source_fine if source_fine > 0 else
		int(snapshot["physical"][0]["id"]))
	_update_hud()

func _open_strategic() -> void:
	mode = "strategic"
	camera_distance = 3.35
	camera.position.z = camera_distance
	destination_fine = 0
	_rebuild()
	_focus_on_cell(selected_coarse)
	_update_hud()

func _pick(mouse_position: Vector2) -> void:
	var size := view_container.size
	if size.x <= 0 or size.y <= 0:
		return
	var pixel := Vector2(mouse_position.x * float(view.size.x) / size.x,
		mouse_position.y * float(view.size.y) / size.y)
	var ray := camera.project_ray_normal(pixel).normalized()
	var origin := camera.project_ray_origin(pixel)
	var offset := origin - globe.global_position
	var b := 2.0 * offset.dot(ray)
	var c := offset.length_squared() - 1.004 * 1.004
	var disc := b*b - 4.0*c
	if disc < 0.0:
		return
	var t := (-b - sqrt(disc)) * 0.5
	if t <= 0.0:
		return
	var local := globe.to_local(origin + ray*t).normalized()
	var best := -2.0
	var chosen := 0
	for cell in _visible_cells():
		var score := local.dot(_vector(cell["center"]))
		if score > best:
			best = score
			chosen = int(cell["id"])
	if chosen == 0 or (mode == "tactical" and best < 0.985):
		return
	_select(chosen)

func _on_globe_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var button := event as InputEventMouseButton
		if button.button_index == MOUSE_BUTTON_LEFT:
			if button.pressed:
				dragging = true
				drag_distance = 0.0
			else:
				if dragging and drag_distance < 7.0:
					_pick(button.position)
				dragging = false
		elif button.pressed and button.button_index == MOUSE_BUTTON_WHEEL_UP:
			camera_distance = maxf(1.55, camera_distance - 0.2)
			camera.position.z = camera_distance
		elif button.pressed and button.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			camera_distance = minf(6.3, camera_distance + 0.2)
			camera.position.z = camera_distance
	elif dragging and event is InputEventMouseMotion:
		var motion := event as InputEventMouseMotion
		drag_distance += motion.relative.length()
		globe.rotate_y(motion.relative.x * 0.005)
		globe.rotate_object_local(Vector3.RIGHT, motion.relative.y * 0.005)

func _select(cell_id: int) -> void:
	if mode == "strategic":
		selected_coarse = cell_id
	else:
		selected_fine = cell_id
		var cell := _physical_by_id(cell_id)
		if not cell.is_empty() and int(cell["workers"]) > 0:
			source_fine = cell_id
			destination_fine = 0
		elif _is_adjacent_to_source(cell_id) and bool(cell.get("workable", false)):
			destination_fine = cell_id
		else:
			destination_fine = 0
	_rebuild()
	_update_hud()

func _physical_by_id(tile: int) -> Dictionary:
	for cell in snapshot.get("physical", []):
		if int(cell["id"]) == tile:
			return cell
	return {}

func _update_hud() -> void:
	if snapshot.is_empty():
		return
	title_label.text = "GLOBAL ARENA  |  VS03"
	status_label.text = "Visão: %s | trabalhadores: %d | tiles táticos ocupados: %d | revisão %d" % [
		"ESTRATÉGICA" if mode == "strategic" else "TÁTICA",
		int(snapshot["totalWorkers"]), int(snapshot["occupiedPhysicalTiles"]),
		int(snapshot["revision"])]
	open_button.visible = mode == "strategic"
	overview_button.visible = mode == "tactical"
	move_button.visible = mode == "tactical"
	from_job.visible = mode == "tactical"
	to_job.visible = mode == "tactical"
	amount.visible = mode == "tactical"
	move_button.disabled = destination_fine == 0 or actions.size() >= 64
	preview_button.disabled = actions.size() >= 64

	var line := ""
	if mode == "strategic":
		line = "[b]Setor estratégico %d[/b]\n" % selected_coarse
		for cell in snapshot["coarse"]:
			if int(cell["id"]) == selected_coarse:
				line += "Trabalhadores no resumo: %d\n" % int(cell["workers"])
				line += "Bioma ilustrativo do setor: %s\n\n" % str(cell["biome"])
				break
		line += "A malha estratégica não contém cidades nem trabalhadores próprios.\n\n"
		line += "Os números acima são [b]somatórios derivados[/b] dos tiles táticos, com cada trabalhador contado uma única vez.\n\n"
		line += "Selecione um setor e clique em [b]Abrir malha tática[/b] para trabalhar diretamente nos tiles físicos."
	else:
		line = "[b]Tile físico:[/b] %d • [b]Origem:[/b] %d\n" % [
			selected_fine, source_fine]
		var cell := _physical_by_id(selected_fine)
		if not cell.is_empty():
			line += "Bioma: %s | Trabalhadores: %d\n" % [
				str(cell["biome"]), int(cell["workers"])]
			line += "Setores incidentes: %s\n\n" % str(cell["incidents"])
		line += "[b]Trabalhadores e atividades na origem:[/b]\n"
		var assigned := false
		for item in snapshot.get("assignments", []):
			if int(item["tile"]) == source_fine:
				assigned = true
				line += " %s: %d\n" % [_job_label(int(item["activity"])),
					int(item["workers"])]
		if not assigned:
			line += " Nenhum trabalhador nesta origem.\n"
		line += "\n[b]Demanda e oferta locais:[/b]\n"
		for item in snapshot.get("demand", []):
			if int(item["tile"]) == source_fine:
				line += " Consumo alimentar: %.3f\n" % [
					float(item["raw"]) * RATE]
		for item in snapshot.get("supply", []):
			if int(item["tile"]) == source_fine:
				line += " %s: %.3f\n" % [
					_job_label(int(item["activity"])), float(item["raw"]) * RATE]
		line += "\n[b]Destino físico:[/b] %s\n" % (
			str(destination_fine) if destination_fine != 0 else
			"clique em um tile verde-oliva adjacente")
		line += "Deslocar alguém para outro tile não funda uma cidade. Apenas altera onde essa pessoa trabalha e consome."
	info.text = line

func _job_label(job: int) -> String:
	match job:
		1: return "Agricultura"
		2: return "Pedra"
		4: return "Madeira"
		_: return "Atividade %d" % job

func _move_workers() -> void:
	if destination_fine == 0 or source_fine == 0 or actions.size() >= 64:
		return
	var job_source := from_job.get_selected_id()
	var job_dest := to_job.get_selected_id()
	var command := "m:%d:%d:%d:%d:%d" % [
		source_fine, destination_fine, job_source,
		job_dest, int(amount.value)]
	actions.append(command)
	if not _load():
		actions.pop_back()
		return
	source_fine = destination_fine
	selected_fine = destination_fine
	destination_fine = 0
	_rebuild()
	_update_hud()

func _advance_preview() -> void:
	if actions.size() >= 64:
		return
	actions.append("p")
	if not _load():
		actions.pop_back()
		return
	_update_hud()
