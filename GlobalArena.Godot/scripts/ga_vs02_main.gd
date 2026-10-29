extends Control
## GA-VS02: canonical spherical tile visualization of C# Neolithic preview.
## All world state and orders come from the .NET engine; this script never
## simulates a production turn, price, stock, save or military action.

const SCHEMA := "GA-VS02-DEV-1"
const DISPLAY_SCALE := 1.0 / 1000000.0

var seed: int = 0
var history: Array[String] = []
var snapshot: Dictionary = {}
var cell_lookup: Dictionary = {}
var cell_bodies: Dictionary = {}

var source_point_id: int = 1
var selected_cell_id: int = 0
var target_cell_id: int = 0
var rotating: bool = false
var drag_distance: float = 0.0
var distance: float = 3.5

var globe: Node3D
var viewport_box: SubViewportContainer
var scene_view: SubViewport
var camera: Camera3D
var details: RichTextLabel
var status_label: Label
var hint_label: Label
var found_button: Button
var preview_button: Button
var worker_count: SpinBox

func _ready() -> void:
	_build_layout()
	if not _load_snapshot():
		return
	_rebuild_globe()
	_focus_initial_settlement()
	_refresh_details()

func _build_layout() -> void:
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

	viewport_box = SubViewportContainer.new()
	viewport_box.stretch = true
	viewport_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	viewport_box.size_flags_vertical = Control.SIZE_EXPAND_FILL
	viewport_box.gui_input.connect(_on_globe_drag)
	square.add_child(viewport_box)

	scene_view = SubViewport.new()
	scene_view.size = Vector2i(760, 760)
	scene_view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	scene_view.handle_input_locally = true
	# GUI picking below uses a ray-sphere intersection and the canonical
	# Goldberg centers. It does not depend on SubViewport 3D picking.
	viewport_box.add_child(scene_view)

	var environment := WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.025, 0.044, 0.058)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.63, 0.73, 0.83)
	env.ambient_light_energy = 0.7
	environment.environment = env
	scene_view.add_child(environment)

	globe = Node3D.new()
	globe.name = "CanonicalGlobe"
	scene_view.add_child(globe)
	camera = Camera3D.new()
	camera.position = Vector3(0, 0, distance)
	camera.fov = 48.0
	camera.current = true
	scene_view.add_child(camera)
	camera.look_at(Vector3.ZERO, Vector3.UP)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-40, -32, 0)
	sun.light_energy = 0.95
	scene_view.add_child(sun)

	var sidebar := PanelContainer.new()
	sidebar.custom_minimum_size = Vector2(365, 0)
	sidebar.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(sidebar)
	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 12)
	sidebar.add_child(content)

	var heading := Label.new()
	heading.text = "GLOBAL ARENA  |  VS02"
	heading.add_theme_font_size_override("font_size", 22)
	content.add_child(heading)

	var subtitle := Label.new()
	subtitle.text = "Globo estratégico • protótipo neolítico offline"
	subtitle.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(subtitle)

	status_label = Label.new()
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(status_label)

	hint_label = Label.new()
	hint_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	hint_label.text = "Clique em um povoamento para escolher a origem. Clique em uma terra adjacente para fundar. Arraste para girar e use a roda para zoom."
	content.add_child(hint_label)

	details = RichTextLabel.new()
	details.bbcode_enabled = true
	details.scroll_active = true
	details.size_flags_vertical = Control.SIZE_EXPAND_FILL
	details.custom_minimum_size = Vector2(340, 310)
	content.add_child(details)

	var controls := HBoxContainer.new()
	content.add_child(controls)
	var workers_label := Label.new()
	workers_label.text = "Transferir:"
	controls.add_child(workers_label)
	worker_count = SpinBox.new()
	worker_count.min_value = 1
	worker_count.max_value = 7
	worker_count.step = 1
	worker_count.value = 3
	controls.add_child(worker_count)

	found_button = Button.new()
	found_button.text = "Fundar povoamento"
	found_button.disabled = true
	found_button.pressed.connect(_on_found)
	content.add_child(found_button)

	preview_button = Button.new()
	preview_button.text = "Avançar prévia econômica"
	preview_button.pressed.connect(_on_preview)
	content.add_child(preview_button)

	var caution := Label.new()
	caution.text = "Limite: sem mercado liquidado, save, combate ou turno canônico. Estado recomposto pelo motor C#."
	caution.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	caution.modulate = Color(0.83, 0.75, 0.54)
	content.add_child(caution)

