using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Mirror;
using OperatorModAPI;
using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

public sealed partial class CerberusNativeTabFix
{
    // This partial is deliberately additive and unreachable from the live
    // standalone launch path. It captures the exact-native construction and
    // clone invariants needed by the later RuntimeReady implementation without
    // registering a Mirror handler, installing a lifecycle hook, activating a
    // root, or spawning/adopting a network identity.
    private const int PeerNativeGameModeMaximumBehaviours = 64;
    private const int PeerNativeGameModeMaximumSyncObjects = 256;
    private const int PeerNativeGameModeMaximumTypeIdentityBytes = 256;
    private static readonly UTF8Encoding StrictPeerNativeGameModeUtf8 =
        new UTF8Encoding(false, true);

    private enum PeerNativeGameModeRootRole
    {
        Template = 1,
        PreflightProbe = 2,
        HostClone = 3,
        RemoteClone = 4
    }

    private sealed class PeerNativeGameModeGenerationDraft
    {
        public readonly ulong Epoch;
        public readonly ModdedOperationMode Mode;
        public readonly uint AssetId;
        public readonly int SceneHandle;
        public readonly PeerNativeGameModeRootDraft Template;
        public readonly PeerNativeGameModeRootDraft PreflightProbe;
        public readonly PeerNativeGameModeRootDraft HostClone;

        public PeerNativeGameModeGenerationDraft(
            ulong epoch,
            ModdedOperationMode mode,
            uint assetId,
            int sceneHandle,
            PeerNativeGameModeRootDraft template,
            PeerNativeGameModeRootDraft preflightProbe,
            PeerNativeGameModeRootDraft hostClone)
        {
            Epoch = epoch;
            Mode = mode;
            AssetId = assetId;
            SceneHandle = sceneHandle;
            Template = template ?? throw new ArgumentNullException(nameof(template));
            PreflightProbe = preflightProbe ??
                throw new ArgumentNullException(nameof(preflightProbe));
            HostClone = hostClone ?? throw new ArgumentNullException(nameof(hostClone));
        }
    }

    private sealed class PeerNativeGameModeRootDraft
    {
        public readonly PeerNativeGameModeRootRole Role;
        public readonly GameObject Root;
        public readonly NetworkIdentity Identity;
        public readonly global::GameMode Owner;
        public readonly PeerNativeNetworkBehaviourGraph ConstructorGraph;

        public PeerNativeGameModeRootDraft(
            PeerNativeGameModeRootRole role,
            GameObject root,
            NetworkIdentity identity,
            global::GameMode owner,
            PeerNativeNetworkBehaviourGraph constructorGraph)
        {
            Role = role;
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            ConstructorGraph = constructorGraph ??
                throw new ArgumentNullException(nameof(constructorGraph));
        }
    }

    private sealed class PeerNativeNetworkBehaviourGraph
    {
        public readonly IntPtr RootPointer;
        public readonly IntPtr IdentityPointer;
        public readonly IntPtr OwnerPointer;
        public readonly int RootInstanceId;
        public readonly string OwnerTypeIdentity;
        public readonly bool AuthoritativeArrayWasNull;
        public readonly int TotalSyncObjectCount;
        public readonly ReadOnlyCollection<PeerNativeNetworkBehaviourShape> Behaviours;

        public PeerNativeNetworkBehaviourGraph(
            IntPtr rootPointer,
            IntPtr identityPointer,
            IntPtr ownerPointer,
            int rootInstanceId,
            string ownerTypeIdentity,
            bool authoritativeArrayWasNull,
            int totalSyncObjectCount,
            IList<PeerNativeNetworkBehaviourShape> behaviours)
        {
            RootPointer = rootPointer;
            IdentityPointer = identityPointer;
            OwnerPointer = ownerPointer;
            RootInstanceId = rootInstanceId;
            OwnerTypeIdentity = ownerTypeIdentity ??
                throw new ArgumentNullException(nameof(ownerTypeIdentity));
            AuthoritativeArrayWasNull = authoritativeArrayWasNull;
            TotalSyncObjectCount = totalSyncObjectCount;
            Behaviours = new ReadOnlyCollection<PeerNativeNetworkBehaviourShape>(
                new List<PeerNativeNetworkBehaviourShape>(behaviours ??
                    throw new ArgumentNullException(nameof(behaviours))));
        }
    }

