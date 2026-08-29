using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

public sealed partial class CerberusNativeTabFix
{
    private bool TryPreflightStandalonePeerGameModeClone(
        ActiveMapOperation operation,
        out string error)
    {
        error = string.Empty;
        GameObject probe = null;
        try
        {
            if (!TryCreateStandalonePeerGameModeClone(
                    operation,
                    operation.BootstrapPrefabRoot.transform.position,
                    operation.BootstrapPrefabRoot.transform.rotation,
                    operation.BootstrapPrefabRoot.transform.localScale,
                    track: false,
                    out probe,
                    out _,
                    out error))
            {
                return false;
            }
            return true;
        }
        finally
        {
            if (probe != null)
                Object.Destroy(probe);
        }
    }

    private GameObject SpawnStandalonePeerGameModeClone(
        ActiveMapOperation operation,
        Vector3 position,
        uint assetId)
    {
        if (operation == null || !ReferenceEquals(activeOperation, operation))
        {
            throw new InvalidOperationException(
                "the Mirror spawn handler no longer owns the active operation");
        }
        if (!operation.PeerGameModeHandlerRegistered ||
            operation.PeerGameModeSpawnHandler == null ||
            operation.PeerGameModeUnspawnHandler == null ||
            operation.BootstrapAssetId == 0 ||
            assetId != operation.BootstrapAssetId)
        {
            throw new InvalidOperationException(
                "the Mirror spawn message is outside the frozen game-mode handler contract");
        }
        if (!TryCreateStandalonePeerGameModeClone(
                operation,
                position,
                operation.BootstrapPrefabRoot.transform.rotation,
                operation.BootstrapPrefabRoot.transform.localScale,
                track: true,
                out GameObject clone,
                out NetworkIdentity identity,
                out string error))
        {
            operation.NetworkSpawnFailed = true;
            log.LogError("Standalone Mirror clone failed before payload deserialization: " +
                error + ".");
            throw new InvalidOperationException(error);
        }

        log.LogInfo("Standalone Mirror clone passed its pre-deserialization contract: " +
            "assetId=0x" + assetId.ToString("X8") +
            ", cloneInstanceId=" + clone.GetInstanceID() +
            ", behaviours=" + identity.NetworkBehaviours.Length + ".");
        return clone;
    }

