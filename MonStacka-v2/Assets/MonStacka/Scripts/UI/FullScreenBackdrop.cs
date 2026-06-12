using UnityEngine;

namespace MonStacka.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FullScreenBackdrop : MonoBehaviour
    {
        [SerializeField] private Texture2D texture;
        [SerializeField] private float pixelsPerUnit = 100f;
        [SerializeField] private int sortingOrder = -1000;

        private void Awake()
        {
            var renderer = GetComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;

            if (!texture)
            {
                return;
            }

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );
            sprite.name = $"{texture.name}_RuntimeBackdrop";
            renderer.sprite = sprite;
            transform.position = new Vector3(0f, 0f, 5f);
        }
    }
}