    private sealed class PeerNativeNetworkBehaviourShape
    {
        public readonly int EnumerationIndex;
        public readonly byte ConstructorComponentIndex;
        public readonly string TypeIdentity;
        public readonly string RelativeComponentKey;
        public readonly IntPtr BehaviourPointer;
        public readonly IntPtr SyncObjectsPointer;
        public readonly bool ConstructorIdentityWasNull;
        public readonly ReadOnlyCollection<PeerNativeSyncObjectShape> SyncObjects;

        public PeerNativeNetworkBehaviourShape(
            int enumerationIndex,
            byte constructorComponentIndex,
            string typeIdentity,
            string relativeComponentKey,
            IntPtr behaviourPointer,
            IntPtr syncObjectsPointer,
            bool constructorIdentityWasNull,
            IList<PeerNativeSyncObjectShape> syncObjects)
        {
            EnumerationIndex = enumerationIndex;
            ConstructorComponentIndex = constructorComponentIndex;
            TypeIdentity = typeIdentity ?? throw new ArgumentNullException(nameof(typeIdentity));
            RelativeComponentKey = relativeComponentKey ??
                throw new ArgumentNullException(nameof(relativeComponentKey));
            BehaviourPointer = behaviourPointer;
            SyncObjectsPointer = syncObjectsPointer;
            ConstructorIdentityWasNull = constructorIdentityWasNull;
            SyncObjects = new ReadOnlyCollection<PeerNativeSyncObjectShape>(
                new List<PeerNativeSyncObjectShape>(syncObjects ??
                    throw new ArgumentNullException(nameof(syncObjects))));
        }
    }

    private sealed class PeerNativeSyncObjectShape
    {
        public readonly int Index;
        public readonly string TypeIdentity;
        public readonly IntPtr Pointer;

        public PeerNativeSyncObjectShape(
            int index,
            string typeIdentity,
            IntPtr pointer)
        {
            Index = index;
            TypeIdentity = typeIdentity ?? throw new ArgumentNullException(nameof(typeIdentity));
            Pointer = pointer;
        }
    }

    private static bool TryBuildPeerNativeGameModeGenerationDraft(
        Scene scene,
        ModdedOperationMode mode,
        ulong epoch,
        uint assetId,
        out PeerNativeGameModeGenerationDraft generation,
        out string error)
    {
        generation = null;
        error = string.Empty;
        PeerNativeGameModeRootDraft template = null;
        PeerNativeGameModeRootDraft preflightProbe = null;
        PeerNativeGameModeRootDraft hostClone = null;
        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("the target package scene is not loaded");
            if (epoch == 0)
                throw new InvalidOperationException("the exact-native draft epoch is zero");
            uint expectedAssetId = mode == ModdedOperationMode.PlayerVersusEnvironment
                ? StandalonePveGameModeAssetId
                : mode == ModdedOperationMode.PlayerVersusPlayer
                    ? StandalonePvpGameModeAssetId
                    : 0;
            if (expectedAssetId == 0 || assetId != expectedAssetId)
            {
                throw new InvalidOperationException(
                    "the exact-native draft mode/asset identity is invalid");
            }

            if (!TryCreatePeerNativeGameModeTemplateDraft(
                    scene,
                    mode,
                    assetId,
                    out template,
                    out error))
            {
                return false;
            }
            if (!TryClonePeerNativeGameModeRootDraft(
                    scene,
                    mode,
                    assetId,
                    PeerNativeGameModeRootRole.PreflightProbe,
                    "MODDED_OPERATIONS_EXACT_NATIVE_PREFLIGHT_PROBE",
                    template,
                    Array.Empty<PeerNativeNetworkBehaviourGraph>(),
                    out preflightProbe,
                    out error))
            {
                return false;
            }
            if (!TryClonePeerNativeGameModeRootDraft(
                    scene,
                    mode,
                    assetId,
                    PeerNativeGameModeRootRole.HostClone,
                    "MODDED_OPERATIONS_EXACT_NATIVE_HOST_CLONE",
                    template,
                    new[] { preflightProbe.ConstructorGraph },
                    out hostClone,
                    out error))
            {
                return false;
            }

            generation = new PeerNativeGameModeGenerationDraft(
                epoch,
                mode,
                assetId,
                scene.handle,
                template,
                preflightProbe,
                hostClone);
            template = null;
            preflightProbe = null;
            hostClone = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
        finally
        {
            DestroyPeerNativeGameModeDraftRoot(hostClone);
            DestroyPeerNativeGameModeDraftRoot(preflightProbe);
            DestroyPeerNativeGameModeDraftRoot(template);
        }
    }

