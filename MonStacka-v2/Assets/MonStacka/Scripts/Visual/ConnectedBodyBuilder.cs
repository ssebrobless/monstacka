using System;
using System.Collections.Generic;
using System.Linq;
using MonStacka.Core;
using UnityEngine;

namespace MonStacka.Visual
{
    public sealed class ConnectedBodyBuildResult
    {
        public Transform RendererHost { get; set; }
        public SpriteRenderer BodyRenderer { get; set; }
        public Sprite[] BodyFrameSprites { get; set; } = new Sprite[0];
        public Texture2D[] RotatedFrameTextures { get; set; }
        public bool UsesFullBoxSprite { get; set; }
    }

    public static class ConnectedBodyBuilder
    {
        private static readonly Dictionary<string, Texture2D[]> RotatedTextureCache = new();
        private static readonly Dictionary<string, Sprite> BodySpriteCache = new();
        public static ConnectedBodyBuildResult Build(
            Transform host,
            PieceSkinData skinData,
            PieceType pieceType,
            int rotation,
            IReadOnlyCollection<Vector2Int> localCells,
            float cellWorldSize,
            bool previewOnly,
            bool useFullBoxSprite)
        {
            var result = new ConnectedBodyBuildResult
            {
                RotatedFrameTextures = GetRotatedFrameTextures(skinData, pieceType, rotation),
                UsesFullBoxSprite = false,
            };

            var rendererHost = new GameObject("ConnectedBody");
            rendererHost.transform.SetParent(host, false);
            var bounds = GetBounds(localCells);
            rendererHost.transform.localPosition = new Vector3(
                bounds.minX * cellWorldSize,
                -bounds.minY * cellWorldSize,
                0f
            );
            var renderer = rendererHost.AddComponent<SpriteRenderer>();
            renderer.color = Color.white;

            var frameSprites = new Sprite[skinData.bodyFrames.Length];
            for (var frameIndex = 0; frameIndex < skinData.bodyFrames.Length; frameIndex += 1)
            {
                frameSprites[frameIndex] = GetBodySprite(
                    result.RotatedFrameTextures[frameIndex],
                    localCells,
                    bounds.minX,
                    bounds.minY,
                    bounds.width,
                    bounds.height,
                    skinData.pixelsPerCell,
                    pieceType,
                    rotation,
                    frameIndex
                );
            }

            renderer.sprite = frameSprites[0];
            result.RendererHost = rendererHost.transform;
            result.BodyRenderer = renderer;
            result.BodyFrameSprites = frameSprites;

            return result;
        }

        public static bool MatchesFullDefinition(PieceType type, int rotation, IReadOnlyCollection<Vector2Int> localCells)
        {
            var expected = NormalizeCells(PieceDefinitions.GetCells(type, rotation))
                .OrderBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .ToArray();
            var actual = NormalizeCells(localCells)
                .OrderBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .ToArray();

            if (expected.Length != actual.Length)
            {
                return false;
            }

            for (var index = 0; index < expected.Length; index += 1)
            {
                if (expected[index] != actual[index])
                {
                    return false;
                }
            }

            return true;
        }

        // O is rotation-invariant: its definition is identical at every rotation index,
        // so its (off-center) art must never be turned.
        public static int GetTextureTurns(PieceSkinData skinData, PieceType pieceType, int rotation)
        {
            if (pieceType == PieceType.O)
            {
                return 0;
            }

            var normalizedRotation = ((rotation % 4) + 4) % 4;
            return ((normalizedRotation - skinData.baseRotation) % 4 + 4) % 4;
        }

