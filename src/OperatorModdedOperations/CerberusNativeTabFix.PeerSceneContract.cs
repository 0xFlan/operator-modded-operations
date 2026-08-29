using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mirror;
using OperatorModAPI;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class CerberusNativeTabFix
{
    private const ushort PeerSceneContractSchemaVersion = 2;
    private const string PeerSceneContractDomain =
        "operator-peer-scene-contract-v2";
    private const string PeerTemplateShapeDomain =
        "operator-peer-game-mode-template-v1";
    private const int PeerSceneContractMaximumPlayerSpawns = 64;
    private const int PeerSceneContractMaximumEnemyPopulation = 100;
    private const int PeerSceneContractMaximumEnemyCandidates = 512;
    private const float PeerStandingClearanceRadiusMetres = 0.30f;
    private const float PeerStandingClearanceHeightMetres = 1.80f;
    private const float PeerStandingClearanceSkinMetres = 0.02f;
    private const int PeerStandingClearanceMaximumOverlaps = 128;
    private static readonly UTF8Encoding StrictPeerUtf8 =
        new UTF8Encoding(false, true);

    private sealed class PeerSceneContractSnapshot
    {
        public string IdentityDigest;
        public ulong Epoch;
        public string PackageId;
        public string PackageVersion;
        public string PackageContentId;
        public string MapId;
        public string OperationId;
        public int Mode;
        public string SpawnSetId;
        public string VariantId;
        public string DeclaredScenePath;
        public string ActualScenePath;
        public PeerCompanionReceipt Companion;
        public PeerTerrainReceipt Terrain;
        public PeerNavigationReceipt Navigation;
        public readonly List<PeerGroundReceipt> Ground = new();
        public readonly List<PeerPlayerSpawnReceipt> PlayerSpawns = new();
        public PeerPveCapacityReceipt PveCapacity;
        public PeerExfilReceipt Exfil;
        public PeerHandlerReceipt Handler;
        public string TemplateShapeDigest;
    }

    private sealed class PeerCompanionReceipt
    {
        public bool Declared;
        public int ReadyCount;
        public int FailureCount;
    }

    private sealed class PeerTerrainReceipt
    {
        public bool Applicable;
        public string RootKey;
        public PeerVector3Int SizeTenThousandths;
        public PeerVector3Int RootPositionMillimetres;
        public PeerQuaternionInt RootRotationHundredThousandths;
        public PeerVector3Int RootScaleTenThousandths;
        public int HeightmapResolution;
        public int AlphamapResolution;
        public int HolesResolution;
        public PeerVector3Int HeightmapScaleTenThousandths;
    }

    private sealed class PeerNavigationReceipt
    {
        public bool Applicable;
        public string OwnerKey;
        public string GraphType;
        public string GraphName;
        public uint NodeCount;
        public string RvoOwnerKey;
        public PeerVector3Int GraphBoundsCenterMillimetres;
        public PeerVector3Int GraphBoundsSizeMillimetres;
        public bool IsGridGraph;
        public int GridWidth;
        public int GridDepth;
        public PeerVector3Int GridCenterMillimetres;
        public PeerQuaternionInt GridRotationHundredThousandths;
        public int GridNodeSizeTenThousandths;
        public int GridMaxStepTenThousandths;
        public bool GridMaxStepUsesSlope;
        public int GridMaxSlopeTenThousandths;
        public int GridErodeIterations;
        public int GridNeighbours;
        public bool GridCutCorners;
        public int CollisionType;
        public int CollisionDiameterTenThousandths;
        public int CollisionHeightTenThousandths;
        public int CollisionOffsetTenThousandths;
        public int CollisionRayDirection;
        public int CollisionMask;
        public int CollisionHeightMask;
        public int CollisionFromHeightTenThousandths;
        public bool CollisionThickRaycast;
        public int CollisionThickRaycastDiameterTenThousandths;
        public bool CollisionUnwalkableWhenNoGround;
        public bool CollisionUse2D;
        public bool CollisionCheck;
        public bool CollisionHeightCheck;
        public int RvoDesiredFps;
        public int RvoWorkerThreads;
        public bool RvoDoubleBuffering;
        public bool RvoHardCollisions;
        public int RvoSymmetryBiasHundredThousandths;
        public int RvoMovementPlane;
        public bool RvoUseNavmeshAsObstacle;
        public readonly List<PeerNavigationMarkerReceipt> Markers = new();
    }

    private sealed class PeerNavigationMarkerReceipt
    {
        public string MarkerKey;
        public string GraphKey;
        public PeerVector3Int MarkerPositionMillimetres;
        public PeerQuaternionInt MarkerRotationHundredThousandths;
        public PeerVector3Int NearestPositionMillimetres;
        public int HorizontalCorrectionMillimetres;
        public int VerticalCorrectionMillimetres;
    }

    private sealed class PeerGroundReceipt
    {
        public string MarkerKey;
        public string ColliderKey;
        public string ColliderType;
        public int Layer;
        public int GapMillimetres;
        public PeerVector3Int NormalTenThousandths;
        public string GeometryKind;
        public PeerVector3Int LocalCenterTenThousandths;
        public PeerVector3Int LocalSizeTenThousandths;
        public int RadiusTenThousandths;
        public int HeightTenThousandths;
        public int Direction;
        public PeerVector3Int TransformPositionMillimetres;
        public PeerQuaternionInt TransformRotationHundredThousandths;
        public PeerVector3Int TransformScaleTenThousandths;
        public string MeshName;
        public int MeshVertexCount;
        public int MeshSubMeshCount;
        public bool MeshConvex;
        public int ClearanceRadiusMillimetres;
        public int ClearanceHeightMillimetres;
    }

    private sealed class PeerPlayerSpawnReceipt
    {
        public int Index;
        public string MarkerKey;
        public PeerVector3Int PositionMillimetres;
        public PeerQuaternionInt RotationHundredThousandths;
        public int Team;
        public bool CanSpawnPlayer;
    }

    private sealed class PeerPveCapacityReceipt
    {
        public bool Applicable;
        public int Minimum;
        public int Maximum;
        public int Requested;
        public int AuthoredCount;
        public int NavigationCount;
        public readonly List<string> SafeMarkerKeys = new();
    }

    private sealed class PeerExfilReceipt
    {
        public bool Applicable;
        public string MarkerKey;
        public PeerVector3Int PositionMillimetres;
        public PeerQuaternionInt RotationHundredThousandths;
        public PeerVector3Int ScaleTenThousandths;
        public int Layer;
        public PeerVector3Int ColliderCenterTenThousandths;
        public PeerVector3Int ColliderSizeTenThousandths;
    }

    private sealed class PeerHandlerReceipt
    {
        public uint AssetId;
        public int Mode;
        public string ContractVersion;
        public bool OwnedSpawn;
        public bool OwnedUnspawn;
        public bool PrefabEntry;
    }

    private readonly struct PeerVector3Int
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public PeerVector3Int(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    private readonly struct PeerQuaternionInt
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        public readonly int W;

        public PeerQuaternionInt(int x, int y, int z, int w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    private sealed class PeerCanonicalBinaryWriter : IDisposable
    {
        private readonly MemoryStream stream = new MemoryStream(4096);

        public byte[] ToArray() => stream.ToArray();

        public void WriteBoolean(bool value) => stream.WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUInt16(ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        public void WriteUInt32(uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        public void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)(value >> 32));
            WriteUInt32((uint)value);
        }

        public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

        public void WriteString(string value)
        {
            byte[] bytes = StrictPeerUtf8.GetBytes(value ?? string.Empty);
            WriteUInt32(checked((uint)bytes.Length));
            stream.Write(bytes, 0, bytes.Length);
        }

        public void WriteHash(string lowercaseSha256)
        {
            if (!IsLowercasePvpSha256(lowercaseSha256))
                throw new InvalidDataException("canonical hash is not lowercase SHA-256");
            for (int index = 0; index < lowercaseSha256.Length; index += 2)
            {
                stream.WriteByte(byte.Parse(
                    lowercaseSha256.Substring(index, 2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture));
            }
        }

        public void WriteVector(PeerVector3Int value)
        {
            WriteInt32(value.X);
            WriteInt32(value.Y);
            WriteInt32(value.Z);
        }

        public void WriteQuaternion(PeerQuaternionInt value)
        {
            WriteInt32(value.X);
            WriteInt32(value.Y);
            WriteInt32(value.Z);
            WriteInt32(value.W);
        }

        public void Dispose() => stream.Dispose();
    }

    private static string ComputePeerSceneContractDigest(
        PeerSceneContractSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Epoch == 0 ||
            snapshot.Companion == null || snapshot.Terrain == null ||
            snapshot.Navigation == null || snapshot.PveCapacity == null ||
            snapshot.Exfil == null || snapshot.Handler == null ||
            !IsLowercasePvpSha256(snapshot.IdentityDigest) ||
            !IsLowercasePvpSha256(snapshot.TemplateShapeDigest))
        {
            throw new InvalidDataException("peer scene-contract snapshot is incomplete");
        }
        if (snapshot.PlayerSpawns.Count == 0 ||
            snapshot.PlayerSpawns.Count > PeerSceneContractMaximumPlayerSpawns ||
            snapshot.Ground.Count >
                PeerSceneContractMaximumPlayerSpawns +
                PeerSceneContractMaximumEnemyCandidates ||
            snapshot.PveCapacity.SafeMarkerKeys.Count >
                PeerSceneContractMaximumEnemyCandidates)
        {
            throw new InvalidDataException("peer scene-contract collection bound failed");
        }

        RequireUniqueOrderedPeerKeys(
            snapshot.Ground.Select(item => item.MarkerKey),
            "ground marker");
        RequireUniqueOrderedPeerKeys(
            snapshot.PveCapacity.SafeMarkerKeys,
            "safe PVE marker");
        using var writer = new PeerCanonicalBinaryWriter();
        writer.WriteString(PeerSceneContractDomain);
        writer.WriteUInt16(PeerSceneContractSchemaVersion);
        writer.WriteHash(snapshot.IdentityDigest);
        writer.WriteUInt64(snapshot.Epoch);
        writer.WriteString(snapshot.PackageId);
        writer.WriteString(snapshot.PackageVersion);
        writer.WriteString(snapshot.PackageContentId);
        writer.WriteString(snapshot.MapId);
        writer.WriteString(snapshot.OperationId);
        writer.WriteInt32(snapshot.Mode);
        writer.WriteString(snapshot.SpawnSetId);
        writer.WriteString(snapshot.VariantId);
        writer.WriteString(snapshot.DeclaredScenePath);
        writer.WriteString(snapshot.ActualScenePath);

        writer.WriteBoolean(snapshot.Companion.Declared);
        writer.WriteInt32(snapshot.Companion.ReadyCount);
        writer.WriteInt32(snapshot.Companion.FailureCount);

        writer.WriteBoolean(snapshot.Terrain.Applicable);
        if (snapshot.Terrain.Applicable)
        {
            writer.WriteString(snapshot.Terrain.RootKey);
            writer.WriteVector(snapshot.Terrain.SizeTenThousandths);
            writer.WriteVector(snapshot.Terrain.RootPositionMillimetres);
            writer.WriteQuaternion(
                snapshot.Terrain.RootRotationHundredThousandths);
            writer.WriteVector(snapshot.Terrain.RootScaleTenThousandths);
            writer.WriteInt32(snapshot.Terrain.HeightmapResolution);
            writer.WriteInt32(snapshot.Terrain.AlphamapResolution);
            writer.WriteInt32(snapshot.Terrain.HolesResolution);
            writer.WriteVector(snapshot.Terrain.HeightmapScaleTenThousandths);
        }

        writer.WriteUInt32(checked((uint)snapshot.Ground.Count));
        foreach (PeerGroundReceipt item in snapshot.Ground)
        {
            writer.WriteString(item.MarkerKey);
            writer.WriteString(item.ColliderKey);
            writer.WriteString(item.ColliderType);
            writer.WriteInt32(item.Layer);
            writer.WriteInt32(item.GapMillimetres);
            writer.WriteVector(item.NormalTenThousandths);
            writer.WriteString(item.GeometryKind);
            writer.WriteVector(item.LocalCenterTenThousandths);
            writer.WriteVector(item.LocalSizeTenThousandths);
            writer.WriteInt32(item.RadiusTenThousandths);
            writer.WriteInt32(item.HeightTenThousandths);
            writer.WriteInt32(item.Direction);
            writer.WriteVector(item.TransformPositionMillimetres);
            writer.WriteQuaternion(item.TransformRotationHundredThousandths);
            writer.WriteVector(item.TransformScaleTenThousandths);
            writer.WriteString(item.MeshName);
            writer.WriteInt32(item.MeshVertexCount);
            writer.WriteInt32(item.MeshSubMeshCount);
            writer.WriteBoolean(item.MeshConvex);
            writer.WriteInt32(item.ClearanceRadiusMillimetres);
            writer.WriteInt32(item.ClearanceHeightMillimetres);
        }

        writer.WriteBoolean(snapshot.Navigation.Applicable);
        if (snapshot.Navigation.Applicable)
        {
            writer.WriteString(snapshot.Navigation.OwnerKey);
            writer.WriteString(snapshot.Navigation.GraphType);
            writer.WriteString(snapshot.Navigation.GraphName);
            writer.WriteUInt32(snapshot.Navigation.NodeCount);
            writer.WriteString(snapshot.Navigation.RvoOwnerKey);
            writer.WriteVector(snapshot.Navigation.GraphBoundsCenterMillimetres);
            writer.WriteVector(snapshot.Navigation.GraphBoundsSizeMillimetres);
            writer.WriteBoolean(snapshot.Navigation.IsGridGraph);
            if (snapshot.Navigation.IsGridGraph)
            {
                writer.WriteInt32(snapshot.Navigation.GridWidth);
                writer.WriteInt32(snapshot.Navigation.GridDepth);
                writer.WriteVector(snapshot.Navigation.GridCenterMillimetres);
                writer.WriteQuaternion(
                    snapshot.Navigation.GridRotationHundredThousandths);
                writer.WriteInt32(snapshot.Navigation.GridNodeSizeTenThousandths);
                writer.WriteInt32(snapshot.Navigation.GridMaxStepTenThousandths);
                writer.WriteBoolean(snapshot.Navigation.GridMaxStepUsesSlope);
                writer.WriteInt32(snapshot.Navigation.GridMaxSlopeTenThousandths);
                writer.WriteInt32(snapshot.Navigation.GridErodeIterations);
                writer.WriteInt32(snapshot.Navigation.GridNeighbours);
                writer.WriteBoolean(snapshot.Navigation.GridCutCorners);
                writer.WriteInt32(snapshot.Navigation.CollisionType);
                writer.WriteInt32(
                    snapshot.Navigation.CollisionDiameterTenThousandths);
                writer.WriteInt32(
                    snapshot.Navigation.CollisionHeightTenThousandths);
                writer.WriteInt32(
                    snapshot.Navigation.CollisionOffsetTenThousandths);
                writer.WriteInt32(snapshot.Navigation.CollisionRayDirection);
                writer.WriteInt32(snapshot.Navigation.CollisionMask);
                writer.WriteInt32(snapshot.Navigation.CollisionHeightMask);
                writer.WriteInt32(
                    snapshot.Navigation.CollisionFromHeightTenThousandths);
                writer.WriteBoolean(snapshot.Navigation.CollisionThickRaycast);
                writer.WriteInt32(
                    snapshot.Navigation.CollisionThickRaycastDiameterTenThousandths);
                writer.WriteBoolean(
                    snapshot.Navigation.CollisionUnwalkableWhenNoGround);
                writer.WriteBoolean(snapshot.Navigation.CollisionUse2D);
                writer.WriteBoolean(snapshot.Navigation.CollisionCheck);
                writer.WriteBoolean(snapshot.Navigation.CollisionHeightCheck);
            }
            writer.WriteInt32(snapshot.Navigation.RvoDesiredFps);
            writer.WriteInt32(snapshot.Navigation.RvoWorkerThreads);
            writer.WriteBoolean(snapshot.Navigation.RvoDoubleBuffering);
            writer.WriteBoolean(snapshot.Navigation.RvoHardCollisions);
            writer.WriteInt32(
                snapshot.Navigation.RvoSymmetryBiasHundredThousandths);
            writer.WriteInt32(snapshot.Navigation.RvoMovementPlane);
            writer.WriteBoolean(snapshot.Navigation.RvoUseNavmeshAsObstacle);
            writer.WriteUInt32(checked((uint)snapshot.Navigation.Markers.Count));
            foreach (PeerNavigationMarkerReceipt marker in
                     snapshot.Navigation.Markers)
            {
                writer.WriteString(marker.MarkerKey);
                writer.WriteString(marker.GraphKey);
                writer.WriteVector(marker.MarkerPositionMillimetres);
                writer.WriteQuaternion(
                    marker.MarkerRotationHundredThousandths);
                writer.WriteVector(marker.NearestPositionMillimetres);
                writer.WriteInt32(marker.HorizontalCorrectionMillimetres);
                writer.WriteInt32(marker.VerticalCorrectionMillimetres);
            }
        }

        writer.WriteUInt32(checked((uint)snapshot.PlayerSpawns.Count));
        for (int index = 0; index < snapshot.PlayerSpawns.Count; index++)
        {
            PeerPlayerSpawnReceipt item = snapshot.PlayerSpawns[index];
            if (item.Index != index)
                throw new InvalidDataException("player-spawn index/order drifted");
            writer.WriteInt32(item.Index);
            writer.WriteString(item.MarkerKey);
            writer.WriteVector(item.PositionMillimetres);
            writer.WriteQuaternion(item.RotationHundredThousandths);
            writer.WriteInt32(item.Team);
            writer.WriteBoolean(item.CanSpawnPlayer);
        }

        writer.WriteBoolean(snapshot.PveCapacity.Applicable);
        if (snapshot.PveCapacity.Applicable)
        {
            writer.WriteInt32(snapshot.PveCapacity.Minimum);
            writer.WriteInt32(snapshot.PveCapacity.Maximum);
            writer.WriteInt32(snapshot.PveCapacity.Requested);
            writer.WriteInt32(snapshot.PveCapacity.AuthoredCount);
            writer.WriteInt32(snapshot.PveCapacity.NavigationCount);
            writer.WriteUInt32(checked((uint)snapshot.PveCapacity.SafeMarkerKeys.Count));
            foreach (string markerKey in snapshot.PveCapacity.SafeMarkerKeys)
                writer.WriteString(markerKey);
        }

        writer.WriteBoolean(snapshot.Exfil.Applicable);
        if (snapshot.Exfil.Applicable)
        {
            writer.WriteString(snapshot.Exfil.MarkerKey);
            writer.WriteVector(snapshot.Exfil.PositionMillimetres);
            writer.WriteQuaternion(snapshot.Exfil.RotationHundredThousandths);
            writer.WriteVector(snapshot.Exfil.ScaleTenThousandths);
            writer.WriteInt32(snapshot.Exfil.Layer);
            writer.WriteVector(snapshot.Exfil.ColliderCenterTenThousandths);
            writer.WriteVector(snapshot.Exfil.ColliderSizeTenThousandths);
        }

        writer.WriteUInt32(snapshot.Handler.AssetId);
        writer.WriteInt32(snapshot.Handler.Mode);
        writer.WriteString(snapshot.Handler.ContractVersion);
        writer.WriteBoolean(snapshot.Handler.OwnedSpawn);
        writer.WriteBoolean(snapshot.Handler.OwnedUnspawn);
        writer.WriteBoolean(snapshot.Handler.PrefabEntry);
        writer.WriteHash(snapshot.TemplateShapeDigest);

        using SHA256 sha = SHA256.Create();
        return ToLowerHex(sha.ComputeHash(writer.ToArray()));
    }

    private static void RequireUniqueOrderedPeerKeys(
        IEnumerable<string> keys,
        string label)
    {
        string prior = null;
        bool first = true;
        foreach (string key in keys)
        {
            if (string.IsNullOrEmpty(key) ||
                (!first && string.CompareOrdinal(prior, key) >= 0))
            {
                throw new InvalidDataException(
                    label + " keys are empty, duplicate, or not Ordinal ordered");
            }
            prior = key;
            first = false;
        }
    }

    private static PeerVector3Int QuantizePeerVector3(Vector3 value, double scale)
    {
        return new PeerVector3Int(
            QuantizePeerInt32(value.x, scale),
            QuantizePeerInt32(value.y, scale),
            QuantizePeerInt32(value.z, scale));
    }

    private static int QuantizePeerInt32(float value, double scale)
    {
        if (!float.IsFinite(value) || !double.IsFinite(scale) || scale <= 0d)
            throw new InvalidDataException("peer contract contains a non-finite number");
        return checked((int)Math.Round(
            value * scale,
            MidpointRounding.AwayFromZero));
    }

    private static PeerQuaternionInt QuantizeCanonicalPeerQuaternion(
        Quaternion value)
    {
        double x = value.x;
        double y = value.y;
        double z = value.z;
        double w = value.w;
        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            !double.IsFinite(z) || !double.IsFinite(w))
        {
            throw new InvalidDataException("peer contract quaternion is non-finite");
        }
        double magnitude = Math.Sqrt(x * x + y * y + z * z + w * w);
        if (!double.IsFinite(magnitude) || magnitude < 1e-12d)
            throw new InvalidDataException("peer contract quaternion is near zero");
        x /= magnitude;
        y /= magnitude;
        z /= magnitude;
        w /= magnitude;
        double[] values = { x, y, z, w };
        int largestIndex = 0;
        double largestMagnitude = Math.Abs(values[0]);
        for (int index = 1; index < values.Length; index++)
        {
            double candidate = Math.Abs(values[index]);
            if (candidate > largestMagnitude)
            {
                largestIndex = index;
                largestMagnitude = candidate;
            }
        }
        if (values[largestIndex] < 0d)
        {
            x = -x;
            y = -y;
            z = -z;
            w = -w;
        }
        return new PeerQuaternionInt(
            checked((int)Math.Round(x * 100000d, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(y * 100000d, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(z * 100000d, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(w * 100000d, MidpointRounding.AwayFromZero)));
    }

    private static string GetPeerSemanticTransformKey(Transform transform)
    {
        if (transform == null)
            return string.Empty;
        var segments = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            string name = current.name ?? string.Empty;
            if (name.Length == 0)
                throw new InvalidDataException("peer semantic hierarchy has an empty name");
            int sameNameOrdinal = 0;
            Transform parent = current.parent;
            if (parent != null)
            {
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
            }
            else
            {
                Scene scene = current.gameObject.scene;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root == current.gameObject)
                        break;
                    if (root != null && string.Equals(
                            root.name,
                            name,
                            StringComparison.Ordinal))
                    {
                        sameNameOrdinal++;
                    }
                }
            }
            segments.Add(name + "\0" + sameNameOrdinal.ToString(
                CultureInfo.InvariantCulture));
            current = parent;
        }
        segments.Reverse();
        return string.Join("\0/\0", segments);
    }

    private static string GetPeerSemanticComponentKey(Component component)
    {
        if (component == null || component.transform == null)
            return string.Empty;
        string typeName = component.GetType().FullName ?? component.GetType().Name;
        int typeOrdinal = 0;
        foreach (Component sibling in component.gameObject.GetComponents<Component>())
        {
            if (sibling == null || !string.Equals(
                    sibling.GetType().FullName ?? sibling.GetType().Name,
                    typeName,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (sibling == component)
                break;
            typeOrdinal++;
        }
        return GetPeerSemanticTransformKey(component.transform) + "\0" +
            typeName + "\0" + typeOrdinal.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetPeerMarkerKey(string category, Transform marker)
    {
        if (string.IsNullOrEmpty(category) || marker == null ||
            string.IsNullOrEmpty(marker.name))
        {
            throw new InvalidDataException("peer marker key is incomplete");
        }
        return category + "\0" + marker.name;
    }

    private static List<Transform> FindExactPeerContractMarkers(
        Scene scene,
        string prefix)
    {
        var markers = new List<Transform>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null)
                continue;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                string name = item?.name ?? string.Empty;
                if (!name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (!names.Add(name))
                    throw new InvalidDataException(
                        "duplicate exact peer marker name '" + name + "'");
                markers.Add(item);
            }
        }
        markers.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return markers;
    }

    private static bool TryCapturePeerCompanionGate(
        Scene scene,
        PvpPeerIdentity identity,
        out PeerCompanionReceipt receipt,
        out Transform readyMarker,
        out string error)
    {
        receipt = new PeerCompanionReceipt
        {
            Declared = false,
            ReadyCount = 0,
            FailureCount = 0
        };
        readyMarker = null;
        error = string.Empty;
        bool declared = !string.Equals(
            identity.CompanionPluginGuid,
            PvpNoCompanionIdentity,
            StringComparison.Ordinal);
        if (!declared)
        {
            if (!string.Equals(identity.CompanionPluginVersion, PvpNoCompanionIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(identity.CompanionSha256, PvpNoCompanionIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(identity.CompanionReadyMarkerName, PvpNoCompanionIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(identity.CompanionFailureMarkerName, PvpNoCompanionIdentity,
                    StringComparison.Ordinal))
            {
                error = "no-companion identity tuple is not closed";
                return false;
            }
            return true;
        }

        Transform ready = FindExactSceneTransform(
            scene,
            identity.CompanionReadyMarkerName,
            out int readyCount);
        _ = FindExactSceneTransform(
            scene,
            identity.CompanionFailureMarkerName,
            out int failureCount);
        receipt = new PeerCompanionReceipt
        {
            Declared = true,
            ReadyCount = readyCount,
            FailureCount = failureCount
        };
        if (failureCount != 0 || readyCount != 1 || ready == null)
        {
            error = "companion gate requires one ready marker and zero failure markers";
            return false;
        }
        readyMarker = ready;
        return true;
    }

    private static bool TryRevalidatePeerCompanionGate(
        Scene scene,
        PvpPeerIdentity identity,
        PeerCompanionReceipt receipt,
        Transform readyMarker,
        out string error)
    {
        if (!TryCapturePeerCompanionGate(
                scene,
                identity,
                out PeerCompanionReceipt current,
                out Transform currentReady,
                out error))
        {
            return false;
        }
        if (current.Declared != receipt.Declared ||
            current.ReadyCount != receipt.ReadyCount ||
            current.FailureCount != receipt.FailureCount ||
            (current.Declared && currentReady != readyMarker))
        {
            error = "companion gate changed while the scene snapshot was built";
            return false;
        }
        return true;
    }

    private static PeerGroundReceipt CapturePeerGroundReceipt(
        Scene scene,
        Transform marker,
        string markerKey,
        IReadOnlyList<Collider> colliders,
        Collider[] clearanceBuffer)
    {
        if (!TryResolveTightPackageGround(
                scene,
                marker,
                colliders,
                out Collider support,
                out RaycastHit hit,
                out string error))
        {
            throw new InvalidDataException(
                "ground contract failed for '" + markerKey + "': " + error);
        }
        Vector3 effectiveScale = support.transform.lossyScale;
        if (!float.IsFinite(effectiveScale.x) ||
            !float.IsFinite(effectiveScale.y) ||
            !float.IsFinite(effectiveScale.z) ||
            Mathf.Abs(effectiveScale.x) <= 0.00001f ||
            Mathf.Abs(effectiveScale.y) <= 0.00001f ||
            Mathf.Abs(effectiveScale.z) <= 0.00001f)
        {
            throw new InvalidDataException(
                "support collider has a non-finite or zero effective scale");
        }
        var receipt = new PeerGroundReceipt
        {
            MarkerKey = markerKey,
            ColliderKey = GetPeerSemanticComponentKey(support),
            ColliderType = support.GetType().FullName ?? support.GetType().Name,
            Layer = support.gameObject.layer,
            GapMillimetres = QuantizePeerInt32(
                Mathf.Abs(marker.position.y - hit.point.y),
                1000d),
            NormalTenThousandths = QuantizePeerVector3(hit.normal, 10000d),
            GeometryKind = string.Empty,
            LocalCenterTenThousandths = default,
            LocalSizeTenThousandths = default,
            RadiusTenThousandths = 0,
            HeightTenThousandths = 0,
            Direction = 0,
            TransformPositionMillimetres =
                QuantizePeerVector3(support.transform.position, 1000d),
            TransformRotationHundredThousandths =
                QuantizeCanonicalPeerQuaternion(support.transform.rotation),
            TransformScaleTenThousandths =
                QuantizePeerVector3(effectiveScale, 10000d),
            MeshName = string.Empty,
            MeshVertexCount = 0,
            MeshSubMeshCount = 0,
            MeshConvex = false,
            ClearanceRadiusMillimetres = checked((int)Math.Round(
                PeerStandingClearanceRadiusMetres * 1000d,
                MidpointRounding.AwayFromZero)),
            ClearanceHeightMillimetres = checked((int)Math.Round(
                PeerStandingClearanceHeightMetres * 1000d,
                MidpointRounding.AwayFromZero))
        };
        if (support is BoxCollider box)
        {
            if (!float.IsFinite(box.size.x) || !float.IsFinite(box.size.y) ||
                !float.IsFinite(box.size.z) || box.size.x <= 0f ||
                box.size.y <= 0f || box.size.z <= 0f)
            {
                throw new InvalidDataException(
                    "support BoxCollider has invalid local size");
            }
            receipt.GeometryKind = "box";
            receipt.LocalCenterTenThousandths =
                QuantizePeerVector3(box.center, 10000d);
            receipt.LocalSizeTenThousandths =
                QuantizePeerVector3(box.size, 10000d);
        }
        else if (support is CapsuleCollider capsule)
        {
            if (!float.IsFinite(capsule.radius) || capsule.radius <= 0f ||
                !float.IsFinite(capsule.height) || capsule.height <= 0f ||
                capsule.direction < 0 || capsule.direction > 2)
            {
                throw new InvalidDataException(
                    "support CapsuleCollider has invalid radius/height/direction");
            }
            receipt.GeometryKind = "capsule";
            receipt.LocalCenterTenThousandths =
                QuantizePeerVector3(capsule.center, 10000d);
            receipt.RadiusTenThousandths =
                QuantizePeerInt32(capsule.radius, 10000d);
            receipt.HeightTenThousandths =
                QuantizePeerInt32(capsule.height, 10000d);
            receipt.Direction = capsule.direction;
        }
        else if (support is SphereCollider sphere)
        {
            if (!float.IsFinite(sphere.radius) || sphere.radius <= 0f)
            {
                throw new InvalidDataException(
                    "support SphereCollider has invalid radius");
            }
            receipt.GeometryKind = "sphere";
            receipt.LocalCenterTenThousandths =
                QuantizePeerVector3(sphere.center, 10000d);
            receipt.RadiusTenThousandths =
                QuantizePeerInt32(sphere.radius, 10000d);
        }
        else if (support is TerrainCollider)
        {
            receipt.GeometryKind = "terrain";
        }
        else if (support is MeshCollider meshCollider &&
                 meshCollider.sharedMesh != null)
        {
            Mesh mesh = meshCollider.sharedMesh;
            Bounds bounds = mesh.bounds;
            if (!float.IsFinite(bounds.size.x) ||
                !float.IsFinite(bounds.size.y) ||
                !float.IsFinite(bounds.size.z) || bounds.size.x <= 0f ||
                bounds.size.y <= 0f || bounds.size.z <= 0f)
            {
                throw new InvalidDataException(
                    "support MeshCollider has invalid local bounds");
            }
            receipt.GeometryKind = "mesh";
            receipt.MeshName = mesh.name ?? string.Empty;
            receipt.MeshVertexCount = mesh.vertexCount;
            receipt.MeshSubMeshCount = mesh.subMeshCount;
            receipt.MeshConvex = meshCollider.convex;
            receipt.LocalCenterTenThousandths =
                QuantizePeerVector3(bounds.center, 10000d);
            receipt.LocalSizeTenThousandths =
                QuantizePeerVector3(bounds.size, 10000d);
            if (string.IsNullOrEmpty(receipt.MeshName) ||
                receipt.MeshVertexCount <= 0 || receipt.MeshSubMeshCount <= 0)
            {
                throw new InvalidDataException(
                    "support MeshCollider has no stable readable mesh contract");
            }
        }
        else
        {
            throw new InvalidDataException(
                "unsupported support collider kind '" + receipt.ColliderType + "'");
        }
        if (clearanceBuffer == null ||
            clearanceBuffer.Length != PeerStandingClearanceMaximumOverlaps)
        {
            throw new InvalidDataException("standing-clearance scratch bound is invalid");
        }
        Vector3 lower = marker.position + Vector3.up *
            (PeerStandingClearanceRadiusMetres +
             PeerStandingClearanceSkinMetres);
        Vector3 upper = marker.position + Vector3.up *
            (PeerStandingClearanceHeightMetres -
             PeerStandingClearanceRadiusMetres -
             PeerStandingClearanceSkinMetres);
        int overlaps = Physics.OverlapCapsuleNonAlloc(
            lower,
            upper,
            PeerStandingClearanceRadiusMetres,
            clearanceBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);
        if (overlaps >= clearanceBuffer.Length)
            throw new InvalidDataException("standing-clearance overlap bound was saturated");
        for (int index = 0; index < overlaps; index++)
        {
            Collider overlap = clearanceBuffer[index];
            clearanceBuffer[index] = null;
            if (overlap == null || overlap.gameObject == null ||
                overlap.gameObject.scene.handle != scene.handle ||
                !overlap.enabled || overlap.isTrigger)
            {
                continue;
            }
            if (overlap == support)
            {
                Vector3 closest = support.ClosestPoint(lower);
                float radialDistance = Vector3.Distance(closest, lower);
                if (closest.y <= marker.position.y +
                        PeerStandingClearanceSkinMetres ||
                    radialDistance >= PeerStandingClearanceRadiusMetres -
                        0.005f)
                {
                    continue;
                }
                throw new InvalidDataException(
                    "support collider intrudes above the expected foot-contact region");
            }
            throw new InvalidDataException(
                "standing capsule overlaps package collider '" +
                GetPeerSemanticComponentKey(overlap) + "'");
        }
        return receipt;
    }

    private static string GetExactPeerPlayerMarkerCategory(
        ModdedOperationMode mode,
        string name)
    {
        name ??= string.Empty;
        bool sharedTeamOne =
            name.StartsWith("Team1_Spawn_", StringComparison.Ordinal) ||
            name.StartsWith("Team1_Backup_Spawn_", StringComparison.Ordinal);
        if (mode == ModdedOperationMode.PlayerVersusEnvironment &&
            (sharedTeamOne ||
             name.StartsWith("PVE_PlayerSpawn_", StringComparison.Ordinal)))
        {
            return "player-pve";
        }
        if (mode == ModdedOperationMode.PlayerVersusPlayer)
        {
            if (sharedTeamOne ||
                name.StartsWith("PVP_Team1Spawn_", StringComparison.Ordinal))
            {
                return "player-pvp-team1";
            }
            if (name.StartsWith("Team2_Spawn_", StringComparison.Ordinal) ||
                name.StartsWith("Team2_Backup_Spawn_", StringComparison.Ordinal) ||
                name.StartsWith("PVP_Team2Spawn_", StringComparison.Ordinal))
            {
                return "player-pvp-team2";
            }
        }
        return null;
    }

    private static List<Transform> FindExactPeerPlayerMarkers(
        Scene scene,
        ModdedOperationMode mode)
    {
        var teamOne = new List<Transform>();
        var teamTwo = new List<Transform>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null)
                continue;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                string name = item?.name ?? string.Empty;
                string category = GetExactPeerPlayerMarkerCategory(mode, name);
                if (category == null)
                    continue;
                if (!names.Add(name))
                    throw new InvalidDataException(
                        "duplicate exact player marker name '" + name + "'");
                if (string.Equals(
                        category,
                        "player-pvp-team2",
                        StringComparison.Ordinal))
                {
                    teamTwo.Add(item);
                }
                else
                {
                    teamOne.Add(item);
                }
            }
        }
        teamOne.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        teamTwo.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        if (mode == ModdedOperationMode.PlayerVersusPlayer)
            teamOne.AddRange(teamTwo);
        return teamOne;
    }

    private static void CapturePeerPlayerSpawnContract(
        Scene scene,
        ActiveMapOperation operation,
        PeerSceneContractSnapshot snapshot,
        IReadOnlyList<Collider> colliders,
        Collider[] clearanceBuffer,
        HashSet<string> consumedNames)
    {
        if (!TryValidateInstalledPvpSpawnContract(operation, out string spawnError))
            throw new InvalidDataException(spawnError);
        if (global::GameMode.singleton != operation.GameModeComponent)
            throw new InvalidDataException("GameMode.singleton is not operation-owned");
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusEnvironment &&
            (operation.GameModeComponent is not StandalonePveGameMode pve ||
             InfiltrationManager.instance != pve ||
             operation.PveRaidManager == null ||
             RaidManager.singleton != operation.PveRaidManager))
        {
            throw new InvalidDataException(
                "PVE InfiltrationManager/RaidManager globals are not operation-owned");
        }
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusPlayer &&
            (operation.GameModeComponent is not StandalonePvpGameMode pvp ||
             PvpGameode.instance != pvp))
        {
            throw new InvalidDataException(
                "PVP game-mode singleton is not operation-owned");
        }
        int count = operation.OwnedSpawnPoints.Count;
        List<Transform> authored = FindExactPeerPlayerMarkers(
            scene,
            operation.Operation.Mode);
        if (count == 0 || count > PeerSceneContractMaximumPlayerSpawns ||
            operation.OwnedFallbackSpawns.Length != count ||
            authored.Count != count)
        {
            throw new InvalidDataException("player-spawn count is outside the v2 bound");
        }
        var componentIds = new HashSet<int>();
        int teamOne = 0;
        int teamTwo = 0;
        for (int index = 0; index < count; index++)
        {
            SpawnPoint spawn = operation.OwnedSpawnPoints[index];
            GameObject fallback = operation.OwnedFallbackSpawns[index];
            if (spawn == null || spawn.transform == null || fallback == null ||
                fallback != spawn.gameObject ||
                authored[index] != spawn.transform ||
                spawn.gameObject.scene.handle != scene.handle ||
                !componentIds.Add(spawn.GetInstanceID()) ||
                !consumedNames.Add(spawn.name ?? string.Empty))
            {
                throw new InvalidDataException(
                    "player-spawn native/global ownership or exact name is ambiguous");
            }
            string category = GetExactPeerPlayerMarkerCategory(
                operation.Operation.Mode,
                spawn.name);
            if (string.IsNullOrEmpty(category))
            {
                throw new InvalidDataException(
                    "player marker does not use a canonical case-sensitive prefix: " +
                    (spawn.name ?? string.Empty));
            }
            bool expectedTeamTwo = string.Equals(
                category,
                "player-pvp-team2",
                StringComparison.Ordinal);
            int expectedTeam = expectedTeamTwo ? 2 : 1;
            if (spawn.Team != expectedTeam || !spawn.CanSpawnPlayer)
                throw new InvalidDataException("player-spawn Team/CanSpawnPlayer drifted");
            if (expectedTeamTwo)
                teamTwo++;
            else
                teamOne++;
            string markerKey = GetPeerMarkerKey(category, spawn.transform);
            snapshot.PlayerSpawns.Add(new PeerPlayerSpawnReceipt
            {
                Index = index,
                MarkerKey = markerKey,
                PositionMillimetres = QuantizePeerVector3(spawn.transform.position, 1000d),
                RotationHundredThousandths =
                    QuantizeCanonicalPeerQuaternion(spawn.transform.rotation),
                Team = spawn.Team,
                CanSpawnPlayer = spawn.CanSpawnPlayer
            });
            snapshot.Ground.Add(CapturePeerGroundReceipt(
                scene,
                spawn.transform,
                markerKey,
                colliders,
                clearanceBuffer));
        }
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusPlayer)
        {
            int required = Math.Max(
                1,
                (operation.Operation.MaximumPlayers + 1) / 2);
            if (teamOne < required || teamTwo < required)
            {
                throw new InvalidDataException(
                    "PVP player-spawn team capacity is below declared maximum");
            }
        }
    }

    private static List<Transform> CapturePeerPveCapacityContract(
        Scene scene,
        ActiveMapOperation operation,
        PvpPeerIdentity identity,
        PeerSceneContractSnapshot snapshot,
        IReadOnlyList<Collider> colliders,
        Collider[] clearanceBuffer,
        HashSet<string> consumedNames,
        out List<Transform> authoredReferences)
    {
        authoredReferences = new List<Transform>();
        snapshot.PveCapacity = new PeerPveCapacityReceipt
        {
            Applicable = operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment,
            Minimum = identity.MinimumEnemies,
            Maximum = identity.MaximumEnemies,
            Requested = identity.RequestedEnemies,
            AuthoredCount = 0,
            NavigationCount = 0
        };
        if (!snapshot.PveCapacity.Applicable)
            return new List<Transform>();
        List<Transform> authored = FindExactPeerContractMarkers(
            scene,
            "PVE_EnemySpawn_");
        authoredReferences.AddRange(authored);
        if (authored.Count == 0 ||
            authored.Count > PeerSceneContractMaximumEnemyCandidates)
            throw new InvalidDataException("PVE authored enemy marker count is invalid");
        foreach (Transform marker in authored)
        {
            if (!consumedNames.Add(marker.name))
                throw new InvalidDataException("PVE enemy marker name is ambiguous");
            string markerKey = GetPeerMarkerKey("enemy-pve", marker);
            snapshot.Ground.Add(CapturePeerGroundReceipt(
                scene,
                marker,
                markerKey,
                colliders,
                clearanceBuffer));
        }
        global::AstarPath astar = global::AstarPath.active;
        var graph = astar?.graphs?[0];
        if (astar == null || graph == null || !snapshot.Navigation.Applicable)
            throw new InvalidDataException("PVE navigation receipt is unavailable");
        string graphKey = snapshot.Navigation.OwnerKey + "\0" +
            snapshot.Navigation.GraphType + "\0" + snapshot.Navigation.GraphName;
        var safe = new List<Transform>();
        const float maximumCorrectionMetres = 1.0f;
        float minimumSquared = StandalonePveMinimumSpawnSeparationMeters *
            StandalonePveMinimumSpawnSeparationMeters;
        int navigationCount = 0;
        foreach (Transform marker in authored)
        {
            bool onNavigation;
            try { onNavigation = astar.IsPointOnNavmesh(marker.position); }
            catch { onNavigation = false; }
            var nearest = astar.GetNearest(marker.position);
            var node = nearest.node;
            if (!onNavigation || node == null || !node.Walkable || node.Graph != graph)
            {
                throw new InvalidDataException(
                    "PVE marker '" + marker.name +
                    "' is not owned by the accepted walkable graph");
            }
            Vector3 nearestPosition = nearest.position;
            Vector3 correction = nearestPosition - marker.position;
            float horizontal = Mathf.Sqrt(
                correction.x * correction.x + correction.z * correction.z);
            float vertical = Mathf.Abs(correction.y);
            if (!float.IsFinite(horizontal) || !float.IsFinite(vertical) ||
                horizontal > maximumCorrectionMetres ||
                vertical > maximumCorrectionMetres)
            {
                throw new InvalidDataException(
                    "PVE marker nearest-node correction exceeds 1.00 m");
            }
            string markerKey = GetPeerMarkerKey("enemy-pve", marker);
            snapshot.Navigation.Markers.Add(new PeerNavigationMarkerReceipt
            {
                MarkerKey = markerKey,
                GraphKey = graphKey,
                MarkerPositionMillimetres =
                    QuantizePeerVector3(marker.position, 1000d),
                MarkerRotationHundredThousandths =
                    QuantizeCanonicalPeerQuaternion(marker.rotation),
                NearestPositionMillimetres =
                    QuantizePeerVector3(nearestPosition, 1000d),
                HorizontalCorrectionMillimetres =
                    QuantizePeerInt32(horizontal, 1000d),
                VerticalCorrectionMillimetres =
                    QuantizePeerInt32(vertical, 1000d)
            });
            navigationCount++;
            bool separated = true;
            foreach (Transform accepted in safe)
            {
                float deltaX = marker.position.x - accepted.position.x;
                float deltaZ = marker.position.z - accepted.position.z;
                if (deltaX * deltaX + deltaZ * deltaZ < minimumSquared)
                {
                    separated = false;
                    break;
                }
            }
            if (separated)
                safe.Add(marker);
        }
        snapshot.PveCapacity.AuthoredCount = authored.Count;
        snapshot.PveCapacity.NavigationCount = navigationCount;
        foreach (Transform marker in safe)
            snapshot.PveCapacity.SafeMarkerKeys.Add(GetPeerMarkerKey("enemy-pve", marker));
        if (safe.Count == 0 ||
            safe.Count > PeerSceneContractMaximumEnemyCandidates ||
            identity.MinimumEnemies < 0 ||
            identity.MinimumEnemies > identity.RequestedEnemies ||
            identity.RequestedEnemies > identity.MaximumEnemies ||
            identity.MaximumEnemies > PeerSceneContractMaximumEnemyPopulation ||
            identity.MaximumEnemies > safe.Count)
        {
            throw new InvalidDataException(
                "PVE requested/declared/safe capacity ordering is invalid");
        }
        return safe;
    }

    private static PeerExfilReceipt CapturePeerExfilContract(
        Scene scene,
        ActiveMapOperation operation,
        HashSet<string> consumedNames)
    {
        var receipt = new PeerExfilReceipt
        {
            Applicable = operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment,
            MarkerKey = string.Empty,
            PositionMillimetres = default,
            RotationHundredThousandths = default,
            ScaleTenThousandths = default,
            Layer = 0,
            ColliderCenterTenThousandths = default,
            ColliderSizeTenThousandths = default
        };
        if (!receipt.Applicable)
            return receipt;
        List<Transform> markers = FindExactPeerContractMarkers(
            scene,
            StandalonePveExfilMarkerPrefix);
        if (markers.Count != 1 || !consumedNames.Add(markers[0].name))
            throw new InvalidDataException("PVE exfil marker is missing or ambiguous");
        Transform marker = markers[0];
        BoxCollider authored = marker.GetComponent<BoxCollider>();
        BoxCollider runtime = operation.PveExfilCollider;
        if (authored == null || !authored.isTrigger || !authored.enabled ||
            authored.gameObject.scene.handle != scene.handle ||
            authored.size.x <= 0f || authored.size.y <= 0f || authored.size.z <= 0f ||
            runtime == null || !runtime.isTrigger || !runtime.enabled ||
            runtime != operation.PveExfilZone?.GetComponent<BoxCollider>() ||
            operation.PveRaidManager == null ||
            RaidManager.singleton != operation.PveRaidManager ||
            operation.PveExfilZone == null ||
            runtime.gameObject != operation.PveExfilZone.gameObject ||
            runtime.gameObject != operation.BootstrapPrefabRoot ||
            runtime.center != authored.center || runtime.size != authored.size ||
            runtime.transform.position != marker.position ||
            runtime.transform.rotation != marker.rotation ||
            runtime.transform.lossyScale != marker.lossyScale ||
            runtime.gameObject.layer != marker.gameObject.layer)
        {
            throw new InvalidDataException("PVE authored/runtime exfil contract drifted");
        }
        receipt.MarkerKey = GetPeerMarkerKey("exfil-pve", marker);
        receipt.PositionMillimetres = QuantizePeerVector3(marker.position, 1000d);
        receipt.RotationHundredThousandths =
            QuantizeCanonicalPeerQuaternion(marker.rotation);
        receipt.ScaleTenThousandths =
            QuantizePeerVector3(marker.lossyScale, 10000d);
        receipt.Layer = marker.gameObject.layer;
        receipt.ColliderCenterTenThousandths =
            QuantizePeerVector3(authored.center, 10000d);
        receipt.ColliderSizeTenThousandths =
            QuantizePeerVector3(authored.size, 10000d);
        return receipt;
    }

    private static PeerTerrainReceipt CapturePeerTerrainContract(
        Scene scene,
        ActiveMapOperation operation)
    {
        ModdedRuntimeTerrainDefinition descriptor = operation.Map.RuntimeTerrain;
        var receipt = new PeerTerrainReceipt
        {
            Applicable = descriptor != null,
            RootKey = string.Empty,
            SizeTenThousandths = default,
            RootPositionMillimetres = default,
            RootRotationHundredThousandths = default,
            RootScaleTenThousandths = default,
            HeightmapResolution = 0,
            AlphamapResolution = 0,
            HolesResolution = 0,
            HeightmapScaleTenThousandths = default
        };
        if (!receipt.Applicable)
            return receipt;
        Transform root = FindExactSceneTransform(
            scene,
            descriptor.RootObjectName,
            out int matches);
        Terrain terrain = root == null ? null : root.GetComponent<Terrain>();
        TerrainCollider collider =
            root == null ? null : root.GetComponent<TerrainCollider>();
        TerrainData data = operation.RuntimeTerrainData;
        if (matches != 1 || root == null || terrain == null || collider == null ||
            data == null || terrain.terrainData != data || collider.terrainData != data ||
            !collider.enabled || collider.isTrigger ||
            collider.gameObject.scene.handle != scene.handle)
        {
            throw new InvalidDataException(
                "runtime terrain render/collision ownership is incomplete");
        }
        Vector3 size = data.size;
        Vector3 heightmapScale = data.heightmapScale;
        if (size.x <= 0f || size.y <= 0f || size.z <= 0f ||
            data.heightmapResolution <= 0 || data.alphamapResolution <= 0 ||
            data.holesResolution <= 0)
        {
            throw new InvalidDataException("runtime terrain dimensions are invalid");
        }
        receipt.RootKey = GetPeerSemanticTransformKey(root);
        receipt.SizeTenThousandths = QuantizePeerVector3(size, 10000d);
        receipt.RootPositionMillimetres = QuantizePeerVector3(root.position, 1000d);
        receipt.RootRotationHundredThousandths =
            QuantizeCanonicalPeerQuaternion(root.rotation);
        receipt.RootScaleTenThousandths =
            QuantizePeerVector3(root.lossyScale, 10000d);
        receipt.HeightmapResolution = data.heightmapResolution;
        receipt.AlphamapResolution = data.alphamapResolution;
        receipt.HolesResolution = data.holesResolution;
        receipt.HeightmapScaleTenThousandths =
            QuantizePeerVector3(heightmapScale, 10000d);
        return receipt;
    }

    private static bool TryCapturePeerHandlerAndTemplateContract(
        ActiveMapOperation operation,
        out PeerHandlerReceipt handlerReceipt,
        out string templateShapeDigest,
        out string error)
    {
        handlerReceipt = new PeerHandlerReceipt
        {
            AssetId = operation?.BootstrapAssetId ?? 0,
            Mode = (int)(operation?.Operation?.Mode ?? 0),
            ContractVersion = "handler-only-spawnmessage-v1",
            OwnedSpawn = false,
            OwnedUnspawn = false,
            PrefabEntry = false
        };
        templateShapeDigest = string.Empty;
        error = string.Empty;
        if (operation == null || operation.BootstrapAssetId == 0 ||
            operation.BootstrapPrefabRoot == null ||
            operation.BootstrapPrefabIdentity == null ||
            !operation.PeerGameModeHandlerRegistered ||
            operation.PeerGameModeSpawnHandler == null ||
            operation.PeerGameModeUnspawnHandler == null)
        {
            error = "generation-owned handler/template ownership is incomplete";
            return false;
        }
        try
        {
            if (NetworkClient.prefabs == null ||
                NetworkClient.spawnHandlers == null ||
                NetworkClient.unspawnHandlers == null)
            {
                error = "Mirror client registration dictionaries are unavailable";
                return false;
            }
            if (NetworkClient.prefabs.ContainsKey(operation.BootstrapAssetId))
            {
                error = "handler-only registration has a forbidden prefab entry";
                return false;
            }
            if (!NetworkClient.spawnHandlers.TryGetValue(
                    operation.BootstrapAssetId,
                    out SpawnHandlerDelegate registeredSpawn) ||
                !NetworkClient.unspawnHandlers.TryGetValue(
                    operation.BootstrapAssetId,
                    out UnSpawnDelegate registeredUnspawn) ||
                !SamePeerSpawnHandler(
                    registeredSpawn,
                    operation.PeerGameModeSpawnHandler) ||
                !SamePeerUnspawnHandler(
                    registeredUnspawn,
                    operation.PeerGameModeUnspawnHandler))
            {
                error = "generation-owned spawn/unspawn delegates are not exact registry values";
                return false;
            }
            handlerReceipt.OwnedSpawn = true;
            handlerReceipt.OwnedUnspawn = true;
            handlerReceipt.PrefabEntry = false;

            GameObject root = operation.BootstrapPrefabRoot;
            NetworkIdentity identity = operation.BootstrapPrefabIdentity;
            Scene templateScene = root.scene;
            Transform uniqueTemplate = FindExactSceneTransform(
                templateScene,
                root.name,
                out int templateNameCount);
            NetworkIdentity[] identities =
                root.GetComponentsInChildren<NetworkIdentity>(true);
            if (root.activeSelf || identities.Length != 1 || identities[0] != identity ||
                identity.gameObject != root || identity.assetId != operation.BootstrapAssetId ||
                identity.netId != 0 || identity.sceneId != 0 ||
                templateNameCount != 1 || uniqueTemplate != root.transform)
            {
                error = "inactive template root/NetworkIdentity shape is invalid";
                return false;
            }
            int pveOwners = root.GetComponentsInChildren<StandalonePveGameMode>(true).Length;
            int pvpOwners = root.GetComponentsInChildren<StandalonePvpGameMode>(true).Length;
            if ((operation.Operation.Mode == ModdedOperationMode.PlayerVersusEnvironment &&
                 (pveOwners != 1 || pvpOwners != 0)) ||
                (operation.Operation.Mode == ModdedOperationMode.PlayerVersusPlayer &&
                 (pvpOwners != 1 || pveOwners != 0)))
            {
                error = "template does not contain exactly one expected injected owner type";
                return false;
            }
            NetworkBehaviour[] complete =
                root.GetComponentsInChildren<NetworkBehaviour>(true);
            var authoritative = identity.NetworkBehaviours;
            if (complete == null || complete.Length == 0 || authoritative == null ||
                authoritative.Length != complete.Length)
            {
                error = "Mirror authoritative NetworkBehaviour array is incomplete";
                return false;
            }
            var listPointers = new HashSet<IntPtr>();
            var syncObjectPointers = new HashSet<IntPtr>();
            using var shapeWriter = new PeerCanonicalBinaryWriter();
            shapeWriter.WriteString(PeerTemplateShapeDomain);
            shapeWriter.WriteUInt32(operation.BootstrapAssetId);
            shapeWriter.WriteInt32((int)operation.Operation.Mode);
            shapeWriter.WriteUInt32(checked((uint)complete.Length));
            for (int index = 0; index < complete.Length; index++)
            {
                NetworkBehaviour behaviour = authoritative[index];
                if (behaviour == null || behaviour != complete[index] ||
                    behaviour.ComponentIndex != index || behaviour.netIdentity != identity ||
                    behaviour.syncObjects == null ||
                    behaviour.syncObjects.Pointer == IntPtr.Zero ||
                    !listPointers.Add(behaviour.syncObjects.Pointer))
                {
                    error = "template NetworkBehaviour order/index/netIdentity/syncObjects drifted";
                    return false;
                }
                shapeWriter.WriteInt32(index);
                shapeWriter.WriteString(
                    behaviour.GetType().FullName ?? behaviour.GetType().Name);
                shapeWriter.WriteString(GetPeerSemanticTransformKey(behaviour.transform));
                shapeWriter.WriteUInt32(checked((uint)behaviour.syncObjects.Count));
                for (int syncIndex = 0;
                     syncIndex < behaviour.syncObjects.Count;
                     syncIndex++)
                {
                    SyncObject syncObject = behaviour.syncObjects[syncIndex];
                    if (syncObject == null)
                    {
                        error = "template contains a null SyncObject";
                        return false;
                    }
                    if (syncObject.Pointer == IntPtr.Zero ||
                        !syncObjectPointers.Add(syncObject.Pointer))
                    {
                        error = "template contains a null/aliased SyncObject reference";
                        return false;
                    }
                    shapeWriter.WriteString(
                        syncObject.GetType().FullName ?? syncObject.GetType().Name);
                }
            }
            using SHA256 sha = SHA256.Create();
            templateShapeDigest = ToLowerHex(sha.ComputeHash(shapeWriter.ToArray()));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            templateShapeDigest = string.Empty;
            return false;
        }
    }

    private static bool SamePeerSpawnHandler(
        SpawnHandlerDelegate left,
        SpawnHandlerDelegate right)
    {
        if (ReferenceEquals(left, right))
            return true;
        try { return left != null && right != null && left.Pointer == right.Pointer; }
        catch { return false; }
    }

    private static bool SamePeerUnspawnHandler(
        UnSpawnDelegate left,
        UnSpawnDelegate right)
    {
        if (ReferenceEquals(left, right))
            return true;
        try { return left != null && right != null && left.Pointer == right.Pointer; }
        catch { return false; }
    }

    private static PeerNavigationReceipt CapturePeerNavigationContract(
        Scene scene,
        ActiveMapOperation operation)
    {
        var receipt = new PeerNavigationReceipt
        {
            Applicable = operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment,
            OwnerKey = string.Empty,
            GraphType = string.Empty,
            GraphName = string.Empty,
            NodeCount = 0,
            RvoOwnerKey = string.Empty,
            GraphBoundsCenterMillimetres = default,
            GraphBoundsSizeMillimetres = default,
            IsGridGraph = false,
            GridWidth = 0,
            GridDepth = 0,
            GridCenterMillimetres = default,
            GridRotationHundredThousandths = default,
            GridNodeSizeTenThousandths = 0,
            GridMaxStepTenThousandths = 0,
            GridMaxStepUsesSlope = false,
            GridMaxSlopeTenThousandths = 0,
            GridErodeIterations = 0,
            GridNeighbours = 0,
            GridCutCorners = false,
            CollisionType = 0,
            CollisionDiameterTenThousandths = 0,
            CollisionHeightTenThousandths = 0,
            CollisionOffsetTenThousandths = 0,
            CollisionRayDirection = 0,
            CollisionMask = 0,
            CollisionHeightMask = 0,
            CollisionFromHeightTenThousandths = 0,
            CollisionThickRaycast = false,
            CollisionThickRaycastDiameterTenThousandths = 0,
            CollisionUnwalkableWhenNoGround = false,
            CollisionUse2D = false,
            CollisionCheck = false,
            CollisionHeightCheck = false,
            RvoDesiredFps = 0,
            RvoWorkerThreads = 0,
            RvoDoubleBuffering = false,
            RvoHardCollisions = false,
            RvoSymmetryBiasHundredThousandths = 0,
            RvoMovementPlane = 0,
            RvoUseNavmeshAsObstacle = false
        };
        if (!receipt.Applicable)
            return receipt;
        global::AstarPath astar = global::AstarPath.active;
        if (astar == null || astar.gameObject == null ||
            astar.gameObject.scene.handle != scene.handle)
        {
            throw new InvalidDataException(
                "PVE navigation owner is not scoped to the package scene");
        }
        var graphs = astar.graphs;
        if (graphs == null || graphs.Length != 1 || graphs[0] == null)
            throw new InvalidDataException("PVE requires exactly one map-owned graph");
        var graph = graphs[0];
        int nodeCount = graph.CountNodes();
        if (!graph.isScanned || string.IsNullOrEmpty(graph.name) || nodeCount <= 0)
            throw new InvalidDataException("PVE graph is unscanned, unnamed, or empty");

        Pathfinding.RVO.RVOSimulator acceptedRvo = null;
        int rvoCount = 0;
        foreach (Pathfinding.RVO.RVOSimulator rvo in
                 Resources.FindObjectsOfTypeAll<Pathfinding.RVO.RVOSimulator>())
        {
            if (rvo == null || rvo.gameObject == null || !rvo.enabled ||
                rvo.gameObject.scene.handle != scene.handle)
            {
                continue;
            }
            acceptedRvo = rvo;
            rvoCount++;
        }
        if (rvoCount != 1 || acceptedRvo == null ||
            Pathfinding.RVO.RVOSimulator.active != acceptedRvo)
        {
            throw new InvalidDataException(
                "PVE requires one exact map-owned active RVO simulator");
        }
        receipt.OwnerKey = GetPeerSemanticComponentKey(astar);
        receipt.GraphType = graph.GetType().FullName ?? graph.GetType().Name;
        receipt.GraphName = graph.name;
        receipt.NodeCount = checked((uint)nodeCount);
        receipt.RvoOwnerKey = GetPeerSemanticComponentKey(acceptedRvo);
        receipt.GraphBoundsCenterMillimetres =
            QuantizePeerVector3(graph.bounds.center, 1000d);
        receipt.GraphBoundsSizeMillimetres =
            QuantizePeerVector3(graph.bounds.size, 1000d);
        if (graph is not Pathfinding.GridGraph grid || grid.collision == null)
            throw new InvalidDataException("PVE accepted graph is not one GridGraph");
        receipt.IsGridGraph = true;
        receipt.GridWidth = grid.width;
        receipt.GridDepth = grid.depth;
        receipt.GridCenterMillimetres = QuantizePeerVector3(grid.center, 1000d);
        receipt.GridRotationHundredThousandths =
            QuantizeCanonicalPeerQuaternion(Quaternion.Euler(grid.rotation));
        receipt.GridNodeSizeTenThousandths =
            QuantizePeerInt32(grid.nodeSize, 10000d);
        receipt.GridMaxStepTenThousandths =
            QuantizePeerInt32(grid.maxStepHeight, 10000d);
        receipt.GridMaxStepUsesSlope = grid.maxStepUsesSlope;
        receipt.GridMaxSlopeTenThousandths =
            QuantizePeerInt32(grid.maxSlope, 10000d);
        receipt.GridErodeIterations = grid.erodeIterations;
        receipt.GridNeighbours = (int)grid.neighbours;
        receipt.GridCutCorners = grid.cutCorners;
        var collision = grid.collision;
        receipt.CollisionType = (int)collision.type;
        receipt.CollisionDiameterTenThousandths =
            QuantizePeerInt32(collision.diameter, 10000d);
        receipt.CollisionHeightTenThousandths =
            QuantizePeerInt32(collision.height, 10000d);
        receipt.CollisionOffsetTenThousandths =
            QuantizePeerInt32(collision.collisionOffset, 10000d);
        receipt.CollisionRayDirection = (int)collision.rayDirection;
        receipt.CollisionMask = collision.mask.value;
        receipt.CollisionHeightMask = collision.heightMask.value;
        receipt.CollisionFromHeightTenThousandths =
            QuantizePeerInt32(collision.fromHeight, 10000d);
        receipt.CollisionThickRaycast = collision.thickRaycast;
        receipt.CollisionThickRaycastDiameterTenThousandths =
            QuantizePeerInt32(collision.thickRaycastDiameter, 10000d);
        receipt.CollisionUnwalkableWhenNoGround =
            collision.unwalkableWhenNoGround;
        receipt.CollisionUse2D = collision.use2D;
        receipt.CollisionCheck = collision.collisionCheck;
        receipt.CollisionHeightCheck = collision.heightCheck;
        receipt.RvoDesiredFps = acceptedRvo.desiredSimulationFPS;
        receipt.RvoWorkerThreads = (int)acceptedRvo.workerThreads;
        receipt.RvoDoubleBuffering = acceptedRvo.doubleBuffering;
        receipt.RvoHardCollisions = acceptedRvo.hardCollisions;
        receipt.RvoSymmetryBiasHundredThousandths =
            QuantizePeerInt32(acceptedRvo.symmetryBreakingBias, 100000d);
        receipt.RvoMovementPlane = (int)acceptedRvo.movementPlane;
        receipt.RvoUseNavmeshAsObstacle = acceptedRvo.useNavmeshAsObstacle;
        return receipt;
    }

    private static bool TryBuildPeerSceneContractSnapshot(
        ActiveMapOperation operation,
        PvpPeerIdentity identity,
        ulong epoch,
        out PeerSceneContractSnapshot snapshot,
        out string digest,
        out List<Transform> pveEnemyMarkers,
        out List<Transform> pveAuthoredEnemyMarkers,
        out string error)
    {
        snapshot = null;
        digest = string.Empty;
        pveEnemyMarkers = null;
        pveAuthoredEnemyMarkers = null;
        error = string.Empty;
        try
        {
            if (operation == null || identity == null || epoch == 0 ||
                !OperationMatchesPvpIdentity(operation, identity) ||
                !string.Equals(
                    ComputePvpIdentityDigest(identity),
                    identity.Digest,
                    StringComparison.Ordinal) ||
                !IsCanonicalPackageScenePath(identity.ScenePath) ||
                operation.SceneHandle == 0 ||
                !operation.ScenePreparationComplete ||
                !operation.SpawnContractInstalled)
            {
                throw new InvalidDataException(
                    "session/identity/operation ownership is incomplete");
            }
            Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
            if (!scene.IsValid() || !scene.isLoaded ||
                string.IsNullOrEmpty(scene.path) ||
                !string.Equals(scene.path, identity.ScenePath, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "actual package scene path does not exactly match the agreement");
            }
            if (!TryCapturePeerCompanionGate(
                    scene,
                    identity,
                    out PeerCompanionReceipt companion,
                    out Transform companionReadyMarker,
                    out string companionError))
            {
                throw new InvalidDataException(companionError);
            }

            snapshot = new PeerSceneContractSnapshot
            {
                IdentityDigest = identity.Digest,
                Epoch = epoch,
                PackageId = identity.PackageId,
                PackageVersion = identity.PackageVersion,
                PackageContentId = identity.PackageContentId,
                MapId = identity.MapId,
                OperationId = identity.OperationId,
                Mode = identity.Mode,
                SpawnSetId = identity.SpawnSetId,
                VariantId = identity.VariantId,
                DeclaredScenePath = identity.ScenePath,
                ActualScenePath = scene.path,
                Companion = companion,
                Terrain = CapturePeerTerrainContract(scene, operation),
                Navigation = CapturePeerNavigationContract(scene, operation),
                Exfil = null,
                Handler = null,
                TemplateShapeDigest = string.Empty
            };

            var colliders = new List<Collider>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (collider != null && collider.enabled && !collider.isTrigger &&
                        collider.gameObject.activeInHierarchy &&
                        collider.gameObject.scene.handle == scene.handle)
                    {
                        colliders.Add(collider);
                    }
                }
            }
            if (colliders.Count == 0)
                throw new InvalidDataException("package scene has no eligible ground collider");

            var consumedNames = new HashSet<string>(StringComparer.Ordinal);
            var clearanceBuffer =
                new Collider[PeerStandingClearanceMaximumOverlaps];
            CapturePeerPlayerSpawnContract(
                scene,
                operation,
                snapshot,
                colliders,
                clearanceBuffer,
                consumedNames);
            pveEnemyMarkers = CapturePeerPveCapacityContract(
                scene,
                operation,
                identity,
                snapshot,
                colliders,
                clearanceBuffer,
                consumedNames,
                out pveAuthoredEnemyMarkers);
            snapshot.Exfil = CapturePeerExfilContract(
                scene,
                operation,
                consumedNames);
            snapshot.Ground.Sort((left, right) =>
                string.CompareOrdinal(left.MarkerKey, right.MarkerKey));

            if (!TryCapturePeerHandlerAndTemplateContract(
                    operation,
                    out PeerHandlerReceipt handler,
                    out string templateShapeDigest,
                    out string handlerError))
            {
                throw new InvalidDataException(handlerError);
            }
            snapshot.Handler = handler;
            snapshot.TemplateShapeDigest = templateShapeDigest;
            if (!TryRevalidatePeerCompanionGate(
                    scene,
                    identity,
                    snapshot.Companion,
                    companionReadyMarker,
                    out companionError))
            {
                throw new InvalidDataException(companionError);
            }
            digest = ComputePeerSceneContractDigest(snapshot);
            if (!IsLowercasePvpSha256(digest))
                throw new InvalidDataException("scene-contract digest is malformed");
            return true;
        }
        catch (Exception ex)
        {
            snapshot = null;
            digest = string.Empty;
            pveEnemyMarkers = null;
            pveAuthoredEnemyMarkers = null;
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static bool PeerTransformReferencesMatch(
        IReadOnlyList<Transform> expected,
        IReadOnlyList<Transform> observed)
    {
        if (expected == null || observed == null ||
            expected.Count != observed.Count)
        {
            return false;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            if (expected[index] == null || observed[index] == null ||
                !ReferenceEquals(expected[index], observed[index]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryRevalidateFrozenPeerSceneContract(
        ActiveMapOperation operation,
        PvpPeerIdentity identity,
        ulong epoch,
        out string error)
    {
        error = string.Empty;
        if (operation?.FrozenSceneContract == null || identity == null ||
            operation.FrozenSceneContractEpoch != epoch || epoch == 0 ||
            !IsLowercasePvpSha256(operation.FrozenSceneContractDigest))
        {
            error = "frozen scene-contract ledger is incomplete";
            return false;
        }
        if (!TryBuildPeerSceneContractSnapshot(
                operation,
                identity,
                epoch,
                out _,
                out string observedDigest,
                out List<Transform> observedEnemyMarkers,
                out List<Transform> observedAuthoredEnemyMarkers,
                out string observedError))
        {
            error = "live scene-contract capture failed: " + observedError;
            return false;
        }
        if (!string.Equals(
                observedDigest,
                operation.FrozenSceneContractDigest,
                StringComparison.Ordinal))
        {
            error = "live scene-contract digest drifted from the frozen receipt";
            return false;
        }
        if (!PeerTransformReferencesMatch(
                operation.FrozenPveEnemyMarkers,
                observedEnemyMarkers) ||
            !PeerTransformReferencesMatch(
                operation.FrozenPveAuthoredEnemyMarkers,
                observedAuthoredEnemyMarkers))
        {
            error = "live PVE marker ownership drifted from the frozen receipt";
            return false;
        }
        return true;
    }
}
