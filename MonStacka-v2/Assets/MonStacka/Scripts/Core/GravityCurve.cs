namespace MonStacka.Core
{
    /// <summary>
    /// O.G.B.M. gravity speed-up bands, mirroring the v1 reference
    /// (enhanced/src/engine/gravity.ts ARCADE_GRAVITY_BANDS). Other modes use
    /// their configured base gravity unchanged.
    /// </summary>
    public static class GravityCurve
    {
        private readonly struct Band
        {
            public readonly int MinLines;
            public readonly float Seconds;

            public Band(int minLines, float seconds)
            {
                MinLines = minLines;
                Seconds = seconds;
            }
        }

        private static readonly Band[] OgbmBands =
        {
            new(140, 0.033f),
            new(120, 0.050f),
            new(100, 0.070f),
            new(80, 0.100f),
            new(60, 0.150f),
            new(40, 0.220f),
            new(30, 0.300f),
            new(20, 0.400f),
            new(10, 0.500f),
            new(0, 0.650f),
        };

        public static float SecondsFor(MonStackaMode mode, int linesCleared, float baseGravitySeconds)
        {
            if (mode != MonStackaMode.Ogbm)
            {
                return baseGravitySeconds;
            }

            foreach (var band in OgbmBands)
            {
                if (linesCleared >= band.MinLines)
                {
                    return band.Seconds;
                }
            }

            return baseGravitySeconds;
        }
    }
}
