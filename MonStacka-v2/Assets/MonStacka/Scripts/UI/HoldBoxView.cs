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
        private SpriteRenderer innerGlowRenderer;
        private SpriteRenderer outerGlowRenderer;
        private bool abilityGlowActive;
        private Color abilityGlowColor = Color.white;
        private float abilityGlowBaseScale = 1f;

        public void Render(PieceType? holdPiece, IReadOnlyDictionary<PieceType, PieceSkinData> skins, Material outlineMaterial, BorderDeformTuningProfile deformTuning, float cellWorldSize, bool abilityArmed = false)
        {
            if (!contentRoot)
            {
                contentRoot = transform;
            }

            UpdateAbilityGlow(holdPiece, abilityArmed, cellWorldSize);

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

            if (abilityGlowActive)
            {
                var pulse = 0.5f + (0.5f * Mathf.Sin((now * 4.4f) + 0.35f));
                var inner = abilityGlowColor;
                inner.a = Mathf.Lerp(0.34f, 0.58f, pulse);
                var outer = abilityGlowColor;
                outer.a = Mathf.Lerp(0.10f, 0.24f, pulse);
                if (innerGlowRenderer)
                {
                    innerGlowRenderer.color = inner;
                    innerGlowRenderer.transform.localScale = Vector3.one * (abilityGlowBaseScale * Mathf.Lerp(2.78f, 2.94f, pulse));
                }

                if (outerGlowRenderer)
                {
                    outerGlowRenderer.color = outer;
                    outerGlowRenderer.transform.localScale = Vector3.one * (abilityGlowBaseScale * Mathf.Lerp(3.25f, 3.58f, pulse));
                }
            }
        }

        public void SetPreviewVisible(bool visible)
        {
            if (!contentRoot)
            {
                contentRoot = transform;
            }

            contentRoot.gameObject.SetActive(visible);
        }

        private void UpdateAbilityGlow(PieceType? holdPiece, bool abilityArmed, float cellWorldSize)
        {
            abilityGlowActive = abilityArmed && holdPiece.HasValue;
            EnsureAbilityGlow();

            if (!innerGlowRenderer || !outerGlowRenderer)
            {
                return;
            }

            innerGlowRenderer.gameObject.SetActive(abilityGlowActive);
            outerGlowRenderer.gameObject.SetActive(abilityGlowActive);
            if (!abilityGlowActive)
            {
                return;
            }

            abilityGlowColor = PieceDefinitions.PieceColors.TryGetValue(holdPiece.Value, out var pieceColor)
                ? pieceColor
                : Color.white;
            innerGlowRenderer.transform.localPosition = new Vector3(0f, 0f, 0.14f);
            outerGlowRenderer.transform.localPosition = new Vector3(0f, 0f, 0.13f);
            abilityGlowBaseScale = Mathf.Max(0.01f, cellWorldSize);
            innerGlowRenderer.transform.localScale = Vector3.one * (abilityGlowBaseScale * 2.86f);
            outerGlowRenderer.transform.localScale = Vector3.one * (abilityGlowBaseScale * 3.42f);
            innerGlowRenderer.color = new Color(abilityGlowColor.r, abilityGlowColor.g, abilityGlowColor.b, 0.48f);
            outerGlowRenderer.color = new Color(abilityGlowColor.r, abilityGlowColor.g, abilityGlowColor.b, 0.18f);
        }

        private void EnsureAbilityGlow()
        {
            if (innerGlowRenderer && outerGlowRenderer)
            {
                return;
            }

            if (!contentRoot)
            {
                contentRoot = transform;
            }

            outerGlowRenderer = CreateGlowRenderer("AbilityReadyOuterGlow", -32);
            innerGlowRenderer = CreateGlowRenderer("AbilityReadyInnerGlow", -31);
        }

        private SpriteRenderer CreateGlowRenderer(string objectName, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(contentRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetGlowSprite();
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.clear;
            go.SetActive(false);
            return renderer;
        }

        private static Sprite glowSprite;

        private static Sprite GetGlowSprite()
        {
            if (glowSprite)
            {
                return glowSprite;
            }

            var texture = Texture2D.whiteTexture;
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            return glowSprite;
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
