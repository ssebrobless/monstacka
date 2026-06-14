using System.Collections.Generic;
using System.Linq;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    public sealed class PieceSkin : MonoBehaviour
    {
        private static readonly int[] FrameSequence = { 0, 1, 2, 1, 0 };
        private static readonly int[,] Bayer8 =
        {
            { 0, 48, 12, 60, 3, 51, 15, 63 },
            { 32, 16, 44, 28, 35, 19, 47, 31 },
            { 8, 56, 4, 52, 11, 59, 7, 55 },
            { 40, 24, 36, 20, 43, 27, 39, 23 },
            { 2, 50, 14, 62, 1, 49, 13, 61 },
            { 34, 18, 46, 30, 33, 17, 45, 29 },
            { 10, 58, 6, 54, 9, 57, 5, 53 },
            { 42, 26, 38, 22, 41, 25, 37, 21 },
        };
        private static readonly Dictionary<string, Sprite> DitherShadowSpriteCache = new();

        [SerializeField] private float frameAnimSpeed = 0.35f;

        private PieceSkinData skinData;
        private PieceType pieceType;
        private int rotation;
        private float cellWorldSize;
        private bool animateBody = true;
        private bool enableFacialAnimation = true;
        private readonly List<Vector2Int> localCells = new();
        private ConnectedBodyBuildResult bodyBuild;
        private SpriteBorderPulseSystem borderPulseSystem;
        private FacialPartAnimator facialPartAnimator;
        private SpriteRenderer ditherShadowRenderer;
        private Sprite[] ditherShadowSprites;
        private int lastFrameIndex = -1;
        private bool previewOnly;

        public int PieceId { get; set; }
        public bool UsesBorderPulse => borderPulseSystem != null;
        public bool BodyBuildUsesFullBoxSprite => bodyBuild?.UsesFullBoxSprite ?? false;
        public bool RequiresManualUpdate =>
            animateBody ||
            borderPulseSystem != null ||
            (facialPartAnimator != null && facialPartAnimator.Animates);

        public void Initialize(
            PieceSkinData data,
            PieceType type,
            int currentRotation,
            IReadOnlyCollection<Vector2Int> cells,
            IReadOnlyCollection<Vector2Int> sourceCells,
            float worldCellSize,
            Material outlineMaterial,
            BorderDeformTuningProfile deformTuning,
            bool isPreviewOnly,
            float pulseScale,
            bool shouldAnimateBody = true,
            bool shouldEnableFacialAnimation = true,
            bool useFullBoxSprite = false)
        {
            if (!deformTuning)
            {
                deformTuning = ScriptableObject.CreateInstance<BorderDeformTuningProfile>();
            }

            skinData = data;
            pieceType = type;
            rotation = currentRotation;
            cellWorldSize = worldCellSize;
            previewOnly = isPreviewOnly;
            animateBody = shouldAnimateBody;
            var visualExtrasEnabled = MonStackaAppState.VisualExtrasEnabled;
            var effectivePulseScale = visualExtrasEnabled ? pulseScale : 0f;
            enableFacialAnimation = visualExtrasEnabled && shouldEnableFacialAnimation;
            localCells.Clear();
            localCells.AddRange(cells);

            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var bodyRoot = new GameObject("Body");
            bodyRoot.transform.SetParent(transform, false);
            bodyRoot.transform.localScale = new Vector3(cellWorldSize, cellWorldSize, 1f);
            bodyBuild = ConnectedBodyBuilder.Build(bodyRoot.transform, skinData, pieceType, rotation, localCells, cellWorldSize, previewOnly, useFullBoxSprite, sourceCells);
            CreateDitherShadow(bodyBuild.RendererHost);

            if (effectivePulseScale > 0.001f)
            {
                var borderGo = new GameObject("BorderPulse");
                borderGo.transform.SetParent(bodyBuild.RendererHost, false);
                borderPulseSystem = borderGo.AddComponent<SpriteBorderPulseSystem>();
                borderPulseSystem.Initialize(
                    bodyBuild.RendererHost,
                    bodyBuild.BodyFrameSprites,
                    localCells,
                    deformTuning,
                    isPreviewOnly,
                    effectivePulseScale,
                    PieceDefinitions.PieceColors[pieceType]
                );
            }

            // Bodies are featureless now (PSD layer split), so every view - previews and
            // holds included - needs the feature overlay; only live pieces animate it.
            facialPartAnimator = null;
            var facialGo = new GameObject("FacialParts");
            facialGo.transform.SetParent(transform, false);
            facialPartAnimator = facialGo.AddComponent<FacialPartAnimator>();
            facialPartAnimator.Initialize(
                skinData,
                pieceType,
                rotation,
                localCells,
                cellWorldSize,
                bodyBuild.UsesFullBoxSprite,
                enableFacialAnimation
            );
            ApplyMaskInteraction();
            SetFrameIndex(0);
        }

        public void SetNeighborMap(NeighborMap neighborMap)
        {
            if (borderPulseSystem == null)
            {
                return;
            }

            var origin = new Vector2Int(Mathf.RoundToInt(transform.localPosition.x / cellWorldSize), Mathf.RoundToInt((-transform.localPosition.y / cellWorldSize) + PieceDefinitions.HiddenRows));
            borderPulseSystem.ApplyNeighborMap(neighborMap, origin, PieceId);
        }

        public void ManualUpdate(float now)
        {
            if (animateBody)
            {
                var frameStep = Mathf.Clamp(Mathf.FloorToInt(now / Mathf.Max(0.01f, frameAnimSpeed)), 0, int.MaxValue);
                var frameIndex = FrameSequence[frameStep % FrameSequence.Length];
                if (frameIndex != lastFrameIndex)
                {
                    SetFrameIndex(frameIndex);
                }
            }
            else if (lastFrameIndex != 0)
            {
                SetFrameIndex(0);
            }

            facialPartAnimator?.ManualUpdate(now);
            borderPulseSystem?.ManualUpdate(now);
            UpdateDitherShadowVisibility();
        }

        public void TriggerImpact(IReadOnlyList<Vector2Int> absoluteCells, int hiddenRows)
        {
            if (borderPulseSystem == null)
            {
                return;
            }

            var impactedEdges = new List<EdgeKey>();
            foreach (var cell in localCells)
            {
                if (localCells.Contains(cell + Vector2Int.down))
                {
                    continue;
                }

                impactedEdges.Add(new EdgeKey(cell, EdgeDirection.Down));
            }
            borderPulseSystem.TriggerImpact(impactedEdges);
        }

        private void SetFrameIndex(int frameIndex)
        {
            if (bodyBuild?.BodyRenderer == null || bodyBuild.BodyFrameSprites == null || bodyBuild.BodyFrameSprites.Length == 0)
            {
                return;
            }

            bodyBuild.BodyRenderer.sprite = bodyBuild.BodyFrameSprites[Mathf.Clamp(frameIndex, 0, bodyBuild.BodyFrameSprites.Length - 1)];
            if (ditherShadowRenderer != null && ditherShadowSprites != null && ditherShadowSprites.Length > 0)
            {
                ditherShadowRenderer.sprite = ditherShadowSprites[Mathf.Clamp(frameIndex, 0, ditherShadowSprites.Length - 1)];
            }

            borderPulseSystem?.SetFrameIndex(frameIndex);
            UpdateDitherShadowVisibility();

            lastFrameIndex = frameIndex;
        }

        private void CreateDitherShadow(Transform parent)
        {
            if (bodyBuild?.BodyFrameSprites == null || bodyBuild.BodyFrameSprites.Length == 0 || parent == null)
            {
                return;
            }

            var shadowGo = new GameObject("DitherShadow");
            shadowGo.transform.SetParent(parent, false);
            shadowGo.transform.localPosition = new Vector3(0.065f, -0.075f, 0.025f);
            ditherShadowRenderer = shadowGo.AddComponent<SpriteRenderer>();
            ditherShadowRenderer.sortingOrder = -2;
            ditherShadowRenderer.color = Color.white;

            ditherShadowSprites = new Sprite[bodyBuild.BodyFrameSprites.Length];
            for (var index = 0; index < bodyBuild.BodyFrameSprites.Length; index += 1)
            {
                ditherShadowSprites[index] = GetOrCreateDitherShadowSprite(bodyBuild.BodyFrameSprites[index], pieceType, rotation, index);
            }
        }

        private void UpdateDitherShadowVisibility()
        {
            if (ditherShadowRenderer)
            {
                ditherShadowRenderer.enabled = MonStackaAppState.DitherEnabled && MonStackaAppState.VisualExtrasEnabled;
            }
        }

        private static Sprite GetOrCreateDitherShadowSprite(Sprite source, PieceType type, int rotation, int frameIndex)
        {
            var cacheKey = $"{source.GetInstanceID()}:{type}:{rotation}:{frameIndex}:depth-shadow-v2";
            if (DitherShadowSpriteCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var rect = source.rect;
            var width = Mathf.RoundToInt(rect.width);
            var height = Mathf.RoundToInt(rect.height);
            var startX = Mathf.RoundToInt(rect.x);
            var startY = Mathf.RoundToInt(rect.y);
            var sourcePixels = source.texture.GetPixels(startX, startY, width, height);
            var outputPixels = new Color[sourcePixels.Length];

            for (var y = 0; y < height; y += 1)
            {
                var vertical = height <= 1 ? 0f : y / (float)(height - 1);
                for (var x = 0; x < width; x += 1)
                {
                    var index = (y * width) + x;
                    var alpha = sourcePixels[index].a;
                    var silhouetteAlpha = Mathf.Max(alpha, SampleNeighborAlpha(sourcePixels, width, height, x, y));
                    if (silhouetteAlpha < 0.06f)
                    {
                        outputPixels[index] = Color.clear;
                        continue;
                    }

                    var horizontalDepth = x / (float)Mathf.Max(1, width - 1);
                    var edgeWeight = Mathf.Clamp01((horizontalDepth * 0.24f) + (vertical * 0.38f));
                    var silhouetteWeight = Mathf.Lerp(0.12f, 0.30f, silhouetteAlpha);
                    var interiorWeight = alpha > 0.08f ? alpha * 0.22f : 0f;
                    var coverage = Mathf.Clamp01(1.35f * (silhouetteWeight + interiorWeight + edgeWeight));
                    var threshold = (Bayer8[y % 8, x % 8] + 0.5f) / 64f;
                    outputPixels[index] = coverage > threshold
                        ? new Color(0.010f, 0.006f, 0.030f, Mathf.Lerp(0.20f, 0.36f, Mathf.Clamp01((vertical * 0.78f) + (horizontalDepth * 0.22f))))
                        : Color.clear;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{source.name}_dither_shadow"
            };
            texture.SetPixels(outputPixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), source.pivot / source.rect.size, source.pixelsPerUnit);
            sprite.name = $"{source.name}_dither_shadow_sprite";
            DitherShadowSpriteCache[cacheKey] = sprite;
            return sprite;
        }

        private static float SampleNeighborAlpha(Color[] pixels, int width, int height, int x, int y)
        {
            var maxAlpha = 0f;
            for (var dy = -1; dy <= 1; dy += 1)
            {
                var sampleY = y + dy;
                if (sampleY < 0 || sampleY >= height)
                {
                    continue;
                }

                for (var dx = -1; dx <= 1; dx += 1)
                {
                    var sampleX = x + dx;
                    if (sampleX < 0 || sampleX >= width)
                    {
                        continue;
                    }

                    maxAlpha = Mathf.Max(maxAlpha, pixels[(sampleY * width) + sampleX].a);
                }
            }

            return maxAlpha;
        }

        private void ApplyMaskInteraction()
        {
            var interaction = previewOnly
                ? SpriteMaskInteraction.None
                : SpriteMaskInteraction.VisibleInsideMask;

            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.maskInteraction = interaction;
            }
        }
    }
}