    private static bool TryCreatePeerNativeGameModeTemplateDraft(
        Scene scene,
        ModdedOperationMode mode,
        uint assetId,
        out PeerNativeGameModeRootDraft template,
        out string error)
    {
        template = null;
        error = string.Empty;
        GameObject root = null;
        try
        {
            root = new GameObject("MODDED_OPERATIONS_EXACT_NATIVE_TEMPLATE");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            NetworkIdentity identity = root.AddComponent<NetworkIdentity>();
            global::GameMode owner = mode ==
                    ModdedOperationMode.PlayerVersusEnvironment
                ? root.AddComponent<InfiltrationManager>()
                : root.AddComponent<PvpGameode>();
            identity.assetId = assetId;

            if (!TryCapturePeerNativeConstructorGraph(
                    root,
                    identity,
                    owner,
                    mode,
                    scene.handle,
                    assetId,
                    out PeerNativeNetworkBehaviourGraph graph,
                    out error))
            {
                return false;
            }
            template = new PeerNativeGameModeRootDraft(
                PeerNativeGameModeRootRole.Template,
                root,
                identity,
                owner,
                graph);
            root = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
        finally
        {
            if (root != null)
                Object.Destroy(root);
        }
    }

    private static bool TryClonePeerNativeGameModeRootDraft(
        Scene scene,
        ModdedOperationMode mode,
        uint assetId,
        PeerNativeGameModeRootRole role,
        string rootName,
        PeerNativeGameModeRootDraft source,
        IReadOnlyCollection<PeerNativeNetworkBehaviourGraph> additionalForbiddenGraphs,
        out PeerNativeGameModeRootDraft clone,
        out string error)
    {
        clone = null;
        error = string.Empty;
        GameObject root = null;
        try
        {
            if (source == null || source.Root == null || source.Root.activeSelf ||
                role == PeerNativeGameModeRootRole.Template ||
                string.IsNullOrEmpty(rootName))
            {
                throw new InvalidOperationException(
                    "the exact-native clone draft request is invalid");
            }
            root = Object.Instantiate(source.Root);
            root.name = rootName;
            if (root.activeSelf)
            {
                throw new InvalidOperationException(
                    "Unity returned an active exact-native clone draft");
            }
            SceneManager.MoveGameObjectToScene(root, scene);
            NetworkIdentity identity = root.GetComponent<NetworkIdentity>();
            global::GameMode owner = root.GetComponent<global::GameMode>();
            if (!TryCapturePeerNativeConstructorGraph(
                    root,
                    identity,
                    owner,
                    mode,
                    scene.handle,
                    assetId,
                    out PeerNativeNetworkBehaviourGraph graph,
                    out error))
            {
                return false;
            }
            if (!TryValidatePeerNativeCloneNonAliasing(
                    source.ConstructorGraph,
                    graph,
                    additionalForbiddenGraphs,
                    out error))
            {
                return false;
            }
            clone = new PeerNativeGameModeRootDraft(
                role,
                root,
                identity,
                owner,
                graph);
            root = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
        finally
        {
            if (root != null)
                Object.Destroy(root);
        }
    }

    private static bool TryCapturePeerNativeConstructorGraph(
        GameObject root,
        NetworkIdentity identity,
        global::GameMode owner,
        ModdedOperationMode mode,
        int expectedSceneHandle,
        uint expectedAssetId,
        out PeerNativeNetworkBehaviourGraph graph,
        out string error)
    {
        graph = null;
        error = string.Empty;
        try
        {
            if (root == null || identity == null || owner == null ||
                root.activeSelf || root.activeInHierarchy ||
                root.transform.parent != null || root.scene.handle != expectedSceneHandle ||
                identity.gameObject != root || owner.gameObject != root ||
                identity.Pointer == IntPtr.Zero || owner.Pointer == IntPtr.Zero ||
                root.Pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "the exact-native constructor root/scene/component closure is invalid");
            }
            if (identity.assetId != expectedAssetId || identity.netId != 0 ||
                identity.sceneId != 0 || identity.isServer || identity.isClient)
            {
                throw new InvalidOperationException(
                    "the exact-native constructor NetworkIdentity is already live or drifted");
            }
            if (!IsExactPeerNativeGameModeOwner(owner, mode))
            {
                throw new InvalidOperationException(
                    "the GameMode owner is not the exact native mode type");
            }

            NetworkIdentity[] identities =
                root.GetComponentsInChildren<NetworkIdentity>(true);
            global::GameMode[] owners =
                root.GetComponentsInChildren<global::GameMode>(true);
            if (identities == null || identities.Length != 1 || identities[0] != identity ||
                owners == null || owners.Length != 1 || owners[0] != owner)
            {
                throw new InvalidOperationException(
                    "the exact-native draft does not have one root identity and one owner");
            }
            if (mode == ModdedOperationMode.PlayerVersusEnvironment)
            {
                InfiltrationManager[] pveOwners =
                    root.GetComponentsInChildren<InfiltrationManager>(true);
                PvpGameode[] pvpOwners =
                    root.GetComponentsInChildren<PvpGameode>(true);
                if (pveOwners.Length != 1 || pveOwners[0] != owner ||
                    pvpOwners.Length != 0)
                {
                    throw new InvalidOperationException(
                        "the PVE exact-native owner component closure drifted");
                }
            }
            else if (mode == ModdedOperationMode.PlayerVersusPlayer)
            {
                PvpGameode[] pvpOwners =
                    root.GetComponentsInChildren<PvpGameode>(true);
                InfiltrationManager[] pveOwners =
                    root.GetComponentsInChildren<InfiltrationManager>(true);
                if (pvpOwners.Length != 1 || pvpOwners[0] != owner ||
                    pveOwners.Length != 0)
                {
                    throw new InvalidOperationException(
                        "the PVP exact-native owner component closure drifted");
                }
            }
            else
            {
                throw new InvalidOperationException("the exact-native mode is unsupported");
            }

            var authoritative = identity.NetworkBehaviours;
            bool authoritativeArrayWasNull =
                authoritative == null || authoritative.Pointer == IntPtr.Zero;
            if (!authoritativeArrayWasNull)
            {
                throw new InvalidOperationException(
                    "the constructor graph was captured after NetworkIdentity initialization");
            }

            NetworkBehaviour[] behaviours =
                root.GetComponentsInChildren<NetworkBehaviour>(true);
            if (behaviours == null || behaviours.Length == 0 ||
                behaviours.Length > PeerNativeGameModeMaximumBehaviours)
            {
                throw new InvalidOperationException(
                    "the constructor NetworkBehaviour count is outside the bounded contract");
            }

            var behaviourPointers = new HashSet<IntPtr>();
            var listPointers = new HashSet<IntPtr>();
            var syncObjectPointers = new HashSet<IntPtr>();
            var shapes = new List<PeerNativeNetworkBehaviourShape>(behaviours.Length);
            int totalSyncObjects = 0;
            int ownerMatches = 0;
            for (int index = 0; index < behaviours.Length; index++)
            {
                NetworkBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.Pointer == IntPtr.Zero ||
                    !behaviourPointers.Add(behaviour.Pointer))
                {
                    throw new InvalidOperationException(
                        "the constructor graph contains a null/duplicate NetworkBehaviour");
                }
                if (behaviour == owner)
                    ownerMatches++;
                if (behaviour.netIdentity != null || behaviour.ComponentIndex != 0)
                {
                    throw new InvalidOperationException(
                        "a constructor NetworkBehaviour was already bound or indexed");
                }
                if (behaviour.syncObjects == null ||
                    behaviour.syncObjects.Pointer == IntPtr.Zero ||
                    !listPointers.Add(behaviour.syncObjects.Pointer))
                {
                    throw new InvalidOperationException(
                        "a constructor-owned syncObjects list is null or aliased");
                }
                int syncCount = behaviour.syncObjects.Count;
                if (syncCount < 0 ||
                    totalSyncObjects > PeerNativeGameModeMaximumSyncObjects - syncCount)
                {
                    throw new InvalidOperationException(
                        "the constructor SyncObject count is outside the bounded contract");
                }
                totalSyncObjects += syncCount;
                var syncShapes = new List<PeerNativeSyncObjectShape>(syncCount);
                for (int syncIndex = 0; syncIndex < syncCount; syncIndex++)
                {
                    SyncObject syncObject = behaviour.syncObjects[syncIndex];
                    if (syncObject == null || syncObject.Pointer == IntPtr.Zero ||
                        !syncObjectPointers.Add(syncObject.Pointer))
                    {
                        throw new InvalidOperationException(
                            "a constructor-owned SyncObject is null or aliased");
                    }
                    syncShapes.Add(new PeerNativeSyncObjectShape(
                        syncIndex,
                        GetPeerNativeExactTypeIdentity(syncObject.GetType()),
                        syncObject.Pointer));
                }
                shapes.Add(new PeerNativeNetworkBehaviourShape(
                    index,
                    behaviour.ComponentIndex,
                    GetPeerNativeExactTypeIdentity(behaviour.GetType()),
                    GetPeerNativeRelativeComponentKey(root.transform, behaviour),
                    behaviour.Pointer,
                    behaviour.syncObjects.Pointer,
                    behaviour.netIdentity == null,
                    syncShapes));
            }
            if (ownerMatches != 1)
            {
                throw new InvalidOperationException(
                    "the exact native owner is not one authoritative constructor behaviour");
            }

            graph = new PeerNativeNetworkBehaviourGraph(
                root.Pointer,
                identity.Pointer,
                owner.Pointer,
                root.GetInstanceID(),
                GetPeerNativeExactTypeIdentity(owner.GetType()),
                authoritativeArrayWasNull,
                totalSyncObjects,
                shapes);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static bool TryValidatePeerNativeCloneNonAliasing(
        PeerNativeNetworkBehaviourGraph source,
        PeerNativeNetworkBehaviourGraph candidate,
        IReadOnlyCollection<PeerNativeNetworkBehaviourGraph> additionalForbiddenGraphs,
        out string error)
    {
        error = string.Empty;
        try
        {
            if (source == null || candidate == null ||
                source.Behaviours == null || candidate.Behaviours == null ||
                source.Behaviours.Count != candidate.Behaviours.Count ||
                source.TotalSyncObjectCount != candidate.TotalSyncObjectCount ||
                !source.AuthoritativeArrayWasNull ||
                !candidate.AuthoritativeArrayWasNull ||
                !string.Equals(
                    source.OwnerTypeIdentity,
                    candidate.OwnerTypeIdentity,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "the exact-native clone constructor shape differs from its template");
            }

            var forbiddenPointers = new HashSet<IntPtr>();
            AddPeerNativeGraphPointers(source, forbiddenPointers);
            if (additionalForbiddenGraphs != null)
            {
                foreach (PeerNativeNetworkBehaviourGraph forbidden in
                         additionalForbiddenGraphs)
                {
                    if (forbidden == null)
                    {
                        throw new InvalidOperationException(
                            "the exact-native clone forbidden graph set contains null");
                    }
                    AddPeerNativeGraphPointers(forbidden, forbiddenPointers);
                }
            }
            RequireFreshPeerNativePointer(candidate.RootPointer, forbiddenPointers, "root");
            RequireFreshPeerNativePointer(
                candidate.IdentityPointer,
                forbiddenPointers,
                "NetworkIdentity");
            RequireFreshPeerNativePointer(candidate.OwnerPointer, forbiddenPointers, "owner");
            if (candidate.RootInstanceId == source.RootInstanceId)
            {
                throw new InvalidOperationException(
                    "the exact-native clone reused the template Unity instance ID");
            }

            for (int index = 0; index < source.Behaviours.Count; index++)
            {
                PeerNativeNetworkBehaviourShape expected = source.Behaviours[index];
                PeerNativeNetworkBehaviourShape observed = candidate.Behaviours[index];
                if (expected.EnumerationIndex != index || observed.EnumerationIndex != index ||
                    expected.ConstructorComponentIndex != 0 ||
                    observed.ConstructorComponentIndex != 0 ||
                    !expected.ConstructorIdentityWasNull ||
                    !observed.ConstructorIdentityWasNull ||
                    !string.Equals(expected.TypeIdentity, observed.TypeIdentity,
                        StringComparison.Ordinal) ||
                    !string.Equals(expected.RelativeComponentKey,
                        observed.RelativeComponentKey,
                        StringComparison.Ordinal) ||
                    expected.SyncObjects.Count != observed.SyncObjects.Count)
                {
                    throw new InvalidOperationException(
                        "the exact-native clone behaviour order/type/key shape drifted");
                }
                RequireFreshPeerNativePointer(
                    observed.BehaviourPointer,
                    forbiddenPointers,
                    "NetworkBehaviour");
                RequireFreshPeerNativePointer(
                    observed.SyncObjectsPointer,
                    forbiddenPointers,
                    "syncObjects list");
                for (int syncIndex = 0;
                     syncIndex < expected.SyncObjects.Count;
                     syncIndex++)
                {
                    PeerNativeSyncObjectShape expectedSync = expected.SyncObjects[syncIndex];
                    PeerNativeSyncObjectShape observedSync = observed.SyncObjects[syncIndex];
                    if (expectedSync.Index != syncIndex || observedSync.Index != syncIndex ||
                        !string.Equals(
                            expectedSync.TypeIdentity,
                            observedSync.TypeIdentity,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "the exact-native clone SyncObject order/type shape drifted");
                    }
                    RequireFreshPeerNativePointer(
                        observedSync.Pointer,
                        forbiddenPointers,
                        "SyncObject");
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static bool IsExactPeerNativeGameModeOwner(
        global::GameMode owner,
        ModdedOperationMode mode)
    {
        if (owner == null || owner.Pointer == IntPtr.Zero)
            return false;
        Type observed = owner.GetType();
        return mode == ModdedOperationMode.PlayerVersusEnvironment
            ? observed == typeof(InfiltrationManager)
            : mode == ModdedOperationMode.PlayerVersusPlayer &&
              observed == typeof(PvpGameode);
    }

    private static string GetPeerNativeExactTypeIdentity(Type type)
    {
        if (type == null)
            throw new InvalidOperationException("an exact native type is null");
        string assemblyName = type.Assembly?.GetName()?.Name ?? string.Empty;
        string fullName = type.FullName ?? string.Empty;
        if (assemblyName.Length == 0 || fullName.Length == 0)
        {
            throw new InvalidOperationException(
                "an exact native type has no assembly/full-name identity");
        }
        string identity = assemblyName + ":" + fullName;
        int byteCount = StrictPeerNativeGameModeUtf8.GetByteCount(identity);
        if (byteCount == 0 || byteCount > PeerNativeGameModeMaximumTypeIdentityBytes)
        {
            throw new InvalidOperationException(
                "an exact native type identity is outside the bounded UTF-8 contract");
        }
        return identity;
    }

    private static string GetPeerNativeRelativeComponentKey(
        Transform root,
        Component component)
    {
        if (root == null || component == null || component.transform == null)
        {
            throw new InvalidOperationException(
                "the exact-native relative component key is incomplete");
        }
        var segments = new List<string>();
        Transform current = component.transform;
        while (current != root)
        {
            Transform parent = current.parent;
            string name = current.name ?? string.Empty;
            if (parent == null || name.Length == 0)
            {
                throw new InvalidOperationException(
                    "a constructor behaviour is outside the exact-native root");
            }
            int sameNameOrdinal = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform sibling = parent.GetChild(index);
                if (sibling == current)
                    break;
                if (sibling != null && string.Equals(
                        sibling.name,
                        name,
                        StringComparison.Ordinal))
                {
                    sameNameOrdinal++;
                }
            }
            segments.Add(name + "\0" + sameNameOrdinal);
            current = parent;
        }
        segments.Reverse();
        string typeIdentity = GetPeerNativeExactTypeIdentity(component.GetType());
        int typeOrdinal = 0;
        foreach (Component sibling in component.gameObject.GetComponents<Component>())
        {
            if (sibling == null || sibling.GetType() != component.GetType())
                continue;
            if (sibling == component)
                break;
            typeOrdinal++;
        }
        string transformKey = segments.Count == 0
            ? "$root"
            : string.Join("\0/\0", segments);
        string key = transformKey + "\0" + typeIdentity + "\0" + typeOrdinal;
        int byteCount = StrictPeerNativeGameModeUtf8.GetByteCount(key);
        if (byteCount == 0 || byteCount > PeerNativeGameModeMaximumTypeIdentityBytes)
        {
            throw new InvalidOperationException(
                "an exact-native relative component key is outside the UTF-8 bound");
        }
        return key;
    }

    private static void AddPeerNativeGraphPointers(
        PeerNativeNetworkBehaviourGraph graph,
        HashSet<IntPtr> pointers)
    {
        if (graph == null || pointers == null)
            throw new ArgumentNullException(graph == null ? nameof(graph) : nameof(pointers));
        AddPeerNativeGraphPointer(graph.RootPointer, pointers);
        AddPeerNativeGraphPointer(graph.IdentityPointer, pointers);
        AddPeerNativeGraphPointer(graph.OwnerPointer, pointers);
        foreach (PeerNativeNetworkBehaviourShape behaviour in graph.Behaviours)
        {
            AddPeerNativeGraphPointer(behaviour.BehaviourPointer, pointers);
            AddPeerNativeGraphPointer(behaviour.SyncObjectsPointer, pointers);
            foreach (PeerNativeSyncObjectShape syncObject in behaviour.SyncObjects)
                AddPeerNativeGraphPointer(syncObject.Pointer, pointers);
        }
    }

    private static void AddPeerNativeGraphPointer(
        IntPtr pointer,
        HashSet<IntPtr> pointers)
    {
        if (pointer == IntPtr.Zero)
            throw new InvalidOperationException("an exact-native graph pointer is zero");
        pointers.Add(pointer);
    }

    private static void RequireFreshPeerNativePointer(
        IntPtr pointer,
        HashSet<IntPtr> forbiddenPointers,
        string label)
    {
        if (pointer == IntPtr.Zero || forbiddenPointers == null ||
            forbiddenPointers.Contains(pointer))
        {
            throw new InvalidOperationException(
                "the exact-native clone " + label + " pointer is null or aliased");
        }
    }

    private static void DestroyPeerNativeGameModeDraftRoot(
        PeerNativeGameModeRootDraft draft)
    {
        if (draft?.Root == null)
            return;
        try
        {
            Object.Destroy(draft.Root);
        }
        catch
        {
            // The additive draft has no handler, hook, registry, or network
            // ownership. A future live generation must use the stricter reverse
            // teardown ledger instead of this private construction-failure path.
        }
    }
}
