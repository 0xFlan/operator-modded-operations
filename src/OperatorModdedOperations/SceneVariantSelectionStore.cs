using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal sealed class SceneVariantCandidate
{
    internal SceneVariantCandidate(string id, string scenePath)
    {
        Id = id;
        ScenePath = scenePath;
    }

    internal string Id { get; }

    internal string ScenePath { get; }
}

internal sealed class SceneVariantSelection
{
    internal SceneVariantSelection(
        string id,
        string scenePath,
        int remainingVariantCount)
    {
        Id = id;
        ScenePath = scenePath;
        RemainingVariantCount = remainingVariantCount;
    }

    internal string Id { get; }

    internal string ScenePath { get; }

    internal int RemainingVariantCount { get; }
}

/// <summary>
/// Owns the durable, per-package-map shuffle bag used only by maps that
/// explicitly declare more than one scene variant. The one-entry path returns
/// before state is loaded and before the random source is called.
/// </summary>
internal sealed class SceneVariantSelectionStore
{
    private const string FileHeader =
        "OPERATOR_MODDED_OPERATIONS_SCENE_VARIANTS_V1";
    private static readonly UTF8Encoding StrictUtf8 =
        new UTF8Encoding(false, true);

    private readonly object sync = new object();
    private readonly string statePath;
    private readonly string backupPath;
    private readonly Func<int, int> randomIndex;
    private Dictionary<string, StateRecord> records;
    private bool stateLoaded;

    internal SceneVariantSelectionStore(
        string statePath,
        Func<int, int> randomIndex = null)
    {
        if (string.IsNullOrWhiteSpace(statePath))
            throw new ArgumentException("A state path is required.", nameof(statePath));
        this.statePath = Path.GetFullPath(statePath);
        backupPath = this.statePath + ".bak";
        this.randomIndex = randomIndex ??
            (maximumExclusive => RandomNumberGenerator.GetInt32(maximumExclusive));
    }

    internal SceneVariantSelection Select(
        string packageId,
        string packageContentId,
        string mapId,
        IReadOnlyList<SceneVariantCandidate> variants)
    {
        ValidateIdentity(packageId, nameof(packageId));
        ValidateIdentity(packageContentId, nameof(packageContentId));
        ValidateIdentity(mapId, nameof(mapId));
        ValidateCandidates(variants);

        // This is an intentional compatibility boundary. Legacy/schema-v1
        // maps and schema-v2 maps without an explicit sceneVariants array are
        // represented by one synthetic `default` entry in Operator Mod API.
        // They must never read/write variant state or invoke randomness.
        if (variants.Count == 1)
        {
            return new SceneVariantSelection(
                variants[0].Id,
                variants[0].ScenePath,
                0);
        }

        lock (sync)
        {
            EnsureStateLoaded();

            string key = BuildRecordKey(packageId, mapId);
            var workingRecords = CloneRecords(records);
            if (!workingRecords.TryGetValue(key, out StateRecord record))
            {
                record = StateRecord.Create(
                    packageId,
                    packageContentId,
                    mapId,
                    variants);
                workingRecords.Add(key, record);
            }
            else
            {
                ReconcileRecord(record, packageContentId, variants);
            }

            if (record.RemainingVariantIds.Count == 0)
            {
                record.RemainingVariantIds.AddRange(
                    variants.Select(variant => variant.Id));
            }

            var eligibleIds = record.RemainingVariantIds
                .Where(id => !string.Equals(
                    id,
                    record.LastVariantId,
                    StringComparison.Ordinal))
                .ToArray();
            if (eligibleIds.Length == 0)
            {
                throw new InvalidDataException(
                    "Scene variant selection state cannot satisfy the immediate " +
                    "no-repeat contract for package '" + packageId +
                    "', map '" + mapId + "'.");
            }

            int selectedIndex = randomIndex(eligibleIds.Length);
            if (selectedIndex < 0 || selectedIndex >= eligibleIds.Length)
            {
                throw new InvalidOperationException(
                    "The scene variant random-index source returned an out-of-range value.");
            }

            string selectedId = eligibleIds[selectedIndex];
            SceneVariantCandidate selected = variants.First(variant =>
                string.Equals(variant.Id, selectedId, StringComparison.Ordinal));
            if (!record.RemainingVariantIds.Remove(selectedId))
            {
                throw new InvalidDataException(
                    "Scene variant selection state lost its selected bag entry.");
            }
            record.LastVariantId = selectedId;
            record.LastScenePath = selected.ScenePath;

            // Commit the durable state before exposing the selection to the
            // native mission launch. A failed write therefore fails the launch
            // closed instead of making cross-process no-repeat best-effort.
            bool primaryCommitted = false;
            try
            {
                AtomicWrite(statePath, Serialize(workingRecords));
                primaryCommitted = true;
                records = workingRecords;
                AtomicWrite(backupPath, Serialize(workingRecords));
            }
            catch
            {
                // If the primary atomic replace completed, keep memory aligned
                // with disk even when refreshing the redundant backup failed.
                if (primaryCommitted)
                    records = workingRecords;
                throw;
            }

            return new SceneVariantSelection(
                selected.Id,
                selected.ScenePath,
                record.RemainingVariantIds.Count);
        }
    }

