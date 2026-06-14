namespace MonStacka.Core
{
    public enum MonStackaRippleStage
    {
        Off = 0,
        HomePreview = 1,
        ActiveGameplay = 2,
        LandedGameplay = 3,
        ImpactGameplay = 4,
    }

    public static class MonStackaAppState
    {
        public static MonStackaMode SelectedMode { get; set; } = MonStackaMode.Ogbm;

        public static float GravitySeconds { get; set; } = 0.65f;

        public static float LockDelaySeconds { get; set; } = 0.25f;

        public static float DasSeconds { get; set; } = 0.11f;

        public static float ArrSeconds { get; set; } = 0f;

        public static bool MusicEnabled { get; set; } = true;

        public static bool SfxEnabled { get; set; } = true;

        public static int MusicVolume { get; set; } = 20;

        public static int SfxVolume { get; set; } = 90;

        public static string TrainingFeedbackMode { get; set; } = "show";

        public static bool VisualExtrasEnabled { get; set; } = true;

        public static bool DitherEnabled { get; set; } = true;

        public static MonStackaRippleStage RippleStage { get; set; } = MonStackaRippleStage.HomePreview;

        /// <summary>Chapter id the Game scene should run when SelectedMode is Story (e.g. "1.2").</summary>
        public static string SelectedStoryChapterId { get; set; }

        /// <summary>Zany mode enables friendly held-piece abilities outside Story.</summary>
        public static bool FriendlyAbilitiesEnabled { get; set; } = true;

        public static void ResetDefaults()
        {
            SelectedMode = MonStackaMode.Ogbm;
            GravitySeconds = 0.65f;
            LockDelaySeconds = 0.25f;
            DasSeconds = 0.11f;
            ArrSeconds = 0f;
            MusicEnabled = true;
            SfxEnabled = true;
            MusicVolume = 20;
            SfxVolume = 90;
            TrainingFeedbackMode = "show";
            VisualExtrasEnabled = true;
            DitherEnabled = true;
            RippleStage = MonStackaRippleStage.HomePreview;
            SelectedStoryChapterId = null;
            FriendlyAbilitiesEnabled = true;
        }
    }
}
