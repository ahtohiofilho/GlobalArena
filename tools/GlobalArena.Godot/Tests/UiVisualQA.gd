extends SceneTree
## GA-QA-VISUAL-002. Runs in a REAL Godot .NET X11 session under Xvfb.
## Checks viewport geometry, context switching, and actual rendered pixels.
## Deterministic screenshots are artifacts, NOT a pixel-perfect golden image.

const CAPTURE_WIDTH := 1600
const CAPTURE_HEIGHT := 900
const MIN_MAP_EDGE := 520.0
const MAP_SQUARE_TOLERANCE := 4.0


func _initialize() -> void:
    call_deferred("_run")


func _require(ok: bool, message: String) -> bool:
    if not ok:
        push_error("GA_UI_VISUAL_FAIL: " + message)
        print("GA_UI_VISUAL_FAIL: " + message)
        quit(1)
        return false
    return true


func _node(scene: Node, key: String) -> Control:
    return scene.find_child(key, true, false) as Control


func _assert_layout(scene: Node, context: String) -> bool:
    var map := _node(scene, "GAQAMapRegion")
    var panel := _node(scene, "GAQARightPanel")
    var scroller := _node(scene, "GAQACommandScroll")
    var slot := _node(scene, "GAQASquareMapSlot")
    if not _require(map != null and panel != null and scroller != null and slot != null,
            context + " missing a semantic layout node"):
        return false
    var rect := map.get_global_rect()
    var right := panel.get_global_rect()
    var scroll := scroller.get_global_rect()
    if not _require(rect.size.x >= MIN_MAP_EDGE and
            rect.size.y >= MIN_MAP_EDGE,
            context + " map viewport unexpectedly small: " + str(rect)):
        return false
    if not _require(absf(rect.size.x - rect.size.y) <= MAP_SQUARE_TOLERANCE,
            context + " map viewport is no longer square: " + str(rect)):
        return false
    if not _require(rect.end.x + 2.0 <= right.position.x,
            context + " sidebar overlaps map viewport: " + str(rect) + " / " + str(right)):
        return false
    if not _require(right.size.x >= 390.0 and right.size.x <= 550.0 and
            scroll.size.x >= 350.0 and
            scroll.size.y >= maxf(390.0, right.size.y * 0.55),
            context + " usable command sidebar collapsed: " + str(right) + " / " + str(scroll)):
        return false
    print("GA_UI_VISUAL_GEOMETRY_OK context=" + context +
        " map=" + str(rect.size) + " right=" + str(right.size) +
        " scroll=" + str(scroll.size))
    return true


func _capture(file_path: String, context: String) -> bool:
    await process_frame
    await process_frame
    await RenderingServer.frame_post_draw
    var screenshot := root.get_texture().get_image()
    if not _require(screenshot != null and
            screenshot.get_width() >= CAPTURE_WIDTH - 10 and
            screenshot.get_height() >= CAPTURE_HEIGHT - 10,
            context + " viewport framebuffer cannot be captured"):
        return false
    var colors := {}
    var max_x := screenshot.get_width()
    var max_y := screenshot.get_height()
    # Sample ONLY inside the large map area, avoiding labels and borders.
    for yi in range(1, 10):
        for xi in range(1, 10):
            var px := int(max_x * (0.04 + 0.48 * float(xi) / 10.0))
            var py := int(max_y * (0.08 + 0.82 * float(yi) / 10.0))
            colors[screenshot.get_pixel(px, py).to_html(false)] = true
    if not _require(colors.size() >= 5,
            context + " map pixels collapsed to nearly uniform image: " + str(colors.size())):
        return false
    var err := screenshot.save_png(file_path)
    if not _require(err == OK, context + " failed saving screenshot: " + str(err)):
        return false
    print("GA_UI_VISUAL_SCREENSHOT_OK context=" + context +
        " image=" + file_path + " colors=" + str(colors.size()))
    return true


