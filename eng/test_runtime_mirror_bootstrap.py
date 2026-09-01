from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
FRAMEWORK_PATH = (
    REPOSITORY_ROOT
    / "src"
    / "OperatorModdedOperations"
    / "CerberusNativeTabFix.cs"
)
EVIDENCE_PATH = FRAMEWORK_PATH.with_name("FrameworkEvidence.cs")
AGREEMENT_PATH = FRAMEWORK_PATH.with_name(
    "CerberusNativeTabFix.PvpPeerAgreement.cs"
)


def find_matching_brace(source: str, opening: int) -> int:
    depth = 0
    index = opening
    state = "code"
    while index < len(source):
        current = source[index]
        following = source[index + 1] if index + 1 < len(source) else ""

        if state == "line-comment":
            if current in "\r\n":
                state = "code"
        elif state == "block-comment":
            if current == "*" and following == "/":
                state = "code"
                index += 1
        elif state == "string":
            if current == "\\":
                index += 1
            elif current == '"':
                state = "code"
        elif state == "character":
            if current == "\\":
                index += 1
            elif current == "'":
                state = "code"
        else:
            if current == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif current == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif current == '"':
                state = "string"
            elif current == "'":
                state = "character"
            elif current == "{":
                depth += 1
            elif current == "}":
                depth -= 1
                if depth == 0:
                    return index
        index += 1

    raise AssertionError("C# method body has no matching closing brace.")


def extract_method(source: str, name: str) -> str:
    declaration = re.search(
        rf"\bprivate\s+(?:static\s+)?[\w:.]+(?:<[^>{{}}]+>)?\s+{re.escape(name)}\s*\(",
        source,
    )
    if declaration is None:
        raise AssertionError(f"Could not find private method {name}.")
    opening = source.find("{", declaration.end())
    if opening < 0:
        raise AssertionError(f"Could not find opening brace for {name}.")
    closing = find_matching_brace(source, opening)
    return source[declaration.start() : closing + 1]


class RuntimeMirrorBootstrapTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.framework = FRAMEWORK_PATH.read_text(encoding="utf-8")
        cls.evidence = EVIDENCE_PATH.read_text(encoding="utf-8")
        cls.agreement = AGREEMENT_PATH.read_text(encoding="utf-8")

    def test_injected_network_behaviours_receive_only_missing_native_list_baseline(self) -> None:
        method = extract_method(
            self.framework,
            "TryInitializeStandaloneBootstrapSyncObjects",
        )
        self.assertIn("if (behaviour.syncObjects == null)", method)
        self.assertIn(
            "new Il2CppSystem.Collections.Generic.List<SyncObject>()",
            method,
        )
        self.assertIn("behaviour.syncObjects = owned;", method)
        self.assertIn("operation.BootstrapSyncObjects.Add(", method)
        self.assertNotIn("syncVarDirtyBits =", method)
        self.assertNotIn("syncObjectDirtyBits =", method)

    def test_registration_hard_gates_every_root_network_behaviour(self) -> None:
        method = extract_method(
            self.framework,
            "EnsureStandaloneBootstrapPrefabRegistered",
        )
        validation = method.index("ValidateStandaloneBootstrapSyncObjects(")
        preflight = method.index("TryPreflightStandalonePeerGameModeClone(")
        registration = method.index("NetworkClient.RegisterSpawnHandler(")
        self.assertLess(validation, registration)
        self.assertLess(preflight, registration)
        self.assertNotIn("NetworkClient.RegisterPrefab(", method)
        self.assertIn("Mirror custom spawn handler registered", method)

    def test_native_spawn_is_single_attempt_and_preconditioned(self) -> None:
        method = extract_method(self.framework, "MaintainStandaloneGameplay")
        validation = method.index("ValidateStandaloneBootstrapSyncObjects(")
        attempt_record = method.index("operation.NetworkSpawnRequested = true;")
        spawn = method.index("NetworkServer.Spawn(")
        self.assertLess(validation, attempt_record)
        self.assertLess(attempt_record, spawn)
        self.assertIn("!operation.NetworkSpawnFailed", method)
        self.assertIn("operation.NetworkSpawnFailed = true;", method)
        self.assertIn("network spawn failed closed on its ", method)
        self.assertIn('"single native attempt: "', method)
        self.assertNotIn("network spawn is waiting", method)

    def test_teardown_unspawns_then_unregisters_then_destroys_owned_root(self) -> None:
        method = extract_method(self.framework, "ReleaseStandaloneGameMode")
        unspawn = method.index("NetworkServer.UnSpawn(bootstrapRoot)")
        unregister = method.index("ReleaseStandalonePeerGameModeSpawnHandler(operation)")
        destroy = method.index("Object.Destroy(bootstrapRoot)")
        clear_ownership = method.index("operation.BootstrapSyncObjects.Clear()")
        self.assertLess(unspawn, unregister)
        self.assertLess(unregister, destroy)
        self.assertLess(destroy, clear_ownership)
        self.assertNotIn("NetworkClient.ClearSpawners", method)
        self.assertNotIn("NetworkClient.UnregisterPrefab", method)
        self.assertNotIn("NetworkClient.UnregisterSpawnHandler", method)
        self.assertNotIn("syncObjects = null", method)

    def test_plugin_version_changes_with_spawn_contract(self) -> None:
        self.assertIn(
            '[BepInPlugin("operator.modded-operations", '
            '"OPERATOR: Modded Operations", "0.3.33")]',
            self.framework,
        )

    def test_initial_wander_delay_cap_is_optional_server_owned_and_one_time(self) -> None:
        method = extract_method(
            self.framework,
            "TryApplyProfiledPveInitialWanderDelayCap",
        )
        absent_gate = method.index("!declaredCap.HasValue")
        authority_gate = method.index("!NetworkServer.active")
        handled_gate = method.index(
            "ProfiledPveInitialWanderDelayHandledBrainIds.Add(instanceId)"
        )
        clock_write = method.index("brain.wanderTime =")
        self.assertLess(absent_gate, clock_write)
        self.assertLess(authority_gate, clock_write)
        self.assertLess(handled_gate, clock_write)
        self.assertIn("brain.idleStates != BrainAI.IdleStates.Wander", method)
        self.assertIn("brain.responding", method)
        self.assertIn("brain.WanderTimer * brain.Patience", method)
        self.assertNotRegex(method, r"brain\.(?:WanderTimer|Patience|ReactionTime)\s*=")

    def test_initial_wander_delay_stagger_does_not_consume_unity_random(self) -> None:
        method = extract_method(
            self.framework,
            "ChooseProfiledPveInitialWanderRemainingDelay",
        )
        self.assertIn("ComputeStableFnv1a(identity)", method)
        self.assertIn("capSeconds * (0.5f + 0.5f * unit)", method)
        self.assertNotIn("UnityEngine.Random", method)
        self.assertNotIn("Random.Range", method)

    def test_framework_requires_api_with_optional_delay_contract(self) -> None:
        self.assertIn(
            'internal const string RequiredApiVersion = "0.2.0-alpha.7";',
            self.framework,
        )

    def test_hostile_prefabs_are_one_unique_native_team_cohort(self) -> None:
        selection = extract_method(
            self.framework,
            "TrySelectStandalonePvePrefabCohort",
        )
        self.assertIn("identifier?.StartingTeamStats", selection)
        self.assertIn("prefab.GetComponent<WeaponsAI>()", selection)
        self.assertNotIn("GetComponentInChildren<WeaponsAI>", selection)
        self.assertIn("startingStats.GetInstanceID()", selection)
        self.assertIn("startingStats.ThisTeamId", selection)
        self.assertIn("nativeTeamId == playerTeamId", selection)
        self.assertIn("prefab.GetComponent<TeamIdentifierReference>()", selection)
        self.assertIn("teamReference == null", selection)
        self.assertNotIn("identifier?.TargetPool", selection)
        self.assertNotIn("teamReference.MyTeamIdentifier", selection)
        self.assertNotIn("teamReference.TargetPool", selection)
        self.assertNotIn("brain.MyTeamIdentifierReference", selection)
        self.assertIn("ordered[0].Prefabs.Count == ordered[1].Prefabs.Count", selection)
        self.assertNotIn("GameManagerNetwork.FriendlyFire", selection)

    def test_spawned_team_contract_requires_native_reference_and_target_closure(self) -> None:
        validation = extract_method(
            self.framework,
            "TryValidateStandalonePveSpawnedTeamContract",
        )
        self.assertIn("brain.gameObject.GetComponent<TeamIdentifier>()", validation)
        self.assertIn("brain.gameObject.GetComponent<TeamIdentifierReference>()", validation)
        self.assertIn("teamReference.MyTeamIdentifier != identifier", validation)
        self.assertIn("brain.MyTeamIdentifierReference != teamReference", validation)
        self.assertIn("identifier.TargetPool == null", validation)
        self.assertIn("teamReference.TargetPool != identifier.TargetPool", validation)
        self.assertIn("identifier.TargetPool.Team != operation.PveExpectedTeamId", validation)
        self.assertIn("cohortInEnemyLists == 0", validation)
        self.assertIn("cohortInPossibleTargets == 0", validation)

    def test_deferred_team_contract_failure_suppresses_then_cleans_exact_ownership(self) -> None:
        failure = extract_method(self.framework, "FailStandalonePveTeamValidation")
        suppress = failure.index("SuppressStandalonePveExtraction(operation)")
        cleanup = failure.index("DestroyOwnedStandalonePvePopulation(", suppress)
        self.assertLess(suppress, cleanup)

        owned_cleanup = extract_method(
            self.framework,
            "DestroyOwnedStandalonePvePopulation",
        )
        self.assertIn("operation.PveOwnedServerIdentities.ToArray()", owned_cleanup)
        self.assertIn("registeredIdentity == ownedIdentity", owned_cleanup)
        self.assertIn("NetworkServer.Destroy(root)", owned_cleanup)
        self.assertIn("Object.Destroy(root)", owned_cleanup)
        self.assertNotIn("allAI.Remove", owned_cleanup)
        self.assertNotIn("AllTeams", owned_cleanup)
        self.assertNotRegex(owned_cleanup, r"possibleTargets\s*(?:=|\.Remove)")

    def test_spawn_scopes_and_restores_raid_standard_ai(self) -> None:
        method = extract_method(self.framework, "TrySpawnStandalonePveEnemies")
        claim = method.index("operation.PveSpawnAttempted = true")
        player_team = method.index("TryResolveLocalPlayerTeamId", claim)
        retry_release = method.index("operation.PveSpawnAttempted = false", player_team)
        registry_snapshot = method.index("CaptureServerSpawnedNetIds()")
        capture = method.index("previousStandardAi = raid.standardAI")
        narrowed = method.index("raid.standardAI =", capture)
        spawn = method.index("raid.ServerSpawnAI(false)", narrowed)
        restore = method.index("raid.standardAI = previousStandardAi", spawn)
        exact_capture = method.index(
            "CaptureOwnedStandalonePveServerPopulation(",
            restore,
        )
        pending = method.index("operation.PveTeamValidationPending = true", exact_capture)
        self.assertLess(claim, player_team)
        self.assertLess(player_team, retry_release)
        self.assertEqual(method.count("operation.PveSpawnAttempted = false"), 1)
        self.assertLess(registry_snapshot, capture)
        self.assertLess(capture, narrowed)
        self.assertLess(narrowed, spawn)
        self.assertLess(spawn, restore)
        self.assertLess(restore, exact_capture)
        self.assertLess(exact_capture, pending)
        self.assertGreaterEqual(method.count("finally"), 2)
        self.assertNotIn("TryValidateStandalonePveSpawnedTeamContract", method)
        self.assertNotIn("FriendlyFire =", method)
        self.assertNotIn("RandomSpawns =", method)

    def test_server_registry_snapshots_use_manual_il2cpp_enumerators(self) -> None:
        before = extract_method(self.framework, "CaptureServerSpawnedNetIds")
        after = extract_method(
            self.framework,
            "CaptureOwnedStandalonePveServerPopulation",
        )
        for method in (before, after):
            self.assertIn("NetworkServer.spawned.GetEnumerator()", method)
            self.assertIn("while (enumerator.MoveNext())", method)
            self.assertIn("enumerator.Current", method)
            self.assertIn("enumerator.Dispose()", method)
            self.assertNotIn("foreach", method)

        own_every_delta = after.index(
            "operation.PveOwnedServerIdentities.Add(netId, identity)"
        )
        structural_gate = after.index(
            "netId == 0 || identity == null || identity.netId != netId"
        )
        brain_gate = after.index("root.GetComponent<BrainAI>()")
        self.assertLess(own_every_delta, structural_gate)
        self.assertLess(structural_gate, brain_gate)

    def test_pending_validation_runs_each_update_before_maintenance_readiness_gates(self) -> None:
        maintain = extract_method(self.framework, "MaintainStandaloneGameplay")
        scene_ready = maintain.index("!operation.ScenePreparationComplete")
        pending = maintain.index("ProcessPendingStandalonePveTeamValidation(operation)")
        throttle = maintain.index("operation.LastMaintenanceFrame + 15")
        all_players = maintain.index("if (!operation.AllPlayersLoaded)")
        self.assertLess(scene_ready, pending)
        self.assertLess(pending, throttle)
        self.assertLess(pending, all_players)

        process = extract_method(
            self.framework,
            "ProcessPendingStandalonePveTeamValidation",
        )
        self.assertIn(
            "Time.frameCount < operation.PveTeamValidationEarliestFrame",
            process,
        )
        self.assertIn("ownedBrains.Count > expectedCount", process)
        self.assertIn("ownedBrains.Count < expectedCount", process)
        self.assertIn(
            "Time.frameCount < operation.PveTeamValidationDeadlineFrame",
            process,
        )
        self.assertIn("TryValidateOwnedStandalonePveServerPopulation(", process)
        self.assertIn("TryValidateStandalonePveSpawnedTeamContract(", process)
        raw_count = process.index("gameManager.allAI.Count")
        raw_gate = process.index("rawRegisteredCount != expectedCount", raw_count)
        full_contract = process.index(
            "TryValidateStandalonePveSpawnedTeamContract(",
            raw_gate,
        )
        self.assertLess(raw_count, raw_gate)
        self.assertLess(raw_gate, full_contract)
        self.assertIn('", rawAllAI=" + rawRegisteredCount', process)

    def test_spawn_declares_next_frame_to_sixty_frame_validation_window(self) -> None:
        spawn = extract_method(self.framework, "TrySpawnStandalonePveEnemies")
        self.assertIn(
            "operation.PveTeamValidationEarliestFrame = Time.frameCount + 1",
            spawn,
        )
        self.assertIn(
            "Time.frameCount + StandalonePveTeamValidationDeadlineFrames",
            spawn,
        )
        self.assertIn(
            "private const int StandalonePveTeamValidationDeadlineFrames = 60;",
            self.framework,
        )

    def test_registered_owned_brains_bind_exact_root_netid_and_live_registry_identity(self) -> None:
        method = extract_method(self.framework, "GetOwnedStandalonePveBrains")
        self.assertIn("brain.transform != root", method)
        self.assertIn("brain.gameObject.GetComponent<NetworkIdentity>()", method)
        self.assertIn("operation.PveOwnedServerIdentities.TryGetValue(", method)
        self.assertIn("ownedIdentity != identity", method)
        self.assertIn("NetworkServer.spawned.TryGetValue(", method)
        self.assertIn("registeredIdentity != identity", method)
        self.assertIn(
            "registeredIdentity.gameObject.GetComponent<BrainAI>() != brain",
            method,
        )
        self.assertNotIn("operation.SceneHandle", method)
        self.assertNotIn("gameObject.scene", method)

    def test_spawn_requires_empty_global_native_ai_baseline(self) -> None:
        spawn = extract_method(self.framework, "TrySpawnStandalonePveEnemies")
        baseline = spawn.index("CountRegisteredStandalonePveBrains(gameManager)")
        zero_gate = spawn.index("preexistingAllAiEntries != 0", baseline)
        native_spawn = spawn.index("raid.ServerSpawnAI(false)", zero_gate)
        self.assertLess(baseline, zero_gate)
        self.assertLess(zero_gate, native_spawn)
        self.assertIn("GameManager.allAI baseline is not empty", spawn)

        raw_count = extract_method(
            self.framework,
            "CountRegisteredStandalonePveBrains",
        )
        self.assertIn("return gameManager.allAI.Count", raw_count)
        self.assertIn("gameManager.allAI == null", raw_count)
        self.assertNotIn("GetInstanceID", raw_count)
        self.assertNotIn("BrainAI brain", raw_count)

    def test_pending_and_failed_states_reset_both_extraction_owners(self) -> None:
        suppress = extract_method(self.framework, "SuppressStandalonePveExtraction")
        for expected in (
            "exfil._occupants.Clear()",
            "exfil.NetworkPlayersInExfil = 0",
            "exfil.PlayersInExfil = 0",
            "exfil.NetworkcanExtract = false",
            "exfil.canExtract = false",
            "network._globalExfilOccupants.Clear()",
            "network.NetworkPlayersInAnyExfil = 0",
            "network.PlayersInAnyExfil = 0",
            "network.NetworkcanExtract = false",
            "network.canExtract = false",
            "network.NetworkisExtracting = false",
            "network.isExtracting = false",
            "network.NetworkextractionStartTime = 0d",
            "network.extractionStartTime = 0d",
        ):
            self.assertIn(expected, suppress)

        process = extract_method(
            self.framework,
            "ProcessPendingStandalonePveTeamValidation",
        )
        failed_gate = process.index("operation.PveTeamContractFailed")
        failed_suppress = process.index(
            "SuppressStandalonePveExtraction(operation)",
            failed_gate,
        )
        pending_gate = process.index("!operation.PveTeamValidationPending")
        pending_suppress = process.index(
            "SuppressStandalonePveExtraction(operation)",
            pending_gate,
        )
        self.assertLess(failed_gate, failed_suppress)
        self.assertLess(failed_suppress, pending_gate)
        self.assertLess(pending_gate, pending_suppress)

    def test_owned_population_cleanup_precedes_game_mode_release_and_reset(self) -> None:
        release = extract_method(self.framework, "ReleaseStandaloneSceneContracts")
        exact_cleanup = release.index("DestroyOwnedStandalonePvePopulation(")
        count_restore = release.index("RestoreStandalonePveBotCountContract(operation)")
        game_mode = release.index("ReleaseStandaloneGameMode(operation)")
        self.assertLess(exact_cleanup, count_restore)
        self.assertLess(count_restore, game_mode)
        self.assertLess(exact_cleanup, game_mode)

        for lifecycle_name in ("OnSceneLoaded", "OnSceneUnloaded"):
            lifecycle = extract_method(self.framework, lifecycle_name)
            release_call = lifecycle.index("ReleaseStandaloneSceneContracts(operation)")
            reset_call = lifecycle.index("ResetProfiledPveGenerationState(operation)")
            self.assertLess(release_call, reset_call)

        reset = extract_method(self.framework, "ResetProfiledPveGenerationState")
        cleanup = extract_method(
            self.framework,
            "DestroyOwnedStandalonePvePopulation",
        )
        for method in (reset, cleanup):
            self.assertIn("PveOwnedServerIdentities.Clear()", method)
            self.assertIn("PveOwnedBrainInstanceIds.Clear()", method)
            self.assertIn("PveTeamValidationEarliestFrame = -1", method)

    def test_custom_pve_bot_counts_restore_without_overwriting_a_foreign_owner(self) -> None:
        spawn = extract_method(self.framework, "TrySpawnStandalonePveEnemies")
        capture = spawn.index("operation.PreviousBotAmount = gameManager.botAmount")
        write = spawn.index("gameManager.botAmount = operation.OwnedBotAmount")
        self.assertLess(capture, write)
        self.assertIn("operation.PreviousBotHvtAmount = gameManager.botHVTAmount", spawn)
        self.assertIn("operation.BotCountsCaptured = true", spawn)

        restore = extract_method(
            self.framework,
            "RestoreStandalonePveBotCountContract",
        )
        self.assertIn("gameManager.botAmount == operation.OwnedBotAmount", restore)
        self.assertIn("gameManager.botHVTAmount == operation.OwnedBotHvtAmount", restore)
        self.assertIn("gameManager.botAmount = operation.PreviousBotAmount", restore)
        self.assertIn("gameManager.botHVTAmount = operation.PreviousBotHvtAmount", restore)
        self.assertIn("operation.BotCountsCaptured = false", restore)

    def test_native_death_callback_remains_all_enemies_dead_completion_owner(self) -> None:
        bootstrap = extract_method(self.framework, "ConfigureStandalonePveController")
        self.assertIn("raid.enabled = false", bootstrap)
        self.assertIn("Health.UserCode_Die directly invokes RaidManager.UpdateAICount", bootstrap)
        self.assertIn("independent of Behaviour.enabled", bootstrap)
        self.assertNotIn("raid.UpdateAICount", bootstrap)

    def test_team_repair_uses_only_native_syncvar_setter(self) -> None:
        method = extract_method(
            self.framework,
            "TryValidateStandalonePveSpawnedTeamContract",
        )
        self.assertIn("identifier.NetworkTeamID = operation.PveExpectedTeamId", method)
        self.assertNotRegex(method, r"identifier\.(?:TeamID|TeamStats|TargetPool)\s*=(?!=)")
        self.assertNotRegex(
            method,
            r"brain\.(?:possibleTargets|allFriendlyBrains|allEnemyBrains|allEnemyPlayers)\s*=",
        )

    def test_reaction_cap_is_optional_server_only_one_shot_and_narrow(self) -> None:
        method = extract_method(
            self.framework,
            "TryApplyProfiledPveMaximumReactionTimeCap",
        )
        absent_gate = method.index("!declaredCap.HasValue")
        server_gate = method.index("!NetworkServer.active")
        handled_gate = method.index(
            "ProfiledPveReactionTimeHandledBrainIds.Add(instanceId)"
        )
        base_write = method.index("brain._baseReactionTime = cappedBase")
        current_write = method.index("brain.ReactionTime = cappedCurrent")
        self.assertLess(absent_gate, base_write)
        self.assertLess(server_gate, base_write)
        self.assertLess(handled_gate, base_write)
        self.assertLess(base_write, current_write)
        assignments = re.findall(r"brain\.([A-Za-z_][A-Za-z0-9_]*)\s*=", method)
        self.assertEqual(assignments, ["_baseReactionTime", "ReactionTime"])
        self.assertIn("Mathf.Min(nativeBase, capSeconds)", method)
        self.assertIn("Mathf.Min(nativeCurrent, capSeconds)", method)
        self.assertIn("nativeBase < 0f", method)
        self.assertIn("nativeCurrent < 0f", method)
        self.assertNotIn("nativeBase <= 0f", method)
        self.assertNotIn("nativeCurrent <= 0f", method)
        self.assertNotIn("reactionTimer =", method)

    def test_scene_generation_resets_profiled_pve_capture_ownership(self) -> None:
        reset = extract_method(self.framework, "ResetProfiledPveGenerationState")
        loaded = extract_method(self.framework, "OnSceneLoaded")
        unloaded = extract_method(self.framework, "OnSceneUnloaded")
        self.assertIn("PveOwnedServerIdentities.Clear()", reset)
        self.assertIn("PveOwnedBrainInstanceIds.Clear()", reset)
        self.assertIn("PveTeamValidationPending = false", reset)
        self.assertIn("PveTeamValidationIssuedFrame = -1", reset)
        self.assertIn("PveTeamValidationEarliestFrame = -1", reset)
        self.assertIn("PveTeamValidationDeadlineFrame = -1", reset)
        self.assertIn("PveExpectedTeamId = -1", reset)
        self.assertIn("PveExpectedStartingTeamStats = null", reset)
        self.assertIn("PveTeamContractValidated = false", reset)
        self.assertIn("ProfiledPveDiagnosticBrains.Clear()", reset)
        self.assertIn("ProfiledPveReactionTimeHandledBrainIds.Clear()", reset)
        self.assertIn("ResetProfiledPveGenerationState(operation)", loaded)
        self.assertIn("ResetProfiledPveGenerationState(operation)", unloaded)

    def test_reaction_disposition_omission_performs_no_marker_write(self) -> None:
        method = extract_method(self.framework, "ConfigureStandaloneBotDetails")
        gate = method.index("profile?.ReactionDisposition != null")
        write = method.index("details.reactType =", gate)
        self.assertLess(gate, write)
        self.assertIn('case "defensive"', method)
        self.assertIn('case "offensive"', method)
        self.assertIn('case "random"', method)

    def test_loader_neutral_pve_evidence_covers_count_spawn_and_validation(self) -> None:
        self.assertIn('internal const string Prefix = "MODDED_OPS_EVIDENCE";', self.evidence)
        self.assertIn('"|loader=" + Encode(loaderKind)', self.evidence)
        self.assertIn('"|sceneGeneration=" +', self.evidence)
        self.assertIn(
            "sceneGeneration.ToString(CultureInfo.InvariantCulture)",
            self.evidence,
        )

        confirm = extract_method(self.framework, "BeginCatalogOperationLaunch")
        validate = confirm.index("TryValidateConfirmedSelection(")
        pending = confirm.index('"pve-count-pending"', validate)
        loading = confirm.index("SetNativeConfirmationLoadingState(presentation, true)")
        self.assertLess(validate, pending)
        self.assertLess(pending, loading)

        active = extract_method(self.framework, "InvokeNativeCatalogLaunch")
        assign = active.index("activeOperation = new ActiveMapOperation")
        active_marker = active.index('"pve-count-active"', assign)
        native_start = active.index("InvokeNativeBoardStart(", active_marker)
        self.assertLess(assign, active_marker)
        self.assertLess(active_marker, native_start)

        loaded = extract_method(self.framework, "OnSceneLoaded")
        prior_handle = loaded.index(
            "priorEvidenceSceneHandle = operation.EvidenceLastSceneHandle"
        )
        generation = loaded.index("operation.EvidenceSceneGeneration++")
        overwrite_handle = loaded.index("operation.EvidenceLastSceneHandle = scene.handle")
        restart_marker = loaded.index('"pve-count-restart-retained"', generation)
        contract = loaded.index("ValidateStandaloneSceneContract", restart_marker)
        self.assertLess(prior_handle, generation)
        self.assertLess(generation, overwrite_handle)
        self.assertLess(generation, restart_marker)
        self.assertLess(restart_marker, contract)
        self.assertIn(
            '"|priorSceneHandle=" + FrameworkEvidence.Number(',
            loaded,
        )
        self.assertIn("ResetPvpSceneAgreementForReload(operation, scene.handle)", loaded)

        spawn = extract_method(self.framework, "TrySpawnStandalonePveEnemies")
        capacity_validation = spawn.index("TryValidateConfirmedSelection(")
        capacity_pass = spawn.index('"pve-safe-navigation-capacity-passed"')
        cohort = spawn.index("TrySelectStandalonePvePrefabCohort(", capacity_pass)
        native_spawn = spawn.index("raid.ServerSpawnAI(false)")
        returned = spawn.index('"pve-server-spawn-returned"', native_spawn)
        restore = spawn.index("raid.standardAI = previousStandardAi", returned)
        pending_validation = spawn.index('"pve-deferred-validation-pending"', restore)
        self.assertLess(capacity_validation, capacity_pass)
        self.assertLess(capacity_pass, cohort)
        self.assertLess(native_spawn, returned)
        self.assertLess(returned, restore)
        self.assertLess(restore, pending_validation)

        validation_method = extract_method(
            self.framework,
            "ProcessPendingStandalonePveTeamValidation",
        )
        active_count = validation_method.index("operation.PveEnemyCount = expectedCount")
        passed = validation_method.index('"pve-deferred-validation-passed"', active_count)
        self.assertLess(active_count, passed)

    def test_pve_completion_evidence_is_edge_latched_and_read_only(self) -> None:
        observe = extract_method(
            self.framework,
            "ObserveStandalonePveLifecycleEvidence",
        )
        for field in (
            "EvidencePveAllEnemiesDeadLogged",
            "EvidencePveExtractionUnlockedLogged",
            "EvidencePveExtractionTimerLogged",
            "EvidencePveSuccessfulOperationLogged",
        ):
            self.assertIn(field, observe)
        for event in (
            '"pve-all-enemies-dead"',
            '"pve-extraction-unlocked"',
            '"pve-extraction-timer-started"',
            '"pve-successful-operation"',
        ):
            self.assertIn(event, observe)
        self.assertIn("GameManager.instance.allAI.Count", observe)
        self.assertIn("network?.SuccessfulOperation == true", observe)
        self.assertIn("FrameworkEvidence.Number(rawAllAi)", observe)
        self.assertNotIn("UpdateAICount(", observe)
        self.assertNotRegex(
            observe,
            r"(?:NetworkcanExtract|NetworkisExtracting|SuccessfulOperation)\s*=(?!=)",
        )

        maintain = extract_method(self.framework, "MaintainStandaloneGameplay")
        validation = maintain.index("ProcessPendingStandalonePveTeamValidation(operation)")
        observation = maintain.index("ObserveStandalonePveLifecycleEvidence(operation)")
        throttle = maintain.index("operation.LastMaintenanceFrame + 15")
        self.assertLess(validation, throttle)
        self.assertLess(throttle, observation)

        unload = extract_method(self.framework, "OnSceneUnloaded")
        self.assertNotIn("ObserveStandalonePveLifecycleEvidence(operation)", unload)
        self.assertIn("CompletePvpPeerAgreementOnNativeReturn(operation)", unload)
        native_return = extract_method(
            self.agreement,
            "CompletePvpPeerAgreementOnNativeReturn",
        )
        self.assertIn("EvidencePveSuccessfulOperationLogged", native_return)
        self.assertIn('"|observation=verified-operation-room-return"', native_return)
        self.assertIn('"pve-operation-room-return"', native_return)

    def test_teardown_and_framework_unload_have_one_shot_evidence(self) -> None:
        release = extract_method(self.framework, "ReleaseStandaloneSceneContracts")
        capture = release.index("CaptureStandaloneTeardownEvidence(operation)")
        population = release.index("DestroyOwnedStandalonePvePopulation(")
        game_mode = release.index("ReleaseStandaloneGameMode(operation)")
        summary = release.index("LogStandaloneTeardownSummary(", game_mode)
        self.assertLess(capture, population)
        self.assertLess(population, game_mode)
        self.assertLess(game_mode, summary)
        self.assertIn("finally", release)
        self.assertIn("cleanupCompleted", release)

        teardown = extract_method(self.framework, "LogStandaloneTeardownSummary")
        self.assertIn("operation.EvidenceTeardownLogged", teardown)
        self.assertIn('"scene-teardown-summary"', teardown)
        self.assertIn("operation.EvidenceTeardownLogged = true", teardown)
        self.assertIn('"|outcome=" + (cleanupCompleted', teardown)
        self.assertIn('"|ownedAiRootsAfter="', teardown)
        self.assertNotIn("!operation.ScenePreparationStarted", teardown)

        declaration = re.search(r"public\s+override\s+bool\s+Unload\s*\(", self.framework)
        self.assertIsNotNone(declaration)
        opening = self.framework.find("{", declaration.end())
        closing = find_matching_brace(self.framework, opening)
        unload = self.framework[declaration.start() : closing + 1]
        marker = unload.index('"framework-unload-success"')
        clear_instance = unload.index("instance = null", marker)
        success = unload.index("return true", clear_instance)
        self.assertLess(marker, clear_instance)
        self.assertLess(clear_instance, success)

    def test_failed_native_return_cannot_be_reclassified_as_restart(self) -> None:
        loaded = extract_method(self.framework, "OnSceneLoaded")
        failed_branch = loaded.index("if (IsNativeAbortReturnPending(operation))")
        restart_branch = loaded.index(
            "if (operation.PeerSceneUnloadDispositionPending)",
            failed_branch,
        )
        failed_return = loaded.index("return;", failed_branch)
        self.assertLess(failed_branch, failed_return)
        self.assertLess(failed_return, restart_branch)
        failed_body = loaded[failed_branch:failed_return]
        self.assertIn("ReleaseStandaloneSceneContracts(operation)", failed_body)
        self.assertIn("operation.NetworkSpawnFailed = true", failed_body)
        self.assertIn("operation.ScenePreparationStarted = true", failed_body)
        self.assertIn("operation.ScenePreparationEarliestFrame = -1", failed_body)
        self.assertIn("NotifyNativeAbortReturnPackageReloaded", failed_body)
        self.assertNotIn("ResetPvpSceneAgreementForReload", failed_body)
        self.assertNotIn("NotifyPvpSceneLoading", failed_body)
        self.assertNotIn("PrepareStandaloneScene", failed_body)

        unloaded = extract_method(self.framework, "OnSceneUnloaded")
        capture = unloaded.index(
            "bool abortReturnPending = IsNativeAbortReturnPending(operation)"
        )
        release = unloaded.index("ReleaseStandaloneSceneContracts(operation)")
        restore = unloaded.index(
            "operation.NetworkSpawnFailed =\n"
            "            abortReturnPending && !nativeReturnCompleted;"
        )
        self.assertLess(capture, release)
        self.assertLess(release, restore)
        self.assertIn(
            "bool protocolLifecycle = IsPeerAgreementMode(operation.Operation?.Mode)",
            unloaded,
        )
        self.assertIn("protocolLifecycle || abortReturnPending", unloaded)

    def test_solo_pve_return_retires_transition_and_membership_ownership(self) -> None:
        unloaded = extract_method(self.framework, "OnSceneUnloaded")
        complete = extract_method(
            self.agreement,
            "CompletePvpPeerAgreementOnNativeReturn",
        )
        self.assertIn("CompletePvpPeerAgreementOnNativeReturn(operation)", unloaded)
        self.assertIn("operation.PveSoloMembershipFrozen = false", complete)
        self.assertIn("operation.PveSoloSessionDigest = string.Empty", complete)
        self.assertIn("operation.NativeTransitionStarted = false", complete)
        self.assertIn("ReferenceEquals(activeOperation, operation)", complete)
        self.assertIn("activeOperation = null", complete)

        rejection = extract_method(
            self.agreement,
            "NotifyNativeAbortReturnPackageReloaded",
        )
        self.assertIn("operation.PvpAbortReturnAccepted = false", rejection)
        self.assertIn("operation.PvpAbortReturnDeadlineTimestamp = 0", rejection)
        self.assertIn("operation.PvpAbortReturnNextRetryTimestamp = 0", rejection)
        self.assertIn("RequestNativePvpAbortReturn(operation)", rejection)
        self.assertNotIn("PvpAbortReturnAttemptCount = 0", rejection)
        self.assertIn(
            '"pvp-native-abort-package-reload-rejected"',
            rejection,
        )


if __name__ == "__main__":
    unittest.main()
