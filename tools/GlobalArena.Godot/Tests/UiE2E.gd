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

    var tactical_map := app.find_child("GAQATacticalMap", true, false) as Control
    var globe_map := app.find_child("GAQAStrategicGlobe", true, false) as Control
    var globe_button := _button(app, "GAQAReturnStrategic")
    var session_pulse := _label(app, "GAQASessionPulse")
    var local_pulse := _label(app, "GAQALocalEconomyPulse")
    var transit_pulse := _label(app, "GAQAWorkerTransitPulse")
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
    var hypothetical_food := app.find_child("GAQAHypotheticalFood", true, false) as SpinBox
    var camp_button := _button(app, "GAQAForecastCampStops")
    var camp_result := _label(app, "GAQACampForecastResult")
    var real_food := app.find_child("GAQARealTravelFood", true, false) as SpinBox
    var real_food_result := _label(app, "GAQARealTravelFoodResult")
    var real_order := _button(app, "GAQAOrderProvisionedJourney")
    var camp_order := _button(app, "GAQAOrderAutomaticCampJourney")
    var food_assessment := _button(app, "GAQAAssessJourneyFood")
    var food_assessment_result := _label(app, "GAQAJourneySupplyResult")
    var remote_result := _label(app, "GAQARemoteRouteResult")
    var order_move := _button(app, "GAQAMoveWorkers")
    var reassign := _button(app, "GAQAReassign")
    var five := _button(app, "GAQAAdvanceFiveCycles")
    var one := _button(app, "GAQAAdvanceOneCycle")
    var save := _button(app, "GAQASave")
    var load_save := _button(app, "GAQALoad")
    var treasury_status := _label(app, "GAQATreasuryStatus")
    var treasury_reserve := _button(app, "GAQATreasuryReserve")
    var treasury_migration_inspect := _button(app, "GAQATreasuryMigrationInspect")
    var treasury_migration_status := _label(app, "GAQATreasuryMigrationStatus")
    for node in [tactical_map, globe_map, globe_button, session_pulse,
            local_pulse, transit_pulse, inspect, source, destination,
            quantity, status, settlements,
            cycle_report, preview, food, compare, diagnose, survey,
            survey_result, plan_destination, advanced_toggle, remote_target,
            remote_button, remote_order, remote_result, hypothetical_food,
            camp_button, camp_result, real_food, real_food_result,
            real_order, camp_order, food_assessment,
            food_assessment_result, order_move, reassign, five,
            one, save, load_save, treasury_status, treasury_reserve,
            treasury_migration_inspect, treasury_migration_status]:
        if not _require(node != null, "expected named production control missing"):
            return

    # VS106: assert a tactical-first startup and a compact, real data pulse.
    if not _require(tactical_map.visible and not globe_map.visible and
            globe_button.visible and
            session_pulse.text.contains("TURNO 0") and
            session_pulse.text.contains("PRÓX. CICLO") and
            local_pulse.text.contains("PESSOAS 8") and
            local_pulse.text.contains("AG 4") and
            local_pulse.text.contains("MAD 2") and
            local_pulse.text.contains("PED 2") and
            local_pulse.text.contains("ESTOQUES") and
            transit_pulse.text.contains("0 pessoas em trânsito"),
            "VS106 tactical-first dashboard lacks real local economic state"):
        return
    print("GA_UI_E2E_VS106_TACTICAL_FIRST_PULSE_OK")

    # GA-UI-VISUAL-02: the strategic lens must display the exact G18
    # physical polyhedron, not the old G3 diagnostic political colors.
    # Hidden borders and no fluorescent neighbor rings are default UI
    # policy; neither changes the canonical movement graph.
    var global_physical_globe := app.find_child(
            "GA_CanonicalPhysicalTacticalGlobe", true, false) as MeshInstance3D
    var boundaries := app.find_child(
            "GA_TacticalTerritoryBoundaries", true, false) as MeshInstance3D
    var border_toggle := app.find_child(
            "GA_TerritoryBoundariesToggle", true, false) as CheckBox
    var worker_rings := app.find_child(
            "GA_VS05_PhysicalWorkerRings", true, false) as MeshInstance3D
    if not _require(global_physical_globe != null and boundaries != null
            and border_toggle != null and worker_rings != null,
            "canonical tactical globe or optional-overlay control is missing"):
        return
    var real_mesh := global_physical_globe.mesh as ArrayMesh
    var worker_mesh := worker_rings.mesh as ArrayMesh
    if not _require(global_physical_globe.visible and real_mesh != null
            and real_mesh.get_surface_count() == 1
            and real_mesh.surface_get_array_len(0) == 58320,
            "global globe does not show all 3242 canonical G18 polygons"):
        return
    if not _require(not boundaries.visible and
            not border_toggle.button_pressed,
            "strategic territory boundary overlay must begin disabled"):
        return
    if not _require(worker_mesh != null and
            worker_mesh.surface_get_array_len(0) <= 36,
            "neighbour rings still occupy the tactical map by default"):
        return
    print("GA_UI_E2E_CANONICAL_G18_GLOBE_DEFAULT_OVERLAYS_OFF_OK")

    _press(inspect)
    await process_frame
    if not _require(status.text.contains("turno 0") and
            status.text.contains("ciclos 0") and
            settlements.text.contains("agricultura 4, madeira 2, pedra 2") and
            treasury_status.text.contains("RESERVA NÃO REGISTRADA") and
            not treasury_reserve.disabled,
            "initial HUD or explicit dormant-treasury preflight is incorrect"):
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
    if not _require(session_pulse.text.contains("TURNO 5") and
            session_pulse.text.contains("CICLOS 5") and
            local_pulse.text.contains("PESSOAS 8"),
            "VS106 compact pulse not updated after real economic cycles"):
        return
    print("GA_UI_E2E_VS106_CYCLE_PULSE_OK")
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

    # VS103: the player explicitly registers only the empty dormant reservation.
    # This advances one canonical revision, adds no economic action, and issues
    # no money. The V20 save must outrank the still-present V14 compatibility file.
    if not _require(treasury_status.text.contains("RESERVA NÃO REGISTRADA") and
            not treasury_reserve.disabled,
            "VS103 reservation control should be available before explicit registration"):
        return
    # VS105: inspect the V14 file on disk before the player's treasury action.
    # The live session is at movement turn 5 and MUST stay unreserved.
    var legacy_preflight_path := ProjectSettings.globalize_path(
            "user://ga-vs19b-born-civilization.json")
    var legacy_before_preflight := FileAccess.get_file_as_string(
            legacy_preflight_path)
    if not _require(not legacy_before_preflight.is_empty(),
            "VS105 sandbox V14 save must exist for the UI migration test"):
        return
    _press(treasury_migration_inspect)
    await process_frame
    if not _require(treasury_migration_status.text.contains(
                    "SAVE V14 COMPATÍVEL COM V20") and
            treasury_migration_status.text.contains("SOMENTE LEITURA") == false and
            treasury_migration_status.text.contains("nenhum arquivo gravado") and
            treasury_migration_status.text.contains(
                    "Registro da reserva exige ação explícita") and
            not treasury_reserve.disabled and
            treasury_status.text.contains("RESERVA NÃO REGISTRADA") and
            status.text.contains("turno 5") and
            FileAccess.get_file_as_string(legacy_preflight_path) ==
                    legacy_before_preflight,
            "VS105 V14 preflight changed the save/world or failed to diagnose it"):
        return
    print("GA_UI_E2E_VS105_LEGACY_MIGRATION_READ_ONLY_OK")

    _press(treasury_reserve)
    await process_frame
    if not _require(treasury_status.text.contains("RESERVA DORMENTE V1") and
            treasury_status.text.contains("SALDO GASTÁVEL: 0") and
            treasury_status.text.contains("RECIBOS: 0") and
            treasury_status.text.contains("SEM EMISSÃO") and
            treasury_reserve.disabled and status.text.contains("turno 5"),
            "VS103 reservation action created money, an event, or failed to update the HUD"):
        return
    print("GA_UI_E2E_DORMANT_TREASURY_REGISTERED_OK")

    _press(save)
    await process_frame
    if not _require(status.text.contains("SAVE INTEGRADO V20 GRAVADO"),
            "VS103 client did not save the explicit dormant reservation as V20"):
        return
    _press(one)
    await process_frame
    if not _require(status.text.contains("turno 6"),
            "VS103 post-save movement did not advance before restore"):
        return
    _press(load_save)
    await process_frame
    if not _require(status.text.contains("SAVE INTEGRADO V20 REABERTO") and
            status.text.contains("turno 5") and
            treasury_status.text.contains("RESERVA DORMENTE V1") and
            treasury_status.text.contains("SALDO GASTÁVEL: 0") and
            treasury_status.text.contains("RECIBOS: 0") and
            treasury_reserve.disabled,
            "VS103 V20 restore failed to prioritize and reconstruct the dormant reservation"):
        return
    print("GA_UI_E2E_DORMANT_TREASURY_V20_SAVE_RESTORE_OK")

    # VS105: after explicit registration, the exact V20 save is inspected
    # without a second activation, overwriting it, or changing the live turn.
    var v20_preflight_path := ProjectSettings.globalize_path(
            "user://ga-vs20-dormant-treasury.json")
    var v20_before_preflight := FileAccess.get_file_as_string(v20_preflight_path)
    _press(treasury_migration_inspect)
    await process_frame
    if not _require(not v20_before_preflight.is_empty() and
            treasury_migration_status.text.contains(
                    "V20 EXISTENTE: RESERVA DORMENTE JÁ REGISTRADA") and
            treasury_migration_status.text.contains(
                    "Sem nova ativação") and
            treasury_migration_status.text.contains("nenhum arquivo gravado") and
            treasury_reserve.disabled and
            treasury_status.text.contains("SALDO GASTÁVEL: 0") and
            treasury_status.text.contains("RECIBOS: 0") and
            status.text.contains("turno 5") and
            FileAccess.get_file_as_string(v20_preflight_path) ==
                    v20_before_preflight,
            "VS105 V20 inspection mutated save, game, or institutional reserve"):
        return
    print("GA_UI_E2E_VS105_V20_MIGRATION_READ_ONLY_OK")

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
    var selected_remote_legs: int = 0
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
                selected_remote_legs = int(count_text)
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

    _press(food_assessment)
    await process_frame
    if not _require(food_assessment_result.visible and
            food_assessment_result.text.contains("PROPOSTA") and
            food_assessment_result.text.contains("demanda alimentar dos viajantes") and
            food_assessment_result.text.contains("Produção não realizada") and
            food_assessment_result.text.contains("NÃO RESERVA") and
            status.text.contains("turno 5") and
            status.text.contains("Viagens: nenhuma em andamento."),
            "VS30 food/opportunity planning was not read-only or did not display"):
        return
    print("GA_UI_E2E_SUPPLY_DIAGNOSIS_READ_ONLY_OK")

    # VS32: a hypothetical cargo is NOT taken from the real food stock.
    # A group with enough assumed food must reach the same physical ETA,
    # without any mandatory camp or change to existing journal/save.
    hypothetical_food.value = 25.0
    _press(camp_button)
    await process_frame
    if not _require(camp_result.visible and
            camp_result.text.contains("SIMULAÇÃO VS32") and
            camp_result.text.contains("Nenhuma parada") and
            camp_result.text.contains("Chegada estimada turno " +
                str(5 + selected_remote_legs * 2)) and
            camp_result.text.contains("HIPOTÉTICA") and
            camp_result.text.contains("Sem compra") and
            status.text.contains("turno 5") and
            status.text.contains("Viagens: nenhuma em andamento."),
            "VS32 hypothetical food preview failed or committed a journey"):
        return
    print("GA_UI_E2E_VS32_NONBINDING_CAMP_FORECAST_OK")

    hypothetical_food.value = 0.0
    _press(camp_button)
    await process_frame
    if not _require(camp_result.visible and
            camp_result.text.contains("SIMULAÇÃO VS32") and
            camp_result.text.contains("HIPOTÉTICA") and
            status.text.contains("Viagens: nenhuma em andamento."),
            "VS32 no-cargo scenario modified physical state"):
        return
    print("GA_UI_E2E_VS32_ZERO_CARGO_SCENARIO_OK")

    # VS33/VS34: physical purchase routes are distinct from the nonbinding
    # forecast, and rejected zero-food purchases cannot create an itinerary.
    real_food.value = 0.0
    _press(camp_order)
    await process_frame
    if not _require(real_food_result.visible and
            real_food_result.text.contains("COMPRA/PARTIDA RECUSADA") and
            status.text.contains("turno 5") and
            status.text.contains("Viagens: nenhuma em andamento."),
            "VS34 real-camp purchase should fail atomically with zero food"):
        return
    print("GA_UI_E2E_VS34_REAL_CAMP_REJECTION_ATOMIC_OK")

    _press(real_order)
    await process_frame
    if not _require(real_food_result.visible and
            real_food_result.text.contains("COMPRA/PARTIDA RECUSADA") and
            status.text.contains("turno 5") and
            status.text.contains("Viagens: nenhuma em andamento."),
            "VS33 real-food purchase must remain an independent atomic control"):
        return
    print("GA_UI_E2E_VS33_REAL_FOOD_REJECTION_ATOMIC_OK")

    _press(remote_order)
    await process_frame
    if not _require(status.text.contains("ORDEM GLOBAL ÚNICA") and
            status.text.contains("chegada no turno " + str(5 + selected_remote_legs * 2)) and
            status.text.contains("Em viagem longa: 1 pessoas") and
            status.text.contains("turno 5") and
            settlements.text.contains("viajando 1"),
            "VS29 one-order remote travel button failed to commit physical journey"):
        return
    print("GA_UI_E2E_LONG_JOURNEY_ISSUED_OK")
    print("GA_UI_E2E_VS31_LEGACY_TIMELINE_OK legs=" + str(selected_remote_legs))

    _press(food_assessment)
    await process_frame
    if not _require(food_assessment_result.visible and
            food_assessment_result.text.contains("VIAGEM ATIVA") and
            food_assessment_result.text.contains("demanda alimentar dos viajantes") and
            status.text.contains("Em viagem longa: 1 pessoas") and
            settlements.text.contains("viajando 1"),
            "VS30 active journey nutrition diagnostic modified travelling workforce"):
        return
    print("GA_UI_E2E_ACTIVE_SUPPLY_DIAGNOSIS_OK")

    _press(save)
    await process_frame
    if not _require(status.text.contains("SAVE INTEGRADO V20 GRAVADO"),
            "VS29 canonical long-journey was not written to the active V20 envelope"):
        return
    _press(one)
    await process_frame
    if not _require(status.text.contains("turno 6") and
            status.text.contains("Em viagem longa: 1 pessoas"),
            "VS29 travellers vanished or teleported after one turn"):
        return
    _press(load_save)
    await process_frame
    if not _require(status.text.contains("SAVE INTEGRADO V20 REABERTO") and
            status.text.contains("turno 5") and
            status.text.contains("Em viagem longa: 1 pessoas") and
            settlements.text.contains("viajando 1"),
            "VS29 V15 replay did not restore ongoing physical journey"):
        return
    print("GA_UI_E2E_LONG_JOURNEY_V20_REOPEN_OK")

    _press(food_assessment)
    await process_frame
    if not _require(food_assessment_result.visible and
            food_assessment_result.text.contains("VIAGEM ATIVA") and
            status.text.contains("turno 5") and
            status.text.contains("Em viagem longa: 1 pessoas"),
            "VS30 supply diagnostic failed V20 save/reopen or mutated timeline"):
        return
    print("GA_UI_E2E_SUPPLY_DIAGNOSIS_V20_REOPEN_OK")
    print("GA_UI_E2E_PASS main_scene=Main.tscn signals=real godot=4.7.2")
    quit(0)