func _dll_path() -> String:
	return ProjectSettings.globalize_path(
		"res://../GlobalArena.Console/bin/Release/net10.0/GlobalArena.Console.dll")

func _load_snapshot() -> bool:
	var dll := _dll_path()
	if not FileAccess.file_exists(dll):
		_show_error("Motor C# não compilado. Execute no diretório do repositório: dotnet build GlobalArena.Console/GlobalArena.Console.csproj -c Release")
		return false
	var parameters := PackedStringArray([dll, "godot-bridge", str(seed)])
	for command in history:
		parameters.append(command)
	var output: Array = []
	var exit_code := OS.execute("dotnet", parameters, output, false)
	if exit_code != 0 or output.is_empty():
		_show_error("Motor C# recusou a ordem ou .NET indisponível (código %d)." % exit_code)
		return false
	var parser := JSON.new()
	if parser.parse(str(output[0])) != OK or typeof(parser.data) != TYPE_DICTIONARY:
		_show_error("Resposta JSON inválida do motor: " + parser.get_error_message())
		return false
	var result: Dictionary = parser.data
	if result.get("schema", "") != SCHEMA:
		_show_error("Versão incompatível da ponte C#/Godot.")
		return false
	snapshot = result
	cell_lookup.clear()
	for cell in snapshot.get("cells", []):
		cell_lookup[int(cell["id"])] = cell
	return true

func _show_error(message: String) -> void:
	status_label.text = "ERRO: " + message
	push_error(message)

func _vector(data: Array) -> Vector3:
	return Vector3(float(data[0]), float(data[1]), float(data[2]))

