extends SceneTree
## Real Godot .NET scene and signal test. This is not a .NET unit test or a
## check that C# merely compiles. All actions below emit signals on actual
## Button/OptionButton nodes instantiated by Main.tscn.
##
## Must only run with a sandboxed user:// directory to protect player saves.

func _initialize() -> void:
    call_deferred("_run")


func _require(ok: bool, message: String) -> bool:
    if not ok:
        push_error("GA_UI_E2E_FAIL: " + message)
        print("GA_UI_E2E_FAIL: " + message)
        quit(1)
        return false
    return true


func _button(scene: Node, node_name: String) -> Button:
    return scene.find_child(node_name, true, false) as Button


func _label(scene: Node, node_name: String) -> Label:
    return scene.find_child(node_name, true, false) as Label


func _press(button: Button) -> void:
    # Emit the SAME Godot signal to which production Main.cs subscribes.
    # A handler removed from the UI makes the assertions below fail.
    button.emit_signal("pressed")


func _run() -> void:
    if not _require(OS.get_environment("GA_UI_E2E_ISOLATED") == "1",
            "must run only with GA_UI_E2E_ISOLATED=1"):
        return
    var user_dir := ProjectSettings.globalize_path("user://")
    if not _require(user_dir.contains("ga-ui-e2e"),
            "refusing to touch non-isolated player saves: " + user_dir):
        return

    var scene := load("res://Scenes/Main.tscn") as PackedScene
    if not _require(scene != null, "real Godot scene failed to import"):
        return
    var app := scene.instantiate()
    if not _require(app != null, "Main.tscn cannot be instantiated"):
        return
    root.add_child(app)
    await process_frame
    await process_frame

    var inspect := _button(app, "GAQAInspect")
    var source := app.find_child("GAQAFromJob", true, false) as OptionButton
    var destination := app.find_child("GAQAToJob", true, false) as OptionButton
    var quantity := app.find_child("GAQAWorkerAmount", true, false) as SpinBox
    var status := _label(app, "GAQAWorkerStatus")
    var settlements := _label(app, "GAQASettlementSummary")
    var cycle_report := _label(app, "GAQAEconomicSummary")
    var preview := _label(app, "GAQAComparisonResult")
    var food := _label(app, "GAQAFoodResult")
    var compare := _button(app, "GAQACompare")
    var diagnose := _button(app, "GAQAFoodDiagnosis")
    var survey := _button(app, "GAQAAdjacentSurvey")
    var survey_result := _label(app, "GAQAAdjacentSurveyResult")
    var plan_destination := _button(app, "GAQAPlanDestination")
    var advanced_toggle := _button(app, "GAQAAdvancedToggle")
    var remote_target := app.find_child("GAQARemoteDestination", true, false) as SpinBox
    var remote_button := _button(app, "GAQAPreviewRemoteRoute")
    var remote_order := _button(app, "GAQAOrderRemoteJourney")
    var remote_result := _label(app, "GAQARemoteRouteResult")
    var order_move := _button(app, "GAQAMoveWorkers")
    var reassign := _button(app, "GAQAReassign")
    var five := _button(app, "GAQAAdvanceFiveCycles")
    var one := _button(app, "GAQAAdvanceOneCycle")
    var save := _button(app, "GAQASave")
    var load_save := _button(app, "GAQALoad")
    for node in [inspect, source, destination, quantity, status, settlements,
            cycle_report, preview, food, compare, diagnose, survey,
            survey_result, plan_destination, advanced_toggle, remote_target,
            remote_button, remote_order, remote_result, order_move, reassign, five,
            one, save, load_save]:
        if not _require(node != null, "expected named production control missing"):
            return

    _press(inspect)
    await process_frame
    if not _require(status.text.contains("turno 0") and
            status.text.contains("ciclos 0") and
            settlements.text.contains("agricultura 4, madeira 2, pedra 2"),
            "inspect button failed to initialize and render initial workers"):
        return
    print("GA_UI_E2E_INSPECTION_OK")

    # The selector drives real Godot ItemSelected → Main.UpdateWorkerLabHud.
    source.select(0)  # Agriculture
    destination.select(2)  # Forestry
    destination.emit_signal("item_selected", 2)
    quantity.value = 2.0
    await process_frame
    if not _require(not reassign.disabled,
            "selection did not enable the connected worker reassignment button"):
        return

    _press(compare)
    await process_frame
    if not _require(preview.visible and
            preview.text.contains("Manter:") and
            preview.text.contains("Mudar 2 trabalhador") and
            settlements.text.contains("agricultura 4, madeira 2, pedra 2") and
            status.text.contains("turno 0"),
            "comparison preview altered the world or did not reach its HUD label"):
        return
    print("GA_UI_E2E_COMPARISON_READ_ONLY_OK")

    _press(diagnose)
    await process_frame
    if not _require(food.visible and not preview.visible and
            food.text.contains("Estoque inicial") and
            food.text.contains("falta física") and
            status.text.contains("turno 0"),
            "food diagnosis did not use its live Godot button/HUD or changed turn"):
        return
    print("GA_UI_E2E_DIAGNOSIS_READ_ONLY_OK")

    _press(survey)
    await process_frame
    if not _require(survey_result.visible and not food.visible and
            survey_result.text.contains("TILES VIZINHOS VIÁVEIS") and
            survey_result.text.contains("agricultura") and
            survey_result.text.contains("madeira") and
            survey_result.text.contains("pedra") and
            settlements.text.contains("agricultura 4, madeira 2, pedra 2") and
            status.text.contains("turno 0"),
            "VS26 neighbour survey failed to show real yields or mutated live game"):
        return
    print("GA_UI_E2E_NEIGHBOUR_SCOUT_READ_ONLY_OK")

    _press(reassign)
    await process_frame
    if not _require(settlements.text.contains("agricultura 2, madeira 4, pedra 2") and
            status.text.contains("turno 0") and
            not food.visible,
            "reassignment button did not commit 4/2/2 to 2/4/2 while preserving turn"):
        return
    print("GA_UI_E2E_REASSIGN_OK")

    _press(five)
    await process_frame
    if not _require(status.text.contains("turno 5") and
            status.text.contains("ciclos 5") and
            cycle_report.text.contains("ÚLTIMOS 5 CICLOS"),
            "five-cycle Godot button failed to advance and refresh the HUD"):
        return
    print("GA_UI_E2E_FIVE_CYCLES_OK")

    _press(save)
    await process_frame
    if not _require(status.text.contains("V14 GRAVADO"),
            "Godot save button did not write canonical V14 state"):
        return

    _press(one)
    await process_frame
    if not _require(status.text.contains("turno 6") and
            status.text.contains("ciclos 6"),
            "one-cycle Godot button did not advance after saving"):
        return

    _press(load_save)
    await process_frame
    if not _require(status.text.contains("V14 REABERTO") and
            status.text.contains("turno 5") and
            status.text.contains("ciclos 5") and
            settlements.text.contains("agricultura 2, madeira 4, pedra 2") and
            cycle_report.text.contains("ÚLTIMOS 5 CICLOS"),
            "load button did not restore canonical save, worker HUD and cycle summary"):
        return
    print("GA_UI_E2E_SAVE_RESTORE_OK")

    # VS27: the destination is chosen by a real Godot button using the
    # VS26 geographic survey, but NO workers are moved at this stage.
    source.select(0)
    destination.select(2)
    destination.emit_signal("item_selected", 2)
    quantity.value = 1.0
    await process_frame
    _press(plan_destination)
    await process_frame
    if not _require(survey_result.visible and
            survey_result.text.contains("DESTINO PREPARADO") and
            survey_result.text.contains("NENHUMA VIAGEM INICIADA") and
            not order_move.disabled and
            status.text.contains("turno 5") and
            settlements.text.contains("agricultura 2, madeira 4, pedra 2") and
            status.text.contains("Viagens: nenhuma em andamento."),
            "VS27 destination preparation did not select the real move control or committed travel prematurely"):
        return
    print("GA_UI_E2E_DESTINATION_PLANNED_READ_ONLY_OK")

    _press(order_move)
    await process_frame
    if not _require(status.text.contains("turno 5") and
            status.text.contains("Em viagem: 1 pessoas") and
            settlements.text.contains("viajando 1") and
            not survey_result.visible,
            "VS27 explicit movement order failed to create canonical transit"):
        return
    print("GA_UI_E2E_EXPLICIT_TRANSIT_ORDER_OK")

    _press(one)
    _press(one)
    await process_frame
    if not _require(status.text.contains("turno 7") and
            not status.text.contains("Em viagem:") and
            settlements.text.contains("NÚCLEOS HABITADOS 2") and
            settlements.text.contains("viajando 0"),
            "VS27 canonical worker transit failed to arrive after two movement ticks"):
        return
    print("GA_UI_E2E_TWO_TICK_ARRIVAL_OK")

    # An old save must still restore the before-travel timeline.
    _press(load_save)
    await process_frame
    if not _require(status.text.contains("turno 5") and
            status.text.contains("Viagens: nenhuma em andamento.") and
            settlements.text.contains("agricultura 2, madeira 4, pedra 2"),
            "VS27 save reload did not undo a later travel order"):
        return
    print("GA_UI_E2E_TRAVEL_SAVE_REWIND_OK")

    # VS28: preview of ANY valid physical tile ID, not just neighbours.
    # Target tile 1 is far from the seeded starting region. A primitive
    # walking path may be available or may need a different future modal;
    # neither case is permission to create an instantaneous colony.
    advanced_toggle.button_pressed = true
    advanced_toggle.emit_signal("toggled", true)
    await process_frame
    remote_target.value = 1.0
    _press(remote_button)
    await process_frame
    if not _require(remote_result.visible and
            remote_result.text.contains("PLANEJAMENTO GLOBAL") and
            remote_result.text.contains("destino 1") and
            remote_result.text.contains("NÃO ORDENA DESLOCAMENTO") and
            status.text.contains("turno 5") and
            status.text.contains("Viagens: nenhuma em andamento.") and
            settlements.text.contains("agricultura 2, madeira 4, pedra 2"),
            "VS28 arbitrary destination route preview failed or committed an action"):
        return
    print("GA_UI_E2E_REMOTE_ROUTE_READ_ONLY_OK")

    # VS29: choose a genuinely reachable nonadjacent destination. This
    # scans physical tile IDs around the loaded workforce origin, relying
    # only on the REAL route-preview button (never a kernel shortcut).
    var origin_regex := RegEx.new()
    origin_regex.compile("Origem ([0-9]+)")
    var origin_match := origin_regex.search(status.text)
    if not _require(origin_match != null,
            "initial physical origin missing from canonical HUD"):
        return
    var origin_id := int(origin_match.get_string(1))
    var found_long_route := false
    for step in range(1, 450):
        for sign in [-1, 1]:
            var candidate: int = origin_id + sign * step
            if candidate < 1 or candidate > int(remote_target.max_value):
                continue
            remote_target.value = float(candidate)
            _press(remote_button)
            if not remote_result.text.contains("ROTA TERRESTRE:"):
                continue
            var count_text := remote_result.text.split("ROTA TERRESTRE: ")[1].split(" ")[0]
            if int(count_text) >= 2:
                found_long_route = true
                break
        if found_long_route:
            break
    if not _require(found_long_route,
            "could not locate a reachable nonadjacent tile through actual UI"):
        return
    await process_frame
    var selected_remote_tile := int(remote_target.value)
    if not _require(status.text.contains("turno 5") and
            status.text.contains("Viagens: nenhuma em andamento."),
            "VS29 remote reconnaissance changed the timeline"):
        return
    print("GA_UI_E2E_REAL_REMOTE_DESTINATION_SELECTED_OK tile=" +
            str(selected_remote_tile))

    _press(remote_order)
    await process_frame
    if not _require(status.text.contains("ORDEM GLOBAL ÚNICA") and
            status.text.contains("Em viagem longa: 1 pessoas") and
            status.text.contains("turno 5") and
            settlements.text.contains("viajando 1"),
            "VS29 one-order remote travel button failed to commit physical journey"):
        return
    print("GA_UI_E2E_LONG_JOURNEY_ISSUED_OK")

    _press(save)
    await process_frame
    if not _require(status.text.contains("V15 GRAVADO"),
            "VS29 canonical long-journey V15 save was not written"):
        return
    _press(one)
    await process_frame
    if not _require(status.text.contains("turno 6") and
            status.text.contains("Em viagem longa: 1 pessoas"),
            "VS29 travellers vanished or teleported after one turn"):
        return
    _press(load_save)
    await process_frame
    if not _require(status.text.contains("V15 REABERTO") and
            status.text.contains("turno 5") and
            status.text.contains("Em viagem longa: 1 pessoas") and
            settlements.text.contains("viajando 1"),
            "VS29 V15 replay did not restore ongoing physical journey"):
        return
    print("GA_UI_E2E_LONG_JOURNEY_V15_REOPEN_OK")
    print("GA_UI_E2E_PASS main_scene=Main.tscn signals=real godot=4.7.2")
    quit(0)
