using System.Collections.Generic;
using System.Linq;
using MonStacka.Core;
using MonStacka.Visual;
using UnityEngine;

namespace MonStacka.UI
{
    public sealed class NextQueueView : MonoBehaviour
    {
        private const float SlotStepWorld = 1.32f;

        [SerializeField] private Transform contentRoot;
        private readonly List<PieceSkin> previews = new();
        private readonly List<PieceType?> previewSlots = new();
        private readonly List<PieceType> currentQueue = new();
        private float currentCellWorldSize = -1f;

        public void Render(IReadOnlyList<PieceType> queue, IReadOnlyDictionary<PieceType, PieceSkinData> skins, Material outlineMaterial, BorderDeformTuningProfile deformTuning, float cellWorldSize)
        {
            if (!contentRoot)
            {
                contentRoot = transform;
            }

            if (QueueMatches(queue) && Mathf.Approximately(cellWorldSize, currentCellWorldSize))
            {
                return;
            }

            for (var index = 0; index < queue.Count; index += 1)
            {
                var piece = queue[index];
                if (!skins.TryGetValue(piece, out var skinData))
                {
                    continue;
                }

                var definition = PieceDefinitions.GetCells(piece, 0).ToList();
                var minX = definition.Min(cell => cell.x);
                var minY = definition.Min(cell => cell.y);
                var maxX = definition.Max(cell => cell.x);
                var maxY = definition.Max(cell => cell.y);
                var normalized = definition.Select(cell => new Vector2Int(cell.x - minX, cell.y - minY)).ToList();
                var widthCells = maxX - minX + 1;
                var heightCells = maxY - minY + 1;
                var widthWorld = widthCells * cellWorldSize;
                var heightWorld = heightCells * cellWorldSize;
                var slotCenterY = -(index * SlotStepWorld);
                var skin = EnsureSlot(index, piece, skinData, normalized, outlineMaterial, deformTuning, cellWorldSize);
                skin.gameObject.SetActive(true);
                skin.transform.localPosition = new Vector3(
                    -(widthWorld * 0.5f),
                    slotCenterY + (heightWorld * 0.5f),
                    0f
                );
            }

            for (var index = queue.Count; index < previews.Count; index += 1)
            {
                if (previews[index])
                {
                    previews[index].gameObject.SetActive(false);
                }

                if (index < previewSlots.Count)
                {
                    previewSlots[index] = null;
                }
            }

            currentQueue.Clear();
            currentQueue.AddRange(queue);
            currentCellWorldSize = cellWorldSize;
        }

        public void ManualUpdate(float now)
        {
            foreach (var preview in previews)
            {
                if (preview && preview.RequiresManualUpdate)
                {
                    preview.ManualUpdate(now);
                }
            }
        }

        public void SetPreviewsVisible(bool visible)
        {
            if (!contentRoot)
            {
                contentRoot = transform;
            }

            contentRoot.gameObject.SetActive(visible);
        }

        private bool QueueMatches(IReadOnlyList<PieceType> queue)
        {
            if (currentQueue.Count != queue.Count)
            {
                return false;
            }

            for (var index = 0; index < queue.Count; index += 1)
            {
                if (currentQueue[index] != queue[index])
                {
                    return false;
                }
            }

            return true;
        }

        private PieceSkin EnsureSlot(
            int index,
            PieceType piece,
            PieceSkinData skinData,
            IReadOnlyCollection<Vector2Int> normalizedCells,
            Material outlineMaterial,
            BorderDeformTuningProfile deformTuning,
            float cellWorldSize)
        {
            while (previews.Count <= index)
            {
                previews.Add(null);
                previewSlots.Add(null);
            }

            if (previews[index] && previewSlots[index] == piece && Mathf.Approximately(cellWorldSize, currentCellWorldSize))
            {
                return previews[index];
            }

            if (previews[index])
            {
                previews[index].gameObject.SetActive(false);
                Destroy(previews[index].gameObject);
            }

            var go = new GameObject($"Next_{index}_{piece}");
            go.transform.SetParent(contentRoot, false);
            var skin = go.AddComponent<PieceSkin>();
            skin.Initialize(
                skinData,
                piece,
                0,
                normalizedCells,
                normalizedCells,
                cellWorldSize,
                outlineMaterial,
                deformTuning,
                true,
                0f,
                false,
                false
            );
            previews[index] = skin;
            previewSlots[index] = piece;
            return skin;
        }
    }
}
