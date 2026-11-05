extends SceneTree
## GA-QA-VISUAL-002. Runs in a REAL Godot .NET X11 session under Xvfb.
## Checks viewport geometry, context switching, and actual rendered pixels.
## Deterministic screenshots are artifacts, NOT a pixel-perfect golden image.

const CAPTURE_WIDTH := 1600
const CAPTURE_HEIGHT := 900
const MIN_MAP_EDGE := 520.0
const MIN_LANDSCAPE_RATIO := 1.15
const MAX_UNUSED_MAP_SIDEBAR_GAP := 15.0


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
    if not _require(map != null and panel != null and scroller != null,
            context + " missing a semantic layout node"):
        return false
    var rect := map.get_global_rect()
    var right := panel.get_global_rect()
    var scroll := scroller.get_global_rect()
    if not _require(rect.size.x >= MIN_MAP_EDGE and
            rect.size.y >= MIN_MAP_EDGE,
            context + " map viewport unexpectedly small: " + str(rect)):
        return false
    # Approved player direction (2026-11-05): occupy the horizontal area
    # naturally instead of fitting a centered 1:1 AspectRatioContainer.
    if not _require(rect.size.x >= rect.size.y * MIN_LANDSCAPE_RATIO,
            context + " tactical/strategic viewport is not landscape: " + str(rect)):
        return false
    if not _require(right.position.x - rect.end.x <= MAX_UNUSED_MAP_SIDEBAR_GAP,
            context + " unused horizontal space remains between viewport and sidebar: " +
            str(rect) + " / " + str(right)):
        return false
    if not _require(rect.end.x + 2.0 <= right.position.x,
            context + " sidebar overlaps map viewport: " + str(rect) + " / " + str(right)):
        return false
    if not _require(right.size.x >= 350.0 and right.size.x <= 465.0 and
            scroll.size.x >= 315.0 and
            scroll.size.y >= maxf(390.0, right.size.y * 0.55),
            context + " usable command sidebar collapsed: " + str(right) + " / " + str(scroll)):
        return false
    print("GA_UI_VISUAL_GEOMETRY_OK context=" + context +
        " map=" + str(rect.size) + " ratio=" +
        str(rect.size.x / maxf(rect.size.y, 1.0)) +
        " right=" + str(right.size) +
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

    var globe := _node(app, "GAQAStrategicGlobe")
    var open_tactical := _node(app, "GAQAOpenTactical") as Button
    var tactical := _node(app, "GAQATacticalMap")
    var tactical_hud := _node(app, "GAQATacticalHUD")
    var return_strategic := _node(app, "GAQAReturnStrategic") as Button
    var inspect := _node(app, "GAQAInspect") as Button
    var cycle := _node(app, "GAQASessionPulse") as Label
    var local := _node(app, "GAQALocalEconomyPulse") as Label
    var journeys := _node(app, "GAQAWorkerTransitPulse") as Label
    var advanced := _node(app, "GAQAAdvancedToggle") as Button
    if not _require(globe != null and open_tactical != null and tactical != null and
            tactical_hud != null and return_strategic != null and inspect != null and
            cycle != null and local != null and journeys != null and advanced != null,
            "missing real Godot map navigation or dense information controls"):
        return

    # VS106: the inhabited spherical tactical chart is the actual start
    # screen. The global Goldberg globe remains an available second lens.
    if not _require(tactical.visible and tactical_hud.visible and
            not globe.visible and return_strategic.visible and
            inspect.is_visible_in_tree() and not advanced.button_pressed and
            cycle.text.contains("TURNO 0") and
            cycle.text.contains("PRÓX. CICLO") and
            local.text.contains("PESSOAS 8") and
            local.text.contains("ESTOQUES") and
            journeys.text.contains("0 pessoas em trânsito"),
            "VS106 tactical-first or canonical sidebar did not initialize"):
        return
    print("GA_UI_VISUAL_VS106_TACTICAL_FIRST_OK")
    # GA-UI-SETTLEMENT-GLYPH-01: settled population is pictured as
    # camera-oblique roof geometry, not three permanently visible job dots.
    # MeshInstance3D is Node3D, not Control: find via the Node tree.
    var housing := app.find_child("GASettlementBuiltForms", true, false) as MeshInstance3D
    var worker_badges := app.find_child("GA_VS16_WorkerGroupMarkers", true, false) as MeshInstance3D
    var settlement_hover := _node(app, "GASettlementHover")
    var worker_group_debug := _node(app, "GASettlementLegacyGroupDebug") as CheckBox
    if not _require(housing != null and housing.visible and
            housing.mesh != null and housing.mesh.get_surface_count() > 0 and
            worker_badges != null and not worker_badges.visible and
            settlement_hover != null and worker_group_debug != null and
            not worker_group_debug.button_pressed,
            "settlement built-form marker or hidden legacy job circles missing"):
        return
    worker_group_debug.button_pressed = true
    await process_frame
    if not _require(worker_badges.visible and
            worker_badges.mesh != null,
            "legacy workforce circles diagnostic fallback unavailable"):
        return
    worker_group_debug.button_pressed = false
    await process_frame
    if not _require(not worker_badges.visible and housing.visible,
            "normal unified settlement glyph did not replace debug circles"):
        return
    print("GA_UI_VISUAL_SETTLEMENT_GLYPH_OK 3d_pitched_roof=true debug_fallback=true")
    # No construction has occurred in the initially selected physical world.
    # The new production marker layer must exist but must NOT manufacture
    # a shed or an active worksite for the population-only settlement.
    var worksite_map := app.find_child("GAWorksiteProgressMarkers", true, false) as MeshInstance3D
    if not _require(worksite_map != null and not worksite_map.visible,
            "a production facility was painted without physical construction evidence"):
        return
    print("GA_UI_VISUAL_WORKSITE_OVERLAY_READY no_phantom_facility=true")
    if not _assert_layout(app, "tactical-initial"):
        return
    if not await _capture(output_dir.path_join("02-tactical.png"), "tactical-initial"):
        return

    return_strategic.emit_signal("pressed")
    await process_frame
    await process_frame
    if not _require(globe.visible and not tactical.visible and not tactical_hud.visible and
            open_tactical.visible and not return_strategic.visible,
            "auxiliary global globe did not open"):
        return
    if not _assert_layout(app, "strategic-auxiliary"):
        return
    if not await _capture(output_dir.path_join("01-strategic.png"), "strategic-auxiliary"):
        return

    # Retain the real spherical globe picker and reversible return path.
    # Use actual Godot mouse-button events, not a manual selected-id stub.
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
            "mouse-input territory picker failed to enable tactical return"):
        return
    print("GA_UI_VISUAL_GLOBE_PICK_OK")

    open_tactical.emit_signal("pressed")
    await process_frame
    await process_frame
    if not _require(tactical.visible and tactical_hud.visible and
            not globe.visible and return_strategic.visible,
            "real tactical-context button did not switch map/HUD visibility"):
        return
    if not _assert_layout(app, "returned-tactical"):
        return
    inspect.emit_signal("pressed")
    await process_frame
    var summary := _node(app, "GAQASettlementSummary") as Label
    if not _require(summary != null and summary.text.contains("agricultura 4") and
            cycle.text.contains("TURNO 0") and local.text.contains("PESSOAS 8"),
            "canonical tactical summary lost after spherical navigation"):
        return
    if not await _capture(output_dir.path_join("03-tactical-return.png"), "returned-tactical"):
        return
    if not _require(housing.visible and housing.mesh != null and
            not worker_badges.visible,
            "settlement glyph disappeared after tactical/strategic round trip"):
        return
    print("GA_UI_VISUAL_PASS resolution=1600x900 scenes=tactical,strategic,tactical screenshots=3 settlement_glyphs=true")
    quit(0)
