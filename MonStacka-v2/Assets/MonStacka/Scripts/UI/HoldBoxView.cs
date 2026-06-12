using System.Collections.Generic;
using System.Linq;
using MonStacka.Core;
using MonStacka.Visual;
using UnityEngine;

namespace MonStacka.UI
{
    public sealed class HoldBoxView : MonoBehaviour
    {
        [SerializeField] private Transform contentRoot;
        private PieceSkin currentSkin;
        private PieceType? currentPiece;
        private float currentCellWorldSize = -1f;

        public void Render(PieceType? holdPiece, IReadOnlyDictionary<PieceType, PieceSkinData> skins, Material outlineMaterial, BorderDeformTuningProfile deformTuning, float cellWorldSize)
        {
            if (!contentRoot)
            {
                contentRoot = transform;
            }

            if (holdPiece == currentPiece && currentSkin && Mathf.Approximately(cellWorldSize, currentCellWorldSize))
            {
                PositionCurrentSkin(holdPiece.Value, cellWorldSize);
                return;
            }

            if (currentSkin)
            {
                currentSkin.gameObject.SetActive(false);
                Destroy(currentSkin.gameObject);
                currentSkin = null;
            }

            if (!holdPiece.HasValue || !skins.TryGetValue(holdPiece.Value, out var skinData))
            {
                currentPiece = null;
                currentCellWorldSize = cellWorldSize;
                return;
            }

            var go = new GameObject("HoldPreview");
            go.transform.SetParent(contentRoot, false);
            currentSkin = go.AddComponent<PieceSkin>();
            var definition = PieceDefinitions.GetCells(holdPiece.Value, 0).ToList();
            var minX = definition.Min(cell => cell.x);
            var minY = definition.Min(cell => cell.y);
            var maxX = definition.Max(cell => cell.x);
            var maxY = definition.Max(cell => cell.y);
            var normalized = definition.Select(cell => new Vector2Int(cell.x - minX, cell.y - minY)).ToList();
            currentSkin.Initialize(
                skinData,
                holdPiece.Value,
                0,
                normalized,
                cellWorldSize,
                outlineMaterial,
                deformTuning,
                true,
                0f,
                false,
                false
            );
            PositionCurrentSkin(holdPiece.Value, cellWorldSize);
            currentPiece = holdPiece.Value;
            currentCellWorldSize = cellWorldSize;
        }

        public void ManualUpdate(float now)
        {
            if (currentSkin && currentSkin.RequiresManualUpdate)
            {
                currentSkin.ManualUpdate(now);
            }
        }

        private void PositionCurrentSkin(PieceType piece, float cellWorldSize)
        {
            if (!currentSkin)
            {
                return;
            }

            var definition = PieceDefinitions.GetCells(piece, 0).ToList();
            var minX = definition.Min(cell => cell.x);
            var minY = definition.Min(cell => cell.y);
            var maxX = definition.Max(cell => cell.x);
            var maxY = definition.Max(cell => cell.y);
            var widthWorld = (maxX - minX + 1) * cellWorldSize;
            var heightWorld = (maxY - minY + 1) * cellWorldSize;
            currentSkin.transform.localPosition = new Vector3(
                -(widthWorld * 0.5f),
                heightWorld * 0.5f,
                0f
            );
        }
    }
}
