extends SceneTree
## This runs Godot's ACTUAL imported main scene without a desktop window.
## It sends a tile click + founding action through the C# JSON bridge.

func _initialize() -> void:
	call_deferred("_run_smoke")

func _run_smoke() -> void:
	var scene = load("res://main.tscn")
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
	var target: int = int(targets[0])
	ui.call("_select_cell", target)
	if int(ui.get("target_cell_id")) != target:
		_fail("Globe selection did not select the adjacent land")
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
