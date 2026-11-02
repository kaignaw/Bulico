namespace Bulico
{
    internal static class InputLimits
    {
        public const int MaxMullionLines = 500;
        public const double MaxMullionOffsetMm = 100000.0;
        public const double MaxFloorOffsetMeters = 1000.0;
        public const int MaxRegionCurves = 600;

        public static bool IsValidFloorOffset(double value)
        {
            return double.IsFinite(value)
                && value >= -MaxFloorOffsetMeters
                && value <= MaxFloorOffsetMeters;
        }
    }
}