        public static Texture2D[] GetRotatedFrameTextures(PieceSkinData skinData, PieceType pieceType, int rotation)
        {
            var turns = GetTextureTurns(skinData, pieceType, rotation);
            var cacheKey = $"{skinData.GetInstanceID()}:{pieceType}:{turns}";
            if (RotatedTextureCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var textures = new Texture2D[skinData.bodyFrames.Length];
            for (var frameIndex = 0; frameIndex < skinData.bodyFrames.Length; frameIndex += 1)
            {
                var source = ExtractSpriteTexture(skinData.bodyFrames[frameIndex]);
                textures[frameIndex] = RotateSquareTexture(source, turns);
            }

            RotatedTextureCache[cacheKey] = textures;
            return textures;
        }

        private static Sprite GetBodySprite(
            Texture2D texture,
            IReadOnlyCollection<Vector2Int> localCells,
            int minCellX,
            int minCellY,
            int widthCells,
            int heightCells,
            int pixelsPerCell,
            PieceType pieceType,
            int rotation,
            int frameIndex)
        {
            var signature = string.Join(";", localCells
                .OrderBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .Select(cell => $"{cell.x},{cell.y}"));
            var cacheKey = $"{texture.GetInstanceID()}:{signature}:{pixelsPerCell}:{pieceType}:{rotation}:{frameIndex}";
            if (BodySpriteCache.TryGetValue(cacheKey, out var sprite))
            {
                return sprite;
            }

            if (MatchesFullDefinition(pieceType, rotation, localCells))
            {
                var sourceCells = PieceDefinitions.GetCells(pieceType, rotation).ToArray();
                var sourceBounds = GetBounds(sourceCells);
                sprite = BuildCroppedFigureSprite(
                    texture,
                    sourceBounds.minX,
                    sourceBounds.minY,
                    sourceBounds.width,
                    sourceBounds.height,
                    pixelsPerCell,
                    pieceType,
                    rotation,
                    frameIndex
                );
                BodySpriteCache[cacheKey] = sprite;
                return sprite;
            }

            sprite = BuildCompositeSprite(
                texture,
                localCells,
                minCellX,
                minCellY,
                widthCells,
                heightCells,
                pixelsPerCell,
                pieceType,
                rotation,
                frameIndex,
                fullDefinition: false
            );
            BodySpriteCache[cacheKey] = sprite;
            return sprite;
        }

        private static Sprite BuildCroppedFigureSprite(
            Texture2D texture,
            int minCellX,
            int minCellY,
            int widthCells,
            int heightCells,
            int pixelsPerCell,
            PieceType pieceType,
            int rotation,
            int frameIndex)
        {
            var pixelX = minCellX * pixelsPerCell;
            var pixelY = texture.height - ((minCellY + heightCells) * pixelsPerCell);
            var pixelWidth = Mathf.Max(1, widthCells * pixelsPerCell);
            var pixelHeight = Mathf.Max(1, heightCells * pixelsPerCell);
            var rect = new Rect(pixelX, pixelY, pixelWidth, pixelHeight);
            var sprite = Sprite.Create(texture, rect, new Vector2(0f, 1f), pixelsPerCell);
            sprite.name = $"{pieceType}_{rotation}_{frameIndex}_body_full";
            return sprite;
        }

        private static Sprite BuildCompositeSprite(
            Texture2D texture,
            IReadOnlyCollection<Vector2Int> sourceCells,
            int minCellX,
            int minCellY,
            int widthCells,
            int heightCells,
            int pixelsPerCell,
            PieceType pieceType,
            int rotation,
            int frameIndex,
            bool fullDefinition)
        {
            var outputWidth = Mathf.Max(1, widthCells * pixelsPerCell);
            var outputHeight = Mathf.Max(1, heightCells * pixelsPerCell);
            var composite = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{pieceType}_{rotation}_{frameIndex}_{(fullDefinition ? "full" : "partial")}_composite"
            };
            var clearPixels = new Color[outputWidth * outputHeight];
            for (var i = 0; i < clearPixels.Length; i += 1)
            {
                clearPixels[i] = Color.clear;
            }
            composite.SetPixels(clearPixels);

            foreach (var cell in sourceCells)
            {
                var sourceY = texture.height - ((cell.y + 1) * pixelsPerCell);
                var sourcePixels = texture.GetPixels(cell.x * pixelsPerCell, sourceY, pixelsPerCell, pixelsPerCell);
                var destX = (cell.x - minCellX) * pixelsPerCell;
                var destY = outputHeight - (((cell.y - minCellY) + 1) * pixelsPerCell);
                composite.SetPixels(destX, destY, pixelsPerCell, pixelsPerCell, sourcePixels);
            }

            composite.Apply();

            var rect = new Rect(0f, 0f, composite.width, composite.height);
            var sprite = Sprite.Create(composite, rect, new Vector2(0f, 1f), pixelsPerCell);
            sprite.name = $"{pieceType}_{rotation}_{frameIndex}_{(fullDefinition ? "body_full" : "body_partial")}";
            return sprite;
        }

