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
var hovered_tile: int = 0
var flat_normal: Vector3 = Vector3.UP
var flat_east: Vector3 = Vector3.RIGHT
var flat_north: Vector3 = Vector3.FORWARD
var rendered_cells: Array = []

var globe: Node3D
var view_container: SubViewportContainer
var view: SubViewport
var camera: Camera3D

var title_label: Label
var status_label: Label
var info: RichTextLabel
var legend: RichTextLabel
var hover_label: Label
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

	legend = RichTextLabel.new()
	legend.bbcode_enabled = true
	legend.fit_content = true
	legend.scroll_active = false
	content.add_child(legend)

	hover_label = Label.new()
	hover_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(hover_label)

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
	if mode == "strategic":
		return snapshot.get("coarse", [])
	# Show a bounded LOCAL tactical patch, including the source and its
	# neighbors; all identities still refer to the global physical mesh.
	var selection: Array = []
	var limit := cos(0.82)
	for cell in snapshot.get("physical", []):
		if flat_normal.dot(_vector(cell["center"])) >= limit:
			selection.append(cell)
	return selection

func _set_flat_basis() -> void:
	var anchor: Dictionary = {}
	for cell in snapshot.get("physical", []):
		if int(cell["id"]) == source_fine:
			anchor = cell
			break
	if anchor.is_empty():
		for cell in snapshot.get("coarse", []):
			if int(cell["id"]) == focus:
				anchor = cell
				break
	if anchor.is_empty():
		push_error("No canonical tactical focus available")
		return
	flat_normal = _vector(anchor["center"]).normalized()
	var north_reference := Vector3.UP
	if absf(flat_normal.dot(north_reference)) > 0.95:
		north_reference = Vector3.FORWARD
	flat_east = north_reference.cross(flat_normal).normalized()
	flat_north = flat_normal.cross(flat_east).normalized()

# Same azimuthal-equidistant LOCAL presentation principle as the existing
# tools/GlobalArena.Godot/Scripts/TacticalContextPreview.cs (Flatten).
# The fine-tile physical IDs, adjacency, topology and worker state NEVER
# change with the projection.
func _flat_point(point: Vector3, elevation: float = 0.0) -> Vector3:
	var east := point.dot(flat_east)
	var north := point.dot(flat_north)
	var depth := clampf(point.dot(flat_normal), -1.0, 1.0)
	var radial := sqrt(east * east + north * north)
	var factor := 1.0
	if radial > 0.000001:
		factor = atan2(radial, depth) / radial
	return Vector3(-east * factor, elevation, north * factor)

func _present(point: Vector3, lift: float = 0.0) -> Vector3:
	if mode == "tactical":
		return _flat_point(point, lift)
	return point * (1.004 + lift)

func _highlight_kind(cell: Dictionary) -> String:
	if mode != "tactical":
		return ""
	var id := int(cell["id"])
	if id == destination_fine:
		return "destination"
	if id == source_fine:
		return "source"
	if id == selected_fine:
		return "selected"
	if _is_adjacent_to_source(id) and bool(cell.get("workable", false)):
		return "eligible"
	if id == hovered_tile:
		return "hover"
	return ""

func _highlight_color(kind: String) -> Color:
	match kind:
		"source": return Color("#29e8ff")
		"destination": return Color("#ff9d3b")
		"selected": return Color("#fbe375")
		"eligible": return Color("#baff6c")
		"hover": return Color("#ffffff")
	return Color.TRANSPARENT

# A filled triangular RIBBON rather than Godot's one-pixel line primitive:
# a source/destination highlight is conspicuous at Windows 1080p and
# remains attached to the same physical polygon at every zoom.
func _add_highlight(cell: Dictionary, parent: Node3D) -> void:
	var kind := _highlight_kind(cell)
	if kind.is_empty():
		return
	var fraction := 0.17 if kind in ["source", "destination"] else 0.11
	if kind == "eligible" or kind == "hover":
		fraction = 0.075
	var boundary: Array = cell["boundary"]
	var center := _present(_vector(cell["center"]), 0.018)
	var ribbon := SurfaceTool.new()
	ribbon.begin(Mesh.PRIMITIVE_TRIANGLES)
	for index in range(boundary.size()):
		var outer_a := _present(_vector(boundary[index]), 0.018)
		var outer_b := _present(_vector(boundary[(index + 1) % boundary.size()]), 0.018)
		var inner_a := outer_a.lerp(center, fraction)
		var inner_b := outer_b.lerp(center, fraction)
		for triangle_vertex in [outer_a, outer_b, inner_b, outer_a, inner_b, inner_a]:
			ribbon.set_normal(Vector3.UP)
			ribbon.add_vertex(triangle_vertex)
	var mesh := ribbon.commit()
	var mark := MeshInstance3D.new()
	mark.name = "Highlight_%s_%d" % [kind, int(cell["id"])]
	mark.set_meta("ga_highlight", kind)
	mark.set_meta("ga_tile_id", int(cell["id"]))
	mark.mesh = mesh
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	material.albedo_color = _highlight_color(kind)
	mark.material_override = material
	parent.add_child(mark)


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
	rendered_cells = _visible_cells()
	for child in globe.get_children():
		globe.remove_child(child)
		child.queue_free()
	for cell in rendered_cells:
		var geometry := SurfaceTool.new()
		geometry.begin(Mesh.PRIMITIVE_TRIANGLES)
		var center := _present(_vector(cell["center"]))
		var boundary: Array = cell["boundary"]
		for i in range(boundary.size()):
			var a := _present(_vector(boundary[i]))
			var b := _present(_vector(boundary[(i + 1) % boundary.size()]))
			var normal := Vector3.UP if mode == "tactical" else (a - center).cross(b - center).normalized()
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
			outline.add_vertex(_present(_vector(boundary[i]), 0.008))
			outline.add_vertex(_present(_vector(boundary[(i + 1) % boundary.size()]), 0.008))
		var border := MeshInstance3D.new()
		border.mesh = outline.commit()
		var ink := StandardMaterial3D.new()
		ink.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		ink.albedo_color = Color(0.04, 0.08, 0.09)
		border.material_override = ink
		globe.add_child(border)
		_add_highlight(cell, globe)

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
	hovered_tile = 0
	_set_flat_basis()
	globe.quaternion = Quaternion.IDENTITY
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 1.35
	camera.position = Vector3(0.0, 3.0, 0.0)
	camera.look_at(Vector3.ZERO, Vector3.BACK)
	_rebuild()
	_update_hud()

