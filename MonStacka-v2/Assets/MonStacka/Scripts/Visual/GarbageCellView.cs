using System.Collections.Generic;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    /// <summary>
    /// Pooled renderer for nuisance/garbage cells (story territory cells, hunger
    /// garbage rows, Dousema stitch repairs). Garbage cells have no PieceSkin, so
    /// they are drawn as flat tinted quads. Renderers are pooled and only refreshed
    /// when BoardState raises OnGarbageChanged - no per-frame churn.
    /// </summary>
    public sealed class GarbageCellView : MonoBehaviour
    {
        private static readonly Color GarbageColor = new(0.32f, 0.3f, 0.36f, 1f);
        private static readonly Color GarbageEdgeTint = new(0.21f, 0.2f, 0.25f, 1f);

        private readonly List<SpriteRenderer> pool = new();
        private Transform cellRoot;
        private Sprite cellSprite;
        private BoardState board;
        private float cellWorldSize = 1f;

        public void Initialize(BoardState boardState, Transform parent, float cellSize)
        {
            if (board != null)
            {
                board.OnGarbageChanged -= Refresh;
            }

            board = boardState;
            cellWorldSize = cellSize;

            if (!cellRoot)
            {
                var rootGo = new GameObject("GarbageCells");
                rootGo.transform.SetParent(parent, worldPositionStays: false);
                cellRoot = rootGo.transform;
            }

            if (!cellSprite)
            {
                cellSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                    new Vector2(0.5f, 0.5f),
                    Texture2D.whiteTexture.width
                );
            }

            board.OnGarbageChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (board != null)
            {
                board.OnGarbageChanged -= Refresh;
            }
        }

        public void Refresh()
        {
            if (board == null || !cellRoot)
            {
                return;
            }

            var cells = board.GetGarbageCells();
            EnsurePoolSize(cells.Count);

            for (var index = 0; index < cells.Count; index += 1)
            {
                var cell = cells[index];
                var renderer = pool[index];
                var visibleY = cell.y - PieceDefinitions.HiddenRows;
                renderer.transform.localPosition = new Vector3(cell.x * cellWorldSize, -visibleY * cellWorldSize, 0f);
                renderer.transform.localScale = new Vector3(cellWorldSize * 0.94f, cellWorldSize * 0.94f, 1f);
                renderer.color = (cell.x + cell.y) % 2 == 0 ? GarbageColor : GarbageEdgeTint;
                renderer.gameObject.SetActive(visibleY >= 0);
            }

            for (var index = cells.Count; index < pool.Count; index += 1)
            {
                pool[index].gameObject.SetActive(false);
            }
        }

        private void EnsurePoolSize(int needed)
        {
            while (pool.Count < needed)
            {
                var go = new GameObject($"GarbageCell{pool.Count}");
                go.transform.SetParent(cellRoot, worldPositionStays: false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = cellSprite;
                renderer.sortingOrder = 4;
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                pool.Add(renderer);
            }
        }
    }
}
