using System;
using System.Globalization;

namespace OperatorModdedOperations
{
    internal static class FrameworkEvidence
    {
        internal const string Prefix = "MODDED_OPS_EVIDENCE";
        internal const int SchemaVersion = 1;

        internal static string Build(
            string eventName,
            string loaderKind,
            string operationId,
            string mapId,
            int sceneHandle,
            ulong sceneGeneration,
            string payload = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Evidence event name is required.", nameof(eventName));
            if (payload != null &&
                (payload.IndexOf('\r') >= 0 || payload.IndexOf('\n') >= 0))
            {
                throw new ArgumentException(
                    "Evidence payload must remain on one line.",
                    nameof(payload));
            }

            string marker = Prefix +
                "|schema=" + SchemaVersion.ToString(CultureInfo.InvariantCulture) +
                "|event=" + Encode(eventName) +
                "|loader=" + Encode(loaderKind) +
                "|operation=" + Encode(operationId) +
                "|map=" + Encode(mapId) +
                "|sceneHandle=" + sceneHandle.ToString(CultureInfo.InvariantCulture) +
                "|sceneGeneration=" +
                    sceneGeneration.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(payload))
                marker += "|" + payload.TrimStart('|');
            return marker;
        }

        internal static bool TryWrite(
            Action<string> sink,
            string eventName,
            string loaderKind,
            string operationId,
            string mapId,
            int sceneHandle,
            ulong sceneGeneration,
            string payload = null)
        {
            if (sink == null)
                return false;
            try
            {
                sink(Build(
                    eventName,
                    loaderKind,
                    operationId,
                    mapId,
                    sceneHandle,
                    sceneGeneration,
                    payload));
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static string Encode(string value)
        {
            try
            {
                return Uri.EscapeDataString(
                    string.IsNullOrEmpty(value) ? "none" : value);
            }
            catch
            {
                return "invalid";
            }
        }

        internal static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal static string Number(uint value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal static string Number(ulong value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
