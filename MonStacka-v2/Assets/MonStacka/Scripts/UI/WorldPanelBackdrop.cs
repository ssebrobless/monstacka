using UnityEngine;

namespace MonStacka.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WorldPanelBackdrop : MonoBehaviour
    {
        [SerializeField] private Vector2 size = Vector2.one;
        [SerializeField] private Color color = new(0.08f, 0.10f, 0.18f, 0.72f);
        [SerializeField] private int sortingOrder = -80;

        private static Sprite whiteSprite;

        private void Awake()
        {
            var renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite)
            {
                return whiteSprite;
            }

            var texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            whiteSprite.name = "WorldPanelWhiteSprite";
            return whiteSprite;
        }
    }
}
