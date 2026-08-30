using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OperatorModdedOperations
{
    internal static class Program
    {
        private static readonly (string Name, Action Body)[] Tests =
        {
            ("single scene bypasses state and RNG", SingleSceneBypassesStateAndRandom),
            ("two variants alternate without repeats", TwoVariantsAlternate),
            ("ten variants form complete shuffle bags", TenVariantsUseCompleteShuffleBags),
            ("selection state survives store reconstruction", StatePersistsAcrossStoreInstances),
            ("package content change preserves no-repeat boundary", PackageContentChangePreservesNoRepeatBoundary),
            ("unchanged content rejects a changed authored set", UnchangedContentRejectsChangedVariantSet),
            ("different maps keep independent histories", DifferentMapsKeepIndependentHistories),
            ("corrupt primary recovers from backup", CorruptPrimaryRecoversFromBackup),
            ("corrupt primary and backup fail closed", CorruptPrimaryAndBackupFailClosed),
            ("PVE enemy selector accepts the absolute 100 cap", PveEnemySelectorAcceptsAbsoluteCap),
            ("PVE enemy selector accepts every integer from 1 through 100", PveEnemySelectorAcceptsEverySupportedCount),
            ("PVE enemy selector rejects unsafe or out-of-range counts", PveEnemySelectorRejectsUnsafeCounts),
            ("framework evidence marker has one loader-neutral schema", FrameworkEvidenceUsesStableSchema),
            ("framework evidence marker escapes identity values", FrameworkEvidenceEscapesIdentityValues),
            ("framework evidence is invariant and single-line", FrameworkEvidenceIsInvariantAndSingleLine),
            ("framework evidence sink failure is isolated", FrameworkEvidenceSinkFailureIsIsolated),
        };

        public static int Main()
        {
            int failures = 0;

            foreach ((string name, Action body) in Tests)
            {
                try
                {
                    body();
                    Console.WriteLine("PASS: " + name);
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.Error.WriteLine("FAIL: " + name);
                    Console.Error.WriteLine(ex);
                }
            }

            Console.WriteLine(
                failures == 0
                    ? $"All {Tests.Length} scene-variant selector tests passed."
                    : $"{failures} of {Tests.Length} scene-variant selector tests failed.");

            return failures == 0 ? 0 : 1;
        }

        private static void SingleSceneBypassesStateAndRandom()
        {
            using TempDirectory temp = new TempDirectory();
            string statePath = temp.File("selector-state.json");
            int randomCalls = 0;
            SceneVariantSelectionStore store = new SceneVariantSelectionStore(
                statePath,
                _ =>
                {
                    randomCalls++;
                    throw new InvalidOperationException("RNG must not run for a single-scene map.");
                });

            SceneVariantSelection selection = store.Select(
                "package.single",
                "content-a",
                "map.single",
                Variants(1));

            Equal("v01", selection.Id, "single-scene selection id");
            Equal("Assets/Maps/V01.unity", selection.ScenePath, "single-scene selection path");
            Equal(0, selection.RemainingVariantCount, "single-scene remaining count");
            Equal(0, randomCalls, "single-scene RNG calls");
            False(File.Exists(statePath), "single-scene selection created a state file");
            False(File.Exists(statePath + ".bak"), "single-scene selection created a backup file");

            const string corruptSentinel = "not valid selector state";
            File.WriteAllText(statePath, corruptSentinel);
            SceneVariantSelectionStore bypassWithCorruptState = new SceneVariantSelectionStore(
                statePath,
                _ => throw new InvalidOperationException("RNG must not run for a single-scene map."));

            SceneVariantSelection secondSelection = bypassWithCorruptState.Select(
                "package.single",
                "content-a",
                "map.single",
                Variants(1));

            Equal("v01", secondSelection.Id, "single-scene selection with unrelated corrupt state");
            Equal(corruptSentinel, File.ReadAllText(statePath), "single-scene selection mutated state");
            False(File.Exists(statePath + ".bak"), "single-scene selection touched backup state");
        }

        private static void TwoVariantsAlternate()
        {
            using TempDirectory temp = new TempDirectory();
            SceneVariantSelectionStore store = new SceneVariantSelectionStore(
                temp.File("selector-state.json"),
                _ => 0);
            IReadOnlyList<SceneVariantCandidate> variants = Variants(2);
            List<string> selected = SelectIds(store, "package.two", "content-a", "map.two", variants, 12);

            for (int i = 1; i < selected.Count; i++)
            {
                NotEqual(selected[i - 1], selected[i], $"two-variant immediate repeat at selection {i + 1}");
            }

            for (int i = 2; i < selected.Count; i++)
            {
                Equal(selected[i - 2], selected[i], $"two variants did not alternate at selection {i + 1}");
            }
        }

        private static void TenVariantsUseCompleteShuffleBags()
        {
            using TempDirectory temp = new TempDirectory();
            int randomCall = 0;
            SceneVariantSelectionStore store = new SceneVariantSelectionStore(
                temp.File("selector-state.json"),
                exclusiveMaximum => (randomCall++ * 7 + 3) % exclusiveMaximum);
            IReadOnlyList<SceneVariantCandidate> variants = Variants(10);
            List<string> selected = SelectIds(store, "package.ten", "content-a", "map.ten", variants, 30);

            for (int i = 1; i < selected.Count; i++)
            {
                NotEqual(selected[i - 1], selected[i], $"ten-variant immediate repeat at selection {i + 1}");
            }

            for (int cycle = 0; cycle < 3; cycle++)
            {
                string[] bag = selected.Skip(cycle * 10).Take(10).ToArray();
                Equal(10, bag.Distinct(StringComparer.Ordinal).Count(), $"shuffle bag {cycle + 1} uniqueness");
                SetEqual(
                    variants.Select(candidate => candidate.Id),
                    bag,
                    $"shuffle bag {cycle + 1} membership");
            }
        }

        private static void StatePersistsAcrossStoreInstances()
        {
            using TempDirectory temp = new TempDirectory();
            string statePath = temp.File("selector-state.json");
            IReadOnlyList<SceneVariantCandidate> variants = Variants(4);

            SceneVariantSelection first = new SceneVariantSelectionStore(statePath, _ => 0).Select(
                "package.persistence",
                "content-a",
                "map.persistence",
                variants);
            True(File.Exists(statePath), "first variant selection did not persist state");

            SceneVariantSelection second = new SceneVariantSelectionStore(statePath, _ => 0).Select(
                "package.persistence",
                "content-a",
                "map.persistence",
                variants);

            NotEqual(first.Id, second.Id, "new store instance repeated the persisted previous selection");
            Equal(
                first.RemainingVariantCount - 1,
                second.RemainingVariantCount,
                "remaining bag did not persist");
        }

        private static void PackageContentChangePreservesNoRepeatBoundary()
        {
            using TempDirectory temp = new TempDirectory();
            string statePath = temp.File("selector-state.json");
            IReadOnlyList<SceneVariantCandidate> variants = Variants(2);

            SceneVariantSelection beforeUpdate = new SceneVariantSelectionStore(statePath, _ => 0).Select(
                "package.update",
                "content-old",
                "map.update",
                variants);
            IReadOnlyList<SceneVariantCandidate> renamedVariants = variants
                .Select(variant => new SceneVariantCandidate(
                    "renamed-" + variant.Id,
                    variant.ScenePath))
                .ToArray();
            SceneVariantSelection afterUpdate = new SceneVariantSelectionStore(statePath, _ => 0).Select(
                "package.update",
                "content-new",
                "map.update",
                renamedVariants);

            NotEqual(
                beforeUpdate.ScenePath,
                afterUpdate.ScenePath,
                "package-content change repeated a scene whose variant ID was renamed");
            Equal(
                1,
                afterUpdate.RemainingVariantCount,
                "package-content change did not refill the authored bag");
        }

        private static void DifferentMapsKeepIndependentHistories()
        {
            using TempDirectory temp = new TempDirectory();
            SceneVariantSelectionStore store = new SceneVariantSelectionStore(
                temp.File("selector-state.json"),
                _ => 0);
            IReadOnlyList<SceneVariantCandidate> variants = Variants(3);

            SceneVariantSelection mapAFirst = store.Select("package.shared", "content-a", "map.a", variants);
            SceneVariantSelection mapBFirst = store.Select("package.shared", "content-a", "map.b", variants);
            Equal(mapAFirst.Id, mapBFirst.Id, "map B inherited map A's selection history");

            SceneVariantSelection mapASecond = store.Select("package.shared", "content-a", "map.a", variants);
            SceneVariantSelection mapBSecond = store.Select("package.shared", "content-a", "map.b", variants);
            NotEqual(mapAFirst.Id, mapASecond.Id, "map A repeated its own previous selection");
            NotEqual(mapBFirst.Id, mapBSecond.Id, "map B repeated its own previous selection");
            Equal(mapASecond.Id, mapBSecond.Id, "independent map bags advanced differently with identical deterministic RNG");
        }

        private static void UnchangedContentRejectsChangedVariantSet()
        {
            using TempDirectory temp = new TempDirectory();
            string statePath = temp.File("selector-state.json");
            new SceneVariantSelectionStore(statePath, _ => 0).Select(
                "package.manifest",
                "content-unchanged",
                "map.manifest",
                Variants(3));

            IReadOnlyList<SceneVariantCandidate> changedVariants = new[]
            {
                new SceneVariantCandidate("v01", "Assets/Maps/V01.unity"),
                new SceneVariantCandidate("v02", "Assets/Maps/V02.unity"),
                new SceneVariantCandidate("v04", "Assets/Maps/V04.unity"),
            };
            InvalidDataException exception = Throws<InvalidDataException>(() =>
                new SceneVariantSelectionStore(statePath, _ => 0).Select(
                    "package.manifest",
                    "content-unchanged",
                    "map.manifest",
                    changedVariants));

            True(
                exception.Message.Contains("exact authored variant set", StringComparison.Ordinal),
                "same-content authored-set mismatch did not fail with the exact-set contract");
        }

        private static void CorruptPrimaryRecoversFromBackup()
        {
            using TempDirectory temp = new TempDirectory();
            string statePath = temp.File("selector-state.json");
            IReadOnlyList<SceneVariantCandidate> variants = Variants(3);
            SceneVariantSelectionStore initialStore = new SceneVariantSelectionStore(statePath, _ => 0);

            initialStore.Select(
                "package.recovery",
                "content-a",
                "map.recovery",
                variants);
            SceneVariantSelection backupLast = initialStore.Select(
                "package.recovery",
                "content-a",
                "map.recovery",
                variants);
            True(File.Exists(statePath + ".bak"), "second persisted selection did not create a backup");

            WriteChecksummedState(
                statePath,
                string.Join("\t", new[]
                {
                    EncodeStateField("package.recovery"),
                    EncodeStateField("map.recovery"),
                    EncodeStateField("content-a"),
                    EncodeStateField(backupLast.Id),
                    EncodeStateField(backupLast.ScenePath),
                    int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    EncodeStateField("unused-id"),
                    EncodeStateField("Assets/Maps/Unused.unity"),
                    "0",
                    EncodeStateField("unused-tail"),
                }));
            SceneVariantSelection recovered = new SceneVariantSelectionStore(statePath, _ => 0).Select(
                "package.recovery",
                "content-a",
                "map.recovery",
                variants);

            NotEqual(backupLast.Id, recovered.Id, "backup recovery repeated the backup's previous selection");
            True(variants.Any(candidate => candidate.Id == recovered.Id), "backup recovery selected an undeclared variant");
        }

        private static void CorruptPrimaryAndBackupFailClosed()
        {
            using TempDirectory temp = new TempDirectory();
            string statePath = temp.File("selector-state.json");
            IReadOnlyList<SceneVariantCandidate> variants = Variants(3);
            SceneVariantSelectionStore initialStore = new SceneVariantSelectionStore(statePath, _ => 0);

            initialStore.Select("package.corrupt", "content-a", "map.corrupt", variants);
            initialStore.Select("package.corrupt", "content-a", "map.corrupt", variants);
            True(File.Exists(statePath + ".bak"), "test setup did not create a backup");

            File.WriteAllText(statePath, "corrupt-primary");
            File.WriteAllText(statePath + ".bak", "corrupt-backup");

            InvalidDataException exception = Throws<InvalidDataException>(() =>
                new SceneVariantSelectionStore(statePath, _ => 0).Select(
                    "package.corrupt",
                    "content-a",
                    "map.corrupt",
                    variants));
            True(
                exception.Message.StartsWith(
                    "Scene variant selection state is corrupt and no valid backup is available:",
                    StringComparison.Ordinal),
                "corrupt-state exception did not explain the fail-closed condition");
        }

        private static void PveEnemySelectorAcceptsAbsoluteCap()
        {
            Equal(100, PveEnemyCountSelection.GetBriefingMaximum(1, 100),
                "absolute briefing maximum");
            Equal(10, PveEnemyCountSelection.GetDefault(10, 100),
                "performance-safe minimum default");
            Equal(10, PveEnemyCountSelection.NormalizeBriefingSelection(101, 10, 100),
                "invalid persisted selection did not reset to the safe minimum default");
            True(PveEnemyCountSelection.TryValidateConfirmedSelection(
                    100, 1, 100, 100, out string error),
                "100-enemy selection was rejected: " + error);
        }

        private static void PveEnemySelectorAcceptsEverySupportedCount()
        {
            for (int selected = 1; selected <= PveEnemyCountSelection.AbsoluteMaximum;
                 selected++)
            {
                Equal(selected, PveEnemyCountSelection.NormalizeBriefingSelection(
                        selected,
                        1,
                        PveEnemyCountSelection.AbsoluteMaximum),
                    $"briefing selection {selected}");
                True(PveEnemyCountSelection.TryValidateConfirmedSelection(
                        selected,
                        1,
                        PveEnemyCountSelection.AbsoluteMaximum,
                        PveEnemyCountSelection.AbsoluteMaximum,
                        out string error),
                    $"supported enemy count {selected} was rejected: {error}");
            }
        }

        private static void PveEnemySelectorRejectsUnsafeCounts()
        {
            Throws<ArgumentOutOfRangeException>(() =>
                PveEnemyCountSelection.GetBriefingMaximum(1, 101));
            False(PveEnemyCountSelection.TryValidateConfirmedSelection(
                    21, 10, 100, 20, out string capacityError),
                "selection above safe marker capacity was accepted");
            True(capacityError.Contains("safe-capacity", StringComparison.Ordinal),
                "unsafe-selection error did not name safe capacity");
            False(PveEnemyCountSelection.TryValidateConfirmedSelection(
                    10, 10, 100, 9, out string minimumError),
                "capacity below package minimum was accepted");
            True(minimumError.Contains("below the package minimum", StringComparison.Ordinal),
                "minimum-capacity error was not specific");
        }

        private static void FrameworkEvidenceUsesStableSchema()
        {
            string marker = FrameworkEvidence.Build(
                "pve-count-active",
                "melonloader",
                "community.example.pve",
                "community.example.map",
                42,
                3,
                "selected=100");
            Equal(
                "MODDED_OPS_EVIDENCE|schema=1|event=pve-count-active" +
                "|loader=melonloader|operation=community.example.pve" +
                "|map=community.example.map|sceneHandle=42" +
                "|sceneGeneration=3|selected=100",
                marker,
                "framework evidence schema");
        }

        private static void FrameworkEvidenceEscapesIdentityValues()
        {
            string marker = FrameworkEvidence.Build(
                "pvp-session-close",
                "bepinex",
                "operation|line\nnext",
                null,
                0,
                0);
            True(
                marker.Contains(
                    "|operation=operation%7Cline%0Anext|map=none|",
                    StringComparison.Ordinal),
                "framework evidence did not escape a delimiter/newline identity");
            Equal("none", FrameworkEvidence.Encode(null), "null evidence value");
        }

        private static void FrameworkEvidenceIsInvariantAndSingleLine()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                var customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                customCulture.NumberFormat.NegativeSign = "~";
                CultureInfo.CurrentCulture = customCulture;
                string marker = FrameworkEvidence.Build(
                    "scene-teardown-summary",
                    "bepinex",
                    "operation",
                    "map",
                    -42,
                    9);
                True(
                    marker.Contains("|sceneHandle=-42|sceneGeneration=9", StringComparison.Ordinal),
                    "framework evidence used ambient numeric formatting");
                Equal("-42", FrameworkEvidence.Number(-42),
                    "signed payload number formatting");
                Equal("42", FrameworkEvidence.Number(42U),
                    "unsigned payload number formatting");
                Equal("9", FrameworkEvidence.Number(9UL),
                    "epoch payload number formatting");
                string payloadMarker = FrameworkEvidence.Build(
                    "pve-extraction-unlocked",
                    "bepinex",
                    "operation",
                    "map",
                    1,
                    1,
                    "rawAllAI=" + FrameworkEvidence.Number(-42));
                True(
                    payloadMarker.EndsWith("|rawAllAI=-42", StringComparison.Ordinal),
                    "numeric evidence payload used ambient formatting");
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }

            Throws<ArgumentException>(() => FrameworkEvidence.Build(
                "scene-teardown-summary",
                "bepinex",
                "operation",
                "map",
                1,
                1,
                "reason=line1\nline2"));
        }

        private static void FrameworkEvidenceSinkFailureIsIsolated()
        {
            bool written = FrameworkEvidence.TryWrite(
                _ => throw new InvalidOperationException("injected sink failure"),
                "framework-unload-success",
                "melonloader",
                null,
                null,
                0,
                0);
            False(written, "throwing evidence sink escaped its isolation boundary");
        }

        private static IReadOnlyList<SceneVariantCandidate> Variants(int count)
        {
            return Enumerable.Range(1, count)
                .Select(index => new SceneVariantCandidate(
                    $"v{index:00}",
                    $"Assets/Maps/V{index:00}.unity"))
                .ToArray();
        }

        private static void WriteChecksummedState(string path, string payload)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            string checksum = Convert.ToHexString(SHA256.HashData(payloadBytes));
            File.WriteAllText(
                path,
                "OPERATOR_MODDED_OPERATIONS_SCENE_VARIANTS_V1\n" +
                checksum + "\n" + payload,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string EncodeStateField(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static List<string> SelectIds(
            SceneVariantSelectionStore store,
            string packageId,
            string contentId,
            string mapId,
            IReadOnlyList<SceneVariantCandidate> variants,
            int count)
        {
            List<string> selections = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                selections.Add(store.Select(packageId, contentId, mapId, variants).Id);
            }

            return selections;
        }

        private static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void False(bool condition, string message)
        {
            True(!condition, message);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'.");
            }
        }

        private static void NotEqual<T>(T left, T right, string message)
        {
            if (EqualityComparer<T>.Default.Equals(left, right))
            {
                throw new InvalidOperationException($"{message}: both values were '{left}'.");
            }
        }

        private static void SetEqual(IEnumerable<string> expected, IEnumerable<string> actual, string message)
        {
            HashSet<string> expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
            HashSet<string> actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
            if (!expectedSet.SetEquals(actualSet))
            {
                throw new InvalidOperationException(
                    $"{message}: expected [{string.Join(", ", expectedSet)}], got [{string.Join(", ", actualSet)}].");
            }
        }

        private static TException Throws<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException ex)
            {
                return ex;
            }

            throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
        }

        private sealed class TempDirectory : IDisposable
        {
            private readonly string path;

            public TempDirectory()
            {
                path = Path.Combine(
                    Path.GetTempPath(),
                    "OperatorModdedOperationsVariantTests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path);
            }

            public string File(string name)
            {
                return Path.Combine(path, name);
            }

            public void Dispose()
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
        }
    }
}