func _open_strategic() -> void:
	mode = "strategic"
	camera.projection = Camera3D.PROJECTION_PERSPECTIVE
	camera_distance = 3.35
	camera.position = Vector3(0, 0, camera_distance)
	camera.look_at(Vector3.ZERO, Vector3.UP)
	hovered_tile = 0
	destination_fine = 0
	_rebuild()
	_focus_on_cell(selected_coarse)
	_update_hud()

# Return a rendered canonical tile ID at the actual pointer. In the flat
# tactical chart we use precise 2D polygon hit-testing, not a guessed
# nearest-center threshold that can choose another cell through an edge.
func _cell_at(mouse_position: Vector2) -> int:
	var size := view_container.size
	if size.x <= 0 or size.y <= 0:
		return 0
	var pixel := Vector2(mouse_position.x * float(view.size.x) / size.x,
		mouse_position.y * float(view.size.y) / size.y)
	var origin := camera.project_ray_origin(pixel)
	var ray := camera.project_ray_normal(pixel).normalized()
	if mode == "tactical":
		if absf(ray.y) < 0.000001:
			return 0
		var t_plane := -origin.y / ray.y
		if t_plane <= 0.0:
			return 0
		var hit := globe.to_local(origin + ray * t_plane)
		var point := Vector2(hit.x, hit.z)
		for cell in rendered_cells:
			var points: Array = []
			for coords in cell["boundary"]:
				var p := _flat_point(_vector(coords))
				points.append(Vector2(p.x, p.z))
			if _contains_point(point, points):
				return int(cell["id"])
		return 0
	var offset := origin - globe.global_position
	var b := 2.0 * offset.dot(ray)
	var c := offset.length_squared() - 1.004 * 1.004
	var disc := b * b - 4.0 * c
	if disc < 0.0:
		return 0
	var t := (-b - sqrt(disc)) * 0.5
	if t <= 0.0:
		return 0
	var local := globe.to_local(origin + ray * t).normalized()
	var best := -2.0
	var chosen := 0
	for cell in rendered_cells:
		var score := local.dot(_vector(cell["center"]))
		if score > best:
			best = score
			chosen = int(cell["id"])
	return chosen

func _contains_point(point: Vector2, vertices: Array) -> bool:
	var inside := false
	for i in range(vertices.size()):
		var a: Vector2 = vertices[i]
		var b: Vector2 = vertices[(i + 1) % vertices.size()]
		if (a.y > point.y) != (b.y > point.y):
			var crossing := (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x
			if point.x < crossing:
				inside = not inside
	return inside

func _pick(mouse_position: Vector2) -> void:
	var id := _cell_at(mouse_position)
	if id != 0:
		_select(id)

func _hover(mouse_position: Vector2) -> void:
	var candidate := _cell_at(mouse_position) if mode == "tactical" else 0
	if hovered_tile == candidate:
		return
	hovered_tile = candidate
	_rebuild()
	_update_hud()

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
			if mode == "tactical":
				camera.size = maxf(0.55, camera.size - 0.16)
			else:
				camera_distance = maxf(1.55, camera_distance - 0.2)
				camera.position.z = camera_distance
		elif button.pressed and button.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			if mode == "tactical":
				camera.size = minf(2.1, camera.size + 0.16)
			else:
				camera_distance = minf(6.3, camera_distance + 0.2)
				camera.position.z = camera_distance
	elif event is InputEventMouseMotion:
		var motion := event as InputEventMouseMotion
		if dragging:
			drag_distance += motion.relative.length()
			if mode == "tactical":
				var delta := camera.size / maxf(1.0, view_container.size.y)
				# Chart pan; zoom and orientation stay unchanged.
				camera.position += camera.global_basis.x * (-motion.relative.x * delta)
				camera.position += camera.global_basis.y * motion.relative.y * delta
			else:
				globe.rotate_y(motion.relative.x * 0.005)
				globe.rotate_object_local(Vector3.RIGHT, motion.relative.y * 0.005)
		else:
			_hover(motion.position)

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
		"ESTRATÉGICA" if mode == "strategic" else "TÁTICA PLANA",
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
	legend.visible = mode == "tactical"
	hover_label.visible = mode == "tactical"
	legend.text = "[color=#29e8ff]■[/color] Origem   [color=#ff9d3b]■[/color] Destino   [color=#baff6c]■[/color] Vizinhos válidos   [color=#fbe375]■[/color] Selecionado"
	hover_label.text = "Sob o cursor: tile %d" % hovered_tile if hovered_tile > 0 else "Sob o cursor: — | roda: zoom | arrastar: mover mapa"

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
		line = "[b]Origem:[/b] tile %d   [b]Selecionado:[/b] tile %d\n" % [
			source_fine, selected_fine]
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
			"tile %d pronto para alocação" % destination_fine if destination_fine != 0 else
			"escolha um tile contornado em verde-lima")
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
