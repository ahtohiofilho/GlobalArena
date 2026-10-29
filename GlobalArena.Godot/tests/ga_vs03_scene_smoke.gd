extends SceneTree
## Real Godot scene, GUI click, strategic-to-fine zoom, worker relocation.

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var scene = load("res://main.tscn")
	if scene == null:
		_fail("VS03 scene cannot load")
		return
	var ui = scene.instantiate()
	root.add_child(ui)
	await process_frame
	var before: Dictionary = ui.get("snapshot")
	if before.get("schema", "") != "GA-VS03-PHYSICAL-1":
		_fail("Physical tactical C# bridge not loaded")
		return
	if int(before.get("totalWorkers", 0)) != 8 or int(before.get("occupiedPhysicalTiles", 0)) != 1:
		_fail("Expected one populated physical tile, not a city")
		return
	if int(before.get("globalFineCount", 0)) != 1442:
		_fail("Wrong canonical fine Goldberg physical topology")
		return
	if ui.get("mode") != "strategic":
		_fail("Expected read-only strategic overview")
		return
	ui.call("_open_tactical")
	if ui.get("mode") != "tactical":
		_fail("Cannot open tactical physical view")
		return
	var camera = ui.get("camera") as Camera3D
	if camera.projection != Camera3D.PROJECTION_ORTHOGONAL:
		_fail("Tactical view is not actually projected flat with an orthographic camera")
		return
	if not ui.get("legend").visible or not ui.get("hover_label").visible:
		_fail("Origin/destination and mouse hover controls are missing")
		return
	if not _has_marker(ui, "source", int(before["initialTile"])):
		_fail("Active physical workforce source has no visible cyan ribbon")
		return
	if not _has_kind(ui, "eligible"):
		_fail("Valid neighboring physical tiles are not visually outlined")
		return
	var center: int = int(before["initialTile"])
	var visible: Array = ui.get("snapshot").get("physical", [])
	var source: Dictionary = {}
	var target: Dictionary = {}
	for cell in visible:
		if int(cell["id"]) == center:
			source = cell
	if source.is_empty() or int(source["workers"]) != 8:
		_fail("All eight workers must begin on the same physical fine tile")
		return
	for dest in source["adjacent"]:
		for cell in visible:
			if int(cell["id"]) == int(dest) and bool(cell["workable"]):
				target = cell
				break
		if not target.is_empty():
			break
	if target.is_empty():
		_fail("No visible adjacent workable physical tile")
		return
	await process_frame
	await _click(ui, source)
	if int(ui.get("source_fine")) != center:
		_fail("Mouse failed to select occupied physical source tile")
		return
	await _hover(ui, target)
	if int(ui.get("hovered_tile")) != int(target["id"]):
		_fail("Pointer hover did not reveal the physical tile")
		return
	if not ui.get("hover_label").text.contains(str(int(target["id"]))):
		_fail("Mouse hover ID was not announced in the HUD")
		return
	await _click(ui, target)
	if not _has_marker(ui, "destination", int(target["id"])):
		_fail("Chosen destination has no visible amber ribbon")
		return
	if not _has_marker(ui, "source", center):
		_fail("Selecting a destination hid the origin cyan ribbon")
		return
	if int(ui.get("destination_fine")) != int(target["id"]):
		_fail("Mouse failed to select adjacent physical target")
		return
	if ui.get("move_button").disabled:
		_fail("Worker allocation button was not enabled after selecting target")
		return
	# Zoom is purely visual. It must not change worker state or source.
	var before_size: float = camera.size
	var wheel := InputEventMouseButton.new()
	wheel.button_index = MOUSE_BUTTON_WHEEL_UP
	wheel.pressed = true
	ui.get("view_container").gui_input.emit(wheel)
	if camera.size >= before_size:
		_fail("Orthographic tactical zoom did not work")
		return
	ui.call("_move_workers")
	var moved: Dictionary = ui.get("snapshot")
	if int(moved["revision"]) != 1 or int(moved["totalWorkers"]) != 8:
		_fail("Workforce relocation did not conserve workers and revision")
		return
	if int(moved["occupiedPhysicalTiles"]) != 2:
		_fail("Moving workers to a new fine tile did not change occupancy")
		return
	var allocations: Array = moved["assignments"]
	var at_source := 0
	var at_target := 0
	for row in allocations:
		if int(row["tile"]) == center:
			at_source += int(row["workers"])
		if int(row["tile"]) == int(target["id"]):
			at_target += int(row["workers"])
	if at_source != 5 or at_target != 3:
		_fail("Unexpected physical worker split 5/3")
		return
	ui.call("_advance_preview")
	if int(ui.get("snapshot")["previews"]) != 1:
		_fail("Local economic preview did not advance")
		return
	ui.call("_open_strategic")
	if ui.get("mode") != "strategic":
		_fail("Cannot return to strategic overview")
		return
	if camera.projection != Camera3D.PROJECTION_PERSPECTIVE:
		_fail("Returning to strategic view did not restore spherical perspective")
		return
	print("GA-VS03-GODOT-PASS: 1442 physical tiles, GUI picking, tactical worker allocation 8=5+3, no cities")
	quit(0)

func _click(ui: Control, cell: Dictionary) -> void:
	var box = ui.get("view_container") as SubViewportContainer
	var viewport = ui.get("view") as SubViewport
	var camera = ui.get("camera") as Camera3D
	var globe = ui.get("globe") as Node3D
	var c: Array = cell["center"]
	var position := Vector3(float(c[0]), float(c[1]), float(c[2]))
	var center: Vector3 = globe.to_global(ui.call("_present", position))
	var pixel := camera.unproject_position(center)
	var relative := Vector2(
		pixel.x * box.size.x / float(viewport.size.x),
		pixel.y * box.size.y / float(viewport.size.y))
	for pressed in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = MOUSE_BUTTON_LEFT
		event.pressed = pressed
		event.position = relative
		box.gui_input.emit(event)
	await process_frame

func _hover(ui: Control, cell: Dictionary) -> void:
	var box = ui.get("view_container") as SubViewportContainer
	var viewport = ui.get("view") as SubViewport
	var camera = ui.get("camera") as Camera3D
	var globe = ui.get("globe") as Node3D
	var c: Array = cell["center"]
	var position := Vector3(float(c[0]), float(c[1]), float(c[2]))
	var pixel := camera.unproject_position(globe.to_global(ui.call("_present", position)))
	var motion := InputEventMouseMotion.new()
	motion.position = Vector2(
		pixel.x * box.size.x / float(viewport.size.x),
		pixel.y * box.size.y / float(viewport.size.y))
	box.gui_input.emit(motion)
	await process_frame

func _has_marker(ui: Control, kind: String, tile_id: int) -> bool:
	var globe = ui.get("globe") as Node3D
	for child in globe.get_children():
		if child.has_meta("ga_highlight") and str(child.get_meta("ga_highlight")) == kind:
			if int(child.get_meta("ga_tile_id")) == tile_id:
				return true
	return false

func _has_kind(ui: Control, kind: String) -> bool:
	var globe = ui.get("globe") as Node3D
	for child in globe.get_children():
		if child.has_meta("ga_highlight") and str(child.get_meta("ga_highlight")) == kind:
			return true
	return false

func _fail(reason: String) -> void:
	push_error("GA-VS03 GODOT FAIL: " + reason)
	quit(1)
