using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonStacka.Core;
using MonStacka.UI;
using MonStacka.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonStacka.Editor
{
    public static class MonStackaV2Bootstrap
    {
        private readonly struct SilhouetteMask
        {
            public readonly int Width;
            public readonly int Height;
            public readonly byte[] Data;

            public SilhouetteMask(int width, int height, byte[] data)
            {
                Width = width;
                Height = height;
                Data = data;
            }
        }

        [Serializable]
        private sealed class FeatureManifest
        {
            public FeatureManifestEntry[] features;
        }

        [Serializable]
        private sealed class FeatureManifestEntry
        {
            public string piece;
            public string name;
            public string motion;
            public string psdLayer;
            // Original frame-1 sheet position (drives on-piece placement).
            public int x;
            public int y;
            public int w;
            public int h;
            // Position in the packed features atlas (drives the sprite crop).
            public int ax;
            public int ay;
        }

        private const string Root = "Assets/MonStacka";
        private const string SpriteSheetDir = Root + "/Art/SpriteSheets";
        private const string GeneratedDir = Root + "/Art/Generated/BodyFrames";
        private const string UiArtDir = Root + "/Art/UI";
        private const string AudioDir = Root + "/Audio";
        private const string MonstosAudioDir = AudioDir + "/Monstos";
        private const string PieceSkinDir = Root + "/Data/PieceSkins";
        private const string TuningDir = Root + "/Data/Tuning";
        private const string TuningAssetPath = TuningDir + "/DefaultBorderDeformTuning.asset";
        private const string MaterialPath = Root + "/Art/Materials/OutlineMeshMat.mat";
        private const string BootScenePath = Root + "/Scenes/Boot.unity";
        private const string HomeScenePath = Root + "/Scenes/Home.unity";
        private const string GameScenePath = Root + "/Scenes/Game.unity";
        private const string StorySelectScenePath = Root + "/Scenes/StorySelect.unity";
        private const string PieceViewPrefabPath = Root + "/Prefabs/PieceView.prefab";
        private const string CellViewPrefabPath = Root + "/Prefabs/CellView.prefab";
        private const string OutlinePrefabPath = Root + "/Prefabs/OutlineMesh.prefab";
        private const string HomeBackgroundPath = UiArtDir + "/monstacka-home-menu-clean.png";
        private const string GameBackgroundPath = UiArtDir + "/monstacka-background.png";
        private const string BackgroundMusicPath = AudioDir + "/monstacka-bgm.wav";

        private const float ArtboardWidth = 1920f;
        private const float ArtboardHeight = 1080f;
        private const float PixelsPerUnit = 100f;
        private const float CameraHalfHeight = ArtboardHeight / PixelsPerUnit / 2f;

        // Featureless body sheets + the features-only sheet are generated from the artist
        // PSD by tools/generate_layered_sheets.py - rerun it when the PSD changes.
        private static readonly string[] SpriteSheets =
        {
            SpriteSheetDir + "/monster-sheet-body1.png",
            SpriteSheetDir + "/monster-sheet-body2.png",
            SpriteSheetDir + "/monster-sheet-body3.png",
        };

        private const string FeaturesSheetPath = SpriteSheetDir + "/monster-sheet-features.png";
        private const string FeatureManifestPath = SpriteSheetDir + "/feature-manifest.json";

        [MenuItem("MonStacka/Bootstrap Vertical Slice")]
        public static void Run()
        {
            EnsureDirectories();
            EnsureUiArtAssets();
            EnsureAudioAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureSourceImports();
            ConfigureUiImports();
            var skins = GeneratePieceSkins();
            var tuning = EnsureDefaultTuningProfile();
            var material = EnsureOutlineMaterial();
            CreatePrefabs(material);
            CreateScenes(skins, tuning, material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MonStacka v2 bootstrap complete.");
        }

        public static void RunBatchMode()
        {
            Run();
            EditorApplication.Exit(0);
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MonStacka/Art/Generated/BodyFrames"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MonStacka/Art/UI"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MonStacka/Audio/Monstos"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MonStacka/Data/PieceSkins"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MonStacka/Data/Tuning"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MonStacka/Prefabs"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "MonStacka/Scenes"));
        }

        private static void EnsureUiArtAssets()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var sourceDir = Path.GetFullPath(Path.Combine(projectRoot, "..", "enhanced", "src", "assets", "ui"));
            CopyAssetIfChanged(Path.Combine(sourceDir, "monstacka-home-menu-clean.png"), Path.Combine(projectRoot, HomeBackgroundPath));
            CopyAssetIfChanged(Path.Combine(sourceDir, "monstacka-background.png"), Path.Combine(projectRoot, GameBackgroundPath));
            AssetDatabase.ImportAsset(HomeBackgroundPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(GameBackgroundPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureAudioAssets()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var sourceDir = Path.GetFullPath(Path.Combine(projectRoot, "..", "enhanced", "src", "assets", "audio"));
            CopyAssetIfChanged(Path.Combine(sourceDir, "monstacka-bgm.wav"), Path.Combine(projectRoot, BackgroundMusicPath));
            AssetDatabase.ImportAsset(BackgroundMusicPath, ImportAssetOptions.ForceSynchronousImport);

            var monstosSourceDir = Path.Combine(sourceDir, "monstos");
            if (!Directory.Exists(monstosSourceDir))
            {
                return;
            }

            foreach (var sourcePath in Directory.GetFiles(monstosSourceDir, "*.mp3"))
            {
                var destinationPath = Path.Combine(projectRoot, MonstosAudioDir, Path.GetFileName(sourcePath));
                CopyAssetIfChanged(sourcePath, destinationPath);
                AssetDatabase.ImportAsset($"{MonstosAudioDir}/{Path.GetFileName(sourcePath)}", ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void CopyAssetIfChanged(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            if (!File.Exists(destinationPath) ||
                File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(destinationPath))
            {
                File.Copy(sourcePath, destinationPath, true);
            }
        }

        private static void ConfigureSourceImports()
        {
            foreach (var assetPath in SpriteSheets)
            {
                ConfigureTextureImporter(assetPath, 112f, FilterMode.Point);
            }

            ConfigureTextureImporter(FeaturesSheetPath, 112f, FilterMode.Point);
        }

        private static void ConfigureUiImports()
        {
            ConfigureTextureImporter(HomeBackgroundPath, PixelsPerUnit, FilterMode.Bilinear);
            ConfigureTextureImporter(GameBackgroundPath, PixelsPerUnit, FilterMode.Bilinear);
        }

        private static void ConfigureTextureImporter(string assetPath, float pixelsPerUnit, FilterMode filterMode)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = filterMode;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static PieceSkinData[] GeneratePieceSkins()
        {
            var manifest = LoadFeatureManifest();
            var featuresSheet = AssetDatabase.LoadAssetAtPath<Sprite>(FeaturesSheetPath);
            if (!featuresSheet)
            {
                throw new InvalidOperationException($"Features sheet missing at {FeaturesSheetPath}; run tools/generate_layered_sheets.py.");
            }

            var pieceTypes = new[] { PieceType.I, PieceType.O, PieceType.T, PieceType.S, PieceType.Z, PieceType.J, PieceType.L };
            var skins = new List<PieceSkinData>();
            foreach (var pieceType in pieceTypes)
            {
                var frameSprites = new List<Sprite>();
                for (var frameIndex = 0; frameIndex < 3; frameIndex += 1)
                {
                    var sprite = GenerateBodyFrameSprite(pieceType, frameIndex);
                    frameSprites.Add(sprite);
                }

                var skinPath = $"{PieceSkinDir}/Skin_{pieceType}.asset";
                var skin = AssetDatabase.LoadAssetAtPath<PieceSkinData>(skinPath);
                if (!skin)
                {
                    skin = ScriptableObject.CreateInstance<PieceSkinData>();
                    AssetDatabase.CreateAsset(skin, skinPath);
                }

                skin.pieceType = pieceType;
                skin.bodyFrames = frameSprites.ToArray();
                skin.cropBounds = PieceDefinitions.GetFrameBounds(pieceType, 0);
                skin.baseRotation = PieceDefinitions.GetBaseRotation(pieceType);
                skin.boxSize = PieceDefinitions.GetBoxSize(pieceType);
                skin.pixelsPerCell = 112;
                skin.featuresSheet = featuresSheet;
                skin.features = BuildFeatureSeeds(pieceType, manifest);
                EditorUtility.SetDirty(skin);
                skins.Add(skin);
            }

            return skins.ToArray();
        }

        private static FeatureManifest LoadFeatureManifest()
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), FeatureManifestPath);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Feature manifest missing at {FeatureManifestPath}; run tools/generate_layered_sheets.py.");
            }

            var manifest = JsonUtility.FromJson<FeatureManifest>(File.ReadAllText(fullPath));
            if (manifest?.features == null || manifest.features.Length == 0)
            {
                throw new InvalidOperationException("Feature manifest parsed empty.");
            }

            return manifest;
        }

        private static FeatureSeed[] BuildFeatureSeeds(PieceType pieceType, FeatureManifest manifest)
        {
            // Sheet rect -> normalized box rect, the inverse of the resampling used in
            // BuildMaskedBaseBodyTexture (frame-1 bounds onto the 112px cell grid).
            var bounds = PieceDefinitions.GetFrameBounds(pieceType, 0);
            var baseCells = PieceDefinitions.GetCells(pieceType, PieceDefinitions.GetBaseRotation(pieceType));
            var minX = baseCells.Min(cell => cell.x);
            var minY = baseCells.Min(cell => cell.y);
            var widthCells = baseCells.Max(cell => cell.x) - minX + 1;
            var heightCells = baseCells.Max(cell => cell.y) - minY + 1;
            var scaleX = ((widthCells * 112f) - 1f) / Mathf.Max(1f, bounds.width - 1f);
            var scaleY = ((heightCells * 112f) - 1f) / Mathf.Max(1f, bounds.height - 1f);

            var seeds = new List<FeatureSeed>();
            foreach (var entry in manifest.features)
            {
                if (entry.piece != pieceType.ToString())
                {
                    continue;
                }

                if (!Enum.TryParse<FeatureMotion>(entry.motion, out var motion))
                {
                    throw new InvalidOperationException($"Unknown feature motion '{entry.motion}' for {entry.piece}/{entry.name}.");
                }

                seeds.Add(new FeatureSeed
                {
                    featureName = entry.name,
                    motion = motion,
                    sheetRect = new RectInt(entry.ax, entry.ay, entry.w, entry.h),
                    boxRect = new Rect(
                        (minX * 112f) + ((entry.x - bounds.x) * scaleX),
                        (minY * 112f) + ((entry.y - bounds.y) * scaleY),
                        entry.w * scaleX,
                        entry.h * scaleY),
                });
            }

            if (seeds.Count == 0)
            {
                throw new InvalidOperationException($"No features found in manifest for piece {pieceType}.");
            }

            return seeds.ToArray();
        }

        private static BorderDeformTuningProfile EnsureDefaultTuningProfile()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BorderDeformTuningProfile>(TuningAssetPath);
            if (!tuning)
            {
                tuning = ScriptableObject.CreateInstance<BorderDeformTuningProfile>();
                AssetDatabase.CreateAsset(tuning, TuningAssetPath);
            }

            tuning.springStiffness = 168f;
            tuning.springDamping = 33f;
            tuning.maxDisplacementCells = 0.026f;
            tuning.ambientAmplitudeCells = 0.011f;
            tuning.ambientDriveFrequency = 3.3f;
            tuning.ambientFlutterFrequency = 19.5f;
            tuning.edgeNoiseDensity = 4.1f;
            tuning.seamAmplitudeCells = 0.017f;
            tuning.seamDriveFrequency = 6.8f;
            tuning.seamNoiseBlend = 0.7f;
            tuning.impactImpulseCells = 0.02f;
            tuning.settleDurationSeconds = 0.085f;
            tuning.reboundFrequency = 14.5f;
            tuning.previewAmplitudeScale = 0.95f;
            EditorUtility.SetDirty(tuning);
            return tuning;
        }

        private static Sprite GenerateBodyFrameSprite(PieceType pieceType, int frameIndex)
        {
            var baseRotation = PieceDefinitions.GetBaseRotation(pieceType);
            var baseCells = PieceDefinitions.GetCells(pieceType, baseRotation);
            var silhouetteMask = GetSilhouetteMask(pieceType);
            var occupiedCells = new HashSet<Vector2Int>(baseCells);
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;
            foreach (var cell in baseCells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            var targetWidth = (maxX - minX + 1) * 112;
            var targetHeight = (maxY - minY + 1) * 112;
            var squareSize = PieceDefinitions.GetBoxSize(pieceType) * 112;
            var result = BuildMaskedBaseBodyTexture(pieceType, frameIndex, silhouetteMask, occupiedCells, minX, minY, targetWidth, targetHeight, squareSize);
            result.Apply();

            var outputPath = $"{GeneratedDir}/{pieceType}_frame{frameIndex + 1}.png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), outputPath), result.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter(outputPath, 112f, FilterMode.Point);
            return AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
        }

        private static Texture2D BuildMaskedBaseBodyTexture(
            PieceType pieceType,
            int frameIndex,
            SilhouetteMask silhouetteMask,
            IReadOnlyCollection<Vector2Int> occupiedCells,
            int minX,
            int minY,
            int targetWidth,
            int targetHeight,
            int squareSize)
        {
            var sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteSheets[frameIndex]);
            var bounds = PieceDefinitions.GetFrameBounds(pieceType, frameIndex);
            var isolated = CropBoundsTexture(sourceTexture, bounds);
            var result = new Texture2D(squareSize, squareSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[squareSize * squareSize];
            for (var i = 0; i < pixels.Length; i += 1)
            {
                pixels[i] = Color.clear;
            }
            result.SetPixels(pixels);

            for (var destY = 0; destY < targetHeight; destY += 1)
            {
                for (var destX = 0; destX < targetWidth; destX += 1)
                {
                    var u = destX / (float)Mathf.Max(1, targetWidth - 1);
                    var v = destY / (float)Mathf.Max(1, targetHeight - 1);
                    var sampleX = Mathf.Clamp(Mathf.RoundToInt((isolated.width - 1) * u), 0, isolated.width - 1);
                    var sampleYTop = Mathf.Clamp(Mathf.RoundToInt((isolated.height - 1) * v), 0, isolated.height - 1);
                    var color = GetPixelTopLeft(isolated, sampleX, sampleYTop);

                    var finalX = (minX * 112) + destX;
                    var finalYTop = (minY * 112) + destY;
                    var cellX = finalX / 112;
                    var cellY = finalYTop / 112;
                    if (occupiedCells.Contains(new Vector2Int(cellX, cellY)))
                    {
                        SetPixelTopLeft(result, finalX, finalYTop, color);
                    }
                }
            }

            return RetainConnectedPiece(result, occupiedCells.ToArray());
        }

        private static Texture2D CropBoundsTexture(Texture2D sourceTexture, RectInt bounds)
        {
            var output = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (var y = 0; y < bounds.height; y += 1)
            {
                for (var x = 0; x < bounds.width; x += 1)
                {
                    SetPixelTopLeft(output, x, y, GetPixelTopLeft(sourceTexture, bounds.x + x, bounds.y + y));
                }
            }

            output.Apply();
            return output;
        }

        private static SilhouetteMask GetSilhouetteMask(PieceType pieceType)
        {
            var sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteSheets[0]);
            var bounds = PieceDefinitions.GetFrameBounds(pieceType, 0);
            var outside = new byte[bounds.width * bounds.height];
            var queue = new Queue<Vector2Int>();

            void Enqueue(int x, int y)
            {
                var localIndex = (y * bounds.width) + x;
                if (outside[localIndex] != 0)
                {
                    return;
                }

                if (!IsNearBlack(GetPixelTopLeft(sourceTexture, bounds.x + x, bounds.y + y)))
                {
                    return;
                }

                outside[localIndex] = 1;
                queue.Enqueue(new Vector2Int(x, y));
            }

            for (var x = 0; x < bounds.width; x += 1)
            {
                Enqueue(x, 0);
                Enqueue(x, bounds.height - 1);
            }

            for (var y = 1; y < bounds.height - 1; y += 1)
            {
                Enqueue(0, y);
                Enqueue(bounds.width - 1, y);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.x > 0) Enqueue(current.x - 1, current.y);
                if (current.x + 1 < bounds.width) Enqueue(current.x + 1, current.y);
                if (current.y > 0) Enqueue(current.x, current.y - 1);
                if (current.y + 1 < bounds.height) Enqueue(current.x, current.y + 1);
            }

            var mask = new byte[outside.Length];
            for (var index = 0; index < outside.Length; index += 1)
            {
                mask[index] = outside[index] == 0 ? (byte)1 : (byte)0;
            }

            return new SilhouetteMask(bounds.width, bounds.height, mask);
        }

        private static Texture2D ApplySilhouetteMask(Texture2D sourceTexture, RectInt bounds, SilhouetteMask mask)
        {
            var output = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[bounds.width * bounds.height];
            for (var y = 0; y < bounds.height; y += 1)
            {
                var sampleY = Mathf.Min(mask.Height - 1, Mathf.Max(0, Mathf.FloorToInt(((y + 0.5f) * mask.Height) / bounds.height)));
                for (var x = 0; x < bounds.width; x += 1)
                {
                    var sampleX = Mathf.Min(mask.Width - 1, Mathf.Max(0, Mathf.FloorToInt(((x + 0.5f) * mask.Width) / bounds.width)));
                    if (mask.Data[(sampleY * mask.Width) + sampleX] == 0)
                    {
                        continue;
                    }

                    pixels[((bounds.height - 1 - y) * bounds.width) + x] = GetPixelTopLeft(sourceTexture, bounds.x + x, bounds.y + y);
                }
            }

            output.SetPixels(pixels);
            output.Apply();
            return output;
        }

        private static Texture2D RetainConnectedPiece(Texture2D source, IReadOnlyList<Vector2Int> definition)
        {
            var width = source.width;
            var height = source.height;
            var pixels = source.GetPixels();
            var keep = new byte[width * height];
            var queue = new Queue<int>();

            foreach (var cell in definition)
            {
                var seed = FindNearestOpaquePixel(pixels, width, height, cell.x * 112, cell.y * 112, 112);
                if (!seed.HasValue)
                {
                    continue;
                }

                var index = (seed.Value.y * width) + seed.Value.x;
                if (keep[index] != 0)
                {
                    continue;
                }

                keep[index] = 1;
                queue.Enqueue(index);
            }

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                var x = index % width;
                var y = index / width;

                for (var dy = -1; dy <= 1; dy += 1)
                {
                    for (var dx = -1; dx <= 1; dx += 1)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }

                        TryEnqueueOpaqueNeighbor(pixels, width, height, x + dx, y + dy, keep, queue);
                    }
                }
            }

            for (var index = 0; index < pixels.Length; index += 1)
            {
                if (pixels[index].a >= 0.094f && keep[index] == 0)
                {
                    pixels[index] = Color.clear;
                }
            }

            var output = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            output.SetPixels(pixels);
            output.Apply();
            return output;
        }

        private static Vector2Int? FindNearestOpaquePixel(Color[] pixels, int width, int height, int xStart, int yStartTop, int size)
        {
            var centerX = xStart + (size * 0.5f);
            var centerY = yStartTop + (size * 0.5f);
            Vector2Int? best = null;
            float bestScore = float.MaxValue;

            for (var y = yStartTop; y < yStartTop + size; y += 1)
            {
                for (var x = xStart; x < xStart + size; x += 1)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        continue;
                    }

                    var index = ToBottomLeftIndex(width, height, x, y);
                    if (pixels[index].a < 0.094f)
                    {
                        continue;
                    }

                    var dx = (x + 0.5f) - centerX;
                    var dy = (y + 0.5f) - centerY;
                    var score = (dx * dx) + (dy * dy);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new Vector2Int(x, height - 1 - y);
                    }
                }
            }

            return best;
        }

        private static void TryEnqueueOpaqueNeighbor(Color[] pixels, int width, int height, int x, int y, byte[] keep, Queue<int> queue)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            var index = (y * width) + x;
            if (keep[index] != 0 || pixels[index].a < 0.094f)
            {
                return;
            }

            keep[index] = 1;
            queue.Enqueue(index);
        }

        private static bool IsNearBlack(Color color)
        {
            return color.a > 0f && color.r < 0.047f && color.g < 0.047f && color.b < 0.047f;
        }

        private static Color GetPixelTopLeft(Texture2D texture, int x, int yTop)
        {
            if (x < 0 || yTop < 0 || x >= texture.width || yTop >= texture.height)
            {
                return Color.clear;
            }

            return texture.GetPixel(x, texture.height - 1 - yTop);
        }

        private static void SetPixelTopLeft(Texture2D texture, int x, int yTop, Color color)
        {
            if (x < 0 || yTop < 0 || x >= texture.width || yTop >= texture.height)
            {
                return;
            }

            texture.SetPixel(x, texture.height - 1 - yTop, color);
        }

        private static int ToBottomLeftIndex(int width, int height, int x, int yTop)
        {
            return ((height - 1 - yTop) * width) + x;
        }

        private static Material EnsureOutlineMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material)
            {
                return material;
            }

            var shader = Shader.Find("Sprites/Default");
            material = new Material(shader)
            {
                color = Color.white,
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void CreatePrefabs(Material material)
        {
            var pieceView = new GameObject("PieceView");
            pieceView.AddComponent<PieceSkin>();
            PrefabUtility.SaveAsPrefabAsset(pieceView, PieceViewPrefabPath);
            UnityEngine.Object.DestroyImmediate(pieceView);

            var cellView = new GameObject("CellView");
            cellView.AddComponent<SpriteRenderer>();
            PrefabUtility.SaveAsPrefabAsset(cellView, CellViewPrefabPath);
            UnityEngine.Object.DestroyImmediate(cellView);

            var outline = new GameObject("OutlineMesh");
            outline.AddComponent<MeshFilter>();
            var renderer = outline.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            outline.AddComponent<ImpactSettleSystem>();
            outline.AddComponent<BorderDeformSystem>();
            PrefabUtility.SaveAsPrefabAsset(outline, OutlinePrefabPath);
            UnityEngine.Object.DestroyImmediate(outline);
        }

        private static MonStackaAudioController CreateAudioController(string objectName)
        {
            var go = new GameObject(objectName);
            var controller = go.AddComponent<MonStackaAudioController>();
            SetPrivateField(controller, "backgroundMusic", AssetDatabase.LoadAssetAtPath<AudioClip>(BackgroundMusicPath));
            SetPrivateField(controller, "pieceBanks", BuildPieceAudioBanks());
            SetPrivateField(controller, "musicVolume", 0.34f);
            SetPrivateField(controller, "sfxVolume", 0.76f);
            SetPrivateField(controller, "playMusicOnAwake", true);
            return controller;
        }

        private static MonStackaAudioController.PieceAudioBank[] BuildPieceAudioBanks()
        {
            return Enum.GetValues(typeof(PieceType))
                .Cast<PieceType>()
                .Select(pieceType => new MonStackaAudioController.PieceAudioBank
                {
                    PieceType = pieceType,
                    ImpactClips = LoadAudioClips(pieceType, "impact"),
                    NeutralClips = LoadAudioClips(pieceType, "neutral"),
                })
                .ToArray();
        }

        private static AudioClip[] LoadAudioClips(PieceType pieceType, string family)
        {
            var prefix = pieceType.ToString().ToLowerInvariant();
            var clips = new List<AudioClip>();
            for (var index = 1; index <= 3; index += 1)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MonstosAudioDir}/{prefix}-{family}-{index}.mp3");
                if (clip)
                {
                    clips.Add(clip);
                }
            }

            return clips.ToArray();
        }

        private static void CreateScenes(PieceSkinData[] skins, BorderDeformTuningProfile tuning, Material material)
        {
            CreateBootScene();
            CreateHomeScene(skins, tuning, material);
            CreateGameScene(skins, tuning, material);
            CreateStorySelectScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(HomeScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
                new EditorBuildSettingsScene(StorySelectScenePath, true),
            };
        }

        private static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("BootLoader");
            boot.AddComponent<BootLoader>();
            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void CreateHomeScene(PieceSkinData[] skins, BorderDeformTuningProfile tuning, Material material)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateMainCamera(new Color(0.23f, 0.31f, 0.78f));
            CreateBackdrop("HomeBackground", AssetDatabase.LoadAssetAtPath<Texture2D>(HomeBackgroundPath));
            var canvas = CreateCanvas("HomeCanvas");
            BindCanvasToArtboard(camera, canvas);
            CreateEventSystem();
            var audioController = CreateAudioController("HomeAudio");
            CreateDitherOverlay(canvas.transform);

            var previewLeft = CreateWorldAnchor("PreviewLeftAnchor", 199.5f, 880.5f);
            var previewCenter = CreateWorldAnchor("PreviewCenterAnchor", 491f, 753.5f);
            var previewRight = CreateWorldAnchor("PreviewRightAnchor", 785.5f, 915f);

            var nameFill = CreatePanel(canvas.transform, "MonstosNameFill", new Rect(150f, 428f, 334f, 71f), new Color(0.54f, 0.49f, 1f, 1f));
            nameFill.GetComponent<Image>().raycastTarget = false;
            var nameText = CreateText(nameFill.transform, "MonstosName", new Rect(12f, 4f, 310f, 61f), 30, TextAnchor.MiddleCenter);
            nameText.text = string.Empty;
            nameText.color = Color.black;
            nameText.fontSize = 28;
            nameText.resizeTextForBestFit = false;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;

            var voiceButton = CreateTransparentButton(canvas.transform, "VoiceButton", new Rect(500f, 454f, 72f, 72f));
            var loreButton = CreateTransparentButton(canvas.transform, "LoreButton", new Rect(590f, 484f, 82f, 82f));
            var prevButton = CreateTransparentButton(canvas.transform, "PrevButton", new Rect(123f, 683f, 261f, 164f));
            var nextButton = CreateTransparentButton(canvas.transform, "NextButton", new Rect(588f, 684f, 276f, 164f));
            var settingsButton = CreateTransparentButton(canvas.transform, "SettingsButton", new Rect(1724f, 10f, 145f, 108f));
            var quitButton = CreateTransparentButton(canvas.transform, "QuitButton", new Rect(1720f, 118f, 150f, 118f));
            var arcadeButton = CreateTransparentButton(canvas.transform, "ArcadeButton", new Rect(1386f, 617f, 407f, 121f));
            var sprintButton = CreateTransparentButton(canvas.transform, "SprintButton", new Rect(1386f, 763f, 407f, 121f));
            var trainingButton = CreateTransparentButton(canvas.transform, "TrainingButton", new Rect(1386f, 907f, 407f, 121f));
            var storyButton = CreateButton(canvas.transform, "StoryButton", new Rect(1386f, 536f, 407f, 76f), "STORY MODE");
            var storyImage = storyButton.GetComponent<Image>();
            storyImage.color = new Color(0.32f, 0.12f, 0.3f, 0.94f);
            var storyLabel = storyButton.GetComponentInChildren<Text>();
            if (storyLabel)
            {
                storyLabel.fontSize = 28;
                storyLabel.color = new Color(0.97f, 0.9f, 0.95f, 1f);
            }
            ConfigureHomeNavigation(settingsButton, quitButton, storyButton, arcadeButton, sprintButton, trainingButton);

            var lorePanel = new GameObject("LorePanel");
            lorePanel.transform.SetParent(canvas.transform, false);
            ConfigureRect(lorePanel.AddComponent<RectTransform>(), new Rect(564f, 322f, 612f, 214f));
            var loreTail = CreatePanel(lorePanel.transform, "LoreTail", new Rect(184f, 142f, 56f, 56f), new Color(1f, 0.996f, 0.988f, 1f));
            loreTail.GetComponent<RectTransform>().localEulerAngles = new Vector3(0f, 0f, 45f);
            var loreTailOutline = loreTail.AddComponent<Outline>();
            loreTailOutline.effectColor = new Color(0.035f, 0.025f, 0.08f, 1f);
            loreTailOutline.effectDistance = new Vector2(5f, 5f);
            loreTail.GetComponent<Image>().raycastTarget = false;

            var loreSurface = CreatePanel(lorePanel.transform, "LoreSurface", new Rect(56f, 0f, 486f, 152f), new Color(1f, 0.996f, 0.988f, 1f));
            var loreSurfaceImage = loreSurface.GetComponent<Image>();
            loreSurfaceImage.raycastTarget = false;
            var loreSurfaceOutline = loreSurface.AddComponent<Outline>();
            loreSurfaceOutline.effectColor = new Color(0.035f, 0.025f, 0.08f, 1f);
            loreSurfaceOutline.effectDistance = new Vector2(5f, 5f);

            var loreText = CreateText(loreSurface.transform, "LoreText", new Rect(22f, 18f, 442f, 116f), 22, TextAnchor.MiddleCenter);
            loreText.color = new Color(0.06f, 0.05f, 0.1f, 1f);
            loreText.resizeTextForBestFit = true;
            loreText.resizeTextMinSize = 16;
            loreText.resizeTextMaxSize = 24;
            loreText.horizontalOverflow = HorizontalWrapMode.Wrap;
            loreText.verticalOverflow = VerticalWrapMode.Truncate;
            lorePanel.SetActive(false);

            var settingsPanel = CreatePanel(canvas.transform, "SettingsPanel", new Rect(500f, 128f, 920f, 820f), new Color(0.06f, 0.08f, 0.16f, 0.98f));
            settingsPanel.SetActive(false);
            var settingsTitle = CreateText(settingsPanel.transform, "SettingsTitle", new Rect(44f, 32f, 832f, 52f), 30, TextAnchor.MiddleLeft);
            settingsTitle.text = "Settings";
            var settingsBody = CreateText(settingsPanel.transform, "SettingsBody", new Rect(44f, 104f, 832f, 470f), 20, TextAnchor.UpperLeft);
            settingsBody.text = "Unity shell port in progress.\n\nThe current focus is restoring the original MonStacka layout, menu flow, and board shell around the upgraded visuals.";
            settingsBody.resizeTextForBestFit = true;
            settingsBody.resizeTextMinSize = 15;
            settingsBody.resizeTextMaxSize = 20;
            settingsBody.verticalOverflow = VerticalWrapMode.Truncate;
            var musicToggleButton = CreateButton(settingsPanel.transform, "MusicToggleButton", new Rect(44f, 596f, 160f, 48f), "Music ON");
            var musicDownButton = CreateButton(settingsPanel.transform, "MusicDownButton", new Rect(220f, 596f, 118f, 48f), "Music -");
            var musicUpButton = CreateButton(settingsPanel.transform, "MusicUpButton", new Rect(354f, 596f, 118f, 48f), "Music +");
            var sfxToggleButton = CreateButton(settingsPanel.transform, "SfxToggleButton", new Rect(44f, 654f, 160f, 48f), "SFX ON");
            var sfxDownButton = CreateButton(settingsPanel.transform, "SfxDownButton", new Rect(220f, 654f, 118f, 48f), "SFX -");
            var sfxUpButton = CreateButton(settingsPanel.transform, "SfxUpButton", new Rect(354f, 654f, 118f, 48f), "SFX +");
            var ditherToggleButton = CreateButton(settingsPanel.transform, "DitherToggleButton", new Rect(504f, 596f, 170f, 48f), "Dither ON");
            var controlPreviousButton = CreateButton(settingsPanel.transform, "ControlPreviousButton", new Rect(44f, 728f, 160f, 56f), "< Control");
            var controlBindButton = CreateButton(settingsPanel.transform, "ControlBindButton", new Rect(220f, 728f, 160f, 56f), "Bind Key");
            var controlNextButton = CreateButton(settingsPanel.transform, "ControlNextButton", new Rect(396f, 728f, 160f, 56f), "Control >");
            var controlResetButton = CreateButton(settingsPanel.transform, "ControlResetButton", new Rect(572f, 728f, 140f, 56f), "Defaults");
            var closeSettingsButton = CreateButton(settingsPanel.transform, "CloseSettingsButton", new Rect(728f, 728f, 148f, 56f), "Close");
            ConfigureSettingsNavigation(musicToggleButton, musicDownButton, musicUpButton, sfxToggleButton, sfxDownButton, sfxUpButton, ditherToggleButton, controlPreviousButton, controlBindButton, controlNextButton, controlResetButton, closeSettingsButton);

            var controllerGo = new GameObject("HomeMenuController");
            var controller = controllerGo.AddComponent<HomeMenuController>();
            SetPrivateField(controller, "previewLeftAnchor", previewLeft);
            SetPrivateField(controller, "previewCenterAnchor", previewCenter);
            SetPrivateField(controller, "previewRightAnchor", previewRight);
            SetPrivateField(controller, "monstosNameText", nameText);
            SetPrivateField(controller, "monstosVoiceButton", voiceButton);
            SetPrivateField(controller, "monstosLoreButton", loreButton);
            SetPrivateField(controller, "monstosLorePanel", lorePanel);
            SetPrivateField(controller, "monstosLoreText", loreText);
            SetPrivateField(controller, "prevButton", prevButton);
            SetPrivateField(controller, "nextButton", nextButton);
            SetPrivateField(controller, "startOgbmButton", arcadeButton);
            SetPrivateField(controller, "startSprintButton", sprintButton);
            SetPrivateField(controller, "startTrainingButton", trainingButton);
            SetPrivateField(controller, "startStoryButton", storyButton);
            SetPrivateField(controller, "settingsButton", settingsButton);
            SetPrivateField(controller, "quitButton", quitButton);
            SetPrivateField(controller, "settingsPanel", settingsPanel);
            SetPrivateField(controller, "musicToggleButton", musicToggleButton);
            SetPrivateField(controller, "musicDownButton", musicDownButton);
            SetPrivateField(controller, "musicUpButton", musicUpButton);
            SetPrivateField(controller, "sfxToggleButton", sfxToggleButton);
            SetPrivateField(controller, "sfxDownButton", sfxDownButton);
            SetPrivateField(controller, "sfxUpButton", sfxUpButton);
            SetPrivateField(controller, "ditherToggleButton", ditherToggleButton);
            SetPrivateField(controller, "controlPreviousButton", controlPreviousButton);
            SetPrivateField(controller, "controlBindButton", controlBindButton);
            SetPrivateField(controller, "controlNextButton", controlNextButton);
            SetPrivateField(controller, "controlResetButton", controlResetButton);
            SetPrivateField(controller, "closeSettingsButton", closeSettingsButton);
            SetPrivateField(controller, "audioController", audioController);
            SetPrivateField(controller, "pieceSkins", skins);
            SetPrivateField(controller, "outlineMaterial", material);
            SetPrivateField(controller, "deformTuning", tuning);

            EditorSceneManager.SaveScene(scene, HomeScenePath);
        }

        private static void CreateGameScene(PieceSkinData[] skins, BorderDeformTuningProfile tuning, Material material)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateMainCamera(new Color(0.08f, 0.09f, 0.16f));
            CreateBackdrop("GameBackground", AssetDatabase.LoadAssetAtPath<Texture2D>(GameBackgroundPath));
            var canvas = CreateCanvas("GameCanvas");
            BindCanvasToArtboard(camera, canvas);
            CreateEventSystem();
            var audioController = CreateAudioController("GameAudio");
            CreateDitherOverlay(canvas.transform);

            var managerGo = new GameObject("GameManager");
            var boardRoot = new GameObject("BoardRoot").transform;
            boardRoot.SetParent(managerGo.transform, false);
            boardRoot.localPosition = PixelToWorld(700f, 20f);

            var boardBackdropRoot = new GameObject("BoardBackdrop");
            boardBackdropRoot.transform.SetParent(boardRoot, false);
            var boardBackdrop = boardBackdropRoot.AddComponent<BoardBackdropView>();
            boardBackdrop.Initialize(PieceDefinitions.Columns, PieceDefinitions.VisibleRows, 0.52f);

            var boardClipRoot = new GameObject("BoardClipMask");
            boardClipRoot.transform.SetParent(boardRoot, false);
            var boardClipMask = boardClipRoot.AddComponent<BoardClipMask>();
            boardClipMask.Initialize(PieceDefinitions.Columns, PieceDefinitions.VisibleRows, 0.52f);

            var stackRoot = new GameObject("StackView").transform;
            stackRoot.SetParent(boardRoot, false);
            var activeRoot = new GameObject("ActivePieceView").transform;
            activeRoot.SetParent(boardRoot, false);

            // Previews sit slightly in front of the canvas plane (z=0) so the slot frame
            // images can never draw over them.
            var holdRoot = new GameObject("HoldBox");
            holdRoot.transform.position = PixelToWorld(230f, 710f) + new Vector3(0f, 0f, -9.5f);
            var hold = holdRoot.AddComponent<HoldBoxView>();

            var nextRoot = new GameObject("NextQueue");
            nextRoot.transform.position = PixelToWorld(430f, 640f) + new Vector3(0f, 0f, -9.5f);
            var next = nextRoot.AddComponent<NextQueueView>();

            var primaryPanel = CreatePanel(canvas.transform, "PrimaryPanelBackdrop", new Rect(92f, 142f, 510f, 818f), new Color(0.08f, 0.10f, 0.18f, 0.64f));
            primaryPanel.GetComponent<Image>().raycastTarget = false;
            var boardPanel = CreatePanel(canvas.transform, "BoardPanelBackdrop", new Rect(700f, 20f, 520f, 1040f), new Color(0.08f, 0.10f, 0.18f, 0.36f));
            boardPanel.GetComponent<Image>().raycastTarget = false;
            var secondaryPanel = CreatePanel(canvas.transform, "SecondaryPanelBackdrop", new Rect(1310f, 142f, 498f, 818f), new Color(0.08f, 0.10f, 0.18f, 0.64f));
            secondaryPanel.GetComponent<Image>().raycastTarget = false;

            var modeDescription = CreateText(canvas.transform, "ModeDescription", new Rect(126f, 184f, 438f, 78f), 21, TextAnchor.UpperLeft);
            var scoreText = CreateText(canvas.transform, "ScoreValue", new Rect(126f, 342f, 126f, 50f), 32, TextAnchor.MiddleLeft);
            var goalValueText = CreateText(canvas.transform, "GoalValue", new Rect(286f, 342f, 162f, 50f), 32, TextAnchor.MiddleLeft);
            var linesText = CreateText(canvas.transform, "LinesValue", new Rect(486f, 342f, 70f, 50f), 32, TextAnchor.MiddleRight);
            var timerText = CreateText(canvas.transform, "TimerValue", new Rect(126f, 460f, 246f, 54f), 32, TextAnchor.MiddleLeft);
            var leaderboardTitle = CreateText(canvas.transform, "LeaderboardTitle", new Rect(1340f, 200f, 250f, 36f), 22, TextAnchor.MiddleLeft);
            // Status sits below the three leaderboard rows (rows end at ~y=442).
            var statusText = CreateText(canvas.transform, "StatusText", new Rect(1340f, 496f, 420f, 130f), 22, TextAnchor.UpperLeft);
            var assistText = CreateText(canvas.transform, "AssistText", new Rect(126f, 536f, 392f, 36f), 22, TextAnchor.MiddleLeft);
            assistText.color = new Color(0.62f, 0.93f, 0.78f, 1f);

            modeDescription.resizeTextForBestFit = false;
            modeDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            modeDescription.verticalOverflow = VerticalWrapMode.Truncate;
            goalValueText.resizeTextForBestFit = false;
            goalValueText.fontSize = 30;
            goalValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            goalValueText.verticalOverflow = VerticalWrapMode.Truncate;
            leaderboardTitle.resizeTextForBestFit = false;
            leaderboardTitle.fontSize = 22;
            leaderboardTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            leaderboardTitle.verticalOverflow = VerticalWrapMode.Truncate;
            statusText.resizeTextForBestFit = false;
            statusText.fontSize = 22;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Truncate;

            CreateText(canvas.transform, "ScoreLabel", new Rect(126f, 300f, 120f, 28f), 20, TextAnchor.MiddleLeft).text = "Score";
            CreateText(canvas.transform, "GoalLabel", new Rect(286f, 300f, 120f, 28f), 20, TextAnchor.MiddleLeft).text = "Goal";
            CreateText(canvas.transform, "LinesLabel", new Rect(486f, 300f, 70f, 28f), 20, TextAnchor.MiddleRight).text = "Lines";
            CreateText(canvas.transform, "TimeLabel", new Rect(126f, 420f, 120f, 28f), 20, TextAnchor.MiddleLeft).text = "Time";
            CreateText(canvas.transform, "HoldLabel", new Rect(154f, 560f, 132f, 28f), 21, TextAnchor.MiddleCenter).text = "Hold";
            CreateText(canvas.transform, "NextLabel", new Rect(358f, 560f, 144f, 28f), 21, TextAnchor.MiddleCenter).text = "Next";

            // Framed world-space slots sit behind the world-space hold/next previews.
            CreateWorldSlotFrame("HoldSlotFrame", 230f, 710f, 154f);
            for (var slot = 0; slot < 3; slot += 1)
            {
                CreateWorldSlotFrame($"NextSlotFrame{slot + 1}", 430f, 640f + (slot * 132f), 124f);
            }

            var leaderboardValues = CreateLeaderboardPlaceholders(canvas.transform);

            var settingsButton = CreateTransparentButton(canvas.transform, "GameSettingsButton", new Rect(1760f, 0f, 140f, 116f));
            var quitButton = CreateTransparentButton(canvas.transform, "GameQuitButton", new Rect(1754f, 94f, 148f, 126f));
            var homeButton = CreateTransparentButton(canvas.transform, "GameHomeButton", new Rect(1760f, 190f, 140f, 118f));
            ConfigurePauseNavigation(settingsButton, quitButton, homeButton);

            var settingsPanel = CreatePanel(canvas.transform, "GameSettingsPanel", new Rect(500f, 128f, 920f, 820f), new Color(0.06f, 0.08f, 0.16f, 0.98f));
            settingsPanel.SetActive(false);
            var settingsTitle = CreateText(settingsPanel.transform, "SettingsTitle", new Rect(44f, 32f, 832f, 52f), 30, TextAnchor.MiddleLeft);
            settingsTitle.text = "Settings";
            var settingsBody = CreateText(settingsPanel.transform, "SettingsBody", new Rect(44f, 104f, 832f, 470f), 20, TextAnchor.UpperLeft);
            settingsBody.text = "Gameplay shell restored around the new visuals.\n\nMore detailed settings parity is still being ported into Unity.";
            settingsBody.resizeTextForBestFit = true;
            settingsBody.resizeTextMinSize = 15;
            settingsBody.resizeTextMaxSize = 20;
            settingsBody.verticalOverflow = VerticalWrapMode.Truncate;
            var musicToggleButton = CreateButton(settingsPanel.transform, "MusicToggleButton", new Rect(44f, 596f, 160f, 48f), "Music ON");
            var musicDownButton = CreateButton(settingsPanel.transform, "MusicDownButton", new Rect(220f, 596f, 118f, 48f), "Music -");
            var musicUpButton = CreateButton(settingsPanel.transform, "MusicUpButton", new Rect(354f, 596f, 118f, 48f), "Music +");
            var sfxToggleButton = CreateButton(settingsPanel.transform, "SfxToggleButton", new Rect(44f, 654f, 160f, 48f), "SFX ON");
            var sfxDownButton = CreateButton(settingsPanel.transform, "SfxDownButton", new Rect(220f, 654f, 118f, 48f), "SFX -");
            var sfxUpButton = CreateButton(settingsPanel.transform, "SfxUpButton", new Rect(354f, 654f, 118f, 48f), "SFX +");
            var ditherToggleButton = CreateButton(settingsPanel.transform, "DitherToggleButton", new Rect(504f, 596f, 170f, 48f), "Dither ON");
            var controlPreviousButton = CreateButton(settingsPanel.transform, "ControlPreviousButton", new Rect(44f, 728f, 160f, 56f), "< Control");
            var controlBindButton = CreateButton(settingsPanel.transform, "ControlBindButton", new Rect(220f, 728f, 160f, 56f), "Bind Key");
            var controlNextButton = CreateButton(settingsPanel.transform, "ControlNextButton", new Rect(396f, 728f, 160f, 56f), "Control >");
            var controlResetButton = CreateButton(settingsPanel.transform, "ControlResetButton", new Rect(572f, 728f, 140f, 56f), "Defaults");
            var closeSettingsButton = CreateButton(settingsPanel.transform, "CloseSettingsButton", new Rect(728f, 728f, 148f, 56f), "Close");
            ConfigureSettingsNavigation(musicToggleButton, musicDownButton, musicUpButton, sfxToggleButton, sfxDownButton, sfxUpButton, ditherToggleButton, controlPreviousButton, controlBindButton, controlNextButton, controlResetButton, closeSettingsButton);

            var pauseRoot = new GameObject("PauseOverlay");
            pauseRoot.transform.SetParent(canvas.transform, false);
            ConfigureRect(pauseRoot.AddComponent<RectTransform>(), new Rect(0f, 0f, ArtboardWidth, ArtboardHeight));
            var pausePanel = CreatePanel(pauseRoot.transform, "PausePanel", new Rect(760f, 430f, 400f, 116f), new Color(0.06f, 0.08f, 0.16f, 0.92f));
            var pauseLabel = CreateText(pausePanel.transform, "PauseLabel", new Rect(0f, 0f, 400f, 116f), 34, TextAnchor.MiddleCenter);
            pauseLabel.text = "PAUSED";
            pauseRoot.SetActive(false);
            var pauseOverlay = pauseRoot.AddComponent<PauseOverlay>();
            SetPrivateField(pauseOverlay, "root", pauseRoot);

            var hud = canvas.gameObject.AddComponent<HUDController>();
            SetPrivateField(hud, "modeDescriptionText", modeDescription);
            SetPrivateField(hud, "scoreText", scoreText);
            SetPrivateField(hud, "goalValueText", goalValueText);
            SetPrivateField(hud, "linesText", linesText);
            SetPrivateField(hud, "timeText", timerText);
            SetPrivateField(hud, "leaderboardTitleText", leaderboardTitle);
            SetPrivateField(hud, "statusText", statusText);
            SetPrivateField(hud, "assistText", assistText);
            SetPrivateField(hud, "leaderboardValueTexts", leaderboardValues);

            var manager = managerGo.AddComponent<GameManager>();
            SetPrivateField(manager, "gravitySeconds", 0.65f);
            SetPrivateField(manager, "lockDelaySeconds", 0.25f);
            SetPrivateField(manager, "cellWorldSize", 0.52f);
            SetPrivateField(manager, "homeSceneName", "Home");
            SetPrivateField(manager, "boardRoot", boardRoot);
            SetPrivateField(manager, "stackRoot", stackRoot);
            SetPrivateField(manager, "activeRoot", activeRoot);
            SetPrivateField(manager, "holdBoxView", hold);
            SetPrivateField(manager, "nextQueueView", next);
            SetPrivateField(manager, "hudController", hud);
            SetPrivateField(manager, "pauseOverlay", pauseOverlay);
            SetPrivateField(manager, "outlineMaterial", material);
            SetPrivateField(manager, "audioController", audioController);
            SetPrivateField(manager, "deformTuning", tuning);
            SetPrivateField(manager, "pieceSkins", skins);
            SetPrivateField(manager, "useThreePieceVerticalSlice", false);

            var dialoguePresenter = CreateDialoguePanel(canvas.transform);
            SetPrivateField(manager, "dialoguePresenter", dialoguePresenter);

            var shell = canvas.gameObject.AddComponent<GameSceneShellController>();
            SetPrivateField(shell, "gameManager", manager);
            SetPrivateField(shell, "settingsButton", settingsButton);
            SetPrivateField(shell, "quitButton", quitButton);
            SetPrivateField(shell, "homeButton", homeButton);
            SetPrivateField(shell, "settingsPanel", settingsPanel);
            SetPrivateField(shell, "musicToggleButton", musicToggleButton);
            SetPrivateField(shell, "musicDownButton", musicDownButton);
            SetPrivateField(shell, "musicUpButton", musicUpButton);
            SetPrivateField(shell, "sfxToggleButton", sfxToggleButton);
            SetPrivateField(shell, "sfxDownButton", sfxDownButton);
            SetPrivateField(shell, "sfxUpButton", sfxUpButton);
            SetPrivateField(shell, "ditherToggleButton", ditherToggleButton);
            SetPrivateField(shell, "controlPreviousButton", controlPreviousButton);
            SetPrivateField(shell, "controlBindButton", controlBindButton);
            SetPrivateField(shell, "controlNextButton", controlNextButton);
            SetPrivateField(shell, "controlResetButton", controlResetButton);
            SetPrivateField(shell, "closeSettingsButton", closeSettingsButton);

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void CreateSlotFrame(Transform parent, string objectName, float centerX, float centerY, float size)
        {
            // Near-transparent fill: hold/next preview sprites render behind the canvas,
            // so an opaque fill would hide them (only their edges peeked out).
            var frame = CreatePanel(parent, objectName, new Rect(centerX - (size * 0.5f), centerY - (size * 0.5f), size, size), new Color(0.13f, 0.16f, 0.3f, 0.22f));
            frame.GetComponent<Image>().raycastTarget = false;
            var outline = frame.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.34f, 0.55f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);
        }

        private static void CreateWorldSlotFrame(string objectName, float centerX, float centerY, float size)
        {
            var fillSize = size / PixelsPerUnit;
            var edgeSize = 3f / PixelsPerUnit;
            var center = PixelToWorld(centerX, centerY);
            center.z = -9.55f;

            CreateWorldPanelAt($"{objectName}_Fill", center, new Vector2(fillSize, fillSize), new Color(0.13f, 0.16f, 0.3f, 0.26f), -28);

            CreateWorldPanelAt($"{objectName}_Top", center + new Vector3(0f, fillSize * 0.5f, 0f), new Vector2(fillSize, edgeSize), new Color(0.34f, 0.38f, 0.6f, 0.92f), -27);
            CreateWorldPanelAt($"{objectName}_Bottom", center + new Vector3(0f, -fillSize * 0.5f, 0f), new Vector2(fillSize, edgeSize), new Color(0.34f, 0.38f, 0.6f, 0.92f), -27);
            CreateWorldPanelAt($"{objectName}_Left", center + new Vector3(-fillSize * 0.5f, 0f, 0f), new Vector2(edgeSize, fillSize), new Color(0.34f, 0.38f, 0.6f, 0.92f), -27);
            CreateWorldPanelAt($"{objectName}_Right", center + new Vector3(fillSize * 0.5f, 0f, 0f), new Vector2(edgeSize, fillSize), new Color(0.34f, 0.38f, 0.6f, 0.92f), -27);
        }

        private static DialoguePresenter CreateDialoguePanel(Transform canvasTransform)
        {
            var holder = new GameObject("DialoguePresenter");
            holder.transform.SetParent(canvasTransform, false);
            var holderRect = holder.AddComponent<RectTransform>();
            ConfigureRect(holderRect, new Rect(0f, 0f, ArtboardWidth, ArtboardHeight));

            var panel = CreatePanel(holder.transform, "DialoguePanel", new Rect(160f, 730f, 1600f, 310f), new Color(0.04f, 0.05f, 0.1f, 0.96f));
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.45f, 0.25f, 0.5f, 1f);
            panelOutline.effectDistance = new Vector2(3f, 3f);

            var speaker = CreateText(panel.transform, "SpeakerText", new Rect(40f, 26f, 500f, 40f), 26, TextAnchor.MiddleLeft);
            speaker.color = new Color(0.85f, 0.65f, 0.9f, 1f);
            var line = CreateText(panel.transform, "LineText", new Rect(40f, 80f, 1520f, 160f), 28, TextAnchor.UpperLeft);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Truncate;
            var hint = CreateText(panel.transform, "ContinueHint", new Rect(1080f, 256f, 480f, 36f), 20, TextAnchor.MiddleRight);
            hint.color = new Color(0.6f, 0.62f, 0.7f, 1f);

            var presenter = holder.AddComponent<DialoguePresenter>();
            SetPrivateField(presenter, "root", panel);
            SetPrivateField(presenter, "speakerText", speaker);
            SetPrivateField(presenter, "lineText", line);
            SetPrivateField(presenter, "continueHintText", hint);
            panel.SetActive(false);
            return presenter;
        }

        private static void CreateStorySelectScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateMainCamera(new Color(0.07f, 0.06f, 0.12f));
            var canvas = CreateCanvas("StorySelectCanvas");
            BindCanvasToArtboard(camera, canvas);
            CreateEventSystem();

            var header = CreateText(canvas.transform, "Header", new Rect(120f, 50f, 1400f, 90f), 30, TextAnchor.UpperLeft);
            header.horizontalOverflow = HorizontalWrapMode.Wrap;

            var listPanel = CreatePanel(canvas.transform, "ChapterListPanel", new Rect(100f, 170f, 1500f, 840f), new Color(0.09f, 0.1f, 0.18f, 0.85f));
            listPanel.GetComponent<Image>().raycastTarget = false;
            var listRoot = new GameObject("ChapterList");
            listRoot.transform.SetParent(listPanel.transform, false);
            var listRect = listRoot.AddComponent<RectTransform>();
            ConfigureRect(listRect, new Rect(30f, 60f, 1440f, 760f));

            var legend = CreateText(listPanel.transform, "Legend", new Rect(30f, 14f, 1200f, 34f), 18, TextAnchor.MiddleLeft);
            legend.text = "* dialogue is a generated draft pending your review";
            legend.color = new Color(0.55f, 0.56f, 0.64f, 1f);

            var backButton = CreateButton(canvas.transform, "BackButton", new Rect(1640f, 60f, 180f, 64f), "Back");

            var controllerGo = new GameObject("StorySelectController");
            var controller = controllerGo.AddComponent<StorySelectController>();
            SetPrivateField(controller, "listRoot", (Transform)listRect);
            SetPrivateField(controller, "backButton", backButton);
            SetPrivateField(controller, "headerText", header);

            EditorSceneManager.SaveScene(scene, StorySelectScenePath);
        }

        private static Camera CreateMainCamera(Color backgroundColor)
        {
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraHalfHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static void BindCanvasToArtboard(Camera camera, Canvas canvas)
        {
            var controller = camera.gameObject.AddComponent<ArtboardViewportController>();
            SetPrivateField(controller, "canvases", new[] { canvas });
        }

        private static void CreateBackgroundSprite(string objectName, Sprite sprite)
        {
            var background = new GameObject(objectName);
            var renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -100;
            background.transform.position = new Vector3(0f, 0f, 5f);
        }

        private static void CreateBackdrop(string objectName, Texture2D texture)
        {
            if (!texture)
            {
                return;
            }

            var go = new GameObject(objectName);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -1000;
            var backdrop = go.AddComponent<FullScreenBackdrop>();
            SetPrivateField(backdrop, "texture", texture);
        }

        private static void CreateWorldPanel(string objectName, Rect rect, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.position = PixelToWorld(rect.x + (rect.width * 0.5f), rect.y + (rect.height * 0.5f));
            var panel = go.AddComponent<WorldPanelBackdrop>();
            SetPrivateField(panel, "size", new Vector2(rect.width / PixelsPerUnit, rect.height / PixelsPerUnit));
            SetPrivateField(panel, "color", color);
            SetPrivateField(panel, "sortingOrder", sortingOrder);
        }

        private static void CreateWorldPanelAt(string objectName, Vector3 position, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.position = position;
            var panel = go.AddComponent<WorldPanelBackdrop>();
            SetPrivateField(panel, "size", size);
            SetPrivateField(panel, "color", color);
            SetPrivateField(panel, "sortingOrder", sortingOrder);
        }

        private static void CreateEventSystem()
        {
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }

        private static void ConfigureHomeNavigation(Button settingsButton, Button quitButton, Button storyButton, Button arcadeButton, Button sprintButton, Button trainingButton)
        {
            SetExplicitNavigation(settingsButton, null, quitButton, null, null);
            SetExplicitNavigation(quitButton, settingsButton, storyButton, null, null);
            SetExplicitNavigation(storyButton, quitButton, arcadeButton, null, null);
            SetExplicitNavigation(arcadeButton, storyButton, sprintButton, null, null);
            SetExplicitNavigation(sprintButton, arcadeButton, trainingButton, null, null);
            SetExplicitNavigation(trainingButton, sprintButton, null, null, null);
        }

        private static void ConfigurePauseNavigation(Button settingsButton, Button quitButton, Button homeButton)
        {
            SetExplicitNavigation(settingsButton, null, quitButton, null, null);
            SetExplicitNavigation(quitButton, settingsButton, homeButton, null, null);
            SetExplicitNavigation(homeButton, quitButton, null, null, null);
        }

        private static void ConfigureSettingsNavigation(params Button[] buttons)
        {
            for (var index = 0; index < buttons.Length; index += 1)
            {
                var up = index > 0 ? buttons[index - 1] : null;
                var down = index < buttons.Length - 1 ? buttons[index + 1] : null;
                SetExplicitNavigation(buttons[index], up, down, null, null);
            }
        }

        private static void SetExplicitNavigation(Button button, Button selectOnUp, Button selectOnDown, Button selectOnLeft, Button selectOnRight)
        {
            if (!button)
            {
                return;
            }

            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = selectOnUp;
            navigation.selectOnDown = selectOnDown;
            navigation.selectOnLeft = selectOnLeft;
            navigation.selectOnRight = selectOnRight;
            button.navigation = navigation;
        }

        private static Canvas CreateCanvas(string objectName)
        {
            var canvasGo = new GameObject(objectName);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ArtboardWidth, ArtboardHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static GameObject CreatePanel(Transform parent, string objectName, Rect rect, Color color)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            ConfigureRect(go.GetComponent<RectTransform>(), rect);
            return go;
        }

        private static DitherOverlay CreateDitherOverlay(Transform parent)
        {
            var go = CreatePanel(parent, "DitherOverlay", new Rect(0f, 0f, ArtboardWidth, ArtboardHeight), Color.white);
            go.GetComponent<Image>().raycastTarget = false;
            return go.AddComponent<DitherOverlay>();
        }

        private static Text CreateText(Transform parent, string objectName, Rect rect, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            ConfigureRect(text.rectTransform, rect);
            return text;
        }

        private static Button CreateTransparentButton(Transform parent, string objectName, Rect rect)
        {
            var button = CreateButton(parent, objectName, rect, string.Empty);
            var image = button.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            var label = button.GetComponentInChildren<Text>();
            if (label)
            {
                label.text = string.Empty;
            }
            return button;
        }

        private static Button CreateButton(Transform parent, string objectName, Rect rect, string label)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.42f, 0.92f);
            var button = go.AddComponent<Button>();
            ConfigureRect(go.GetComponent<RectTransform>(), rect);

            var text = CreateText(go.transform, "Label", new Rect(0f, 0f, rect.width, rect.height), 24, TextAnchor.MiddleCenter);
            text.text = label;
            return button;
        }

        private static Text[] CreateLeaderboardPlaceholders(Transform parent)
        {
            var rowTop = 264f;
            var values = new Text[3];
            for (var index = 0; index < 3; index += 1)
            {
                var y = rowTop + (index * 74f);
                var rank = CreateText(parent, $"Rank{index + 1}", new Rect(1328f, y, 42f, 30f), 26, TextAnchor.MiddleLeft);
                rank.text = $"{index + 1}.";
                var value = CreateText(parent, $"LeaderboardValue{index + 1}", new Rect(1372f, y, 240f, 30f), 22, TextAnchor.MiddleLeft);
                value.text = "---";
                values[index] = value;
            }

            return values;
        }

        private static void ConfigureRect(RectTransform rectTransform, Rect rect)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(rect.x, -rect.y);
            rectTransform.sizeDelta = new Vector2(rect.width, rect.height);
        }

        private static Transform CreateWorldAnchor(string objectName, float pixelX, float pixelY)
        {
            var anchor = new GameObject(objectName).transform;
            anchor.position = PixelToWorld(pixelX, pixelY);
            return anchor;
        }

        private static Vector3 PixelToWorld(float pixelX, float pixelY)
        {
            return new Vector3(
                (pixelX - (ArtboardWidth * 0.5f)) / PixelsPerUnit,
                ((ArtboardHeight * 0.5f) - pixelY) / PixelsPerUnit,
                0f
            );
        }

        private static void SetPrivateField<TTarget, TValue>(TTarget target, string fieldName, TValue value)
        {
            var field = typeof(TTarget).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
