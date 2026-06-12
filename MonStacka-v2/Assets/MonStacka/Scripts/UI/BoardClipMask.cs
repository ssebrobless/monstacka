using UnityEngine;

namespace MonStacka.UI
{
    public sealed class BoardClipMask : MonoBehaviour
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
            Build();
        }

        private void Build()
        {
            var spriteMask = GetComponent<SpriteMask>();
            if (!spriteMask)
            {
                spriteMask = gameObject.AddComponent<SpriteMask>();
            }

            spriteMask.sprite = GetWhiteSprite();
            spriteMask.alphaCutoff = 0.01f;
            spriteMask.isCustomRangeActive = true;
            spriteMask.frontSortingOrder = 200;
            spriteMask.backSortingOrder = -200;

            transform.localPosition = new Vector3(
                (columns * cellWorldSize) * 0.5f,
                -(rows * cellWorldSize) * 0.5f,
                0f
            );
            transform.localScale = new Vector3(columns * cellWorldSize, rows * cellWorldSize, 1f);
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite)
            {
                return whiteSprite;
            }

            var texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            whiteSprite.name = "BoardClipMaskSprite";
            return whiteSprite;
        }
    }
}
