using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal sealed class SceneVariantSelectionStore
{
	private sealed class StateRecord
	{
		internal string PackageId { get; }

		internal string PackageContentId { get; set; }

		internal string MapId { get; }

		internal List<string> VariantIds { get; }

		internal List<string> VariantScenePaths { get; }

		internal string LastVariantId { get; set; }

		internal string LastScenePath { get; set; }

		internal List<string> RemainingVariantIds { get; }

		internal StateRecord(string packageId, string packageContentId, string mapId, IEnumerable<string> variantIds, IEnumerable<string> variantScenePaths, string lastVariantId, string lastScenePath, IEnumerable<string> remainingVariantIds)
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

		internal static StateRecord Create(string packageId, string packageContentId, string mapId, IReadOnlyList<SceneVariantCandidate> variants)
		{
			return new StateRecord(packageId, packageContentId, mapId, variants.Select((SceneVariantCandidate variant) => variant.Id), variants.Select((SceneVariantCandidate variant) => variant.ScenePath), string.Empty, string.Empty, variants.Select((SceneVariantCandidate variant) => variant.Id));
		}

		internal StateRecord Clone()
		{
			return new StateRecord(PackageId, PackageContentId, MapId, VariantIds, VariantScenePaths, LastVariantId, LastScenePath, RemainingVariantIds);
		}
	}

	private const string FileHeader = "OPERATOR_MODDED_OPERATIONS_SCENE_VARIANTS_V1";

	private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	private readonly object sync = new object();

	private readonly string statePath;

	private readonly string backupPath;

	private readonly Func<int, int> randomIndex;

	private Dictionary<string, StateRecord> records;

	private bool stateLoaded;

	internal SceneVariantSelectionStore(string statePath, Func<int, int> randomIndex = null)
	{
		if (string.IsNullOrWhiteSpace(statePath))
		{
			throw new ArgumentException("A state path is required.", "statePath");
		}
		this.statePath = Path.GetFullPath(statePath);
		backupPath = this.statePath + ".bak";
		this.randomIndex = randomIndex ?? ((Func<int, int>)((int maximumExclusive) => RandomNumberGenerator.GetInt32(maximumExclusive)));
	}

	internal SceneVariantSelection Select(string packageId, string packageContentId, string mapId, IReadOnlyList<SceneVariantCandidate> variants)
	{
		ValidateIdentity(packageId, "packageId");
		ValidateIdentity(packageContentId, "packageContentId");
		ValidateIdentity(mapId, "mapId");
		ValidateCandidates(variants);
		if (variants.Count == 1)
		{
			return new SceneVariantSelection(variants[0].Id, variants[0].ScenePath, 0);
		}
		lock (sync)
		{
			EnsureStateLoaded();
			string key = BuildRecordKey(packageId, mapId);
			Dictionary<string, StateRecord> dictionary = CloneRecords(records);
			if (!dictionary.TryGetValue(key, out var record))
			{
				record = StateRecord.Create(packageId, packageContentId, mapId, variants);
				dictionary.Add(key, record);
			}
			else
			{
				ReconcileRecord(record, packageContentId, variants);
			}
			if (record.RemainingVariantIds.Count == 0)
			{
				record.RemainingVariantIds.AddRange(variants.Select((SceneVariantCandidate variant) => variant.Id));
			}
			string[] array = record.RemainingVariantIds.Where((string id) => !string.Equals(id, record.LastVariantId, StringComparison.Ordinal)).ToArray();
			if (array.Length == 0)
			{
				throw new InvalidDataException("Scene variant selection state cannot satisfy the immediate no-repeat contract for package '" + packageId + "', map '" + mapId + "'.");
			}
			int num = randomIndex(array.Length);
			if (num < 0 || num >= array.Length)
			{
				throw new InvalidOperationException("The scene variant random-index source returned an out-of-range value.");
			}
			string selectedId = array[num];
			SceneVariantCandidate sceneVariantCandidate = variants.First((SceneVariantCandidate variant) => string.Equals(variant.Id, selectedId, StringComparison.Ordinal));
			if (!record.RemainingVariantIds.Remove(selectedId))
			{
				throw new InvalidDataException("Scene variant selection state lost its selected bag entry.");
			}
			record.LastVariantId = selectedId;
			record.LastScenePath = sceneVariantCandidate.ScenePath;
			bool flag = false;
			try
			{
				AtomicWrite(statePath, Serialize(dictionary));
				flag = true;
				records = dictionary;
				AtomicWrite(backupPath, Serialize(dictionary));
			}
			catch
			{
				if (flag)
				{
					records = dictionary;
				}
				throw;
			}
			return new SceneVariantSelection(sceneVariantCandidate.Id, sceneVariantCandidate.ScenePath, record.RemainingVariantIds.Count);
		}
	}

	private static void ValidateIdentity(string value, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("A non-empty identity is required.", parameterName);
		}
	}

	private static void ValidateCandidates(IReadOnlyList<SceneVariantCandidate> variants)
	{
		if (variants == null || variants.Count == 0)
		{
			throw new ArgumentException("At least one scene variant is required.", "variants");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (SceneVariantCandidate variant in variants)
		{
			if (variant == null || string.IsNullOrWhiteSpace(variant.Id) || string.IsNullOrWhiteSpace(variant.ScenePath))
			{
				throw new ArgumentException("Every scene variant requires an ID and scene path.", "variants");
			}
			if (!hashSet.Add(variant.Id))
			{
				throw new ArgumentException("Scene variant IDs must be unique.", "variants");
			}
			if (!hashSet2.Add(variant.ScenePath))
			{
				throw new ArgumentException("Scene variant paths must be unique.", "variants");
			}
		}
	}

	private void EnsureStateLoaded()
	{
		if (stateLoaded)
		{
			return;
		}
		bool flag = File.Exists(statePath);
		bool flag2 = File.Exists(backupPath);
		if (!flag && !flag2)
		{
			records = new Dictionary<string, StateRecord>(StringComparer.Ordinal);
			stateLoaded = true;
			return;
		}
		List<string> list = new List<string>();
		if (flag)
		{
			if (TryReadState(statePath, out var parsed, out var error))
			{
				records = parsed;
				stateLoaded = true;
				return;
			}
			list.Add("primary=" + error);
		}
		else
		{
			list.Add("primary=missing");
		}
		if (flag2)
		{
			if (TryReadState(backupPath, out var parsed2, out var error2))
			{
				records = parsed2;
				stateLoaded = true;
				return;
			}
			list.Add("backup=" + error2);
		}
		else
		{
			list.Add("backup=missing");
		}
		throw new InvalidDataException("Scene variant selection state is corrupt and no valid backup is available: " + string.Join("; ", list) + ".");
	}

	private static void ReconcileRecord(StateRecord record, string packageContentId, IReadOnlyList<SceneVariantCandidate> variants)
	{
		string[] collection = variants.Select((SceneVariantCandidate variant) => variant.Id).ToArray();
		HashSet<string> currentIds = new HashSet<string>(collection, StringComparer.Ordinal);
		if (string.Equals(record.PackageContentId, packageContentId, StringComparison.Ordinal))
		{
			if (!VariantSetsEqual(record, variants))
			{
				throw new InvalidDataException("Scene variant selection state does not match the exact authored variant set for unchanged package content.");
			}
			ValidateBagRecord(record, currentIds);
			return;
		}
		SceneVariantCandidate sceneVariantCandidate = variants.FirstOrDefault((SceneVariantCandidate variant) => string.Equals(variant.ScenePath, record.LastScenePath, StringComparison.OrdinalIgnoreCase));
		record.PackageContentId = packageContentId;
		record.VariantIds.Clear();
		record.VariantIds.AddRange(collection);
		record.VariantScenePaths.Clear();
		record.VariantScenePaths.AddRange(variants.Select((SceneVariantCandidate variant) => variant.ScenePath));
		record.LastVariantId = sceneVariantCandidate?.Id ?? string.Empty;
		record.LastScenePath = sceneVariantCandidate?.ScenePath ?? string.Empty;
		record.RemainingVariantIds.Clear();
		record.RemainingVariantIds.AddRange(collection);
	}

	private static void ValidateBagRecord(StateRecord record, HashSet<string> currentIds)
	{
		if (string.IsNullOrEmpty(record.LastVariantId) || string.IsNullOrEmpty(record.LastScenePath))
		{
			throw new InvalidDataException("Scene variant selection state contains an incomplete last variant.");
		}
		int num = record.VariantIds.FindIndex((string id) => string.Equals(id, record.LastVariantId, StringComparison.Ordinal));
		if (num < 0 || num >= record.VariantScenePaths.Count || !string.Equals(record.VariantScenePaths[num], record.LastScenePath, StringComparison.OrdinalIgnoreCase) || !currentIds.Contains(record.LastVariantId))
		{
			throw new InvalidDataException("Scene variant selection state contains an unknown last variant.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (string remainingVariantId in record.RemainingVariantIds)
		{
			if (!currentIds.Contains(remainingVariantId) || !hashSet.Add(remainingVariantId) || string.Equals(remainingVariantId, record.LastVariantId, StringComparison.Ordinal))
			{
				throw new InvalidDataException("Scene variant selection state contains an invalid remaining bag.");
			}
		}
	}

	private static bool VariantSetsEqual(StateRecord record, IReadOnlyList<SceneVariantCandidate> variants)
	{
		if (record.VariantIds.Count != record.VariantScenePaths.Count || record.VariantIds.Count != variants.Count)
		{
			return false;
		}
		for (int i = 0; i < record.VariantIds.Count; i++)
		{
			string id = record.VariantIds[i];
			string path = record.VariantScenePaths[i];
			if (!variants.Any((SceneVariantCandidate variant) => string.Equals(variant.Id, id, StringComparison.Ordinal) && string.Equals(variant.ScenePath, path, StringComparison.OrdinalIgnoreCase)))
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

	private static Dictionary<string, StateRecord> CloneRecords(Dictionary<string, StateRecord> source)
	{
		Dictionary<string, StateRecord> dictionary = new Dictionary<string, StateRecord>(StringComparer.Ordinal);
		foreach (KeyValuePair<string, StateRecord> item in source)
		{
			dictionary.Add(item.Key, item.Value.Clone());
		}
		return dictionary;
	}

	private static string Serialize(Dictionary<string, StateRecord> state)
	{
		string text = string.Join("\n", state.Values.OrderBy<StateRecord, string>((StateRecord record) => record.PackageId, StringComparer.Ordinal).ThenBy<StateRecord, string>((StateRecord record) => record.MapId, StringComparer.Ordinal).Select(SerializeRecord));
		string text2 = Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(text)));
		return "OPERATOR_MODDED_OPERATIONS_SCENE_VARIANTS_V1\n" + text2 + "\n" + text;
	}

	private static string SerializeRecord(StateRecord record)
	{
		List<string> list = new List<string>
		{
			Encode(record.PackageId),
			Encode(record.MapId),
			Encode(record.PackageContentId),
			Encode(record.LastVariantId),
			Encode(record.LastScenePath),
			record.VariantIds.Count.ToString(CultureInfo.InvariantCulture)
		};
		for (int i = 0; i < record.VariantIds.Count; i++)
		{
			list.Add(Encode(record.VariantIds[i]));
			list.Add(Encode(record.VariantScenePaths[i]));
		}
		list.Add(record.RemainingVariantIds.Count.ToString(CultureInfo.InvariantCulture));
		list.AddRange(record.RemainingVariantIds.Select(Encode));
		return string.Join("\t", list);
	}

	private static bool TryReadState(string path, out Dictionary<string, StateRecord> parsed, out string error)
	{
		parsed = null;
		error = string.Empty;
		try
		{
			string text = File.ReadAllText(path, StrictUtf8);
			int num = text.IndexOf('\n');
			int num2 = ((num < 0) ? (-1) : text.IndexOf('\n', num + 1));
			if (num < 0 || num2 < 0 || !string.Equals(text.Substring(0, num), "OPERATOR_MODDED_OPERATIONS_SCENE_VARIANTS_V1", StringComparison.Ordinal))
			{
				throw new InvalidDataException("header is invalid");
			}
			string a = text.Substring(num + 1, num2 - num - 1);
			string text2 = text.Substring(num2 + 1);
			string b = Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(text2)));
			if (!string.Equals(a, b, StringComparison.Ordinal))
			{
				throw new InvalidDataException("checksum does not match");
			}
			Dictionary<string, StateRecord> dictionary = new Dictionary<string, StateRecord>(StringComparer.Ordinal);
			if (text2.Length > 0)
			{
				string[] array = text2.Split('\n');
				for (int i = 0; i < array.Length; i++)
				{
					StateRecord stateRecord = ParseRecord(array[i]);
					if (!dictionary.TryAdd(BuildRecordKey(stateRecord.PackageId, stateRecord.MapId), stateRecord))
					{
						throw new InvalidDataException("duplicate package/map record");
					}
				}
			}
			parsed = dictionary;
			return true;
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is FormatException || ex is DecoderFallbackException || ex is OverflowException || ex is ArgumentException)
		{
			error = ex.GetType().Name + ": " + ex.Message;
			return false;
		}
	}

	private static StateRecord ParseRecord(string line)
	{
		string[] array = line.Split('\t');
		if (array.Length < 10)
		{
			throw new InvalidDataException("record has too few fields");
		}
		int num = 0;
		string text = Decode(array[num++]);
		string text2 = Decode(array[num++]);
		string text3 = Decode(array[num++]);
		string lastVariantId = Decode(array[num++]);
		string text4 = Decode(array[num++]);
		ValidateIdentity(text, "stored package ID");
		ValidateIdentity(text2, "stored map ID");
		ValidateIdentity(text3, "stored package content ID");
		ValidateIdentity(lastVariantId, "stored last variant ID");
		ValidateIdentity(text4, "stored last scene path");
		int num2 = ParseCount(array[num++], "variant count");
		int num3 = array.Length - num;
		if (num2 < 2 || num3 < 1 || num2 > (num3 - 1) / 2)
		{
			throw new InvalidDataException("stored variant count is invalid");
		}
		List<string> list = new List<string>(num2);
		List<string> list2 = new List<string>(num2);
		HashSet<string> uniqueVariants = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < num2; i++)
		{
			string text5 = Decode(array[num++]);
			string text6 = Decode(array[num++]);
			ValidateIdentity(text5, "stored variant ID");
			ValidateIdentity(text6, "stored variant scene path");
			if (!uniqueVariants.Add(text5) || !hashSet.Add(text6))
			{
				throw new InvalidDataException("stored variant IDs and scene paths are not unique");
			}
			list.Add(text5);
			list2.Add(text6);
		}
		int num4 = ParseCount(array[num++], "remaining count");
		if (num4 < 0 || num4 != array.Length - num)
		{
			throw new InvalidDataException("stored remaining count is invalid");
		}
		List<string> list3 = new List<string>(num4);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		for (int j = 0; j < num4; j++)
		{
			string text7 = Decode(array[num++]);
			ValidateIdentity(text7, "stored remaining variant ID");
			if (!hashSet2.Add(text7))
			{
				throw new InvalidDataException("stored remaining IDs are not unique");
			}
			list3.Add(text7);
		}
		int num5 = list.FindIndex((string id) => string.Equals(id, lastVariantId, StringComparison.Ordinal));
		if (num5 < 0 || !string.Equals(list2[num5], text4, StringComparison.OrdinalIgnoreCase) || list3.Any((string id) => !uniqueVariants.Contains(id)) || hashSet2.Contains(lastVariantId))
		{
			throw new InvalidDataException("stored shuffle bag is inconsistent");
		}
		return new StateRecord(text, text3, text2, list, list2, lastVariantId, text4, list3);
	}

	private static int ParseCount(string value, string label)
	{
		if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
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
		string? directoryName = Path.GetDirectoryName(path);
		if (string.IsNullOrEmpty(directoryName))
		{
			throw new InvalidOperationException("The scene variant state directory is invalid.");
		}
		Directory.CreateDirectory(directoryName);
		string text = path + ".tmp." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "." + Guid.NewGuid().ToString("N");
		try
		{
			byte[] bytes = StrictUtf8.GetBytes(contents);
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
			{
				fileStream.Write(bytes, 0, bytes.Length);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text, path, overwrite: true);
		}
		finally
		{
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
			catch
			{
			}
		}
	}
}