func _add_triangle(builder: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
	var normal := (b - a).cross(c - a).normalized()
	for vertex in [a, b, c]:
		builder.set_normal(normal)
		builder.add_vertex(vertex)

func _cell_color(cell: Dictionary) -> Color:
	if int(cell.get("id", 0)) == selected_cell_id:
		return Color(0.98, 0.84, 0.34)
	if int(cell.get("owner", 0)) != 0:
		return Color(0.26, 0.77, 0.43)
	if _is_frontier_of_source(int(cell.get("id", 0))):
		return Color(0.58, 0.70, 0.31)
	if not bool(cell.get("land", false)):
		return Color(0.11, 0.32, 0.55)
	match str(cell.get("biome", "")):
		"Forest", "Rainforest":
			return Color(0.17, 0.41, 0.26)
		"Desert":
			return Color(0.70, 0.60, 0.38)
		"PolarIce":
			return Color(0.73, 0.80, 0.84)
		"Highland":
			return Color(0.47, 0.50, 0.44)
		"Wetland":
			return Color(0.32, 0.51, 0.46)
		"Tundra":
			return Color(0.51, 0.58, 0.54)
		_:
			return Color(0.36, 0.58, 0.35)

func _is_frontier_of_source(tile_id: int) -> bool:
	for record in snapshot.get("frontiers", []):
		if int(record["sourcePointId"]) == source_point_id:
			for neighbor in record.get("targetCellIds", []):
				if int(neighbor) == tile_id:
					return true
	return false

func _rebuild_globe() -> void:
	for child in globe.get_children():
		globe.remove_child(child)
		child.queue_free()
	cell_bodies.clear()
	for cell in snapshot.get("cells", []):
		var tile_id := int(cell["id"])
		var center := _vector(cell["center"]) * 1.004
		var boundary: Array = cell["boundary"]
		var surface := SurfaceTool.new()
		surface.begin(Mesh.PRIMITIVE_TRIANGLES)
		for i in range(boundary.size()):
			var a := _vector(boundary[i]) * 1.004
			var b := _vector(boundary[(i + 1) % boundary.size()]) * 1.004
			_add_triangle(surface, center, a, b)
		var mesh := surface.commit()

		var body := StaticBody3D.new()
		body.name = "Cell_%d" % tile_id
		body.input_ray_pickable = true
		var polygon := MeshInstance3D.new()
		polygon.mesh = mesh
		var material := StandardMaterial3D.new()
		material.albedo_color = _cell_color(cell)
		material.cull_mode = BaseMaterial3D.CULL_DISABLED
		material.roughness = 0.85
		polygon.material_override = material
		body.add_child(polygon)
		var collider := CollisionShape3D.new()
		collider.shape = mesh.create_trimesh_shape()
		body.add_child(collider)
		globe.add_child(body)
		cell_bodies[tile_id] = body

		var border := SurfaceTool.new()
		border.begin(Mesh.PRIMITIVE_LINES)
		for i in range(boundary.size()):
			border.add_vertex(_vector(boundary[i]) * 1.012)
			border.add_vertex(_vector(boundary[(i + 1) % boundary.size()]) * 1.012)
		var border_mesh := border.commit()
		var border_instance := MeshInstance3D.new()
		border_instance.mesh = border_mesh
		var border_mat := StandardMaterial3D.new()
		border_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		border_mat.albedo_color = Color(0.045, 0.078, 0.087)
		border_instance.material_override = border_mat
		body.add_child(border_instance)

func _focus_initial_settlement() -> void:
	for cell in snapshot.get("cells", []):
		if int(cell.get("pointId", 0)) == 1:
			selected_cell_id = int(cell["id"])
			var direction := _vector(cell["center"]).normalized()
			# Positive Z faces the camera; all later drags rotate the globe.
			globe.quaternion = Quaternion(direction, Vector3.BACK)
			return

## Coordinate-based picking is independent of Godot physics_object_picking,
## which is disabled by default for a SubViewport. For canonical Goldberg
## dual cells, the nearest spherical cell center provides the correct
## territorial selection after intersecting the visible hemisphere.
func _pick_cell_at(local_mouse_position: Vector2) -> void:
	if snapshot.is_empty() or camera == null or globe == null:
		return
	var container_size := viewport_box.size
	if container_size.x <= 0.0 or container_size.y <= 0.0:
		return
	var viewport_position := Vector2(
		local_mouse_position.x * float(scene_view.size.x) / container_size.x,
		local_mouse_position.y * float(scene_view.size.y) / container_size.y)
	var ray_origin := camera.project_ray_origin(viewport_position)
	var ray_direction := camera.project_ray_normal(viewport_position).normalized()
	# Sphere radius is the same small offset as the canonical surface mesh.
	var relative := ray_origin - globe.global_position
	var b := 2.0 * relative.dot(ray_direction)
	var c := relative.length_squared() - 1.004 * 1.004
	var discriminant := b * b - 4.0 * c
	if discriminant < 0.0:
		return # Click on background, not a territory.
	var distance_to_surface := (-b - sqrt(discriminant)) * 0.5
	if distance_to_surface <= 0.0:
		return
	var hit_direction := globe.to_local(
		ray_origin + ray_direction * distance_to_surface).normalized()
	var best_cell_id := 0
	var best_score := -2.0
	for cell in snapshot.get("cells", []):
		var direction := _vector(cell["center"])
		var score := hit_direction.dot(direction)
		if score > best_score:
			best_score = score
			best_cell_id = int(cell["id"])
	if best_cell_id != 0:
		_select_cell(best_cell_id)

func _on_globe_drag(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var button := event as InputEventMouseButton
		if button.button_index == MOUSE_BUTTON_LEFT:
			if button.pressed:
				rotating = true
				drag_distance = 0.0
			else:
				if rotating and drag_distance < 7.0:
					_pick_cell_at(button.position)
				rotating = false
		elif button.pressed and button.button_index == MOUSE_BUTTON_WHEEL_UP:
			distance = maxf(2.15, distance - 0.25)
			camera.position.z = distance
		elif button.pressed and button.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			distance = minf(6.4, distance + 0.25)
			camera.position.z = distance
	elif rotating and event is InputEventMouseMotion:
		var motion := event as InputEventMouseMotion
		drag_distance += motion.relative.length()
		if motion.relative.length() > 1.0:
			globe.rotate_y(motion.relative.x * 0.005)
			globe.rotate_object_local(Vector3.RIGHT, motion.relative.y * 0.005)

func _select_cell(tile_id: int) -> void:
	if not cell_lookup.has(tile_id):
		return
	var cell: Dictionary = cell_lookup[tile_id]
	selected_cell_id = tile_id
	if int(cell.get("pointId", 0)) != 0:
		source_point_id = int(cell["pointId"])
		target_cell_id = 0
	elif _is_frontier_of_source(tile_id):
		target_cell_id = tile_id
	else:
		target_cell_id = 0
	_rebuild_globe()
	_refresh_details()

func _refresh_details() -> void:
	if snapshot.is_empty():
		return
	var settlements := int(snapshot.get("settlementCount", 0))
	var workers := int(snapshot.get("totalWorkers", 0))
	status_label.text = "Seed %d | revisão %d | povoamentos %d | trabalhadores %d | prévias %d" % [
		seed, int(snapshot["worldRevision"]), settlements,
		workers, int(snapshot["previewTurns"])
	]
	var text := "[b]Povoamento de origem:[/b] P%d\n" % source_point_id
	if cell_lookup.has(selected_cell_id):
		var chosen: Dictionary = cell_lookup[selected_cell_id]
		text += "[b]Tile escolhido:[/b] %d • %s\n" % [
			selected_cell_id, str(chosen["biome"])]
		text += "[b]Proprietário:[/b] %d   [b]Trabalhadores:[/b] %d\n" % [
			int(chosen["owner"]), int(chosen["workers"])]
		text += "[b]Fronteiras selecionáveis:[/b] %s\n\n" % (
			"sim" if _is_frontier_of_source(selected_cell_id) else "não")
	text += "[b]Produção geográfica estimada:[/b]\n"
	var found_supply := false
	for supply in snapshot.get("supply", []):
		if int(supply["pointId"]) == source_point_id:
			found_supply = true
			text += "  %s: %.3f\n" % [
				_commodity(int(supply["commodityId"])),
				float(supply["rawQuantity"]) * DISPLAY_SCALE]
	if not found_supply:
		text += "  Nenhuma oferta positiva neste terreno.\n"
	text += "\n[b]Demanda alimentar:[/b]\n"
	for demand in snapshot.get("demand", []):
		if int(demand["pointId"]) == source_point_id:
			text += "  P%d: %.3f\n" % [
				source_point_id, float(demand["rawQuantity"]) * DISPLAY_SCALE]
	text += "\n[b]Destino para fundação:[/b] %s" % (
		str(target_cell_id) if target_cell_id > 0 else "selecione terra adjacente")
	details.text = text
	found_button.disabled = target_cell_id == 0 or history.size() >= 64
	preview_button.disabled = history.size() >= 64

func _commodity(commodity_id: int) -> String:
	match commodity_id:
		1: return "Alimento"
		2: return "Pedra"
		3: return "Madeira"
		_: return "Item %d" % commodity_id

func _on_found() -> void:
	if target_cell_id == 0 or history.size() >= 64:
		return
	var amount := int(worker_count.value)
	history.append("f:%d:%d:%d" % [
		source_point_id, target_cell_id, amount])
	if not _load_snapshot():
		history.pop_back()
		return
	var new_id := int(snapshot["settlementCount"])
	source_point_id = new_id
	selected_cell_id = target_cell_id
	target_cell_id = 0
	_rebuild_globe()
	_refresh_details()

func _on_preview() -> void:
	if history.size() >= 64:
		return
	history.append("p")
	if not _load_snapshot():
		history.pop_back()
		return
	_refresh_details()