    private static void ValidateIdentity(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-empty identity is required.", parameterName);
    }

    private static void ValidateCandidates(
        IReadOnlyList<SceneVariantCandidate> variants)
    {
        if (variants == null || variants.Count == 0)
            throw new ArgumentException("At least one scene variant is required.", nameof(variants));

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var scenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SceneVariantCandidate variant in variants)
        {
            if (variant == null || string.IsNullOrWhiteSpace(variant.Id) ||
                string.IsNullOrWhiteSpace(variant.ScenePath))
            {
                throw new ArgumentException(
                    "Every scene variant requires an ID and scene path.",
                    nameof(variants));
            }
            if (!ids.Add(variant.Id))
            {
                throw new ArgumentException(
                    "Scene variant IDs must be unique.",
                    nameof(variants));
            }
            if (!scenePaths.Add(variant.ScenePath))
            {
                throw new ArgumentException(
                    "Scene variant paths must be unique.",
                    nameof(variants));
            }
        }
    }

    private void EnsureStateLoaded()
    {
        if (stateLoaded)
            return;

        bool primaryExists = File.Exists(statePath);
        bool backupExists = File.Exists(backupPath);
        if (!primaryExists && !backupExists)
        {
            records = new Dictionary<string, StateRecord>(StringComparer.Ordinal);
            stateLoaded = true;
            return;
        }

        var errors = new List<string>();
        if (primaryExists)
        {
            if (TryReadState(statePath, out Dictionary<string, StateRecord> primary,
                    out string primaryError))
            {
                records = primary;
                stateLoaded = true;
                return;
            }
            errors.Add("primary=" + primaryError);
        }
        else
        {
            errors.Add("primary=missing");
        }

        if (backupExists)
        {
            if (TryReadState(backupPath, out Dictionary<string, StateRecord> backup,
                    out string backupError))
            {
                records = backup;
                stateLoaded = true;
                return;
            }
            errors.Add("backup=" + backupError);
        }
        else
        {
            errors.Add("backup=missing");
        }

        throw new InvalidDataException(
            "Scene variant selection state is corrupt and no valid backup is " +
            "available: " + string.Join("; ", errors) + ".");
    }

    private static void ReconcileRecord(
        StateRecord record,
        string packageContentId,
        IReadOnlyList<SceneVariantCandidate> variants)
    {
        var currentIds = variants.Select(variant => variant.Id).ToArray();
        var currentSet = new HashSet<string>(currentIds, StringComparer.Ordinal);
        if (string.Equals(
                record.PackageContentId,
                packageContentId,
                StringComparison.Ordinal))
        {
            if (!VariantSetsEqual(record, variants))
            {
                throw new InvalidDataException(
                    "Scene variant selection state does not match the exact " +
                    "authored variant set for unchanged package content.");
            }
            ValidateBagRecord(record, currentSet);
            return;
        }

        // A verified package-content change starts a new bag. Match the prior
        // scene path against the new declarations and adopt its current ID, so
        // an ID rename cannot repeat the immediately preceding layout.
        SceneVariantCandidate previousVariant = variants.FirstOrDefault(variant =>
            string.Equals(
                variant.ScenePath,
                record.LastScenePath,
                StringComparison.OrdinalIgnoreCase));
        record.PackageContentId = packageContentId;
        record.VariantIds.Clear();
        record.VariantIds.AddRange(currentIds);
        record.VariantScenePaths.Clear();
        record.VariantScenePaths.AddRange(
            variants.Select(variant => variant.ScenePath));
        record.LastVariantId = previousVariant?.Id ?? string.Empty;
        record.LastScenePath = previousVariant?.ScenePath ?? string.Empty;
        record.RemainingVariantIds.Clear();
        record.RemainingVariantIds.AddRange(currentIds);
    }

    private static void ValidateBagRecord(
        StateRecord record,
        HashSet<string> currentIds)
    {
        if (string.IsNullOrEmpty(record.LastVariantId) ||
            string.IsNullOrEmpty(record.LastScenePath))
        {
            throw new InvalidDataException(
                "Scene variant selection state contains an incomplete last variant.");
        }
        int lastIndex = record.VariantIds.FindIndex(id => string.Equals(
            id,
            record.LastVariantId,
            StringComparison.Ordinal));
        if (lastIndex < 0 || lastIndex >= record.VariantScenePaths.Count ||
            !string.Equals(
                record.VariantScenePaths[lastIndex],
                record.LastScenePath,
                StringComparison.OrdinalIgnoreCase) ||
            !currentIds.Contains(record.LastVariantId))
        {
            throw new InvalidDataException(
                "Scene variant selection state contains an unknown last variant.");
        }

        var remaining = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in record.RemainingVariantIds)
        {
            if (!currentIds.Contains(id) || !remaining.Add(id) ||
                string.Equals(id, record.LastVariantId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Scene variant selection state contains an invalid remaining bag.");
            }
        }
    }

    private static bool VariantSetsEqual(
        StateRecord record,
        IReadOnlyList<SceneVariantCandidate> variants)
    {
        if (record.VariantIds.Count != record.VariantScenePaths.Count ||
            record.VariantIds.Count != variants.Count)
        {
            return false;
        }
        for (int index = 0; index < record.VariantIds.Count; index++)
        {
            string id = record.VariantIds[index];
            string path = record.VariantScenePaths[index];
            if (!variants.Any(variant =>
                    string.Equals(variant.Id, id, StringComparison.Ordinal) &&
                    string.Equals(
                        variant.ScenePath,
                        path,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }
        return true;
    }

    private static string BuildRecordKey(string packageId, string mapId)
    {
        return packageId + "\0" + mapId;
    }

    private static Dictionary<string, StateRecord> CloneRecords(
        Dictionary<string, StateRecord> source)
    {
        var clone = new Dictionary<string, StateRecord>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, StateRecord> item in source)
            clone.Add(item.Key, item.Value.Clone());
        return clone;
    }

    private static string Serialize(Dictionary<string, StateRecord> state)
    {
        string payload = string.Join("\n", state.Values
            .OrderBy(record => record.PackageId, StringComparer.Ordinal)
            .ThenBy(record => record.MapId, StringComparer.Ordinal)
            .Select(SerializeRecord));
        string checksum = Convert.ToHexString(
            SHA256.HashData(StrictUtf8.GetBytes(payload)));
        return FileHeader + "\n" + checksum + "\n" + payload;
    }

    private static string SerializeRecord(StateRecord record)
    {
        var fields = new List<string>
        {
            Encode(record.PackageId),
            Encode(record.MapId),
            Encode(record.PackageContentId),
            Encode(record.LastVariantId),
            Encode(record.LastScenePath),
            record.VariantIds.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (int index = 0; index < record.VariantIds.Count; index++)
        {
            fields.Add(Encode(record.VariantIds[index]));
            fields.Add(Encode(record.VariantScenePaths[index]));
        }
        fields.Add(record.RemainingVariantIds.Count.ToString(
            CultureInfo.InvariantCulture));
        fields.AddRange(record.RemainingVariantIds.Select(Encode));
        return string.Join("\t", fields);
    }

    private static bool TryReadState(
        string path,
        out Dictionary<string, StateRecord> parsed,
        out string error)
    {
        parsed = null;
        error = string.Empty;
        try
        {
            string text = File.ReadAllText(path, StrictUtf8);
            int firstNewline = text.IndexOf('\n');
            int secondNewline = firstNewline < 0
                ? -1
                : text.IndexOf('\n', firstNewline + 1);
            if (firstNewline < 0 || secondNewline < 0 ||
                !string.Equals(
                    text.Substring(0, firstNewline),
                    FileHeader,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("header is invalid");
            }

            string expectedChecksum = text.Substring(
                firstNewline + 1,
                secondNewline - firstNewline - 1);
            string payload = text.Substring(secondNewline + 1);
            string actualChecksum = Convert.ToHexString(
                SHA256.HashData(StrictUtf8.GetBytes(payload)));
            if (!string.Equals(
                    expectedChecksum,
                    actualChecksum,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("checksum does not match");
            }

            var result = new Dictionary<string, StateRecord>(StringComparer.Ordinal);
            if (payload.Length > 0)
            {
                foreach (string line in payload.Split('\n'))
                {
                    StateRecord record = ParseRecord(line);
                    if (!result.TryAdd(
                            BuildRecordKey(record.PackageId, record.MapId),
                            record))
                    {
                        throw new InvalidDataException(
                            "duplicate package/map record");
                    }
                }
            }
            parsed = result;
            return true;
        }
        catch (Exception ex) when (
            ex is IOException || ex is UnauthorizedAccessException ||
            ex is InvalidDataException || ex is FormatException ||
            ex is DecoderFallbackException || ex is OverflowException ||
            ex is ArgumentException)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static StateRecord ParseRecord(string line)
    {
        string[] fields = line.Split('\t');
        if (fields.Length < 10)
            throw new InvalidDataException("record has too few fields");

        int cursor = 0;
        string packageId = Decode(fields[cursor++]);
        string mapId = Decode(fields[cursor++]);
        string packageContentId = Decode(fields[cursor++]);
        string lastVariantId = Decode(fields[cursor++]);
        string lastScenePath = Decode(fields[cursor++]);
        ValidateIdentity(packageId, "stored package ID");
        ValidateIdentity(mapId, "stored map ID");
        ValidateIdentity(packageContentId, "stored package content ID");
        ValidateIdentity(lastVariantId, "stored last variant ID");
        ValidateIdentity(lastScenePath, "stored last scene path");

        int variantCount = ParseCount(fields[cursor++], "variant count");
        int fieldsAvailableForVariants = fields.Length - cursor;
        if (variantCount < 2 || fieldsAvailableForVariants < 1 ||
            variantCount > (fieldsAvailableForVariants - 1) / 2)
            throw new InvalidDataException("stored variant count is invalid");
        var variantIds = new List<string>(variantCount);
        var variantScenePaths = new List<string>(variantCount);
        var uniqueVariants = new HashSet<string>(StringComparer.Ordinal);
        var uniqueScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < variantCount; index++)
        {
            string id = Decode(fields[cursor++]);
            string scenePath = Decode(fields[cursor++]);
            ValidateIdentity(id, "stored variant ID");
            ValidateIdentity(scenePath, "stored variant scene path");
            if (!uniqueVariants.Add(id) || !uniqueScenePaths.Add(scenePath))
            {
                throw new InvalidDataException(
                    "stored variant IDs and scene paths are not unique");
            }
            variantIds.Add(id);
            variantScenePaths.Add(scenePath);
        }

        int remainingCount = ParseCount(fields[cursor++], "remaining count");
        if (remainingCount < 0 || remainingCount != fields.Length - cursor)
            throw new InvalidDataException("stored remaining count is invalid");
        var remainingIds = new List<string>(remainingCount);
        var uniqueRemaining = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < remainingCount; index++)
        {
            string id = Decode(fields[cursor++]);
            ValidateIdentity(id, "stored remaining variant ID");
            if (!uniqueRemaining.Add(id))
                throw new InvalidDataException("stored remaining IDs are not unique");
            remainingIds.Add(id);
        }

        int lastIndex = variantIds.FindIndex(id => string.Equals(
            id,
            lastVariantId,
            StringComparison.Ordinal));
        if (lastIndex < 0 ||
            !string.Equals(
                variantScenePaths[lastIndex],
                lastScenePath,
                StringComparison.OrdinalIgnoreCase) ||
            remainingIds.Any(id => !uniqueVariants.Contains(id)) ||
            uniqueRemaining.Contains(lastVariantId))
        {
            throw new InvalidDataException("stored shuffle bag is inconsistent");
        }

        return new StateRecord(
            packageId,
            packageContentId,
            mapId,
            variantIds,
            variantScenePaths,
            lastVariantId,
            lastScenePath,
            remainingIds);
    }

    private static int ParseCount(string value, string label)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int result))
        {
            throw new InvalidDataException("stored " + label + " is invalid");
        }
        return result;
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(StrictUtf8.GetBytes(value ?? string.Empty));
    }

    private static string Decode(string value)
    {
        return StrictUtf8.GetString(Convert.FromBase64String(value));
    }

    private static void AtomicWrite(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("The scene variant state directory is invalid.");
        Directory.CreateDirectory(directory);

        string temporaryPath = path + ".tmp." +
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "." +
            Guid.NewGuid().ToString("N");
        try
        {
            byte[] bytes = StrictUtf8.GetBytes(contents);
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
            }
        }
    }

    private sealed class StateRecord
    {
        internal StateRecord(
            string packageId,
            string packageContentId,
            string mapId,
            IEnumerable<string> variantIds,
            IEnumerable<string> variantScenePaths,
            string lastVariantId,
            string lastScenePath,
            IEnumerable<string> remainingVariantIds)
        {
            PackageId = packageId;
            PackageContentId = packageContentId;
            MapId = mapId;
            VariantIds = new List<string>(variantIds);
            VariantScenePaths = new List<string>(variantScenePaths);
            LastVariantId = lastVariantId;
            LastScenePath = lastScenePath;
            RemainingVariantIds = new List<string>(remainingVariantIds);
        }

        internal string PackageId { get; }

        internal string PackageContentId { get; set; }

        internal string MapId { get; }

        internal List<string> VariantIds { get; }

        internal List<string> VariantScenePaths { get; }

        internal string LastVariantId { get; set; }

        internal string LastScenePath { get; set; }

        internal List<string> RemainingVariantIds { get; }

        internal static StateRecord Create(
            string packageId,
            string packageContentId,
            string mapId,
            IReadOnlyList<SceneVariantCandidate> variants)
        {
            return new StateRecord(
                packageId,
                packageContentId,
                mapId,
                variants.Select(variant => variant.Id),
                variants.Select(variant => variant.ScenePath),
                string.Empty,
                string.Empty,
                variants.Select(variant => variant.Id));
        }

        internal StateRecord Clone()
        {
            return new StateRecord(
                PackageId,
                PackageContentId,
                MapId,
                VariantIds,
                VariantScenePaths,
                LastVariantId,
                LastScenePath,
                RemainingVariantIds);
        }
    }
}
