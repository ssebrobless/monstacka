using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonStacka.Core
{
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Home";
        [SerializeField] private string playSceneName = "Game";

        private void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            MonStackaAppState.VisualExtrasEnabled = GetVisualExtrasEnabled(args);
            MonStackaAppState.RippleStage = GetRippleStage(args);
            var launchMode = GetLaunchMode(args);
            if (launchMode.HasValue)
            {
                MonStackaAppState.SelectedMode = launchMode.Value;
                if (launchMode.Value == MonStackaMode.Story)
                {
                    MonStackaAppState.SelectedStoryChapterId = GetLaunchChapter(args);
                }
                SceneManager.LoadScene(playSceneName);
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        private static MonStackaMode? GetLaunchMode(string[] args)
        {
            for (var index = 0; index < args.Length; index += 1)
            {
                if (!string.Equals(args[index], "-monstacka-mode", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= args.Length)
                {
                    return MonStackaMode.Ogbm;
                }

                var value = args[index + 1];
                if (string.Equals(value, "ogbm", System.StringComparison.OrdinalIgnoreCase))
                {
                    return MonStackaMode.Ogbm;
                }

                if (string.Equals(value, "sprint", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "x4lines", System.StringComparison.OrdinalIgnoreCase))
                {
                    return MonStackaMode.Sprint40;
                }

                if (string.Equals(value, "training", System.StringComparison.OrdinalIgnoreCase))
                {
                    return MonStackaMode.Training;
                }

                if (string.Equals(value, "story", System.StringComparison.OrdinalIgnoreCase))
                {
                    return MonStackaMode.Story;
                }

                return MonStackaMode.Ogbm;
            }

            return null;
        }

        private static string GetLaunchChapter(string[] args)
        {
            for (var index = 0; index < args.Length - 1; index += 1)
            {
                if (string.Equals(args[index], "-monstacka-chapter", System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static bool GetVisualExtrasEnabled(string[] args)
        {
            var envValue = System.Environment.GetEnvironmentVariable("MONSTACKA_VISUAL_EXTRAS");
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return !string.Equals(envValue, "0", System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(envValue, "false", System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(envValue, "off", System.StringComparison.OrdinalIgnoreCase);
            }

            return !System.Array.Exists(args, arg =>
                string.Equals(arg, "-monstacka-no-visual-extras", System.StringComparison.OrdinalIgnoreCase));
        }

        private static MonStackaRippleStage GetRippleStage(string[] args)
        {
            var envValue = System.Environment.GetEnvironmentVariable("MONSTACKA_RIPPLE_STAGE");
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return ParseRippleStage(envValue);
            }

            for (var index = 0; index < args.Length; index += 1)
            {
                if (!string.Equals(args[index], "-monstacka-ripple-stage", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return index + 1 < args.Length
                    ? ParseRippleStage(args[index + 1])
                    : MonStackaRippleStage.HomePreview;
            }

            return MonStackaRippleStage.HomePreview;
        }

        private static MonStackaRippleStage ParseRippleStage(string value)
        {
            if (string.Equals(value, "off", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "none", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "0", System.StringComparison.OrdinalIgnoreCase))
            {
                return MonStackaRippleStage.Off;
            }

            if (string.Equals(value, "active", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "active-gameplay", System.StringComparison.OrdinalIgnoreCase))
            {
                return MonStackaRippleStage.ActiveGameplay;
            }

            if (string.Equals(value, "landed", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "landed-gameplay", System.StringComparison.OrdinalIgnoreCase))
            {
                return MonStackaRippleStage.LandedGameplay;
            }

            if (string.Equals(value, "impact", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "all", System.StringComparison.OrdinalIgnoreCase))
            {
                return MonStackaRippleStage.ImpactGameplay;
            }

            return MonStackaRippleStage.HomePreview;
        }
    }
}
