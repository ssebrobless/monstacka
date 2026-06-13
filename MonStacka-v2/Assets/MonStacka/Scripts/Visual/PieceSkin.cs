using System.Collections.Generic;
using System.Linq;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    public sealed class PieceSkin : MonoBehaviour
    {
        private static readonly int[] FrameSequence = { 0, 1, 2, 1, 0 };

        [SerializeField] private float frameAnimSpeed = 0.35f;
        [SerializeField] private int outlineSubdivisions = 18;
        [SerializeField] private float outlineBandWidth = 0.0105f;

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
            bodyBuild = ConnectedBodyBuilder.Build(bodyRoot.transform, skinData, pieceType, rotation, localCells, cellWorldSize, previewOnly, useFullBoxSprite);

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
            borderPulseSystem?.SetFrameIndex(frameIndex);

            lastFrameIndex = frameIndex;
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
