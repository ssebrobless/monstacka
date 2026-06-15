using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonStacka.Core
{
    /// <summary>
    /// Live-player render smoke test. Runs automatically in every build and editor play
    /// session. A few seconds after the Game scene loads it inspects the real runtime
    /// state (board state, active piece view, renderers, camera, sprite mask) and writes
    /// a structured [SMOKE] report to the player log, ending in PASS or FAIL.
    /// This exists because editor verification alone proved insufficient: the built
    /// player can show no pieces while every editor check passes.
    /// </summary>
    public sealed class RuntimeRenderSmoke : MonoBehaviour
    {
        private const string LogPrefix = "[MonStackaSmoke]";
        private static RuntimeRenderSmoke instance;

        private float lastSampledPieceY = float.MinValue;
        private bool sawGravityProgress;
        private bool finalPassed;
        private string finalReportText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance)
            {
                return;
            }

            var go = new GameObject("RuntimeRenderSmoke");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RuntimeRenderSmoke>();

            if (!string.IsNullOrEmpty(GetCapturePath()))
            {
                // Keep rendering while unfocused so the capture works without focus.
                Application.runInBackground = true;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Game")
            {
                return;
            }

            StopAllCoroutines();
            lastSampledPieceY = float.MinValue;
            sawGravityProgress = false;
            StartCoroutine(RunSmoke());
        }

        private IEnumerator RunSmoke()
        {
            Debug.Log($"{LogPrefix} Game scene loaded; sampling live render state.");
            yield return new WaitForSecondsRealtime(1f);
            Sample("t+1s", final: false);
            yield return new WaitForSecondsRealtime(2f);
            Sample("t+3s", final: false);
            yield return new WaitForSecondsRealtime(3f);
            Sample("t+6s", final: true);

            // -monstacka-capture <path>: write the player's own framebuffer to disk so
            // headless validation never depends on window focus or the primary display.
            var capturePath = GetCapturePath();
            if (!string.IsNullOrEmpty(capturePath))
            {
                Application.runInBackground = true;
                yield return new WaitForEndOfFrame();
                EnsureParentDirectory(capturePath);
                ScreenCapture.CaptureScreenshot(capturePath);
                Debug.Log($"{LogPrefix} capture requested -> {capturePath}");

                for (var waitFrames = 0; waitFrames < 60; waitFrames += 1)
                {
                    if (File.Exists(capturePath) && new FileInfo(capturePath).Length > 0)
                    {
                        break;
                    }

                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }

            WriteSmokeReport();

            if (HasArgument("-monstacka-smoke-quit"))
            {
                Application.Quit(finalPassed ? 0 : 1);
            }
        }

        private static string GetCapturePath()
        {
            return GetArgumentValue("-monstacka-capture");
        }

        private static string GetReportPath()
        {
            return GetArgumentValue("-monstacka-smoke-report");
        }

        private static string GetArgumentValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index += 1)
            {
                if (string.Equals(args[index], name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static bool HasArgument(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index += 1)
            {
                if (string.Equals(args[index], name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureParentDirectory(string path)
        {
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }

        private void WriteSmokeReport()
        {
            var reportPath = GetReportPath();
            if (string.IsNullOrEmpty(reportPath))
            {
                return;
            }

            EnsureParentDirectory(reportPath);
            File.WriteAllText(reportPath, finalReportText ?? $"{LogPrefix} RESULT: FAIL - final sample did not run.");
            Debug.Log($"{LogPrefix} report requested -> {reportPath}");
        }

        private void Sample(string label, bool final)
        {
            var report = new StringBuilder();
            report.AppendLine($"{LogPrefix} ===== sample {label} =====");

            var manager = FindFirstObjectByType<MonStacka.Core.GameManager>();
            if (!manager)
            {
                report.AppendLine($"{LogPrefix} FAIL: no GameManager in Game scene.");
                if (final)
                {
                    CompleteFinalReport(report, passed: false, " no-GameManager;");
                }
                Debug.Log(report.ToString());
                return;
            }

            var managerType = manager.GetType();
            var boardState = GetField(managerType, manager, "boardState") as BoardState;
            var activeView = GetField(managerType, manager, "activePieceView") as MonoBehaviour;
            var pieceSkins = GetField(managerType, manager, "pieceSkins") as Object[];
            var skinLookup = GetField(managerType, manager, "skinLookup") as System.Collections.IDictionary;
            var paused = GetField(managerType, manager, "paused") is bool pausedValue && pausedValue;
            var gravitySeconds = GetField(managerType, manager, "gravitySeconds") is float gs ? gs : -1f;

            report.AppendLine($"{LogPrefix} mode={MonStackaAppState.SelectedMode} paused={paused} gravitySeconds={gravitySeconds:0.###}");
            report.AppendLine($"{LogPrefix} pieceSkins={(pieceSkins == null ? "null" : pieceSkins.Length.ToString())} nullEntries={(pieceSkins == null ? -1 : System.Array.FindAll(pieceSkins, skin => !skin).Length)} skinLookup={(skinLookup == null ? "null" : skinLookup.Count.ToString())}");

            var hasActive = boardState != null && boardState.HasActivePiece;
            if (boardState == null)
            {
                report.AppendLine($"{LogPrefix} FAIL: boardState is null.");
            }
            else
            {
                var piece = boardState.ActivePiece;
                report.AppendLine($"{LogPrefix} boardState: hasActive={boardState.HasActivePiece} type={piece.Type} x={piece.X} y={piece.Y} rot={piece.Rotation} gameOver={boardState.IsGameOver()} queue={boardState.NextQueue.Count} placed={boardState.PiecesPlaced}");

                if (boardState.HasActivePiece)
                {
                    if (lastSampledPieceY != float.MinValue && (piece.Y > lastSampledPieceY || boardState.PiecesPlaced > 0))
                    {
                        sawGravityProgress = true;
                    }
                    lastSampledPieceY = piece.Y;
                }
            }

            var camera = Camera.main;
            if (camera)
            {
                var halfHeight = camera.orthographicSize;
                var halfWidth = halfHeight * camera.aspect;
                report.AppendLine($"{LogPrefix} camera: pos={camera.transform.position} orthoSize={halfHeight:0.###} aspect={camera.aspect:0.###} rect={camera.rect} worldRect=({camera.transform.position.x - halfWidth:0.##},{camera.transform.position.y - halfHeight:0.##})..({camera.transform.position.x + halfWidth:0.##},{camera.transform.position.y + halfHeight:0.##})");
            }
            else
            {
                report.AppendLine($"{LogPrefix} FAIL: no main camera.");
            }

            var masks = FindObjectsByType<SpriteMask>(FindObjectsSortMode.None);
            foreach (var mask in masks)
            {
                report.AppendLine($"{LogPrefix} spriteMask '{mask.name}': enabled={mask.enabled} sprite={(mask.sprite ? mask.sprite.name : "null")} customRange={mask.isCustomRangeActive} front={mask.frontSortingOrder} back={mask.backSortingOrder} bounds={mask.bounds}");
            }
            if (masks.Length == 0)
            {
                report.AppendLine($"{LogPrefix} spriteMask: none found (VisibleInsideMask renderers will be invisible).");
            }

            var visibleSprites = 0;
            var enabledSprites = 0;
            foreach (var renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.enabled && renderer.sprite)
                {
                    enabledSprites += 1;
                    if (renderer.isVisible)
                    {
                        visibleSprites += 1;
                    }
                }
            }
            report.AppendLine($"{LogPrefix} sceneSprites: enabledWithSprite={enabledSprites} visible={visibleSprites}");

            var activeViewVisible = DescribeActiveView(report, activeView, camera);

            if (final)
            {
                var failures = new StringBuilder();
                if (boardState == null)
                {
                    failures.Append(" boardState-null;");
                }
                else if (!hasActive && !boardState.IsGameOver())
                {
                    failures.Append(" no-active-piece;");
                }

                if (hasActive && activeView == null)
                {
                    failures.Append(" active-view-missing;");
                }

                if (hasActive && activeView != null && !activeViewVisible)
                {
                    failures.Append(" active-view-not-visible;");
                }

                if (hasActive && !sawGravityProgress && !paused)
                {
                    failures.Append(" gravity-not-progressing;");
                }

                if (skinLookup == null || skinLookup.Count == 0)
                {
                    failures.Append(" skin-lookup-empty;");
                }

                CompleteFinalReport(report, failures.Length == 0, failures.ToString());
            }

            Debug.Log(report.ToString());
        }

        private void CompleteFinalReport(StringBuilder report, bool passed, string failureDetails)
        {
            finalPassed = passed;
            report.AppendLine(passed
                ? $"{LogPrefix} RESULT: PASS - active piece is live and visibly rendered."
                : $"{LogPrefix} RESULT: FAIL -{failureDetails}");
            finalReportText = report.ToString();
        }

        private bool DescribeActiveView(StringBuilder report, MonoBehaviour activeView, Camera camera)
        {
            if (!activeView)
            {
                report.AppendLine($"{LogPrefix} activePieceView: null");
                return false;
            }

            var renderers = activeView.GetComponentsInChildren<SpriteRenderer>(true);
            report.AppendLine($"{LogPrefix} activePieceView: '{activeView.name}' worldPos={activeView.transform.position} childRenderers={renderers.Length}");

            var anyVisible = false;
            foreach (var renderer in renderers)
            {
                var inFrustum = false;
                if (camera && renderer.sprite)
                {
                    var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                    inFrustum = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
                }

                report.AppendLine(
                    $"{LogPrefix}   renderer '{renderer.name}': enabled={renderer.enabled} activeInHierarchy={renderer.gameObject.activeInHierarchy} sprite={(renderer.sprite ? renderer.sprite.name : "NULL")} " +
                    $"isVisible={renderer.isVisible} inFrustum={inFrustum} bounds={renderer.bounds} sortingOrder={renderer.sortingOrder} mask={renderer.maskInteraction} color={renderer.color}"
                );

                anyVisible |= renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite && renderer.isVisible;
            }

            if (renderers.Length == 0)
            {
                report.AppendLine($"{LogPrefix}   no child sprite renderers (skin lookup likely missed; PieceSkin.Initialize never ran).");
            }

            return anyVisible;
        }

        private static object GetField(System.Type type, object target, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target);
        }
    }
}
