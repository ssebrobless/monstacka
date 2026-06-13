using MonStacka.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MonStacka.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class DitherOverlay : MonoBehaviour
    {
        private const int TextureWidth = 1920;
        private const int TextureHeight = 1080;
        private const int BayerSize = 4;
        private const int PixelScale = 2;
        private static readonly float[,] Bayer4 =
        {
            { 0f / 16f, 8f / 16f, 2f / 16f, 10f / 16f },
            { 12f / 16f, 4f / 16f, 14f / 16f, 6f / 16f },
            { 3f / 16f, 11f / 16f, 1f / 16f, 9f / 16f },
            { 15f / 16f, 7f / 16f, 13f / 16f, 5f / 16f },
        };

        private static Sprite textureSprite;
        private Image image;

        private void Awake()
        {
            image = GetComponent<Image>();
            image.sprite = GetOrCreateTextureSprite();
            image.type = Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (image)
            {
                image.enabled = MonStackaAppState.DitherEnabled;
            }
        }

        private static Sprite GetOrCreateTextureSprite()
        {
            if (textureSprite)
            {
                return textureSprite;
            }

            var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "MonStackaDepthDither",
            };

            for (var y = 0; y < TextureHeight; y += 1)
            {
                var vertical = y / (float)(TextureHeight - 1);
                var depth = Mathf.Lerp(0.58f, 1.08f, vertical);
                for (var x = 0; x < TextureWidth; x += 1)
                {
                    var cellX = (x / PixelScale) % BayerSize;
                    var cellY = (y / PixelScale) % BayerSize;
                    var centerX = (x / (float)(TextureWidth - 1)) - 0.5f;
                    var centerY = (y / (float)(TextureHeight - 1)) - 0.5f;
                    var vignette = Mathf.Clamp01((centerX * centerX * 0.8f) + (centerY * centerY * 0.55f));
                    var alpha = Bayer4[cellY, cellX] * Mathf.Lerp(0.095f, 0.15f, vignette) * depth;
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            texture.Apply(false, true);
            textureSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureWidth, TextureHeight), new Vector2(0.5f, 0.5f), 100f);
            textureSprite.name = "MonStackaDepthDitherSprite";
            return textureSprite;
        }
    }
}
