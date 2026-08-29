using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static class Program
{
    private static int Main()
    {
        try
        {
            string repository = FindRepositoryRoot();
            string sourceDirectory = Path.Combine(
                repository,
                "src",
                "OperatorModdedOperations");
            string nativePath = Path.Combine(
                sourceDirectory,
                "CerberusNativeTabFix.PeerNativeGameMode.cs");
            string mainPath = Path.Combine(
                sourceDirectory,
                "CerberusNativeTabFix.cs");
            string protocolPath = Path.Combine(
                sourceDirectory,
                "CerberusNativeTabFix.PvpPeerAgreement.cs");
            string native = File.ReadAllText(nativePath);
            string main = File.ReadAllText(mainPath);
            string protocol = File.ReadAllText(protocolPath);

            Require(native.Length > 1000, "exact-native partial is empty/truncated");
            RequireAll(native, new[]
            {
                "PeerNativeGameModeGenerationDraft",
                "PeerNativeGameModeRootRole.Template",
                "PeerNativeGameModeRootRole.PreflightProbe",
                "PeerNativeGameModeRootRole.HostClone",
                "RemoteClone = 4",
                "root.AddComponent<InfiltrationManager>()",
                "root.AddComponent<PvpGameode>()",
                "Object.Instantiate(source.Root)",
                "TryCapturePeerNativeConstructorGraph",
                "TryValidatePeerNativeCloneNonAliasing",
                "GetComponentsInChildren<NetworkBehaviour>(true)",
                "GetComponentsInChildren<NetworkIdentity>(true)",
                "observed == typeof(InfiltrationManager)",
                "observed == typeof(PvpGameode)",
                "authoritative == null || authoritative.Pointer == IntPtr.Zero",
                "behaviour.netIdentity != null || behaviour.ComponentIndex != 0",
                "behaviour.syncObjects == null",
                "!listPointers.Add(behaviour.syncObjects.Pointer)",
                "!syncObjectPointers.Add(syncObject.Pointer)",
                "new[] { preflightProbe.ConstructorGraph }"
            });

            ForbidAll(native, new[]
            {
                "AddComponent<StandalonePveGameMode>",
                "AddComponent<StandalonePvpGameMode>",
                "NetworkClient.RegisterPrefab",
                "NetworkClient.RegisterSpawnHandler",
                "NetworkClient.UnregisterPrefab",
                "NetworkClient.UnregisterSpawnHandler",
                "NetworkServer.Spawn",
                "NetworkServer.UnSpawn",
                "SetActive(true)",
                "InitializeNetworkBehaviours(",
                "Harmony",
                "PvpAgreementMessageKind",
                "PeerBarrierMessageKind"
            });
            Require(!native.Contains(
                    "Action<GameObject, global::GameMode>",
                    StringComparison.Ordinal),
                "the constructor builder accepts an external mutation callback");
            Require(!Regex.IsMatch(
                    native,
                    @"\.syncObjects\s*=(?!=)",
                    RegexOptions.CultureInvariant),
                "exact-native partial assigns/repairs syncObjects");

            Require(main.Contains(
                    "OperatorApi.RegisterIl2CppType(typeof(StandalonePveGameMode))",
                    StringComparison.Ordinal),
                "additive milestone changed the live legacy PVE type registration");
            Require(main.Contains(
                    "OperatorApi.RegisterIl2CppType(typeof(StandalonePvpGameMode))",
                    StringComparison.Ordinal),
                "additive milestone changed the live legacy PVP type registration");
            Require(main.Contains(
                    "TryInitializeStandaloneBootstrapSyncObjects(",
                    StringComparison.Ordinal),
                "additive milestone removed the live legacy path prematurely");

            Require(protocol.Contains(
                    "scene-contract-digest-v2;",
                    StringComparison.Ordinal) &&
                protocol.Contains(
                    "runtime-clone-contract-v1;",
                    StringComparison.Ordinal),
                "live peer protocol does not advertise the completed scene/clone contracts");
            Require(Regex.IsMatch(
                    protocol,
                    @"PveAgreementV6RuntimeContractComplete\s*=\s*true\s*;",
                    RegexOptions.CultureInvariant),
                "BepInEx PVE runtime candidate was not enabled");
            Require(Regex.IsMatch(
                    protocol,
                    @"PvpAgreementV6RuntimeContractComplete\s*=\s*true\s*;",
                    RegexOptions.CultureInvariant),
                "BepInEx PVP runtime candidate was not enabled for physical host/remote proof");

            foreach (string path in Directory.EnumerateFiles(
                         sourceDirectory,
                         "*.cs",
                         SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(path, nativePath, StringComparison.OrdinalIgnoreCase))
                    continue;
                string other = File.ReadAllText(path);
                Require(!other.Contains(
                        "TryBuildPeerNativeGameModeGenerationDraft",
                        StringComparison.Ordinal),
                    "the additive builder is reachable from " + Path.GetFileName(path));
                Require(!other.Contains(
                        "PeerNativeGameModeGenerationDraft",
                        StringComparison.Ordinal),
                    "the additive generation DTO escaped into " + Path.GetFileName(path));
            }

            Console.WriteLine("Peer exact-native additive boundary tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static string FindRepositoryRoot()
    {
        var candidates = new List<string>
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };
        foreach (string candidate in candidates)
        {
            DirectoryInfo current = new DirectoryInfo(candidate);
            while (current != null)
            {
                string marker = Path.Combine(
                    current.FullName,
                    "src",
                    "OperatorModdedOperations",
                    "OperatorModdedOperations.csproj");
                if (File.Exists(marker))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("operator-modded-operations root not found");
    }

    private static void RequireAll(string source, IEnumerable<string> required)
    {
        foreach (string value in required)
        {
            Require(source.Contains(value, StringComparison.Ordinal),
                "required exact-native source token is missing: " + value);
        }
    }

    private static void ForbidAll(string source, IEnumerable<string> forbidden)
    {
        foreach (string value in forbidden)
        {
            Require(!source.Contains(value, StringComparison.Ordinal),
                "forbidden additive source token is present: " + value);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
