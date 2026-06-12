using UnityEngine;

namespace MonStacka.UI
{
    public sealed class BoardBackdropView : MonoBehaviour
    {
        [SerializeField] private int columns = 10;
        [SerializeField] private int rows = 20;
        [SerializeField] private float cellWorldSize = 0.52f;

        private static Sprite whiteSprite;

        public void Initialize(int boardColumns, int boardRows, float worldCellSize)
        {
            columns = boardColumns;
            rows = boardRows;
            cellWorldSize = worldCellSize;
            Build();
        }

        private void Awake()
        {
            if (transform.childCount == 0)
            {
                Build();
            }
        }

        private void Build()
        {
            for (var index = transform.childCount - 1; index >= 0; index -= 1)
            {
                var child = transform.GetChild(index);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            var sprite = GetWhiteSprite();
            for (var row = 0; row < rows; row += 1)
            {
                for (var col = 0; col < columns; col += 1)
                {
                    var cellGo = new GameObject($"Cell_{col}_{row}");
                    cellGo.transform.SetParent(transform, false);
                    cellGo.transform.localPosition = new Vector3(
                        (col * cellWorldSize) + (cellWorldSize * 0.5f),
                        -(row * cellWorldSize) - (cellWorldSize * 0.5f),
                        0f
                    );

                    var renderer = cellGo.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = -20;
                    renderer.color = new Color(0.26f, 0.31f, 0.52f, 0.90f);
                    cellGo.transform.localScale = new Vector3(cellWorldSize * 0.94f, cellWorldSize * 0.94f, 1f);
                }
            }
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite)
            {
                return whiteSprite;
            }

            var texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            whiteSprite.name = "RuntimeWhiteSprite";
            return whiteSprite;
        }
    }
}
