using System;

namespace OperatorModdedOperations
{
    internal static class PveEnemyCountSelection
    {
        internal const int AbsoluteMaximum = 100;

        internal static int GetBriefingMaximum(int minimum, int packageMaximum)
        {
            if (minimum < 1 || packageMaximum < minimum ||
                packageMaximum > AbsoluteMaximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(packageMaximum),
                    "PVE enemy bounds must satisfy 1 <= minimum <= maximum <= 100.");
            }

            return packageMaximum;
        }

        internal static int GetDefault(int minimum, int packageMaximum)
        {
            GetBriefingMaximum(minimum, packageMaximum);
            // The upper bound is an opt-in capacity, not a performance-safe
            // starting population. Large packages such as LOT 12 expose sixty
            // enemies for stress/custom sessions; opening the briefing at the
            // package minimum prevents an accidental 35-AI default while the
            // slider still retains the full declared range.
            return minimum;
        }

        internal static int NormalizeBriefingSelection(
            int selected,
            int minimum,
            int packageMaximum)
        {
            int maximum = GetBriefingMaximum(minimum, packageMaximum);
            if (selected < minimum || selected > maximum)
                return GetDefault(minimum, maximum);
            return selected;
        }

        internal static bool TryValidateConfirmedSelection(
            int selected,
            int minimum,
            int packageMaximum,
            int safeMarkerCapacity,
            out string error)
        {
            error = string.Empty;
            if (minimum < 1 || packageMaximum < minimum ||
                packageMaximum > AbsoluteMaximum)
            {
                error = "package bounds must satisfy 1 <= minimum <= maximum <= 100";
                return false;
            }

            if (safeMarkerCapacity < minimum)
            {
                error = "safe marker/navigation capacity is below the package minimum";
                return false;
            }

            int safeMaximum = Math.Min(packageMaximum, safeMarkerCapacity);
            if (selected < minimum || selected > safeMaximum)
            {
                error = "selected enemy count is outside the package and safe-capacity range";
                return false;
            }

            return true;
        }
    }
}
