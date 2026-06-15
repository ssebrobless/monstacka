using System.Collections.Generic;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    /// <summary>
    /// Pooled renderer for nuisance/garbage cells (story territory cells, hunger
    /// garbage rows, Dousema stitch repairs). Garbage cells have no PieceSkin, so
    /// they use a small procedural enemy tile instead of a stretched placeholder.
    /// Renderers are pooled and only refreshed when BoardState raises OnGarbageChanged.
    /// </summary>
    public sealed class GarbageCellView : MonoBehaviour
    {
        private static readonly Color GarbageColor = new(0.58f, 0.16f, 0.25f, 1f);
        private static readonly Color GarbageEdgeTint = new(0.36f, 0.08f, 0.16f, 1f);
        private static readonly Color GarbageOutline = new(0.035f, 0.012f, 0.026f, 1f);
        private static readonly Color GarbageHighlight = new(0.84f, 0.30f, 0.37f, 1f);
        private static readonly Color GarbageCrack = new(0.12f, 0.025f, 0.05f, 1f);
        private const int SpritePixels = 64;

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
                cellSprite = CreateEnemyCellSprite();
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
                renderer.transform.localScale = new Vector3(cellWorldSize * 0.88f, cellWorldSize * 0.88f, 1f);
                var shade = 0.55f + ((((cell.x * 31) + (cell.y * 17)) % 100) / 220f);
                renderer.color = Color.Lerp(GarbageEdgeTint, GarbageColor, shade);
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
                renderer.sortingOrder = 18;
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                pool.Add(renderer);
            }
        }

        private static Sprite CreateEnemyCellSprite()
        {
            var texture = new Texture2D(SpritePixels, SpritePixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "enemy_territory_cell_texture"
            };

            var pixels = new Color[SpritePixels * SpritePixels];
            for (var y = 0; y < SpritePixels; y += 1)
            {
                for (var x = 0; x < SpritePixels; x += 1)
                {
                    pixels[(y * SpritePixels) + x] = SampleEnemyCellPixel(x, y);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, SpritePixels, SpritePixels),
                new Vector2(0.5f, 0.5f),
                SpritePixels
            );
            sprite.name = "enemy_territory_cell_sprite";
            return sprite;
        }

        private static Color SampleEnemyCellPixel(int x, int y)
        {
            var edge = Mathf.Min(Mathf.Min(x, SpritePixels - 1 - x), Mathf.Min(y, SpritePixels - 1 - y));
            var bite = ((x + (y * 3)) % 19) == 0 && edge >= 3 && edge <= 7;
            if (edge < 3 || bite)
            {
                return GarbageOutline;
            }

            var horizontal = x / (float)(SpritePixels - 1);
            var vertical = y / (float)(SpritePixels - 1);
            var depth = Mathf.Clamp01((horizontal * 0.28f) + ((1f - vertical) * 0.45f));
            var baseColor = Color.Lerp(GarbageEdgeTint, GarbageColor, 0.52f + (depth * 0.48f));

            var isInnerBorder = edge < 7;
            if (isInnerBorder)
            {
                baseColor = Color.Lerp(baseColor, GarbageOutline, 0.45f);
            }

            var crackA = Mathf.Abs((x - 13) - ((SpritePixels - 1 - y) * 0.36f)) < 1.35f && x > 13 && x < 53;
            var crackB = Mathf.Abs((x - 44) + ((SpritePixels - 1 - y) * 0.28f)) < 1.25f && x > 20 && y > 24;
            if (crackA || crackB)
            {
                return GarbageCrack;
            }

            var bayer = Bayer4(x, y);
            var speckle = (((x * 17) + (y * 29)) & 31) == 0;
            if (bayer < 0.30f + (depth * 0.16f) || speckle)
            {
                baseColor = Color.Lerp(baseColor, GarbageOutline, speckle ? 0.33f : 0.20f);
            }

            if (x > 10 && x < 25 && y > 39 && y < 51 && ((x + y) % 5 == 0))
            {
                baseColor = Color.Lerp(baseColor, GarbageHighlight, 0.38f);
            }

            return baseColor;
        }

        private static float Bayer4(int x, int y)
        {
            var value = ((x & 1) == 0 ? 0 : 8)
                + ((y & 1) == 0 ? 0 : 4)
                + ((x & 2) == 0 ? 0 : 2)
                + ((y & 2) == 0 ? 0 : 1);
            return (value + 0.5f) / 16f;
        }
    }
}