        private static (int minX, int minY, int width, int height) GetBounds(IReadOnlyCollection<Vector2Int> cells)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;
            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return (minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        }

        private static IEnumerable<Vector2Int> NormalizeCells(IEnumerable<Vector2Int> cells)
        {
            var buffer = cells.ToArray();
            if (buffer.Length == 0)
            {
                return buffer;
            }

            var minX = buffer.Min(cell => cell.x);
            var minY = buffer.Min(cell => cell.y);
            return buffer.Select(cell => new Vector2Int(cell.x - minX, cell.y - minY));
        }

        private static Texture2D ExtractSpriteTexture(Sprite sprite)
        {
            var source = sprite.texture;
            var rect = sprite.rect;
            var pixels = source.GetPixels(
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                Mathf.RoundToInt(rect.width),
                Mathf.RoundToInt(rect.height)
            );
            var texture = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false);
            texture.name = $"{sprite.name}_Copy";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D RotateSquareTexture(Texture2D source, int rotation)
        {
            var turns = ((rotation % 4) + 4) % 4;
            if (turns == 0)
            {
                return source;
            }

            var current = source;
            for (var step = 0; step < turns; step += 1)
            {
                current = Rotate90Clockwise(current);
            }

            return current;
        }

        private static Texture2D Rotate90Clockwise(Texture2D source)
        {
            var width = source.width;
            var height = source.height;
            var output = new Texture2D(height, width, TextureFormat.RGBA32, false);
            output.filterMode = FilterMode.Point;
            output.wrapMode = TextureWrapMode.Clamp;

            // True visual clockwise rotation (bottom-left texture coords): the previous
            // form rotated counter-clockwise, which made J/L/I spawn sprites show the
            // wrong rotation's art and crop cells away.
            for (var y = 0; y < height; y += 1)
            {
                for (var x = 0; x < width; x += 1)
                {
                    output.SetPixel(y, width - 1 - x, source.GetPixel(x, y));
                }
            }

            output.Apply();
            return output;
        }

        private static Texture2D RetainOccupiedCells(
            Texture2D source,
            IReadOnlyList<Vector2Int> definition,
            int pixelsPerCell)
        {
            var width = source.width;
            var height = source.height;
            var pixels = source.GetPixels();
            var keep = new byte[width * height];

            foreach (var cell in definition)
            {
                var startX = Mathf.Clamp(cell.x * pixelsPerCell, 0, width);
                var startY = Mathf.Clamp(height - ((cell.y + 1) * pixelsPerCell), 0, height);
                var endX = Mathf.Clamp(startX + pixelsPerCell, 0, width);
                var endY = Mathf.Clamp(startY + pixelsPerCell, 0, height);

                for (var y = startY; y < endY; y += 1)
                {
                    for (var x = startX; x < endX; x += 1)
                    {
                        var index = (y * width) + x;
                        if (pixels[index].a >= 0.094f)
                        {
                            keep[index] = 1;
                        }
                    }
                }
            }

            var output = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{source.name}_connected"
            };
            var outputPixels = new Color[pixels.Length];

            for (var index = 0; index < pixels.Length; index += 1)
            {
                outputPixels[index] = keep[index] != 0 ? pixels[index] : Color.clear;
            }

            output.SetPixels(outputPixels);
            output.Apply();
            return output;
        }
    }
}
