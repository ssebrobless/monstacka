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
        private const int BayerSize = 8;
        private const int PixelScale = 1;
        private static readonly int[,] Bayer8 =
        {
            { 0, 48, 12, 60, 3, 51, 15, 63 },
            { 32, 16, 44, 28, 35, 19, 47, 31 },
            { 8, 56, 4, 52, 11, 59, 7, 55 },
            { 40, 24, 36, 20, 43, 27, 39, 23 },
            { 2, 50, 14, 62, 1, 49, 13, 61 },
            { 34, 18, 46, 30, 33, 17, 45, 29 },
            { 10, 58, 6, 54, 9, 57, 5, 53 },
            { 42, 26, 38, 22, 41, 25, 37, 21 },
        };

        private static Sprite textureSprite;
        private Image image;

        private void Reset()
        {
            EnsureFullScreenRect();
        }

        private void OnValidate()
        {
            EnsureFullScreenRect();
        }

        private void Awake()
        {
            EnsureFullScreenRect();
            image = GetComponent<Image>();
            image.sprite = GetOrCreateTextureSprite();
            image.type = Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;
            image.raycastTarget = false;
            transform.SetAsLastSibling();
        }

        private void LateUpdate()
        {
            if (image)
            {
                image.enabled = MonStackaAppState.DitherEnabled;
            }

            if (MonStackaAppState.DitherEnabled && transform.GetSiblingIndex() != transform.parent.childCount - 1)
            {
                transform.SetAsLastSibling();
            }
        }

        public void EnsureFullScreenRect()
        {
            var rect = GetComponent<RectTransform>();
            if (!rect)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
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
                var floorDepth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1f, vertical));
                for (var x = 0; x < TextureWidth; x += 1)
                {
                    var cellX = (x / PixelScale) % BayerSize;
                    var cellY = (y / PixelScale) % BayerSize;
                    var centerX = (x / (float)(TextureWidth - 1)) - 0.5f;
                    var centerY = (y / (float)(TextureHeight - 1)) - 0.5f;
                    var edgeDepth = Mathf.Clamp01((centerX * centerX * 1.56f) + (centerY * centerY * 0.84f));
                    var shaftShadow = Mathf.Clamp01(1f - Mathf.Abs((centerY + 0.18f) + (centerX * 0.16f)) * 3.4f);
                    var lowerPocket = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.70f, 1f, vertical));
                    var wallStrata = Mathf.Abs(Mathf.Sin((vertical * 31f) + (centerX * 5.5f)));
                    var strataDepth = Mathf.SmoothStep(0.38f, 1f, wallStrata) * 0.10f;
                    var coverage = Mathf.Clamp01(
                        0.72f +
                        (floorDepth * 0.08f) +
                        (lowerPocket * 0.08f) +
                        (edgeDepth * 0.13f) +
                        (shaftShadow * Mathf.Lerp(0.10f, 0.17f, floorDepth)) +
                        strataDepth
                    );
                    var threshold = (Bayer8[cellY, cellX] + 0.5f) / 64f;
                    var alpha = coverage > threshold
                        ? Mathf.Lerp(0.122f, 0.198f, Mathf.Clamp01((floorDepth * 0.34f) + (lowerPocket * 0.18f) + (edgeDepth * 0.28f) + (strataDepth * 0.2f)))
                        : 0f;
                    texture.SetPixel(x, y, new Color(0.005f, 0.008f, 0.030f, alpha));
                }
            }

            texture.Apply(false, true);
            textureSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureWidth, TextureHeight), new Vector2(0.5f, 0.5f), 100f);
            textureSprite.name = "MonStackaDepthDitherSprite";
            return textureSprite;
        }
    }
}
