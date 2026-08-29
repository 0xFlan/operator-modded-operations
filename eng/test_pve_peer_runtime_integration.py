from __future__ import annotations

import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src" / "OperatorModdedOperations"


def read(name: str) -> str:
    return (SOURCE / name).read_text(encoding="utf-8")


def method(source: str, name: str) -> str:
    declaration = re.search(
        rf"\bprivate\s+(?:static\s+)?[\w:.,<>\[\]?\s]+?\s+{re.escape(name)}\s*\(",
        source,
    )
    if declaration is None:
        raise AssertionError(f"missing method {name}")
    opening = source.find("{", declaration.end())
    if opening < 0:
        raise AssertionError(f"missing body for {name}")
    depth = 0
    state = "code"
    index = opening
    while index < len(source):
        current = source[index]
        following = source[index + 1] if index + 1 < len(source) else ""
        if state == "line":
            if current in "\r\n":
                state = "code"
        elif state == "block":
            if current == "*" and following == "/":
                state = "code"
                index += 1
        elif state == "string":
            if current == "\\":
                index += 1
            elif current == '"':
                state = "code"
        elif state == "char":
            if current == "\\":
                index += 1
            elif current == "'":
                state = "code"
        else:
            if current == "/" and following == "/":
                state = "line"
                index += 1
            elif current == "/" and following == "*":
                state = "block"
                index += 1
            elif current == '"':
                state = "string"
            elif current == "'":
                state = "char"
            elif current == "{":
                depth += 1
            elif current == "}":
                depth -= 1
                if depth == 0:
                    return source[declaration.start() : index + 1]
        index += 1
    raise AssertionError(f"unterminated body for {name}")


class PvePeerRuntimeIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.main = read("CerberusNativeTabFix.cs")
        cls.protocol = read("CerberusNativeTabFix.PvpPeerAgreement.cs")
        cls.runtime = read("CerberusNativeTabFix.PeerRuntime.cs")
        cls.spawn = read("CerberusNativeTabFix.PeerGameModeSpawn.cs")
        cls.production = "\n".join(
            path.read_text(encoding="utf-8")
            for path in sorted(SOURCE.glob("*.cs"))
        )

    def test_both_multiplayer_modes_are_enabled_for_physical_candidate_testing(self) -> None:
        self.assertRegex(
            self.protocol, r"PveAgreementV6RuntimeContractComplete\s*=\s*true\s*;"
        )
        self.assertRegex(
            self.protocol, r"PvpAgreementV6RuntimeContractComplete\s*=\s*true\s*;"
        )
        self.assertIn("native-pve-v1", self.protocol)

    def test_native_transition_and_owner_spawn_require_mirror_ready(self) -> None:
        transition = method(self.protocol, "CommitPvpNativeTransition")
        authorization = method(self.protocol, "IsPvpNetworkSpawnAuthorized")
        self.assertIn("requireReady: true", transition)
        self.assertIn("requireReady: true", authorization)
        self.assertIn("AreRequiredPvpPeersSceneReadyForCurrentEpoch", authorization)

    def test_solo_pve_waits_for_the_package_runtime_contract_before_owner_spawn(self) -> None:
        authorization = method(self.protocol, "IsPvpNetworkSpawnAuthorized")
        companion = method(self.protocol, "TryValidateSoloPveRuntimeCompanion")
        maintain = method(self.main, "MaintainStandaloneGameplay")
        all_loaded = method(self.main, "OnStandaloneAllPlayersLoaded")
        prepare = method(self.main, "PrepareStandaloneScene")

        self.assertIn("TryValidateSoloPveRuntimeCompanion", authorization)
        self.assertIn("if (!runtimeReady)", authorization)
        self.assertIn("companion.FailureMarkerName", companion)
        self.assertIn("companion.ReadyMarkerName", companion)
        self.assertIn("failureMarkers != 0", companion)
        self.assertIn("readyMarkers != 1", companion)
        self.assertIn("operation.NetworkSpawnRequested", companion)
        self.assertIn("TryValidateSoloPveRuntimeCompanion", maintain)
        self.assertIn("TryValidateSoloPveRuntimeCompanion", all_loaded)
        self.assertIn("FailActivePvpNativeLifecycle", all_loaded)
        self.assertNotIn(
            "!spawnContractReady && operation.Operation.Mode ==",
            prepare,
        )

    def test_remote_clone_is_repaired_before_mirror_deserialization(self) -> None:
        ensure = method(self.main, "EnsureStandaloneBootstrapPrefabRegistered")
        create = method(self.spawn, "TryCreateStandalonePeerGameModeClone")
        release = method(self.spawn, "ReleaseStandalonePeerGameModeSpawnHandler")
        release_mode = method(self.main, "ReleaseStandaloneGameMode")
        self.assertIn("NetworkClient.RegisterSpawnHandler(", ensure)
        self.assertNotIn("NetworkClient.RegisterPrefab(", ensure)
        self.assertIn("PeerGameModeLegacySpawnHandler", ensure)
        self.assertIn("out SpawnHandlerDelegate registeredSpawnHandler", ensure)
        self.assertNotIn("operation.PeerGameModeSpawnHandler,\n", ensure)
        self.assertIn("identity.InitializeNetworkBehaviours()", create)
        self.assertIn("the clone graph is invalid before Mirror payload", create)
        repair = method(
            self.spawn, "TryRepairStandalonePveCloneNetworkBehaviours"
        )
        self.assertIn("Object.DestroyImmediate(cloneBehaviours[2])", repair)
        self.assertIn("Object.DestroyImmediate(cloneBehaviours[1])", repair)
        self.assertIn("clone.AddComponent<ExfilZone>()", repair)
        self.assertIn("clone.AddComponent<RaidManager>()", repair)
        self.assertIn("raid.infiltrationManager = pve", repair)
        self.assertIn("raid.exfilZones.Add(exfil)", repair)
        self.assertIn("cloneBehaviours[1] != exfil", repair)
        self.assertIn("cloneBehaviours[2] != raid", repair)
        self.assertIn("SamePeerSpawnHandler", release)
        self.assertIn("SamePeerUnspawnHandler", release)
        self.assertIn("NetworkClient.UnregisterSpawnHandler(assetId)", release)
        self.assertIn("ReleaseStandalonePeerGameModeSpawnHandler(operation)", release_mode)
        self.assertNotIn("UnregisterSpawnHandler", release_mode)

    def test_remote_player_is_moved_only_by_its_owner(self) -> None:
        self.assertNotIn("MoveRemotePlayerRoot", self.main)
        place = method(self.runtime, "ProcessRemotePeerPlayerPlacement")
        issue = method(self.runtime, "TryIssueRemotePeerPlayerPlacement")
        barrier = method(self.runtime, "IsHostPeerPlayerBarrierReady")
        self.assertIn("player.isOwned", place)
        self.assertIn("spawned?.isLocalPlayer", place)
        self.assertIn("GameManager.instance.MovePlayerToSpawn", place)
        self.assertIn("PlayerReadyStableFrames < 15", place)
        self.assertIn("PvpAgreementMessageKind.PlayerReady", place)
        self.assertIn("SamePvpNetworkConnection", issue)
        self.assertIn("connection.isReady", issue)
        self.assertIn("ParticipantCount", barrier)
        self.assertIn("PlayerReadyConnectionIds", barrier)

        grounded = method(self.main, "TryValidatePlayerGroundSupport")
        at_spawn = method(self.main, "IsPlayerAtPackageSpawn")
        self.assertIn("TryValidatePlayerGroundSupport", at_spawn)
        self.assertIn("hit.collider.gameObject.scene.handle != packageSceneHandle", grounded)
        self.assertIn("groundDelta < -0.05f", grounded)
        self.assertIn("groundDelta > 0.45f", grounded)
        self.assertIn("Math.Abs(verticalVelocity) > 0.5f", grounded)
        self.assertIn("operation.SceneHandle", place)

    def test_player_ready_requires_native_movement_health_animation_and_weapon_authority(
        self,
    ) -> None:
        contract = method(
            self.runtime, "TryValidatePeerPlayerNativeNetworkContract"
        )
        place = method(self.runtime, "ProcessRemotePeerPlayerPlacement")
        host = method(self.runtime, "IsHostPeerPlayerBarrierReady")
        self.assertIn("Smooth.SmoothSyncMirror", contract)
        self.assertIn("smooth.Length != 1", contract)
        self.assertIn('"NetworkAnimatorSmooth"', contract)
        self.assertIn("networkAnimator.netIdentity != identity", contract)
        self.assertIn("!animatorBehaviour.enabled", contract)
        self.assertIn("PeerIdentityContainsBehaviour(identity, networkAnimator)", contract)
        self.assertIn("Health health", contract)
        self.assertIn("PeerIdentityContainsBehaviour", contract)
        self.assertIn("weaponIdentity.netId != unchecked((uint)syncedNetId)", contract)
        self.assertIn("!weaponOwned", contract)
        self.assertIn("ownedWeaponCount == 0", contract)
        self.assertIn("TryValidatePeerPlayerNativeNetworkContract", place)
        self.assertIn("requireOwnedWeaponAuthority: true", place)
        self.assertIn("TryValidatePeerPlayerNativeNetworkContract", host)
        self.assertIn("requireOwnedWeaponAuthority: owned", host)

    def test_ai_waits_for_player_barrier_and_has_replication_receipt(self) -> None:
        self.assertRegex(
            self.main,
            r"hostPvpAgreement\s*!=\s*null\s*&&\s*"
            r"hostPvpAgreement\.PlayerBarrierPassed",
        )
        create = method(self.runtime, "CreateAuthoritativePvePopulationManifest")
        validate = method(self.runtime, "TryValidateAndAcknowledgeRemotePvePopulation")
        self.assertIn("PveOwnedInitialPositions", create)
        self.assertIn("NetworkAnimatorSyncNPC", create)
        self.assertIn("TryValidatePeerPveActorNativeNetworkContract", create)
        self.assertIn("PeerObservedPveInitialPositions", validate)
        self.assertIn("NetworkClient.spawned.TryGetValue", validate)
        self.assertIn("TryValidatePeerPveActorNativeNetworkContract", validate)
        self.assertIn("PopulationReadyStableFrames < 15", validate)
        self.assertIn("PvpAgreementMessageKind.PopulationReady", validate)

    def test_ai_population_requires_native_transform_health_weapon_and_animator_graph(
        self,
    ) -> None:
        contract = method(
            self.runtime, "TryValidatePeerPveActorNativeNetworkContract"
        )
        self.assertIn("BrainAI brain", contract)
        self.assertIn("Health health", contract)
        self.assertIn("WeaponsAI weapons", contract)
        self.assertIn("NetworkAnimatorSyncNPC animator", contract)
        self.assertIn("Smooth.SmoothSyncMirror", contract)
        self.assertIn("smooth.Length != 2", contract)
        self.assertIn("PeerIdentityContainsBehaviour", contract)

    def test_framework_does_not_replace_native_movement_ballistics_or_damage(self) -> None:
        # The multiplayer bridge prepares identical local scenes and proves the
        # native network graph. Mirror/OPERATOR must remain the only owner of
        # player/AI movement, firing, damage, death, and their replication.
        forbidden_runtime_hooks = (
            "CMD_SendBullet",
            "RPC_SendBullet",
            "SERVER_SendBullet",
            "CMDTakeDamage",
            "CMD_TakeDamage",
            "UserCode_CMDTakeDamage",
            "HarmonyPatch",
            "MoveRemotePlayerRoot",
        )
        for forbidden in forbidden_runtime_hooks:
            self.assertNotIn(forbidden, self.production)

        spawn = method(self.main, "TrySpawnStandalonePveEnemies")
        ownership = method(self.main, "MaintainOwnedStandaloneWeaponSlot")
        self.assertIn("raid.ServerSpawnAI(false)", spawn)
        self.assertIn("player.CMD_SetCorrectiveOwnershipOfWeapon", ownership)
        self.assertNotIn("transform.position =", ownership)
        self.assertNotIn("transform.rotation =", ownership)

    def test_gameplay_commit_follows_every_runtime_barrier(self) -> None:
        process = method(self.runtime, "ProcessPeerRuntimeBarriersCore")
        self.assertIn("RuntimeReadyConnectionIds", process)
        self.assertIn("PlayerBarrierPassed", process)
        self.assertIn("PopulationReadyConnectionIds", process)
        begin = process.index("PvpAgreementMessageKind.BeginCommit")
        population = process.index("PopulationReadyConnectionIds")
        commit = process.index("operation.GameplayBeginCommitted = true")
        self.assertLess(population, begin)
        self.assertLess(begin, commit)
        for event in (
            '"pve-peer-runtime-owner"',
            '"pve-peer-runtime-ready"',
            '"pve-peer-player-ready"',
            '"pve-peer-player-barrier"',
            '"pve-peer-population-manifest"',
            '"pve-peer-population-ready"',
            '"pve-peer-begin-commit"',
        ):
            self.assertIn(event, self.runtime)

    def test_runtime_exceptions_fail_the_generation_closed(self) -> None:
        wrapper = method(self.runtime, "ProcessPeerRuntimeBarriers")
        self.assertIn("ProcessPeerRuntimeBarriersCore(operation)", wrapper)
        self.assertIn("FailHostPvpAgreement(reason, true)", wrapper)
        self.assertIn("FailRemotePvpAgreement(reason, true)", wrapper)
        self.assertIn("peer runtime barrier failed closed", wrapper)

    def test_runtime_state_is_generation_scoped(self) -> None:
        self.assertGreaterEqual(self.main.count("GameplayBeginCommitted = false"), 1)
        self.assertIn("PveOwnedInitialPositions.Clear()", self.main)
        self.assertIn("PeerObservedPveInitialPositions.Clear()", self.main)
        self.assertGreaterEqual(
            self.protocol.count("PopulationReadyStableFrames = 0"), 2
        )

    def test_evidence_is_bound_to_one_process_run_and_monotonic_sequence(self) -> None:
        self.assertIn("FrameworkEvidenceProcessRunId", self.main)
        self.assertIn("Guid.NewGuid().ToString(\"N\")", self.main)
        self.assertIn("System.Threading.Interlocked.Increment", self.main)
        self.assertIn('"processRunId="', self.main)
        self.assertIn('"|eventSequence="', self.main)
        self.assertIn("correlatedPayload", self.main)

    def test_control_payload_does_not_truncate_authenticated_json(self) -> None:
        send = method(self.protocol, "SendPvpControl")
        self.assertIn("peer control payload exceeded its character limit", send)
        self.assertIn("throw new InvalidOperationException", send)
        self.assertNotIn("BoundPvpString", send)


if __name__ == "__main__":
    unittest.main()
