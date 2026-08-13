from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = REPOSITORY_ROOT / "src" / "OperatorModdedOperations"
FRAMEWORK_PATH = SOURCE_ROOT / "CerberusNativeTabFix.cs"
AGREEMENT_PATH = SOURCE_ROOT / "CerberusNativeTabFix.PvpPeerAgreement.cs"


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
        rf"\bprivate\s+(?:static\s+)?[\w:.,<>\s]+?\s+{re.escape(name)}\s*\(",
        source,
    )
    if declaration is None:
        raise AssertionError(f"Could not find private method {name}.")
    opening = source.find("{", declaration.end())
    if opening < 0:
        raise AssertionError(f"Could not find opening brace for {name}.")
    closing = find_matching_brace(source, opening)
    return source[declaration.start() : closing + 1]


class PvpPeerAgreementTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.framework = FRAMEWORK_PATH.read_text(encoding="utf-8")
        cls.agreement = AGREEMENT_PATH.read_text(encoding="utf-8")

    def test_marker_discovery_is_mode_isolated(self) -> None:
        method = extract_method(self.framework, "FindStandalonePlayerMarkers")
        pve_branch = method.index(
            "mode == ModdedOperationMode.PlayerVersusEnvironment"
        )
        pvp_branch = method.index(
            "mode == ModdedOperationMode.PlayerVersusPlayer", pve_branch
        )
        self.assertIn('name.StartsWith("PVE_PlayerSpawn_"', method[pve_branch:pvp_branch])
        self.assertNotIn('name.StartsWith("PVP_Team1Spawn_"', method[pve_branch:pvp_branch])
        self.assertIn('name.StartsWith("PVP_Team1Spawn_"', method[pvp_branch:])
        self.assertIn('name.StartsWith("PVP_Team2Spawn_"', method[pvp_branch:])
        self.assertNotIn('name.StartsWith("PVE_PlayerSpawn_"', method[pvp_branch:])

    def test_pvp_capacity_is_derived_from_declared_maximum(self) -> None:
        method = extract_method(self.framework, "ValidateStandaloneSceneContract")
        self.assertIn("(operation.Operation.MaximumPlayers + 1) / 2", method)
        self.assertIn("team1Count < requiredPerTeam", method)
        self.assertIn("team2Count < requiredPerTeam", method)
        self.assertNotRegex(method.lower(), r"killhouse|lot[ _-]?12")
        self.assertNotRegex(method, r"requiredPerTeam\s*=\s*(?:6|8|10|12)\b")

    def test_offer_identity_covers_exact_runtime_contract(self) -> None:
        identity = extract_method(self.agreement, "CreatePvpPeerIdentity")
        digest = extract_method(self.agreement, "ComputePvpIdentityDigest")
        for token in (
            "PvpAgreementFrameworkVersion",
            "runtime.FrameworkSha256",
            "OperatorApi.ApiVersion",
            "runtime.ApiCoreSha256",
            "runtime.ApiHostSha256",
            "OperatorApi.Compatibility.DetectedGameBuildId",
            "PvpAgreementCapabilities",
            "map.PackageId",
            "map.PackageVersion",
            "map.PackageContentId",
            "map.Id",
            "operation.Id",
            "operation.Mode",
            "operation.SpawnSetId",
            "selection.Id",
            "selection.ScenePath",
            "timeCode",
            "operation.MinimumPlayers",
            "operation.MaximumPlayers",
            "runtime.CompanionPluginGuid",
            "runtime.CompanionPluginVersion",
            "runtime.CompanionSha256",
            "runtime.CompanionReadyMarkerName",
            "runtime.CompanionFailureMarkerName",
        ):
            self.assertIn(token, identity)
        self.assertIn("identity.Nonce", digest)
        self.assertIn("PvpAgreementProtocolVersion", digest)
        self.assertIn("SHA256.Create()", digest)
        self.assertIn("private const ushort PvpAgreementProtocolVersion = 2", self.agreement)

    def test_remote_offer_accepts_single_scene_synthetic_default_identity(self) -> None:
        resolve = extract_method(self.agreement, "TryResolveExactLocalPvpOffer")
        self.assertIn("bool declaredVariantMatches = HasDeclaredSceneVariants(map)", resolve)
        self.assertIn("map.SceneVariants != null && map.SceneVariants.Count == 1", resolve)
        self.assertIn("map.SceneVariants[0].Id", resolve)
        self.assertIn("map.ScenePath", resolve)

    def test_host_does_not_launch_until_all_content_acknowledgements(self) -> None:
        process = extract_method(self.agreement, "ProcessPvpPeerAgreement")
        content_barrier = process.index(
            "host.RequiredConnectionIds.SetEquals(\n                    host.ContentReadyConnectionIds)"
        )
        launch = process.index("InvokeNativeCatalogLaunch(", content_barrier)
        bypass = process.index("pvpPeerAgreementSatisfied: true", launch)
        self.assertLess(content_barrier, launch)
        self.assertLess(launch, bypass)
        self.assertIn("PvpContentReadyTimeoutSeconds", self.agreement)
        self.assertIn("TryValidateHostPvpMembership", process)

    def test_remote_preloads_then_commits_before_content_ready(self) -> None:
        accept = extract_method(self.agreement, "AcceptRemotePvpOfferOrReject")
        complete = extract_method(self.agreement, "CompleteRemotePvpContentReady")
        pending = extract_method(self.framework, "ProcessPendingLaunch")
        self.assertIn("new PendingMapLaunch", accept)
        self.assertIn("LaunchRequested = false", accept)
        commit = complete.index("activeOperation = new ActiveMapOperation")
        marked = complete.index("remote.ContentCommitted = true", commit)
        ack = complete.index("PvpAgreementMessageKind.ContentReady", marked)
        self.assertLess(commit, marked)
        self.assertLess(marked, ack)
        self.assertIn("PackageContentId", complete)
        self.assertIn(
            "PvpContentReadyTimeoutSeconds + PvpSceneReadyTimeoutSeconds",
            complete,
        )

        scene_loading = extract_method(self.agreement, "NotifyPvpSceneLoading")
        self.assertIn("PvpSceneReadyTimeoutSeconds", scene_loading)
        self.assertIn("remotePvpAgreementOwnsLoad", pending)
        self.assertIn(
            "pending.LaunchRequested && !remotePvpAgreementOwnsLoad", pending
        )
        self.assertLess(
            pending.index("NotifyPvpVerifiedMapBundlesAvailable(pending.Map)"),
            pending.index("pending.LaunchRequested && !remotePvpAgreementOwnsLoad"),
        )
        loaded = extract_method(self.framework, "OnSceneLoaded")
        self.assertLess(
            loaded.index("NotifyPvpSceneLoading(operation)"),
            loaded.index("ValidateStandaloneSceneContract"),
        )

    def test_scene_ready_barrier_gates_native_spawn(self) -> None:
        maintain = extract_method(self.framework, "MaintainStandaloneGameplay")
        authorization = maintain.index("IsPvpNetworkSpawnAuthorized(operation)")
        spawn = maintain.index("NetworkServer.Spawn(", authorization)
        self.assertLess(authorization, spawn)

        gate = extract_method(self.agreement, "IsPvpNetworkSpawnAuthorized")
        self.assertIn("IsHostLocalSceneReadyForCurrentEpoch(host)", gate)
        self.assertIn("AreRequiredPvpPeersSceneReadyForCurrentEpoch(host)", gate)
        self.assertIn("TryValidateHostPvpMembership", gate)

    def test_remote_adoption_requires_the_agreed_mode_and_asset(self) -> None:
        adoption = extract_method(self.framework, "TryAdoptNetworkSpawnedGameMode")
        asset = adoption.index("identity.assetId != operation.BootstrapAssetId")
        mode = adoption.index("!expectedMode", asset)
        agreement = adoption.index("!IsPvpNetworkSpawnExpected(operation)", mode)
        mutation = adoption.index("operation.BootstrapRoot =", agreement)
        self.assertLess(asset, mode)
        self.assertLess(mode, agreement)
        self.assertLess(agreement, mutation)

    def test_post_transition_agreement_failure_tears_down_host_generation(self) -> None:
        maintain = extract_method(self.framework, "MaintainStandaloneGameplay")
        failure_gate = maintain.index("operation.NetworkSpawnFailed")
        pvp_gate = maintain.rfind(
            "ModdedOperationMode.PlayerVersusPlayer", 0, failure_gate
        )
        release = maintain.index("ReleaseStandaloneSceneContracts(operation)", failure_gate)
        abort_request = maintain.index("RequestNativePvpAbortReturn(operation)", failure_gate)
        normal_spawn = maintain.index("NetworkServer.Spawn(", release)
        self.assertLess(pvp_gate, failure_gate)
        self.assertLess(abort_request, release)
        self.assertLess(failure_gate, release)
        self.assertLess(release, normal_spawn)
        self.assertIn("operation.ScenePreparationStarted = true", maintain[release:])
        self.assertIn("operation.NetworkSpawnFailed = true", maintain[release:])
        self.assertIn("!operation.PvpAbortReturnRequested", maintain)
        self.assertNotIn("operation.BootstrapRoot != null", maintain[:release])

        abort = extract_method(self.agreement, "RequestNativePvpAbortReturn")
        one_shot = abort.index("operation.PvpAbortReturnRequested = true")
        native_return = abort.index("GameManagerNetwork.instance.EndOperation()")
        fallback = abort.index("SceneManager.UnloadSceneAsync(scene)")
        self.assertLess(one_shot, native_return)
        self.assertLess(native_return, fallback)

    def test_remote_post_transition_failure_disconnects_then_local_cleanup_can_run(self) -> None:
        clear = extract_method(self.agreement, "ClearRemotePvpAgreement")
        self.assertIn("activeOperation.NetworkSpawnFailed = true", clear)
        self.assertIn("remote.ContentCommitted", clear)
        self.assertIn("RequestNativePvpAbortReturn(activeOperation)", clear)
        abort = extract_method(self.agreement, "RequestNativePvpAbortReturn")
        self.assertIn("!NetworkServer.active && NetworkClient.active", abort)
        self.assertIn("NetworkClient.Disconnect()", abort)

    def test_transport_is_collision_checked_and_exactly_owned(self) -> None:
        maintain = extract_method(self.agreement, "MaintainPvpPeerAgreementTransport")
        release = extract_method(self.agreement, "ReleasePvpPeerAgreementTransport")
        self.assertIn("NetworkClient.handlers.TryGetValue", maintain)
        self.assertIn("NetworkServer.handlers.TryGetValue", maintain)
        self.assertIn("message ID collision", maintain)
        self.assertIn("SamePvpAgreementHandler", release)
        self.assertIn("NetworkClient.handlers.Remove", release)
        self.assertIn("NetworkServer.handlers.Remove", release)

    def test_reload_resets_only_scene_barrier(self) -> None:
        reset = extract_method(self.agreement, "ResetPvpSceneAgreementForReload")
        begin = extract_method(self.agreement, "TryBeginHostPvpSceneGeneration")
        self.assertIn("TryBeginHostPvpSceneGeneration", reset)
        self.assertIn("host.SceneReadyEpochByConnectionId.Clear()", begin)
        self.assertIn("host.Phase = HostPvpAgreementPhase.WaitingForScenes", begin)
        self.assertIn("host.NativeLifecycleDeadlineTimestamp = 0", begin)
        self.assertIn("remote.SceneReadySent = false", reset)
        self.assertIn("remote.AwaitingLocalSceneGeneration = true", reset)
        self.assertIn("remote.NativeOwnerAdopted = false", reset)
        self.assertIn("remote.NativeReadinessInitialized = false", reset)
        self.assertNotIn("ContentReadyConnectionIds.Clear", reset)
        self.assertNotIn("ContentCommitted = false", reset)

    def test_native_return_closes_agreement_without_arming_restart(self) -> None:
        unload = extract_method(self.framework, "OnSceneUnloaded")
        self.assertIn("CompletePvpPeerAgreementOnNativeReturn(operation)", unload)
        self.assertLess(
            unload.index("CompletePvpPeerAgreementOnNativeReturn(operation)"),
            unload.index("ResetPvpSceneAgreementForReload(operation)"),
        )
        complete = extract_method(
            self.agreement, "CompletePvpPeerAgreementOnNativeReturn"
        )
        self.assertIn('"Operation Room"', complete)
        self.assertIn("NetworkManager.networkSceneName", complete)
        self.assertIn("hostPvpAgreement = null", complete)
        self.assertIn("remotePvpAgreement = null", complete)
        self.assertIn("operation.NativeLaunchInvoked = false", complete)
        self.assertIn("operation.PvpAbortReturnRequested = false", complete)
        self.assertIn("operation.NetworkSpawnFailed = false", complete)
        self.assertIn("return true;", complete[complete.index("operation.NativeLaunchInvoked = false"):])

    def test_plugin_unload_aborts_a_loaded_pvp_scene(self) -> None:
        unload = re.search(
            r"public\s+override\s+bool\s+Unload\s*\(", self.framework
        )
        self.assertIsNotNone(unload)
        opening = self.framework.find("{", unload.end())
        closing = find_matching_brace(self.framework, opening)
        body = self.framework[unload.start() : closing + 1]
        capture = body.index("ActiveMapOperation pvpUnloadOperation")
        transport = body.index("ReleasePvpPeerAgreementTransport", capture)
        abort = body.index("RequestNativePvpAbortReturn", transport)
        release = body.index("ReleaseStandaloneSceneContracts", abort)
        self.assertLess(capture, transport)
        self.assertLess(transport, abort)
        self.assertLess(abort, release)

    def test_remote_teardown_destroys_adopted_root_and_distinct_template(self) -> None:
        release = extract_method(self.framework, "ReleaseStandaloneGameMode")
        unregister = release.index("NetworkClient.UnregisterPrefab(bootstrapPrefabRoot)")
        distinct = release.index(
            "bootstrapPrefabRoot.GetInstanceID() != bootstrapRoot.GetInstanceID()"
        )
        destroy_template = release.index("Object.Destroy(bootstrapPrefabRoot)", distinct)
        destroy_adopted = release.index("Object.Destroy(bootstrapRoot)", destroy_template)
        self.assertLess(unregister, distinct)
        self.assertLess(distinct, destroy_template)
        self.assertLess(destroy_template, destroy_adopted)

    def test_protocol_v2_byte_binds_every_runtime_module(self) -> None:
        write = extract_method(self.agreement, "WritePvpIdentity")
        read = extract_method(self.agreement, "ReadPvpIdentity")
        digest = extract_method(self.agreement, "ComputePvpIdentityDigest")
        resolve = extract_method(self.agreement, "TryResolveExactLocalPvpOffer")
        fields = (
            "FrameworkSha256",
            "ApiCoreSha256",
            "ApiHostSha256",
            "CompanionPluginGuid",
            "CompanionPluginVersion",
            "CompanionSha256",
            "CompanionReadyMarkerName",
            "CompanionFailureMarkerName",
        )
        for field in fields:
            self.assertIn(f"identity.{field}", write)
            self.assertIn(field, read)
            self.assertIn(f"identity.{field}", digest)
        for field in ("FrameworkSha256", "ApiCoreSha256", "ApiHostSha256"):
            self.assertIn(f"offer.{field}", resolve)
            self.assertIn(f"localRuntime.{field}", resolve)
        self.assertIn("PvpCompanionIdentityMatches(offer, mapRuntime)", resolve)

    def test_sha_fields_are_exact_lowercase_and_malformed_wire_values_fail(self) -> None:
        read_hash = extract_method(self.agreement, "ReadPvpSha256")
        validate_hash = extract_method(self.agreement, "IsLowercasePvpSha256")
        read = extract_method(self.agreement, "ReadPvpIdentity")
        validate_companion = extract_method(
            self.agreement, "ValidatePvpCompanionIdentity"
        )
        self.assertIn("ReadBoundedPvpString(reader, 64", read_hash)
        self.assertIn("value.Length == 64", validate_hash)
        self.assertIn("character is >= '0' and <= '9' or >= 'a' and <= 'f'", validate_hash)
        for field in ("frameworkSha256", "apiCoreSha256", "apiHostSha256"):
            self.assertIn(f'ReadPvpSha256(reader, "{field}")', read)
        self.assertIn("IsLowercasePvpSha256(identity.CompanionSha256)", validate_companion)
        self.assertIn("string.IsNullOrWhiteSpace(identity.CompanionPluginGuid)", validate_companion)
        self.assertIn("companion none identity is internally inconsistent", validate_companion)

    def test_loaded_runtime_identity_is_resolved_from_exact_loaded_bytes(self) -> None:
        runtime = extract_method(self.agreement, "TryResolveLocalRuntimeIdentity")
        plugin = extract_method(self.agreement, "TryResolveLoadedPlugin")
        assembly = extract_method(self.agreement, "TryHashLoadedAssembly")
        self.assertIn("typeof(CerberusNativeTabFix).Assembly", runtime)
        self.assertIn("typeof(OperatorApi).Assembly", runtime)
        self.assertIn("PvpApiPluginGuid", runtime)
        self.assertIn("map?.RuntimeCompanion", runtime)
        self.assertIn("IL2CPPChainloader.Instance", plugin)
        self.assertIn("plugin.TypeName, pluginType.FullName", plugin)
        self.assertIn("RequireExpectedPvpSha256", plugin)
        self.assertIn("FileShare.Read", assembly)
        self.assertIn("PEReader", assembly)
        self.assertIn("assembly.ManifestModule.ModuleVersionId", assembly)
        self.assertIn("SHA256.Create()", assembly)

    def test_same_version_different_runtime_bytes_are_rejected(self) -> None:
        resolve = extract_method(self.agreement, "TryResolveExactLocalPvpOffer")
        version = resolve.index("offer.FrameworkVersion")
        framework_hash = resolve.index("offer.FrameworkSha256", version)
        api_version = resolve.index("offer.ApiVersion", framework_hash)
        api_core_hash = resolve.index("offer.ApiCoreSha256", api_version)
        api_host_hash = resolve.index("offer.ApiHostSha256", api_core_hash)
        failure = resolve.index(
            'error = "framework/API binary/game-build/capability identity mismatch"'
        )
        self.assertLess(version, framework_hash)
        self.assertLess(framework_hash, api_version)
        self.assertLess(api_version, api_core_hash)
        self.assertLess(api_core_hash, api_host_hash)
        self.assertLess(api_host_hash, failure)

    def test_runtime_companion_ready_gate_is_exact_and_failure_always_wins(self) -> None:
        process = extract_method(self.agreement, "ProcessPvpPeerAgreement")
        gate = extract_method(self.agreement, "TryAdvancePvpPackageRuntimeReadiness")
        complete = extract_method(self.agreement, "CompletePvpSceneReady")
        self.assertIn("TryAdvancePvpPackageRuntimeReadiness(activeOperation)", process)
        scene = gate.index("FindLoadedSceneByHandle(operation.SceneHandle)")
        failure_lookup = gate.index("companion.FailureMarkerName", scene)
        ready_lookup = gate.index("companion.ReadyMarkerName", failure_lookup)
        failure_decision = gate.index("if (failureMarkers != 0)", ready_lookup)
        already_ready = gate.index(
            "IsHostLocalSceneReadyForCurrentEpoch(currentHost)", failure_decision
        )
        ready_decision = gate.index("if (readyMarkers == 0)", already_ready)
        self.assertLess(failure_decision, already_ready)
        self.assertLess(already_ready, ready_decision)
        self.assertIn("readyMarkers != 1", gate)
        self.assertIn("CompletePvpSceneReady(operation)", gate)
        send_ready = extract_method(
            self.agreement, "TrySendRemotePvpSceneReadyAcknowledgement"
        )
        self.assertIn("PvpAgreementMessageKind.SceneReady", send_ready)

    def test_replacement_offer_rejects_and_returns_before_preload(self) -> None:
        accept = extract_method(self.agreement, "AcceptRemotePvpOfferOrReject")
        replacement = accept.index(
            '"host replaced an unfinished PVP agreement without cancellation"'
        )
        rejection = accept.index("PvpAgreementMessageKind.Reject", replacement)
        override = accept.index("offer);", rejection)
        teardown = accept.index("ClearRemotePvpAgreement(replacementReason)", override)
        immediate_return = accept.index("return;", replacement)
        resolver = accept.index("TryResolveExactLocalPvpOffer", immediate_return)
        preload = accept.index("new PendingMapLaunch", resolver)
        self.assertLess(replacement, rejection)
        self.assertLess(rejection, override)
        self.assertLess(override, teardown)
        self.assertLess(teardown, immediate_return)
        self.assertLess(immediate_return, resolver)
        self.assertLess(resolver, preload)

    def test_native_launch_and_scene_handle_abort_holes_are_closed(self) -> None:
        launch = extract_method(self.framework, "InvokeNativeBoardStart")
        scene_loaded = extract_method(self.framework, "OnSceneLoaded")
        fail_host = extract_method(self.agreement, "FailHostPvpAgreement")
        clear_remote = extract_method(self.agreement, "ClearRemotePvpAgreement")
        self.assertLess(
            launch.index("activeOperation.NativeLaunchInvoked = true"),
            launch.index("board.Start_Operation()"),
        )
        self.assertLess(
            launch.index("NotifyPvpNativeLaunchInvoked(activeOperation)"),
            launch.index("board.Start_Operation()"),
        )
        assign_handle = scene_loaded.index("operation.SceneHandle = scene.handle")
        validate = scene_loaded.index("ValidateStandaloneSceneContract", assign_handle)
        self.assertLess(assign_handle, validate)
        self.assertIn("activeOperation.NativeLaunchInvoked", fail_host)
        self.assertIn("remote.ContentCommitted", clear_remote)
        self.assertIn("RequestNativePvpAbortReturn", fail_host)
        self.assertIn("RequestNativePvpAbortReturn", clear_remote)

    def test_load_before_unload_restart_resets_the_new_generation(self) -> None:
        loaded = extract_method(self.framework, "OnSceneLoaded")
        mismatch = loaded.index(
            "previousSceneHandle != 0 && previousSceneHandle != scene.handle"
        )
        reset = loaded.index("ResetPvpSceneAgreementForReload(operation)", mismatch)
        retire = loaded.index("operation.SceneHandle = 0", reset)
        adopt = loaded.index("operation.SceneHandle = scene.handle", retire)
        self.assertLess(mismatch, reset)
        self.assertLess(reset, retire)
        self.assertLess(retire, adopt)
        unloaded = extract_method(self.framework, "OnSceneUnloaded")
        self.assertIn("scene.handle != operation.SceneHandle", unloaded)

    def test_scene_ready_wire_is_epoch_versioned_and_unversioned_ready_fails(self) -> None:
        client = extract_method(self.agreement, "HandlePvpClientAgreementEnvelope")
        server = extract_method(self.agreement, "HandlePvpServerAgreementEnvelope")
        send = extract_method(self.agreement, "SendPvpControl")
        self.assertIn("SceneReadyRequest = 6", self.agreement)
        self.assertIn("NetworkReaderExtensions.ReadULong(reader)", client)
        self.assertIn("NetworkReaderExtensions.ReadULong(reader)", server)
        self.assertIn("NetworkWriterExtensions.WriteULong(writer, sceneGenerationEpoch)", send)
        self.assertIn(
            '"remote scene-ready acknowledgement omitted its generation epoch"',
            server,
        )
        self.assertIn(
            '"host scene-ready request omitted its generation epoch"', client
        )
        self.assertIn("scene-ready-epoch-v1", self.agreement)
        self.assertNotIn(";scene-ready-v1;", self.agreement)

    def test_host_issues_initial_epoch_once_before_native_scene_transition(self) -> None:
        process = extract_method(self.agreement, "ProcessPvpPeerAgreement")
        begin = extract_method(self.agreement, "TryBeginHostPvpSceneGeneration")
        content = process.index(
            "host.RequiredConnectionIds.SetEquals(\n                    host.ContentReadyConnectionIds)"
        )
        issue = process.index("TryBeginHostPvpSceneGeneration(", content)
        launch = process.index("InvokeNativeCatalogLaunch(", issue)
        self.assertLess(content, issue)
        self.assertLess(issue, launch)
        self.assertIn("host.SceneGenerationEpoch++", begin)
        self.assertIn("BroadcastHostPvpSceneReadyRequest", begin)
        self.assertIn("host.SceneGenerationEpoch == ulong.MaxValue", begin)
        self.assertIn('error = "host scene-generation epoch overflowed"', begin)

    def test_host_restart_epoch_is_incremented_exactly_at_restart_reset(self) -> None:
        reset = extract_method(self.agreement, "ResetPvpSceneAgreementForReload")
        begin = extract_method(self.agreement, "TryBeginHostPvpSceneGeneration")
        loaded = extract_method(self.framework, "OnSceneLoaded")
        unloaded = extract_method(self.framework, "OnSceneUnloaded")
        self.assertEqual(reset.count("TryBeginHostPvpSceneGeneration("), 1)
        self.assertEqual(begin.count("host.SceneGenerationEpoch++"), 1)
        self.assertIn("ResetPvpSceneAgreementForReload(operation)", loaded)
        self.assertIn("previousSceneHandle != scene.handle", loaded)
        self.assertIn("ResetPvpSceneAgreementForReload(operation)", unloaded)
        self.assertIn("scene.handle != operation.SceneHandle", unloaded)

    def test_remote_faster_than_host_cannot_ack_old_epoch_and_resends_new(self) -> None:
        observe = extract_method(self.agreement, "NotifyPvpSceneLoading")
        accept = extract_method(self.agreement, "AcceptRemotePvpSceneReadyRequest")
        send = extract_method(
            self.agreement, "TrySendRemotePvpSceneReadyAcknowledgement"
        )
        reset = extract_method(self.agreement, "ResetPvpSceneAgreementForReload")
        self.assertIn("remote.AwaitingLocalSceneGeneration = true", reset)
        self.assertIn("remote.LocalSceneGeneration++", observe)
        self.assertIn("remote.AwaitingLocalSceneGeneration = false", observe)
        self.assertIn(
            "remote.RequestedSceneGenerationEpoch ==\n                remote.SceneReadySentEpoch",
            send,
        )
        race_comment = send.index("A remote scene can complete")
        self.assertIn("return;", send[race_comment:])
        self.assertIn("TrySendRemotePvpSceneReadyAcknowledgement", accept)
        self.assertIn("allowResend: true", accept)

    def test_host_faster_than_remote_request_targets_strictly_newer_generation(self) -> None:
        accept = extract_method(self.agreement, "AcceptRemotePvpSceneReadyRequest")
        send = extract_method(
            self.agreement, "TrySendRemotePvpSceneReadyAcknowledgement"
        )
        target = accept.index("remote.RequestedLocalSceneGeneration =")
        self.assertIn(
            "remote.SceneReadySentLocalGeneration + 1",
            accept[target:],
        )
        self.assertIn(
            "remote.LocalSceneGeneration < remote.RequestedLocalSceneGeneration",
            send,
        )
        self.assertIn(
            "remote.LocalSceneGeneration == remote.RequestedLocalSceneGeneration",
            extract_method(self.agreement, "IsRemoteSceneReadyForCurrentRequest"),
        )

    def test_stale_scene_ready_is_rejected_and_current_request_is_resent(self) -> None:
        server = extract_method(self.agreement, "HandlePvpServerAgreementEnvelope")
        epoch_match = server.index(
            "sceneGenerationEpoch == host.SceneGenerationEpoch"
        )
        accepted = server.index(
            "host.SceneReadyEpochByConnectionId[connection.connectionId]", epoch_match
        )
        stale = server.index("rejected as", accepted)
        resend = server.index("SendHostPvpSceneReadyRequest", stale)
        self.assertLess(epoch_match, accepted)
        self.assertLess(accepted, stale)
        self.assertLess(stale, resend)
        self.assertIn("receivedEpoch=", server)
        self.assertIn("expectedEpoch=", server)

    def test_scene_ready_request_is_bounded_retried_and_duplicate_resends_ack(self) -> None:
        process = extract_method(self.agreement, "ProcessPvpPeerAgreement")
        broadcast = extract_method(
            self.agreement, "BroadcastHostPvpSceneReadyRequest"
        )
        accept = extract_method(self.agreement, "AcceptRemotePvpSceneReadyRequest")
        self.assertIn("SceneReadyRequestRetryTimestamp", process)
        self.assertIn("BroadcastHostPvpSceneReadyRequest(host, isRetry: true)", process)
        self.assertIn("PvpSceneReadyRequestRetrySeconds", broadcast)
        begin = extract_method(self.agreement, "TryBeginHostPvpSceneGeneration")
        self.assertIn("PvpSceneReadyTimeoutSeconds", begin)
        duplicate = accept.index(
            "sceneGenerationEpoch == remote.RequestedSceneGenerationEpoch"
        )
        resend = accept.index("allowResend: true", duplicate)
        self.assertLess(duplicate, resend)

    def test_connection_id_reuse_cannot_inherit_scene_ack(self) -> None:
        session_source = self.agreement[
            self.agreement.index("private sealed class HostPvpAgreement") :
            self.agreement.index("private sealed class RemotePvpAgreement")
        ]
        membership = extract_method(self.agreement, "TryValidateHostPvpMembership")
        handler = extract_method(self.agreement, "HandlePvpServerAgreementEnvelope")
        self.assertIn("RequiredConnections", session_source)
        self.assertIn("SamePvpNetworkConnection(required, connected)", membership)
        self.assertIn("SamePvpNetworkConnection(requiredConnection, connection)", handler)
        self.assertIn("SceneReadyEpochByConnectionId", handler)

    def test_same_handle_remote_reload_still_advances_one_local_generation(self) -> None:
        reset = extract_method(self.agreement, "ResetPvpSceneAgreementForReload")
        loading = extract_method(self.agreement, "NotifyPvpSceneLoading")
        self.assertIn("remote.AwaitingLocalSceneGeneration = true", reset)
        condition = loading.index("remote.AwaitingLocalSceneGeneration ||")
        increment = loading.index("remote.LocalSceneGeneration++", condition)
        clear = loading.index("remote.AwaitingLocalSceneGeneration = false", increment)
        self.assertLess(condition, increment)
        self.assertLess(increment, clear)

    def test_remote_epoch_mapping_overflow_fails_closed(self) -> None:
        accept = extract_method(self.agreement, "AcceptRemotePvpSceneReadyRequest")
        guard = accept.index(
            "remote.SceneReadySentLocalGeneration == ulong.MaxValue"
        )
        fail = accept.index("FailRemotePvpAgreement(", guard)
        assignment = accept.index("remote.RequestedLocalSceneGeneration =", fail)
        self.assertLess(guard, fail)
        self.assertLess(fail, assignment)
        self.assertIn('"remote scene-generation request mapping overflowed"', accept)

    def test_remote_rejects_skipped_epoch_and_advance_before_prior_ack(self) -> None:
        accept = extract_method(self.agreement, "AcceptRemotePvpSceneReadyRequest")
        monotonic = accept.index(
            "sceneGenerationEpoch != remote.RequestedSceneGenerationEpoch + 1"
        )
        skipped_fail = accept.index("FailRemotePvpAgreement(", monotonic)
        prior = accept.index(
            "remote.RequestedSceneGenerationEpoch !=\n            remote.SceneReadySentEpoch",
            skipped_fail,
        )
        prior_fail = accept.index("FailRemotePvpAgreement(", prior)
        assignment = accept.index(
            "remote.RequestedSceneGenerationEpoch = sceneGenerationEpoch",
            prior_fail,
        )
        self.assertLess(monotonic, skipped_fail)
        self.assertLess(skipped_fail, prior)
        self.assertLess(prior, prior_fail)
        self.assertLess(prior_fail, assignment)
        self.assertIn(
            '"host scene-generation request was not the next monotonic epoch"',
            accept,
        )
        self.assertIn(
            '"host advanced the scene-generation epoch before the prior "', accept
        )

    def test_pvp_spawn_contract_is_required_before_scene_ready(self) -> None:
        configure = extract_method(
            self.framework, "ConfigureStandalonePlayerSpawnContract"
        )
        prepare = extract_method(self.framework, "PrepareStandaloneScene")
        runtime_gate = extract_method(
            self.agreement, "TryAdvancePvpPackageRuntimeReadiness"
        )
        installed_contract = extract_method(
            self.agreement, "TryValidateInstalledPvpSpawnContract"
        )
        self.assertIn("operation.SpawnContractInstalled", configure)
        self.assertIn("SameNativeSpawnList", configure)
        self.assertIn("SameNativeGameObjectArray", configure)
        self.assertIn("gameManager.RandomSpawns != operation.OwnedRandomSpawns", configure)
        random_write = configure.index(
            "GameManager.instance.RandomSpawns = operation.OwnedRandomSpawns"
        )
        installed_index = configure.index("operation.SpawnContractInstalled = true", random_write)
        self.assertLess(random_write, installed_index)
        catch = configure.index("catch (Exception ex)", installed_index)
        self.assertIn("!operation.SpawnContractInstalled", configure[catch:])
        self.assertIn("operation.OwnedSpawnPoints != null", configure[catch:])
        self.assertIn("operation.OwnedFallbackSpawns != null", configure[catch:])
        self.assertIn("TryValidateInstalledPvpSpawnContract", runtime_gate)
        self.assertIn("operation.SpawnContractInstalled", installed_contract)
        self.assertIn("SameNativeSpawnList", installed_contract)
        self.assertIn("SameNativeGameObjectArray", installed_contract)
        self.assertIn(
            "gameManager.RandomSpawns != operation.OwnedRandomSpawns",
            installed_contract,
        )
        install = prepare.index("ConfigureStandalonePlayerSpawnContract")
        fail = prepare.index("if (!spawnContractReady", install)
        notify_failure = prepare.index("NotifyPvpScenePreparationFailed", fail)
        notify_ready = prepare.index("NotifyPvpScenePrepared", notify_failure)
        self.assertLess(install, fail)
        self.assertLess(fail, notify_failure)
        self.assertLess(notify_failure, notify_ready)

    def test_native_pvp_lifecycle_exceptions_abort_once_without_fallback(self) -> None:
        readiness = extract_method(
            self.framework, "MarkStandalonePvpReadinessInitializationFailed"
        )
        all_loaded = extract_method(
            self.framework, "OnStandalonePvpAllPlayersLoadedFailed"
        )
        claim = extract_method(
            self.framework, "TryClaimStandalonePvpAllPlayersLoaded"
        )
        self.assertNotIn("ReadinessInitializationClaimed = false", readiness)
        self.assertIn("FailActivePvpNativeLifecycle(operation, reason)", readiness)
        self.assertIn("operation.AllPlayersLoadedClaimed = true", claim)
        self.assertNotIn("operation.AllPlayersLoaded = true", claim)
        self.assertIn("FailActivePvpNativeLifecycle(operation, reason)", all_loaded)
        self.assertNotIn("AllPlayersLoaded = true", all_loaded)
        self.assertNotIn("SpawnAndPositionStandalonePlayers", all_loaded)
        helper = extract_method(self.agreement, "FailActivePvpNativeLifecycle")
        self.assertIn("PvpAgreementMessageKind.Reject", extract_method(
            self.agreement, "FailRemotePvpAgreement"
        ))
        self.assertIn("FailHostPvpAgreement", helper)
        self.assertIn("FailRemotePvpAgreement", helper)

    def test_remote_owner_and_host_native_lifecycle_waits_are_bounded(self) -> None:
        process = extract_method(self.agreement, "ProcessPvpPeerAgreement")
        complete = extract_method(self.agreement, "CompletePvpSceneReady")
        adopted = extract_method(self.agreement, "NotifyPvpNativeOwnerAdopted")
        spawned = extract_method(self.agreement, "NotifyPvpNetworkOwnerSpawned")
        loaded = extract_method(self.agreement, "NotifyPvpAllPlayersLoaded")
        self.assertIn("HostPvpAgreementPhase.ReadyToSpawn", process)
        self.assertIn("timed out waiting to publish the exact native PVP owner", process)
        self.assertIn("active operation ownership changed after native PVP launch", process)
        self.assertIn("active operation ownership changed after remote content commit", process)
        self.assertIn("hostNativeLifecycleComplete", process)
        self.assertIn("remote.NativeOwnerAdopted", process)
        self.assertIn("remote.NativeReadinessInitialized", process)
        send_ready = extract_method(
            self.agreement, "TrySendRemotePvpSceneReadyAcknowledgement"
        )
        self.assertIn("PvpNativeOwnerTimeoutSeconds", send_ready)
        self.assertIn("PvpNativeLifecycleTimeoutSeconds", adopted)
        self.assertIn("PvpNativeLifecycleTimeoutSeconds", spawned)
        self.assertIn("NativeLifecycleDeadlineTimestamp = 0", loaded)

    def test_plugin_unload_and_transport_release_abort_handle_zero_transitions(self) -> None:
        release = extract_method(self.agreement, "ReleasePvpPeerAgreementTransport")
        self.assertIn("agreementOperation.NativeLaunchInvoked", release)
        self.assertIn("hostPvpAgreement?.NativeLaunchInvoked", release)
        self.assertIn("remotePvpAgreement?.ContentCommitted", release)
        self.assertIn("PvpAgreementMessageKind.Cancel", release)
        self.assertIn("PvpAgreementMessageKind.Reject", release)
        abort = release.index("RequestNativePvpAbortReturn")
        clear_host = release.index("hostPvpAgreement = null", abort)
        clear_remote = release.index("remotePvpAgreement = null", clear_host)
        self.assertLess(abort, clear_host)
        self.assertLess(clear_host, clear_remote)

    def test_pve_launch_and_spawn_paths_remain_outside_pvp_barriers(self) -> None:
        launch = extract_method(self.framework, "InvokeNativeCatalogLaunch")
        spawn = extract_method(self.agreement, "IsPvpNetworkSpawnAuthorized")
        self.assertIn("ClearPvpPeerAgreementForNonPvpLaunch()", launch)
        self.assertIn(
            "operation.Mode ==\n                ModdedOperationMode.PlayerVersusPlayer",
            launch,
        )
        self.assertIn(
            "operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer",
            spawn,
        )
        self.assertIn("return true;", spawn)

        clear = extract_method(
            self.agreement, "ClearPvpPeerAgreementForNonPvpLaunch"
        )
        self.assertIn("hostPvpAgreement = null", clear)
        self.assertIn("remotePvpAgreement = null", clear)


if __name__ == "__main__":
    unittest.main()