func _run() -> void:
    if not _require(OS.get_environment("GA_UI_E2E_ISOLATED") == "1",
            "isolated E2E runtime flag is required"):
        return
    var user_dir := ProjectSettings.globalize_path("user://")
    if not _require(user_dir.contains(".ga-ui-e2e"),
            "refusing to run outside the CI test user:// sandbox: " + user_dir):
        return
    var output_dir := OS.get_environment("GA_UI_VISUAL_ARTIFACT_DIR")
    if not _require(output_dir.contains(".ga-ui-visual"),
            "invalid screenshot artifact directory"):
        return
    if not _require(DirAccess.make_dir_recursive_absolute(output_dir) == OK,
            "cannot create screenshot output directory"):
        return

    var scene := load("res://Scenes/Main.tscn") as PackedScene
    if not _require(scene != null, "Main.tscn failed to import"):
        return
    var app := scene.instantiate()
    if not _require(app != null, "Main.tscn failed to instantiate"):
        return
    root.add_child(app)
    await process_frame
    await process_frame
    await process_frame
    await process_frame

    if not _assert_layout(app, "strategic"):
        return
    if not await _capture(output_dir.path_join("01-strategic.png"), "strategic"):
        return

    var globe := _node(app, "GAQAStrategicGlobe")
    var open_tactical := _node(app, "GAQAOpenTactical") as Button
    var tactical := _node(app, "GAQATacticalMap")
    var tactical_hud := _node(app, "GAQATacticalHUD")
    var return_strategic := _node(app, "GAQAReturnStrategic") as Button
    if not _require(globe != null and open_tactical != null and tactical != null and
            tactical_hud != null and return_strategic != null,
            "missing real Godot map context controls"):
        return
    if not _require(open_tactical.disabled and globe.visible and not tactical.visible,
            "unexpected strategic initial state"):
        return

    # Inject actual Godot mouse-button events through the root Viewport.
    # A raycast in GlobePreview._GuiInput must select a territory, thus
    # enabling the *real* OPEN TACTICAL CONTEXT button.
    var center := globe.get_global_rect().get_center()
    var selected := false
    for offset in [Vector2.ZERO, Vector2(30, 0), Vector2(-30, 0),
            Vector2(0, 30), Vector2(0, -30)]:
        var at: Vector2 = center + offset
        var press := InputEventMouseButton.new()
        press.button_index = MOUSE_BUTTON_LEFT
        press.position = at
        press.global_position = at
        press.pressed = true
        root.push_input(press, true)
        var release := InputEventMouseButton.new()
        release.button_index = MOUSE_BUTTON_LEFT
        release.position = at
        release.global_position = at
        release.pressed = false
        root.push_input(release, true)
        await process_frame
        if not open_tactical.disabled:
            selected = true
            break
    if not _require(selected,
            "mouse-input territory picker failed to enable OPEN TACTICAL"):
        return
    print("GA_UI_VISUAL_GLOBE_PICK_OK")

    open_tactical.emit_signal("pressed")
    await process_frame
    await process_frame
    if not _require(tactical.visible and tactical_hud.visible and
            not globe.visible and return_strategic.visible,
            "real tactical-context button did not switch map/HUD visibility"):
        return
    if not _assert_layout(app, "tactical"):
        return
    var inspect := _node(app, "GAQAInspect") as Button
    if not _require(inspect != null and inspect.is_visible_in_tree(),
            "tactical player commands are not visible"):
        return
    inspect.emit_signal("pressed")
    await process_frame
    var summary := _node(app, "GAQASettlementSummary") as Label
    if not _require(summary != null and summary.text.contains("agricultura 4"),
            "tactical population inspection failed after navigation"):
        return
    if not await _capture(output_dir.path_join("02-tactical.png"), "tactical"):
        return

    return_strategic.emit_signal("pressed")
    await process_frame
    if not _require(globe.visible and not tactical.visible and not tactical_hud.visible,
            "return navigation did not restore strategic view"):
        return
    if not _assert_layout(app, "returned-strategic"):
        return
    print("GA_UI_VISUAL_PASS resolution=1600x900 scenes=strategic,tactical screenshots=2")
    quit(0)