    private static bool TryCreateStandalonePeerGameModeClone(
        ActiveMapOperation operation,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        bool track,
        out GameObject clone,
        out NetworkIdentity identity,
        out string error)
    {
        clone = null;
        identity = null;
        error = string.Empty;
        OwnedPeerGameModeClone owned = null;
        try
        {
            if (operation == null || operation.SceneHandle == 0 ||
                operation.BootstrapPrefabRoot == null ||
                operation.BootstrapPrefabIdentity == null ||
                operation.BootstrapAssetId == 0 ||
                operation.BootstrapPrefabRoot.activeSelf)
            {
                throw new InvalidOperationException(
                    "the inactive operation-owned Mirror template is unavailable");
            }
            Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("the package scene is not loaded");

            NetworkBehaviour[] templateBehaviours =
                operation.BootstrapPrefabRoot.GetComponentsInChildren<NetworkBehaviour>(true);
            if (templateBehaviours == null || templateBehaviours.Length == 0 ||
                operation.BootstrapPrefabIdentity.NetworkBehaviours == null ||
                operation.BootstrapPrefabIdentity.NetworkBehaviours.Length !=
                    templateBehaviours.Length)
            {
                throw new InvalidOperationException(
                    "the template NetworkBehaviour graph is incomplete");
            }
            for (int index = 0; index < templateBehaviours.Length; index++)
            {
                NetworkBehaviour templateBehaviour = templateBehaviours[index];
                if (templateBehaviour == null ||
                    operation.BootstrapPrefabIdentity.NetworkBehaviours[index] !=
                        templateBehaviour ||
                    templateBehaviour.syncObjects == null ||
                    templateBehaviour.syncObjects.Count != 0)
                {
                    throw new InvalidOperationException(
                        "the template contains an unsupported NetworkBehaviour/SyncObject shape");
                }
            }

            clone = Object.Instantiate(
                operation.BootstrapPrefabRoot,
                position,
                rotation);
            if (clone == null || clone.activeSelf || clone.activeInHierarchy)
                throw new InvalidOperationException("Unity returned an active or null clone");
            clone.name = "MODDED_OPERATIONS_GAME_MODE_REMOTE_CLONE";
            clone.transform.localScale = scale;
            if (clone.scene.handle != scene.handle)
                SceneManager.MoveGameObjectToScene(clone, scene);

            identity = clone.GetComponent<NetworkIdentity>();
            NetworkBehaviour[] cloneBehaviours =
                clone.GetComponentsInChildren<NetworkBehaviour>(true);
            if (!TryRepairStandalonePveCloneNetworkBehaviours(
                    operation,
                    clone,
                    templateBehaviours,
                    ref cloneBehaviours,
                    out string repairError))
            {
                throw new InvalidOperationException(repairError);
            }
            if (identity == null || identity.gameObject != clone ||
                identity.assetId != operation.BootstrapAssetId ||
                identity.netId != 0 || identity.sceneId != 0 ||
                cloneBehaviours == null ||
                cloneBehaviours.Length != templateBehaviours.Length)
            {
                throw new InvalidOperationException(
                    "the cloned NetworkIdentity/NetworkBehaviour shape drifted");
            }

            owned = new OwnedPeerGameModeClone
            {
                Root = clone,
                Identity = identity
            };
            identity._NetworkBehaviours_k__BackingField = null;
            var listPointers = new HashSet<IntPtr>();
            for (int index = 0; index < cloneBehaviours.Length; index++)
            {
                NetworkBehaviour templateBehaviour = templateBehaviours[index];
                NetworkBehaviour behaviour = cloneBehaviours[index];
                if (behaviour == null ||
                    behaviour.GetType() != templateBehaviour.GetType())
                {
                    throw new InvalidOperationException(
                        "the cloned NetworkBehaviour order/type drifted at index=" +
                        index + ", template=" +
                        (templateBehaviour == null
                            ? "<null>"
                            : templateBehaviour.GetType().FullName) +
                        ", clone=" +
                        (behaviour == null
                            ? "<null>"
                            : behaviour.GetType().FullName));
                }
                behaviour._netIdentity_k__BackingField = null;
                behaviour._ComponentIndex_k__BackingField = 0;
                var fresh = new Il2CppSystem.Collections.Generic.List<SyncObject>();
                behaviour.syncObjects = fresh;
                if (fresh.Pointer == IntPtr.Zero ||
                    fresh.Pointer == templateBehaviour.syncObjects.Pointer ||
                    !listPointers.Add(fresh.Pointer))
                {
                    throw new InvalidOperationException(
                        "the cloned syncObjects list is null or aliased");
                }
                owned.SyncObjects.Add(new OwnedBootstrapSyncObjects
                {
                    Behaviour = behaviour,
                    Value = fresh
                });
            }

            identity.InitializeNetworkBehaviours();
            var authoritative = identity.NetworkBehaviours;
            if (authoritative == null || authoritative.Length != cloneBehaviours.Length)
                throw new InvalidOperationException("Mirror did not initialize the clone graph");
            for (int index = 0; index < cloneBehaviours.Length; index++)
            {
                NetworkBehaviour behaviour = authoritative[index];
                if (behaviour == null || behaviour != cloneBehaviours[index] ||
                    behaviour.netIdentity != identity ||
                    behaviour.ComponentIndex != index ||
                    behaviour.syncObjects == null ||
                    behaviour.syncObjects.Count != 0)
                {
                    throw new InvalidOperationException(
                        "the clone graph is invalid before Mirror payload deserialization");
                }
            }

            int pveOwners = clone.GetComponentsInChildren<StandalonePveGameMode>(true).Length;
            int pvpOwners = clone.GetComponentsInChildren<StandalonePvpGameMode>(true).Length;
            if ((operation.Operation.Mode ==
                    OperatorModAPI.ModdedOperationMode.PlayerVersusEnvironment &&
                 (pveOwners != 1 || pvpOwners != 0)) ||
                (operation.Operation.Mode ==
                    OperatorModAPI.ModdedOperationMode.PlayerVersusPlayer &&
                 (pvpOwners != 1 || pveOwners != 0)))
            {
                throw new InvalidOperationException(
                    "the clone contains the wrong game-mode owner component");
            }

            if (track)
            {
                int instanceId = clone.GetInstanceID();
                if (instanceId == 0 || operation.PeerGameModeClones.ContainsKey(instanceId))
                    throw new InvalidOperationException("the clone ownership key is invalid");
                operation.PeerGameModeClones.Add(instanceId, owned);
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            if (clone != null)
                Object.Destroy(clone);
            clone = null;
            identity = null;
            return false;
        }
    }

    private static bool TryRepairStandalonePveCloneNetworkBehaviours(
        ActiveMapOperation operation,
        GameObject clone,
        NetworkBehaviour[] templateBehaviours,
        ref NetworkBehaviour[] cloneBehaviours,
        out string error)
    {
        error = string.Empty;
        if (operation.Operation.Mode !=
            OperatorModAPI.ModdedOperationMode.PlayerVersusEnvironment)
        {
            return true;
        }
        if (templateBehaviours == null || templateBehaviours.Length != 3 ||
            templateBehaviours[0] is not StandalonePveGameMode ||
            templateBehaviours[1] is not ExfilZone ||
            templateBehaviours[2] is not RaidManager ||
            cloneBehaviours == null || cloneBehaviours.Length != 3)
        {
            error = "the PVE template/clone does not have the exact owner, " +
                "ExfilZone, RaidManager NetworkBehaviour graph";
            return false;
        }

        if (cloneBehaviours[0] is StandalonePveGameMode &&
            cloneBehaviours[1] is ExfilZone &&
            cloneBehaviours[2] is RaidManager)
        {
            return true;
        }
        if (cloneBehaviours[0] is not StandalonePveGameMode pve ||
            cloneBehaviours[1] == null ||
            cloneBehaviours[1].GetType() != typeof(NetworkBehaviour) ||
            cloneBehaviours[2] == null ||
            cloneBehaviours[2].GetType() != typeof(NetworkBehaviour))
        {
            error = "Unity's PVE clone placeholders were not the observed exact " +
                "owner, NetworkBehaviour, NetworkBehaviour shape";
            return false;
        }

        ExfilZone sourceExfil = (ExfilZone)templateBehaviours[1];
        RaidManager sourceRaid = (RaidManager)templateBehaviours[2];
        Object.DestroyImmediate(cloneBehaviours[2]);
        Object.DestroyImmediate(cloneBehaviours[1]);

        ExfilZone exfil = clone.AddComponent<ExfilZone>();
        RaidManager raid = clone.AddComponent<RaidManager>();
        if (exfil == null || raid == null)
        {
            error = "the exact native PVE components could not replace Unity's " +
                "generic clone placeholders";
            return false;
        }

        GameObject lockedMarker = FindUniqueCloneChild(
            clone,
            sourceExfil.InfilMarker == null
                ? string.Empty
                : sourceExfil.InfilMarker.name);
        GameObject availableMarker = FindUniqueCloneChild(
            clone,
            sourceExfil.ExfilMarker == null
                ? string.Empty
                : sourceExfil.ExfilMarker.name);
        if (lockedMarker == null || availableMarker == null)
        {
            error = "the cloned PVE exfil marker references are absent or ambiguous";
            return false;
        }

        exfil.unlockingKey = sourceExfil.unlockingKey ?? string.Empty;
        exfil.notificationCooldown = sourceExfil.notificationCooldown;
        exfil._notifT = 0f;
        exfil.isHelicopter = sourceExfil.isHelicopter;
        exfil.InfiltrationAnimationPrefab = sourceExfil.InfiltrationAnimationPrefab;
        exfil.exfilName = sourceExfil.exfilName ?? string.Empty;
        exfil.ExfilAnimationName = sourceExfil.ExfilAnimationName ?? string.Empty;
        exfil.exfilSpawned = false;
        exfil.InfilMarker = lockedMarker;
        exfil.ExfilMarker = availableMarker;
        exfil._occupants = new Il2CppSystem.Collections.Generic.HashSet<int>();
        exfil.NetworkPlayersInExfil = 0;
        exfil.NetworkcanExtract = false;
        exfil.linkedInfils = new Il2CppSystem.Collections.Generic.List<string>();
        if (sourceExfil.linkedInfils != null)
        {
            for (int index = 0; index < sourceExfil.linkedInfils.Count; index++)
                exfil.linkedInfils.Add(sourceExfil.linkedInfils[index]);
        }

        raid.enabled = false;
        raid.infiltrationManager = pve;
        raid.EXTRACT_TIMER = sourceRaid.EXTRACT_TIMER;
        raid.objectives = new Il2CppReferenceArray<ObjectiveSetter>(0);
        raid.missionAssets = new Il2CppSystem.Collections.Generic.List<VehicleHealth>();
        raid.standardAI = new Il2CppReferenceArray<GameObject>(0);
        raid.customAI = new Il2CppSystem.Collections.Generic.List<GameObject>();
        raid.hvtSpawnPoints = new Il2CppReferenceArray<GameObject>(0);
        raid.hvtAI = new Il2CppReferenceArray<GameObject>(0);
        raid.staticVehicleSpawnPoints = new Il2CppReferenceArray<GameObject>(0);
        raid.staticVehicleAI = new Il2CppReferenceArray<GameObject>(0);
        raid.Reinforcements = new Il2CppReferenceArray<aiReinforcement>(0);
        raid.prohibitedWeapons = new Il2CppReferenceArray<PuppetWeapon>(0);
        raid.mapSpecificWeapons = new Il2CppReferenceArray<PuppetWeapon>(0);
        raid.IED_locations = new Il2CppReferenceArray<Transform>(0);
        raid.botSpawnPoints = new Il2CppSystem.Collections.Generic.List<GameObject>();
        raid.allHelicopters = new Il2CppSystem.Collections.Generic.List<HelicopterV2>();
        raid.objectiveObjects = new Il2CppSystem.Collections.Generic.List<ObjectiveObject>();
        raid.ImportantObjectiveObjects =
            new Il2CppSystem.Collections.Generic.List<ObjectiveObject>();
        raid.spawnVehicleAI = false;
        raid.hasReinforcements = false;
        raid.timedBackup = false;
        raid.hasIEDs = false;
        raid.hasNotified = false;
        raid.hasNotifiedEnemiesDead = false;
        raid.exfilZones = new Il2CppSystem.Collections.Generic.List<ExfilZone>();
        raid.exfilZones.Add(exfil);

        cloneBehaviours = clone.GetComponentsInChildren<NetworkBehaviour>(true);
        if (cloneBehaviours == null || cloneBehaviours.Length != 3 ||
            cloneBehaviours[0] != pve || cloneBehaviours[1] != exfil ||
            cloneBehaviours[2] != raid)
        {
            error = "the repaired PVE clone did not restore the exact network " +
                "component order";
            return false;
        }
        return true;
    }

    private static GameObject FindUniqueCloneChild(GameObject clone, string name)
    {
        if (clone == null || string.IsNullOrEmpty(name))
            return null;
        GameObject match = null;
        Transform[] transforms = clone.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];
            if (candidate == null || candidate.gameObject == clone ||
                !string.Equals(candidate.name, name, StringComparison.Ordinal))
            {
                continue;
            }
            if (match != null)
                return null;
            match = candidate.gameObject;
        }
        return match;
    }

    private void UnspawnStandalonePeerGameModeClone(
        ActiveMapOperation operation,
        GameObject spawned)
    {
        if (operation == null || spawned == null)
            return;
        if (spawned == operation.BootstrapPrefabRoot)
        {
            // Host mode reuses the operation-owned template. The outer teardown
            // owns its destruction after NetworkServer.UnSpawn returns.
            return;
        }
        int instanceId = spawned.GetInstanceID();
        if (!operation.PeerGameModeClones.Remove(instanceId))
        {
            log.LogError("Mirror attempted to unspawn a game-mode root outside the " +
                "operation-owned clone ledger: instanceId=" + instanceId + ".");
            return;
        }
        if (operation.BootstrapRoot == spawned)
        {
            operation.BootstrapRoot = null;
            operation.BootstrapIdentity = null;
            operation.GameModeComponent = null;
        }
        Object.Destroy(spawned);
    }

    private static void ReleaseStandalonePeerGameModeSpawnHandler(
        ActiveMapOperation operation)
    {
        if (operation == null)
            return;
        uint assetId = operation.BootstrapAssetId;
        if (assetId != 0 && operation.PeerGameModeHandlerRegistered)
        {
            try
            {
                bool exactSpawn = NetworkClient.spawnHandlers != null &&
                    NetworkClient.spawnHandlers.TryGetValue(
                        assetId,
                        out SpawnHandlerDelegate registeredSpawn) &&
                    SamePeerSpawnHandler(
                        registeredSpawn,
                        operation.PeerGameModeSpawnHandler);
                bool exactUnspawn = NetworkClient.unspawnHandlers != null &&
                    NetworkClient.unspawnHandlers.TryGetValue(
                        assetId,
                        out UnSpawnDelegate registeredUnspawn) &&
                    SamePeerUnspawnHandler(
                        registeredUnspawn,
                        operation.PeerGameModeUnspawnHandler);
                if (exactSpawn && exactUnspawn)
                    NetworkClient.UnregisterSpawnHandler(assetId);
            }
            catch { }
        }
        foreach (OwnedPeerGameModeClone owned in operation.PeerGameModeClones.Values)
        {
            if (owned?.Root != null && owned.Root != operation.BootstrapRoot)
                Object.Destroy(owned.Root);
        }
        operation.PeerGameModeClones.Clear();
    }
}
