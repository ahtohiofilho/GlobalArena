extends SceneTree
## This runs Godot's ACTUAL imported main scene without a desktop window.
## It sends mouse-button events to the ACTUAL 3D viewport GUI callback,
## selecting land before founding through the C# JSON bridge.

func _initialize() -> void:
	call_deferred("_run_smoke")

func _run_smoke() -> void:
	var scene = load("res://legacy_vs02.tscn")
	if scene == null:
		_fail("Main scene failed to load")
		return
	var ui = scene.instantiate()
	root.add_child(ui)
	var before: Dictionary = ui.get("snapshot")
	if before.get("schema", "") != "GA-VS02-DEV-1":
		_fail("C# bridge response not loaded by actual scene")
		return
	if int(before.get("settlementCount", 0)) != 1:
		_fail("Expected one initial settlement")
		return
	if int(before.get("totalWorkers", -1)) != 8:
		_fail("Expected eight resident workers")
		return
	var globe = ui.get("globe")
	if globe == null or globe.get_child_count() != int(before.get("cellCount", -1)):
		_fail("Canonical spherical polygons were not built")
		return
	var frontiers: Array = before.get("frontiers", [])
	if frontiers.is_empty():
		_fail("No founding frontiers available")
		return
	var targets: Array = frontiers[0].get("targetCellIds", [])
	if targets.is_empty():
		_fail("No neighboring free land")
		return
	# Test the exact GUI signal path that the Godot window uses when
	# the player clicks the globe. The old test called _select_cell() and
	# missed the nonworking mouse path entirely.
	var box := ui.get("viewport_box") as SubViewportContainer
	var viewport := ui.get("scene_view") as SubViewport
	var camera = ui.get("camera") as Camera3D
	var globe_node = ui.get("globe") as Node3D
	if box == null or viewport == null or camera == null or globe_node == null:
		_fail("Missing interactive globe viewport or camera")
		return
	await process_frame
	if box.size.x <= 0.0 or box.size.y <= 0.0:
		_fail("Globe viewport container has no layout size")
		return
	var cells: Dictionary = ui.get("cell_lookup")
	var origin_cell: Dictionary = {}
	for cell in before.get("cells", []):
		if int(cell.get("pointId", 0)) == 1:
			origin_cell = cell
			break
	if origin_cell.is_empty():
		_fail("Initial settlement cell not found")
		return
	var origin_id := int(origin_cell["id"])
	ui.set("selected_cell_id", 0)
	await _click_cell(box, viewport, camera, globe_node, origin_cell)
	if int(ui.get("selected_cell_id")) != origin_id:
		_fail("GUI mouse click failed to pick the original settlement")
		return
	# Click a real front-facing neighbor (not one hidden behind the sphere).
	var target: int = 0
	var best_z := -1000.0
	for t in targets:
		var tile: Dictionary = cells[int(t)]
		var coords: Array = tile["center"]
		var world_center := globe_node.to_global(Vector3(
			float(coords[0]), float(coords[1]), float(coords[2])))
		if world_center.z > best_z:
			best_z = world_center.z
			target = int(t)
	await _click_cell(box, viewport, camera, globe_node, cells[target])
	if int(ui.get("target_cell_id")) != target:
		_fail("GUI mouse click did not choose free adjacent land")
		return
	ui.call("_on_found")
	var after: Dictionary = ui.get("snapshot")
	if int(after.get("settlementCount", 0)) != 2:
		_fail("Godot founding action did not change C# settlement count")
		return
	if int(after.get("worldRevision", 0)) != 1 or int(after.get("totalWorkers", 0)) != 8:
		_fail("Godot founding did not preserve workers and advance state")
		return
	ui.call("_on_preview")
	var preview: Dictionary = ui.get("snapshot")
	if int(preview.get("previewTurns", 0)) != 1:
		_fail("Godot next-preview action did not reach C#")
		return
	print("GA-VS02-GODOT-SMOKE-PASS: canonical globe, selection, founding, worker conservation, preview")
	quit(0)

func _fail(reason: String) -> void:
	push_error("GA-VS02 GODOT FAIL: " + reason)
	quit(1)

func _click_cell(box: SubViewportContainer, viewport: SubViewport,
		camera: Camera3D, globe_node: Node3D, cell: Dictionary) -> void:
	var coords: Array = cell["center"]
	var point := globe_node.to_global(Vector3(
		float(coords[0]), float(coords[1]), float(coords[2])) * 1.004)
	var pixel := camera.unproject_position(point)
	var mouse_position := Vector2(
		pixel.x * box.size.x / float(viewport.size.x),
		pixel.y * box.size.y / float(viewport.size.y))
	var down := InputEventMouseButton.new()
	down.button_index = MOUSE_BUTTON_LEFT
	down.pressed = true
	down.position = mouse_position
	box.gui_input.emit(down)
	var up := InputEventMouseButton.new()
	up.button_index = MOUSE_BUTTON_LEFT
	up.pressed = false
	up.position = mouse_position
	box.gui_input.emit(up)
	await process_frame
