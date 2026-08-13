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
        registration = method.index("NetworkClient.RegisterPrefab(")
        self.assertLess(validation, registration)
        self.assertIn("Mirror prefab registration refused", method)

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
        unregister = method.index("NetworkClient.UnregisterPrefab(bootstrapPrefabRoot)")
        remove_by_id = method.index("prefabs.Remove(bootstrapAssetId)")
        destroy = method.index("Object.Destroy(bootstrapRoot)")
        clear_ownership = method.index("operation.BootstrapSyncObjects.Clear()")
        self.assertLess(unspawn, unregister)
        self.assertLess(unregister, remove_by_id)
        self.assertLess(remove_by_id, destroy)
        self.assertLess(destroy, clear_ownership)
        self.assertNotIn("NetworkClient.ClearSpawners", method)
        self.assertNotIn("syncObjects = null", method)

    def test_plugin_version_changes_with_spawn_contract(self) -> None:
        self.assertIn(
            '[BepInPlugin("operator.modded-operations", '
            '"OPERATOR: Modded Operations", "0.3.29")]',
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
            'internal const string RequiredApiVersion = "0.2.0-alpha.6";',
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
        game_mode = release.index("ReleaseStandaloneGameMode(operation)")
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


if __name__ == "__main__":
    unittest.main()
